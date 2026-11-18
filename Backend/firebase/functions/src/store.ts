/** Shop rotation, bundles, verified real-money purchases and rewarded-ad callbacks. */
import { FieldValue } from "firebase-admin/firestore";
import { HttpsError, onCall, onRequest } from "firebase-functions/v2/https";
import { defineString } from "firebase-functions/params";
import { GoogleAuth } from "google-auth-library";
import { db, requireUid, walletRef, inventoryRef, ledger, isEmulator } from "./common.js";
import { BUNDLES, featuredItems, findBundle } from "./catalog.js";
import { findGemPack, verifyAdMobCallback, fetchAdMobKeys, AD_REWARD_COINS, AD_REWARDS_PER_DAY } from "./monetization.js";
import { utcDayNumber } from "./rules.js";

const ANDROID_PACKAGE = defineString("ANDROID_PACKAGE", { default: "com.knockrushstudio.knockrush" });

export const getShop = onCall(async request => {
  requireUid(request);
  const now = Date.now();
  const day = utcDayNumber(now);
  return {
    day,
    resetsInSeconds: Math.floor(((day + 1) * 86_400_000 - now) / 1000),
    featured: featuredItems(day),
    bundles: BUNDLES,
  };
});

export const purchaseBundle = onCall(async request => {
  const uid = requireUid(request);
  const bundle = findBundle((request.data as { bundleId?: unknown })?.bundleId);
  if (!bundle) {
    throw new HttpsError("not-found", "Unknown bundle.");
  }

  return db.runTransaction(async tx => {
    const owned = await Promise.all(bundle.items.map(id => tx.get(inventoryRef(uid, id))));
    const missing = owned.filter(doc => !doc.exists);
    if (missing.length === 0) {
      throw new HttpsError("already-exists", "You already own everything in this bundle.");
    }

    const wallet = await tx.get(walletRef(uid));
    const balance = wallet.get(bundle.currency) ?? 0;
    if (balance < bundle.price) {
      throw new HttpsError("failed-precondition", `Not enough ${bundle.currency}.`);
    }

    tx.update(walletRef(uid), { [bundle.currency]: balance - bundle.price });
    for (const doc of missing) {
      tx.create(doc.ref, { source: `bundle_${bundle.id}`, acquiredAt: FieldValue.serverTimestamp() });
    }

    ledger(tx, { uid, type: "bundle", bundleId: bundle.id, [bundle.currency]: -bundle.price });
    return { granted: missing.map(doc => doc.id), [bundle.currency]: balance - bundle.price };
  });
});

/**
 * Verifies a gem-pack purchase with the store and grants gems once per store transaction.
 * data: { store: "google" | "apple" | "test", productId, token }
 *  - google: token = Play Billing purchaseToken (verified with the Android Publisher API using
 *    the function's service account, which must be granted access in Play Console).
 *  - apple: token = StoreKit transaction id (verified with the App Store Server API; requires
 *    the App Store Connect API key configured as secrets; see Docs/PHASE_8).
 *  - test: emulator only, token must start with "test-" (automated tests, editor fake store).
 */
export const validatePurchase = onCall(async request => {
  const uid = requireUid(request);
  const { store, productId, token } = (request.data ?? {}) as { store?: string; productId?: string; token?: string };
  const pack = findGemPack(productId);
  if (!pack || typeof token !== "string" || token.length < 6 || token.length > 4096) {
    throw new HttpsError("invalid-argument", "Unknown product or token.");
  }

  let transactionId: string;
  switch (store) {
    case "test":
      if (!isEmulator() || !token.startsWith("test-")) {
        throw new HttpsError("permission-denied", "Test purchases are only accepted by the emulator.");
      }

      transactionId = token;
      break;

    case "google": {
      const auth = new GoogleAuth({ scopes: ["https://www.googleapis.com/auth/androidpublisher"] });
      const client = await auth.getClient();
      const url = `https://androidpublisher.googleapis.com/androidpublisher/v3/applications/${ANDROID_PACKAGE.value()}`
        + `/purchases/products/${encodeURIComponent(pack.productId)}/tokens/${encodeURIComponent(token)}`;
      const response = await client.request<{ purchaseState?: number; orderId?: string; consumptionState?: number }>({ url });
      if (response.data.purchaseState !== 0 || !response.data.orderId) {
        throw new HttpsError("failed-precondition", "Purchase not completed.");
      }

      transactionId = response.data.orderId;
      break;
    }

    case "apple":
      throw new HttpsError("unimplemented", "App Store validation is enabled once App Store Connect API keys are configured (Phase 10).");

    default:
      throw new HttpsError("invalid-argument", "Unknown store.");
  }

  const purchaseRef = db.collection("purchases").doc(`${store}_${transactionId}`.replace(/[^A-Za-z0-9_.-]/g, "_"));
  return db.runTransaction(async tx => {
    const existing = await tx.get(purchaseRef);
    if (existing.exists) {
      return { duplicate: true, gems: 0 };
    }

    const wallet = await tx.get(walletRef(uid));
    tx.create(purchaseRef, { uid, store, productId: pack.productId, gems: pack.gems, at: FieldValue.serverTimestamp() });
    tx.update(walletRef(uid), { gems: (wallet.get("gems") ?? 0) + pack.gems });
    ledger(tx, { uid, type: "iap", store, productId: pack.productId, gems: pack.gems, transactionId });
    return { duplicate: false, gems: pack.gems };
  });
});

/**
 * AdMob rewarded-ad server-side verification callback. The client sets the ad's SSV user id to
 * the Firebase uid; AdMob calls this URL with a signed query string. Capped per day.
 */
export const adRewardCallback = onRequest(async (req, res) => {
  const rawQuery = req.url.includes("?") ? req.url.substring(req.url.indexOf("?") + 1) : "";
  const verified = verifyAdMobCallback(rawQuery, await fetchAdMobKeys());
  if (!verified.ok || !verified.params) {
    res.status(403).send("Invalid signature");
    return;
  }

  const uid = verified.params.get("user_id");
  const transactionId = verified.params.get("transaction_id");
  if (!uid || !transactionId) {
    res.status(400).send("Missing user_id or transaction_id");
    return;
  }

  const day = utcDayNumber(Date.now());
  const result = await db.runTransaction(async tx => {
    const rewardRef = db.collection("adRewards").doc(transactionId.replace(/[^A-Za-z0-9_.-]/g, "_"));
    const counterRef = db.collection("users").doc(uid).collection("private").doc("ads");
    const [existing, counter, wallet] = await Promise.all([tx.get(rewardRef), tx.get(counterRef), tx.get(walletRef(uid))]);
    if (existing.exists) {
      return "duplicate";
    }

    const today = counter.exists && counter.get("day") === day ? counter.get("count") ?? 0 : 0;
    tx.create(rewardRef, { uid, at: FieldValue.serverTimestamp() });
    if (today >= AD_REWARDS_PER_DAY || !wallet.exists) {
      return "capped";
    }

    tx.set(counterRef, { day, count: today + 1 });
    tx.update(walletRef(uid), { coins: (wallet.get("coins") ?? 0) + AD_REWARD_COINS });
    ledger(tx, { uid, type: "ad_reward", coins: AD_REWARD_COINS, transactionId });
    return "granted";
  });

  res.status(200).json({ result });
});
