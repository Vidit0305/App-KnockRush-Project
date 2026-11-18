import { test } from "node:test";
import assert from "node:assert/strict";
import { createSign, generateKeyPairSync } from "node:crypto";
import { CATALOG, SHOP_ITEMS, featuredItems, BUNDLES, findItem } from "../../src/catalog.js";
import { FREE_TRACK, PREMIUM_TRACK, SEASON, tierForXp, dailyMissions, applyMissionProgress } from "../../src/season.js";
import { verifyAdMobCallback, GEM_PACKS } from "../../src/monetization.js";

test("catalogue has the planned cosmetic counts", () => {
  const count = (slot: string) => CATALOG.filter(i => i.slot === slot).length;
  assert.equal(count("body"), 20, "20 costumes");
  assert.equal(count("color"), 15, "15 colourways");
  assert.equal(count("emote"), 10, "10 emotes");
  assert.equal(count("hat"), 10);
  assert.equal(count("pattern"), 8);
  assert.equal(new Set(CATALOG.map(i => i.id)).size, CATALOG.length, "ids unique");
});

test("every season / bundle reward references a real item", () => {
  for (const reward of [...FREE_TRACK, ...PREMIUM_TRACK]) {
    if (reward.itemId) assert.ok(findItem(reward.itemId), reward.itemId);
  }
  for (const bundle of BUNDLES) {
    for (const id of bundle.items) assert.ok(findItem(id), id);
  }
  assert.equal(FREE_TRACK.length, SEASON.tiers);
  assert.equal(PREMIUM_TRACK.length, SEASON.tiers);
});

test("exclusives are never sold in the shop", () => {
  assert.ok(SHOP_ITEMS.every(i => !i.exclusive && !i.starter));
  for (let day = 20000; day < 20030; day++) {
    assert.ok(featuredItems(day).every(i => !i.exclusive));
  }
});

test("daily shop rotation is deterministic and changes daily", () => {
  assert.deepEqual(featuredItems(20400).map(i => i.id), featuredItems(20400).map(i => i.id));
  assert.notDeepEqual(featuredItems(20400).map(i => i.id), featuredItems(20401).map(i => i.id));
  assert.equal(featuredItems(20400).length, 6);
});

test("season tiers from XP", () => {
  assert.equal(tierForXp(0), 0);
  assert.equal(tierForXp(SEASON.xpPerTier * 3 + 10), 3);
  assert.equal(tierForXp(1e9), SEASON.tiers);
});

test("daily missions: 3 distinct, deterministic, progress capped", () => {
  const missions = dailyMissions(20400, "uid_abc");
  assert.equal(missions.length, 3);
  assert.equal(new Set(missions.map(m => m.id)).size, 3);
  assert.deepEqual(dailyMissions(20400, "uid_abc"), missions);
  let progress: Record<string, number> = {};
  for (let i = 0; i < 20; i++) {
    progress = applyMissionProgress(progress, missions, { matches: 1, roundsCleared: 2, wins: 1, finals: 1 });
  }
  for (const m of missions) assert.equal(progress[m.id], m.target);
});

test("AdMob SSV signatures are verified (valid, tampered, unknown key)", () => {
  const { privateKey, publicKey } = generateKeyPairSync("ec", { namedCurve: "prime256v1" });
  const pem = publicKey.export({ type: "spki", format: "pem" }).toString();
  const message = "ad_network=5450213213286189855&ad_unit=1234&reward_amount=1&reward_item=coins&timestamp=1&transaction_id=tx1&user_id=uid1";
  const signer = createSign("SHA256");
  signer.update(message);
  const signature = signer.sign(privateKey).toString("base64url");
  const query = `${message}&signature=${signature}&key_id=42`;
  const keys = new Map([["42", pem]]);

  assert.equal(verifyAdMobCallback(query, keys).ok, true);
  assert.equal(verifyAdMobCallback(query.replace("user_id=uid1", "user_id=uid2"), keys).ok, false);
  assert.equal(verifyAdMobCallback(query.replace("key_id=42", "key_id=7"), keys).ok, false);
  assert.equal(verifyAdMobCallback(message, keys).ok, false);
});

test("gem packs are unique", () => {
  assert.equal(new Set(GEM_PACKS.map(p => p.productId)).size, GEM_PACKS.length);
});
