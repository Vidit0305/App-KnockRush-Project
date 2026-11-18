/**
 * KnockRush Cloud Functions (2nd gen). These are the ONLY writers of currency, XP, inventory,
 * match history and leaderboards. Clients call the callables with their Firebase ID token; the
 * dedicated game server reports results with a server key. Firestore rules deny direct writes.
 */
import { initializeApp } from "firebase-admin/app";
import { getAuth } from "firebase-admin/auth";
import { FieldValue, getFirestore, Transaction } from "firebase-admin/firestore";
import { HttpsError, onCall, onRequest, CallableRequest } from "firebase-functions/v2/https";
import { defineSecret } from "firebase-functions/params";
import { setGlobalOptions } from "firebase-functions/v2";
import {
  computeReward, levelForXp, xpForLevel, validateUsername, nextDailyClaim, utcDayNumber, newAchievements,
  validateMatchReport, STARTING_COINS, STARTING_GEMS, PlayerStats,
} from "./rules.js";
import { CATALOG, findItem, STARTER_ITEMS } from "./catalog.js";

initializeApp();
setGlobalOptions({ region: "europe-west1", maxInstances: 20 });
const db = getFirestore();
const MATCH_REPORT_KEY = defineSecret("MATCH_REPORT_KEY");

// -------------------------------------------------------------------------------------------
// Helpers
// -------------------------------------------------------------------------------------------
function requireUid(request: CallableRequest<unknown>): string {
  const uid = request.auth?.uid;
  if (!uid) {
    throw new HttpsError("unauthenticated", "Sign in first.");
  }

  return uid;
}

const userRef = (uid: string) => db.collection("users").doc(uid);
const walletRef = (uid: string) => userRef(uid).collection("private").doc("wallet");
const progressRef = (uid: string) => userRef(uid).collection("private").doc("progress");

function ledger(tx: Transaction, entry: Record<string, unknown>) {
  tx.create(db.collection("ledger").doc(), { ...entry, at: FieldValue.serverTimestamp() });
}

async function readProfile(uid: string) {
  const [user, wallet, progress, inventory] = await Promise.all([
    userRef(uid).get(), walletRef(uid).get(), progressRef(uid).get(), userRef(uid).collection("inventory").get(),
  ]);
  if (!user.exists) {
    return null;
  }

  const xp = progress.get("xp") ?? 0;
  return {
    uid,
    ...user.data(),
    coins: wallet.get("coins") ?? 0,
    gems: wallet.get("gems") ?? 0,
    xp,
    level: levelForXp(xp),
    xpForNextLevel: xpForLevel(levelForXp(xp) + 1),
    achievements: progress.get("achievements") ?? [],
    dailyStreak: progress.get("dailyStreak") ?? 0,
    dailyClaimable: nextDailyClaim(progress.get("dailyLastDay"), progress.get("dailyStreak"), utcDayNumber(Date.now())).claimable,
    inventory: inventory.docs.map(d => d.id),
  };
}

// -------------------------------------------------------------------------------------------
// Profile
// -------------------------------------------------------------------------------------------

/** Creates the player's profile, wallet and starter inventory on first sign-in (idempotent). */
export const ensureProfile = onCall(async request => {
  const uid = requireUid(request);
  await db.runTransaction(async tx => {
    const user = await tx.get(userRef(uid));
    if (user.exists) {
      return;
    }

    const guestName = `Bean${uid.slice(-5).toUpperCase()}`;
    tx.create(userRef(uid), {
      displayName: guestName,
      username: null,
      level: 1,
      equipped: { body: "default_bean", color: "default_orange", hat: "none", emote: "emote_celebrate" },
      stats: { matches: 0, wins: 0, roundsCleared: 0, crowns: 0 },
      createdAt: FieldValue.serverTimestamp(),
    });
    tx.create(walletRef(uid), { coins: STARTING_COINS, gems: STARTING_GEMS });
    tx.create(progressRef(uid), { xp: 0, achievements: [], dailyStreak: 0 });
    for (const itemId of STARTER_ITEMS) {
      tx.create(userRef(uid).collection("inventory").doc(itemId), { source: "starter", acquiredAt: FieldValue.serverTimestamp() });
    }
    ledger(tx, { uid, type: "starter_grant", coins: STARTING_COINS, gems: STARTING_GEMS });
  });

  return readProfile(uid);
});

export const getProfile = onCall(async request => {
  const uid = requireUid(request);
  const profile = await readProfile(uid);
  if (!profile) {
    throw new HttpsError("not-found", "Profile not created yet; call ensureProfile.");
  }

  return profile;
});

/** Claims a unique username (case-insensitive). Players may rename at most once per 7 days. */
export const setUsername = onCall(async request => {
  const uid = requireUid(request);
  const name = validateUsername((request.data as { username?: unknown })?.username);
  if (!name) {
    throw new HttpsError("invalid-argument", "Usernames are 3–16 letters, digits or _, starting with a letter.");
  }

  await db.runTransaction(async tx => {
    const user = await tx.get(userRef(uid));
    if (!user.exists) {
      throw new HttpsError("failed-precondition", "Profile missing.");
    }

    const lower = name.toLowerCase();
    const claim = await tx.get(db.collection("usernames").doc(lower));
    if (claim.exists && claim.get("uid") !== uid) {
      throw new HttpsError("already-exists", "That name is taken.");
    }

    const lastChange = user.get("usernameChangedAt")?.toMillis?.() ?? 0;
    if (user.get("username") && Date.now() - lastChange < 7 * 86_400_000) {
      throw new HttpsError("resource-exhausted", "You can change your name once every 7 days.");
    }

    const previous = user.get("username");
    if (previous && previous.toLowerCase() !== lower) {
      tx.delete(db.collection("usernames").doc(previous.toLowerCase()));
    }

    tx.set(db.collection("usernames").doc(lower), { uid });
    tx.update(userRef(uid), { username: name, displayName: name, usernameChangedAt: FieldValue.serverTimestamp() });
  });

  return { username: name };
});

// -------------------------------------------------------------------------------------------
// Economy
// -------------------------------------------------------------------------------------------
export const getCatalog = onCall(() => ({ items: CATALOG.filter(item => !item.starter) }));

export const purchaseShopItem = onCall(async request => {
  const uid = requireUid(request);
  const item = findItem((request.data as { itemId?: unknown })?.itemId);
  if (!item || item.starter) {
    throw new HttpsError("not-found", "Unknown item.");
  }

  return db.runTransaction(async tx => {
    const owned = await tx.get(userRef(uid).collection("inventory").doc(item.id));
    if (owned.exists) {
      throw new HttpsError("already-exists", "Already owned.");
    }

    const wallet = await tx.get(walletRef(uid));
    const balance = wallet.get(item.currency) ?? 0;
    if (balance < item.price) {
      throw new HttpsError("failed-precondition", `Not enough ${item.currency}.`);
    }

    tx.update(walletRef(uid), { [item.currency]: balance - item.price });
    tx.create(owned.ref, { source: "shop", acquiredAt: FieldValue.serverTimestamp() });
    ledger(tx, { uid, type: "purchase", itemId: item.id, [item.currency]: -item.price });
    return { itemId: item.id, [item.currency]: balance - item.price };
  });
});

/** Equips owned cosmetics (validated server side as well as by Firestore rules). */
export const equipItems = onCall(async request => {
  const uid = requireUid(request);
  const equipped = (request.data as { equipped?: Record<string, string> })?.equipped ?? {};
  const allowedSlots = ["body", "color", "hat", "emote"];
  const update: Record<string, string> = {};
  for (const [slot, itemId] of Object.entries(equipped)) {
    if (!allowedSlots.includes(slot)) {
      throw new HttpsError("invalid-argument", `Unknown slot ${slot}.`);
    }

    if (itemId === "none" && slot === "hat") {
      update[`equipped.${slot}`] = itemId;
      continue;
    }

    const item = findItem(itemId);
    const owned = item ? await userRef(uid).collection("inventory").doc(item.id).get() : null;
    if (!item || item.slot !== slot || !owned?.exists) {
      throw new HttpsError("permission-denied", `You don't own ${itemId}.`);
    }

    update[`equipped.${slot}`] = itemId;
  }

  await userRef(uid).update(update);
  return { ok: true };
});

export const claimDailyReward = onCall(async request => {
  const uid = requireUid(request);
  return db.runTransaction(async tx => {
    const progress = await tx.get(progressRef(uid));
    const wallet = await tx.get(walletRef(uid));
    if (!progress.exists || !wallet.exists) {
      throw new HttpsError("failed-precondition", "Profile missing.");
    }

    const today = utcDayNumber(Date.now());
    const claim = nextDailyClaim(progress.get("dailyLastDay"), progress.get("dailyStreak"), today);
    if (!claim.claimable) {
      throw new HttpsError("already-exists", "Already claimed today.");
    }

    tx.update(progressRef(uid), { dailyLastDay: today, dailyStreak: claim.newStreak });
    tx.update(walletRef(uid), {
      coins: (wallet.get("coins") ?? 0) + claim.reward.coins,
      gems: (wallet.get("gems") ?? 0) + claim.reward.gems,
    });
    ledger(tx, { uid, type: "daily", coins: claim.reward.coins, gems: claim.reward.gems, streak: claim.newStreak });
    return { streak: claim.newStreak, ...claim.reward };
  });
});

// -------------------------------------------------------------------------------------------
// Match results (dedicated server → backend). Idempotent per matchId.
// -------------------------------------------------------------------------------------------
export const reportMatchResult = onRequest({ secrets: [MATCH_REPORT_KEY] }, async (req, res) => {
  if (req.method !== "POST") {
    res.status(405).send("POST only");
    return;
  }

  const key = req.get("x-knockrush-server-key");
  if (!key || key !== MATCH_REPORT_KEY.value()) {
    res.status(403).send("Forbidden");
    return;
  }

  const body = req.body;
  const error = validateMatchReport(body);
  if (error) {
    res.status(400).send(error);
    return;
  }

  const matchRef = db.collection("matches").doc(body.matchId);
  const humans = body.players.filter((p: { isBot: boolean }) => !p.isBot);

  const outcome = await db.runTransaction(async tx => {
    const existing = await tx.get(matchRef);
    if (existing.exists) {
      return "duplicate";
    }

    // Reads first (Firestore transactions require all reads before writes).
    const snapshots = await Promise.all(humans.map(async (p: { uid: string }) => ({
      user: await tx.get(userRef(p.uid)),
      wallet: await tx.get(walletRef(p.uid)),
      progress: await tx.get(progressRef(p.uid)),
    })));

    tx.create(matchRef, { ...body, processedAt: FieldValue.serverTimestamp() });

    humans.forEach((p: { uid: string; placement: number; roundsCleared: number; won: boolean; isBot: boolean }, i: number) => {
      const { user, wallet, progress } = snapshots[i];
      if (!user.exists) {
        return; // unknown player (e.g. deleted account): result stored, no rewards
      }

      const reward = computeReward(p, body.modeId);
      const before: PlayerStats = user.get("stats") ?? { matches: 0, wins: 0, roundsCleared: 0 };
      const after: PlayerStats = {
        matches: before.matches + 1,
        wins: before.wins + (p.won ? 1 : 0),
        roundsCleared: before.roundsCleared + p.roundsCleared,
      };
      const xp = (progress.get("xp") ?? 0) + reward.xp;
      const owned: string[] = progress.get("achievements") ?? [];
      const unlocked = newAchievements(before, after, owned);

      tx.update(userRef(p.uid), {
        "stats.matches": after.matches,
        "stats.wins": after.wins,
        "stats.roundsCleared": after.roundsCleared,
        "stats.crowns": FieldValue.increment(reward.crowns),
        level: levelForXp(xp),
      });
      tx.update(walletRef(p.uid), { coins: (wallet.get("coins") ?? 0) + reward.coins });
      tx.update(progressRef(p.uid), { xp, achievements: [...owned, ...unlocked] });
      tx.set(userRef(p.uid).collection("matchHistory").doc(body.matchId), {
        modeId: body.modeId, placement: p.placement, roundsCleared: p.roundsCleared, won: p.won,
        reward, achievements: unlocked, at: FieldValue.serverTimestamp(),
      });
      tx.set(db.collection("leaderboards").doc("global").collection("entries").doc(p.uid), {
        displayName: user.get("displayName"),
        wins: after.wins,
        crowns: FieldValue.increment(reward.crowns),
        xp,
        level: levelForXp(xp),
      }, { merge: true });
      ledger(tx, { uid: p.uid, type: "match_reward", matchId: body.matchId, coins: reward.coins, xp: reward.xp });
    });

    return "processed";
  });

  res.status(200).json({ status: outcome, players: humans.length });
});

// -------------------------------------------------------------------------------------------
// Leaderboards & friends
// -------------------------------------------------------------------------------------------
export const getLeaderboard = onCall(async request => {
  requireUid(request);
  const limit = Math.max(1, Math.min(100, Number((request.data as { limit?: number })?.limit ?? 50)));
  const snapshot = await db.collection("leaderboards").doc("global").collection("entries")
    .orderBy("wins", "desc").limit(limit).get();
  return { entries: snapshot.docs.map((d, i) => ({ rank: i + 1, uid: d.id, ...d.data() })) };
});

export const sendFriendRequest = onCall(async request => {
  const uid = requireUid(request);
  const username = validateUsername((request.data as { username?: unknown })?.username);
  if (!username) {
    throw new HttpsError("invalid-argument", "Invalid username.");
  }

  const claim = await db.collection("usernames").doc(username.toLowerCase()).get();
  const target = claim.get("uid");
  if (!claim.exists || !target || target === uid) {
    throw new HttpsError("not-found", "Player not found.");
  }

  const me = await userRef(uid).get();
  await userRef(target).collection("friendRequests").doc(uid).set({
    displayName: me.get("displayName"), at: FieldValue.serverTimestamp(),
  });
  return { sentTo: target };
});

export const respondFriendRequest = onCall(async request => {
  const uid = requireUid(request);
  const { fromUid, accept } = (request.data ?? {}) as { fromUid?: string; accept?: boolean };
  if (typeof fromUid !== "string") {
    throw new HttpsError("invalid-argument", "fromUid required.");
  }

  const requestRef = userRef(uid).collection("friendRequests").doc(fromUid);
  await db.runTransaction(async tx => {
    const pending = await tx.get(requestRef);
    if (!pending.exists) {
      throw new HttpsError("not-found", "No such request.");
    }

    const [me, them] = await Promise.all([tx.get(userRef(uid)), tx.get(userRef(fromUid))]);
    tx.delete(requestRef);
    if (accept) {
      tx.set(userRef(uid).collection("friends").doc(fromUid), { displayName: them.get("displayName"), since: FieldValue.serverTimestamp() });
      tx.set(userRef(fromUid).collection("friends").doc(uid), { displayName: me.get("displayName"), since: FieldValue.serverTimestamp() });
    }
  });
  return { accepted: !!accept };
});

// -------------------------------------------------------------------------------------------
// Photon custom authentication: Photon calls this with the player's Firebase ID token and uses
// the returned UserId, so match identities are verified Firebase UIDs (no impersonation).
// Configure in the Photon dashboard: Authentication → Custom → URL of this function,
// with the client sending the "token" parameter.
// -------------------------------------------------------------------------------------------
export const photonAuth = onRequest(async (req, res) => {
  const token = (req.query.token ?? req.body?.token) as string | undefined;
  if (!token) {
    res.status(200).json({ ResultCode: 3, Message: "Missing token" });
    return;
  }

  try {
    const decoded = await getAuth().verifyIdToken(token);
    res.status(200).json({ ResultCode: 1, UserId: decoded.uid });
  } catch {
    res.status(200).json({ ResultCode: 2, Message: "Invalid token" });
  }
});
