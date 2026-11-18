import { test } from "node:test";
import assert from "node:assert/strict";
import { call, expectCallError, report, signUpAnonymous, FUNCTIONS, PROJECT, REGION } from "./helpers.js";

test("shop rotation and bundles", async () => {
  const user = await signUpAnonymous();
  await call("ensureProfile", {}, user);
  const shop = await call("getShop", {}, user);
  assert.equal(shop.featured.length, 6);
  assert.ok(shop.bundles.length >= 1);
  assert.ok(shop.resetsInSeconds > 0 && shop.resetsInSeconds <= 86400);

  await expectCallError(call("purchaseShopItem", { itemId: "body_dragon" }, user), "PERMISSION_DENIED");
  // 500 starting coins: party pack costs 1500 coins → refused.
  await expectCallError(call("purchaseBundle", { bundleId: "bundle_party" }, user), "FAILED_PRECONDITION");
});

test("gem packs (emulator test store) are granted once per transaction", async () => {
  const user = await signUpAnonymous();
  await call("ensureProfile", {}, user);
  const token = `test-${Date.now()}`;
  const first = await call("validatePurchase", { store: "test", productId: "knockrush.gems.550", token }, user);
  assert.deepEqual(first, { duplicate: false, gems: 550 });
  const replay = await call("validatePurchase", { store: "test", productId: "knockrush.gems.550", token }, user);
  assert.equal(replay.duplicate, true);
  await expectCallError(call("validatePurchase", { store: "test", productId: "knockrush.gems.free", token: "test-x1234" }, user), "INVALID_ARGUMENT");
  assert.equal((await call("getProfile", {}, user)).gems, 550);

  // Gems buy the space bundle (450 gems) → three items granted.
  const bundle = await call("purchaseBundle", { bundleId: "bundle_space" }, user);
  assert.equal(bundle.granted.length, 3);
  assert.equal(bundle.gems, 100);
  await expectCallError(call("purchaseBundle", { bundleId: "bundle_space" }, user), "ALREADY_EXISTS");
});

test("season pass: XP from matches, tiers, free and premium claims", async () => {
  const user = await signUpAnonymous();
  await call("ensureProfile", {}, user);
  const before = await call("getSeason", {}, user);
  assert.equal(before.player.tier, 0);
  await expectCallError(call("claimSeasonReward", { tier: 1, track: "free" }, user), "FAILED_PRECONDITION");

  // Two winning matches → ≥ 1 tier of season XP.
  for (let i = 0; i < 2; i++) {
    const res = await report({ matchId: `season_${Date.now()}_${i}`, modeId: "classic", players: [{ uid: user.uid, isBot: false, placement: 1, roundsCleared: 4, won: true }] });
    assert.equal(res.status, 200);
  }

  const after = await call("getSeason", {}, user);
  assert.ok(after.player.tier >= 1, `tier ${after.player.tier}`);
  const free = await call("claimSeasonReward", { tier: 1, track: "free" }, user);
  assert.ok(free.coins > 0);
  await expectCallError(call("claimSeasonReward", { tier: 1, track: "free" }, user), "ALREADY_EXISTS");
  await expectCallError(call("claimSeasonReward", { tier: 1, track: "premium" }, user), "PERMISSION_DENIED");
  await expectCallError(call("buyPremiumPass", {}, user), "FAILED_PRECONDITION");

  await call("validatePurchase", { store: "test", productId: "knockrush.gems.1200", token: `test-pass-${Date.now()}` }, user);
  assert.equal((await call("buyPremiumPass", {}, user)).premium, true);
  const premium = await call("claimSeasonReward", { tier: 1, track: "premium" }, user);
  assert.equal(premium.itemId, "hat_propeller");
  assert.ok((await call("getProfile", {}, user)).inventory.includes("hat_propeller"));
});

test("daily missions progress from matches and pay out once", async () => {
  const user = await signUpAnonymous();
  await call("ensureProfile", {}, user);
  for (let i = 0; i < 5; i++) {
    await report({ matchId: `mission_${Date.now()}_${i}`, modeId: "classic", players: [{ uid: user.uid, isBot: false, placement: 1, roundsCleared: 3, won: true }] });
  }

  const { missions } = await call("getMissions", {}, user);
  assert.equal(missions.length, 3);
  for (const mission of missions) {
    assert.equal(mission.progress, mission.target, mission.id);
    const reward = await call("claimMission", { missionId: mission.id }, user);
    assert.ok(reward.coins > 0);
  }

  await expectCallError(call("claimMission", { missionId: missions[0].id }, user), "ALREADY_EXISTS");
});

test("ad reward callback rejects unsigned requests", async () => {
  const response = await fetch(`http://${FUNCTIONS}/${PROJECT}/${REGION}/adRewardCallback?user_id=x&transaction_id=y&signature=abc&key_id=1`);
  assert.equal(response.status, 403);
});
