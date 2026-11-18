import { test } from "node:test";
import assert from "node:assert/strict";
import { call, expectCallError, report, signUpAnonymous, FUNCTIONS, PROJECT, REGION } from "./helpers.js";

test("ensureProfile creates wallet and starter inventory, idempotently", async () => {
  const user = await signUpAnonymous();
  const profile = await call("ensureProfile", {}, user);
  assert.equal(profile.coins, 500);
  assert.equal(profile.gems, 0);
  assert.equal(profile.level, 1);
  assert.ok(profile.inventory.includes("default_bean"));
  assert.equal(profile.dailyClaimable, true);

  const again = await call("ensureProfile", {}, user);
  assert.equal(again.coins, 500, "second call must not grant again");
});

test("callables require sign-in", async () => {
  await expectCallError(call("getProfile", {}), "UNAUTHENTICATED");
});

test("usernames are unique and validated", async () => {
  const a = await signUpAnonymous();
  const b = await signUpAnonymous();
  await call("ensureProfile", {}, a);
  await call("ensureProfile", {}, b);
  const name = `Bean${Date.now() % 1_000_000}`;
  assert.equal((await call("setUsername", { username: name }, a)).username, name);
  await expectCallError(call("setUsername", { username: name.toUpperCase() }, b), "ALREADY_EXISTS");
  await expectCallError(call("setUsername", { username: "x" }, b), "INVALID_ARGUMENT");
  assert.equal((await call("getProfile", {}, a)).displayName, name);
});

test("shop purchases are priced and paid on the server", async () => {
  const user = await signUpAnonymous();
  await call("ensureProfile", {}, user);
  const purchase = await call("purchaseShopItem", { itemId: "color_purple" }, user);
  assert.equal(purchase.coins, 100); // 500 start − 400 (common)
  await expectCallError(call("purchaseShopItem", { itemId: "color_purple" }, user), "ALREADY_EXISTS");
  await expectCallError(call("purchaseShopItem", { itemId: "hat_crown" }, user), "FAILED_PRECONDITION");
  await expectCallError(call("purchaseShopItem", { itemId: "free_gems_hack" }, user), "NOT_FOUND");

  await call("equipItems", { equipped: { color: "color_purple" } }, user);
  await expectCallError(call("equipItems", { equipped: { body: "body_dino" } }, user), "PERMISSION_DENIED");
  const profile = await call("getProfile", {}, user);
  assert.equal(profile.equipped.color, "color_purple");
  assert.equal(profile.coins, 100);
});

test("daily reward can be claimed once per day", async () => {
  const user = await signUpAnonymous();
  await call("ensureProfile", {}, user);
  const claim = await call("claimDailyReward", {}, user);
  assert.equal(claim.streak, 1);
  assert.equal(claim.coins, 50);
  await expectCallError(call("claimDailyReward", {}, user), "ALREADY_EXISTS");
  assert.equal((await call("getProfile", {}, user)).coins, 550);
});

test("match results: server key required, validated, rewarded once", async () => {
  const winner = await signUpAnonymous();
  const loser = await signUpAnonymous();
  await call("ensureProfile", {}, winner);
  await call("ensureProfile", {}, loser);
  const matchId = `match_${Date.now()}`;
  const body = {
    matchId,
    modeId: "classic",
    players: [
      { uid: winner.uid, isBot: false, placement: 1, roundsCleared: 4, won: true },
      { uid: loser.uid, isBot: false, placement: 12, roundsCleared: 1, won: false },
      { isBot: true, placement: 2, roundsCleared: 3, won: false },
    ],
  };

  assert.equal((await report(body, null)).status, 403, "no key");
  assert.equal((await report(body, "wrong")).status, 403, "wrong key");
  assert.equal((await report({ ...body, modeId: "cheat" })).status, 400, "invalid body");

  const first = await report(body);
  assert.equal(first.status, 200);
  assert.equal(first.json.status, "processed");
  const duplicate = await report(body);
  assert.equal(duplicate.json.status, "duplicate");

  const champ = await call("getProfile", {}, winner);
  assert.equal(champ.coins, 500 + 10 + 4 * 20 + 100);
  assert.equal(champ.stats.wins, 1);
  assert.equal(champ.stats.crowns, 1);
  assert.ok(champ.xp >= 410);
  assert.ok(champ.achievements.includes("first_win"));
  assert.equal(champ.level, 2);

  const other = await call("getProfile", {}, loser);
  assert.equal(other.coins, 500 + 10 + 20);
  assert.equal(other.stats.wins, 0);

  const board = await call("getLeaderboard", { limit: 100 }, winner);
  assert.ok(board.entries.some((e: { uid: string }) => e.uid === winner.uid));
});

test("friend requests by username", async () => {
  const a = await signUpAnonymous();
  const b = await signUpAnonymous();
  await call("ensureProfile", {}, a);
  await call("ensureProfile", {}, b);
  const bName = `Pal${Date.now() % 1_000_000}`;
  await call("setUsername", { username: bName }, b);

  await call("sendFriendRequest", { username: bName }, a);
  const pending = await call("getFriends", {}, b);
  assert.ok(pending.requests.some((r: { uid: string }) => r.uid === a.uid), "request listed");
  const accepted = await call("respondFriendRequest", { fromUid: a.uid, accept: true }, b);
  assert.equal(accepted.accepted, true);
  await expectCallError(call("respondFriendRequest", { fromUid: a.uid, accept: true }, b), "NOT_FOUND");
  const listA = await call("getFriends", {}, a);
  assert.ok(listA.friends.some((f: { uid: string }) => f.uid === b.uid), "friendship is mutual");
  const listB = await call("getFriends", {}, b);
  assert.equal(listB.requests.length, 0);
});

test("photon custom auth returns the verified uid", async () => {
  const user = await signUpAnonymous();
  const url = `http://${FUNCTIONS}/${PROJECT}/${REGION}/photonAuth`;
  const ok = await (await fetch(`${url}?token=${encodeURIComponent(user.token)}`)).json() as any;
  assert.deepEqual(ok, { ResultCode: 1, UserId: user.uid });
  const bad = await (await fetch(`${url}?token=forged`)).json() as any;
  assert.equal(bad.ResultCode, 2);
  const missing = await (await fetch(url)).json() as any;
  assert.equal(missing.ResultCode, 3);
});
