import { test } from "node:test";
import assert from "node:assert/strict";
import {
  computeReward, levelForXp, xpForLevel, validateUsername, nextDailyClaim, newAchievements, validateMatchReport, ACHIEVEMENTS,
} from "../../src/rules.js";

test("champion earns most, participation always earns XP", () => {
  const champ = computeReward({ isBot: false, placement: 1, roundsCleared: 4, won: true }, "classic");
  const out = computeReward({ isBot: false, placement: 30, roundsCleared: 0, won: false }, "classic");
  assert.equal(champ.crowns, 1);
  assert.ok(champ.coins > out.coins);
  assert.ok(out.xp > 0);
  assert.equal(out.crowns, 0);
});

test("private parties give no coins or crowns (no farming)", () => {
  const r = computeReward({ isBot: false, placement: 1, roundsCleared: 3, won: true }, "private");
  assert.equal(r.coins, 0);
  assert.equal(r.crowns, 0);
  assert.ok(r.xp > 0);
});

test("level curve is monotonic and consistent", () => {
  assert.equal(levelForXp(0), 1);
  for (let level = 2; level < 60; level++) {
    assert.ok(xpForLevel(level) > xpForLevel(level - 1));
    assert.equal(levelForXp(xpForLevel(level)), level);
    assert.equal(levelForXp(xpForLevel(level) - 1), level - 1);
  }
});

test("usernames", () => {
  assert.equal(validateUsername("Bean_King"), "Bean_King");
  assert.equal(validateUsername("  Rush99 "), "Rush99");
  assert.equal(validateUsername("ab"), null);
  assert.equal(validateUsername("9lives"), null);
  assert.equal(validateUsername("has space"), null);
  assert.equal(validateUsername("x".repeat(17)), null);
  assert.equal(validateUsername(42), null);
});

test("daily reward streaks", () => {
  const first = nextDailyClaim(undefined, undefined, 100);
  assert.deepEqual([first.claimable, first.newStreak], [true, 1]);
  const again = nextDailyClaim(100, 1, 100);
  assert.equal(again.claimable, false);
  const next = nextDailyClaim(100, 1, 101);
  assert.equal(next.newStreak, 2);
  const missed = nextDailyClaim(100, 5, 103);
  assert.equal(missed.newStreak, 1);
  const day7 = nextDailyClaim(106, 6, 107);
  assert.equal(day7.reward.gems, 10);
});

test("achievements unlock once", () => {
  const unlocked = newAchievements({ matches: 0, wins: 0, roundsCleared: 0 }, { matches: 1, wins: 1, roundsCleared: 2 }, []);
  assert.deepEqual(unlocked.sort(), ["first_match", "first_round", "first_win"]);
  assert.deepEqual(newAchievements({ matches: 1, wins: 1, roundsCleared: 2 }, { matches: 2, wins: 1, roundsCleared: 3 }, unlocked), []);
  // Crossing several thresholds in one match unlocks each of them exactly once.
  const big = newAchievements({ matches: 9, wins: 4, roundsCleared: 9 }, { matches: 10, wins: 5, roundsCleared: 10 }, ["first_match", "first_round", "first_win"]);
  assert.deepEqual(big.sort(), ["matches_10", "rounds_10", "wins_5"]);
});

test("achievement ids are unique and targets positive", () => {
  const ids = ACHIEVEMENTS.map(a => a.id);
  assert.equal(new Set(ids).size, ids.length);
  assert.ok(ACHIEVEMENTS.every(a => a.target > 0 && a.title.length > 0 && a.description.length > 0));
});

test("match report validation rejects tampering", () => {
  const ok = { matchId: "match_123456", modeId: "classic", players: [{ uid: "uid_abcdef", isBot: false, placement: 1, roundsCleared: 4, won: true }] };
  assert.equal(validateMatchReport(ok), null);
  assert.match(validateMatchReport({ ...ok, modeId: "hack" })!, /modeId/);
  assert.match(validateMatchReport({ ...ok, players: [{ ...ok.players[0], roundsCleared: 99 }] })!, /roundsCleared/);
  assert.match(validateMatchReport({ ...ok, players: [ok.players[0], { ...ok.players[0] }] })!, /Duplicate/);
  assert.match(validateMatchReport({ ...ok, players: [ok.players[0], { uid: "uid_zzzzzz", isBot: false, placement: 2, roundsCleared: 1, won: true }] })!, /winner/);
});
