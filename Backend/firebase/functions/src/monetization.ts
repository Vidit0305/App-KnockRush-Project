/**
 * Real-money purchases (gem packs) and rewarded ads. Clients never grant anything: purchases are
 * verified with the store, ads with the ad network's server-side verification (SSV) signature.
 */
import { createVerify } from "node:crypto";

export interface GemPack {
  productId: string;
  gems: number;
}

/** Product ids must match Google Play Console / App Store Connect consumables. */
export const GEM_PACKS: ReadonlyArray<GemPack> = [
  { productId: "knockrush.gems.100", gems: 100 },
  { productId: "knockrush.gems.550", gems: 550 },
  { productId: "knockrush.gems.1200", gems: 1200 },
  { productId: "knockrush.gems.2500", gems: 2500 },
];

export function findGemPack(productId: unknown): GemPack | undefined {
  return typeof productId === "string" ? GEM_PACKS.find(pack => pack.productId === productId) : undefined;
}

export const AD_REWARD_COINS = 50;
export const AD_REWARDS_PER_DAY = 5;

/**
 * Verifies an AdMob server-side verification callback. AdMob signs the query string up to (not
 * including) "&signature=" with ECDSA P-256 / SHA-256; `keys` maps key_id → PEM public key
 * (fetched from https://www.gstatic.com/admob/reward/verifier-keys.json).
 */
export function verifyAdMobCallback(rawQuery: string, keys: ReadonlyMap<string, string>): { ok: boolean; params?: URLSearchParams } {
  const signatureIndex = rawQuery.indexOf("&signature=");
  if (signatureIndex < 0) {
    return { ok: false };
  }

  const message = rawQuery.substring(0, signatureIndex);
  const params = new URLSearchParams(rawQuery);
  const signature = params.get("signature");
  const keyId = params.get("key_id");
  const pem = keyId ? keys.get(keyId) : undefined;
  if (!signature || !pem) {
    return { ok: false };
  }

  try {
    const verifier = createVerify("SHA256");
    verifier.update(message);
    verifier.end();
    const ok = verifier.verify(pem, Buffer.from(signature.replace(/-/g, "+").replace(/_/g, "/"), "base64"));
    return ok ? { ok, params } : { ok: false };
  } catch {
    return { ok: false };
  }
}

export async function fetchAdMobKeys(): Promise<Map<string, string>> {
  const override = process.env.KNOCKRUSH_ADMOB_TEST_KEYS;
  const json = override
    ? JSON.parse(override)
    : await (await fetch("https://www.gstatic.com/admob/reward/verifier-keys.json")).json();
  const keys = new Map<string, string>();
  for (const key of (json as { keys: Array<{ keyId: number | string; pem: string }> }).keys) {
    keys.set(String(key.keyId), key.pem);
  }

  return keys;
}
