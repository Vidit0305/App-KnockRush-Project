/** Season pass and daily mission endpoints. */
import { FieldValue } from "firebase-admin/firestore";
import { HttpsError, onCall } from "firebase-functions/v2/https";
import { db, requireUid, walletRef, progressRef, missionsRef, inventoryRef, ledger } from "./common.js";
import { SEASON, FREE_TRACK, PREMIUM_TRACK, tierForXp, dailyMissions, TierReward } from "./season.js";
import { utcDayNumber } from "./rules.js";

function seasonState(progress: FirebaseFirestore.DocumentSnapshot) {
  const sameSeason = progress.get("seasonId") === SEASON.id;
  const seasonXp = sameSeason ? progress.get("seasonXp") ?? 0 : 0;
  return {
    seasonXp,
    tier: tierForXp(seasonXp),
    premium: sameSeason && progress.get("premiumPass") === true,
    claimedFree: (sameSeason ? progress.get("claimedFree") : null) ?? [] as number[],
    claimedPremium: (sameSeason ? progress.get("claimedPremium") : null) ?? [] as number[],
  };
}

export const getSeason = onCall(async request => {
  const uid = requireUid(request);
  const progress = await progressRef(uid).get();
  return { season: SEASON, freeTrack: FREE_TRACK, premiumTrack: PREMIUM_TRACK, player: seasonState(progress) };
});

export const buyPremiumPass = onCall(async request => {
  const uid = requireUid(request);
  return db.runTransaction(async tx => {
    const progress = await tx.get(progressRef(uid));
    const wallet = await tx.get(walletRef(uid));
    if (seasonState(progress).premium) {
      throw new HttpsError("already-exists", "You already own this season's premium pass.");
    }

    const gems = wallet.get("gems") ?? 0;
    if (gems < SEASON.premiumPriceGems) {
      throw new HttpsError("failed-precondition", "Not enough gems.");
    }

    tx.update(walletRef(uid), { gems: gems - SEASON.premiumPriceGems });
    tx.update(progressRef(uid), { seasonId: SEASON.id, premiumPass: true });
    ledger(tx, { uid, type: "premium_pass", season: SEASON.id, gems: -SEASON.premiumPriceGems });
    return { premium: true, gems: gems - SEASON.premiumPriceGems };
  });
});

export const claimSeasonReward = onCall(async request => {
  const uid = requireUid(request);
  const { tier, track } = (request.data ?? {}) as { tier?: number; track?: string };
  if (!Number.isInteger(tier) || tier! < 1 || tier! > SEASON.tiers || (track !== "free" && track !== "premium")) {
    throw new HttpsError("invalid-argument", "tier 1..30 and track free|premium required.");
  }

  return db.runTransaction(async tx => {
    const progress = await tx.get(progressRef(uid));
    const wallet = await tx.get(walletRef(uid));
    const state = seasonState(progress);
    if (tier! > state.tier) {
      throw new HttpsError("failed-precondition", "Tier not reached yet.");
    }

    if (track === "premium" && !state.premium) {
      throw new HttpsError("permission-denied", "Premium pass required.");
    }

    const claimedField = track === "free" ? "claimedFree" : "claimedPremium";
    const claimed: number[] = track === "free" ? state.claimedFree : state.claimedPremium;
    if (claimed.includes(tier!)) {
      throw new HttpsError("already-exists", "Already claimed.");
    }

    const reward: TierReward = (track === "free" ? FREE_TRACK : PREMIUM_TRACK)[tier! - 1];
    if (reward.itemId) {
      const owned = await tx.get(inventoryRef(uid, reward.itemId));
      if (!owned.exists) {
        tx.create(owned.ref, { source: `season_${SEASON.id}`, acquiredAt: FieldValue.serverTimestamp() });
      }
    }

    if (reward.coins || reward.gems) {
      tx.update(walletRef(uid), {
        coins: (wallet.get("coins") ?? 0) + (reward.coins ?? 0),
        gems: (wallet.get("gems") ?? 0) + (reward.gems ?? 0),
      });
    }

    tx.update(progressRef(uid), { seasonId: SEASON.id, [claimedField]: [...claimed, tier] });
    ledger(tx, { uid, type: "season_reward", season: SEASON.id, tier, track, ...reward });
    return reward;
  });
});

export const getMissions = onCall(async request => {
  const uid = requireUid(request);
  const today = utcDayNumber(Date.now());
  const doc = await missionsRef(uid).get();
  const sameDay = doc.exists && doc.get("day") === today;
  const progress = sameDay ? doc.get("progress") ?? {} : {};
  const claimed: string[] = sameDay ? doc.get("claimed") ?? [] : [];
  return {
    day: today,
    missions: dailyMissions(today, uid).map(m => ({ ...m, progress: progress[m.id] ?? 0, claimed: claimed.includes(m.id) })),
  };
});

export const claimMission = onCall(async request => {
  const uid = requireUid(request);
  const missionId = (request.data as { missionId?: string })?.missionId;
  const today = utcDayNumber(Date.now());
  const mission = dailyMissions(today, uid).find(m => m.id === missionId);
  if (!mission) {
    throw new HttpsError("not-found", "Not one of today's missions.");
  }

  return db.runTransaction(async tx => {
    const doc = await tx.get(missionsRef(uid));
    const wallet = await tx.get(walletRef(uid));
    const sameDay = doc.exists && doc.get("day") === today;
    const progress = sameDay ? doc.get("progress") ?? {} : {};
    const claimed: string[] = sameDay ? doc.get("claimed") ?? [] : [];
    if (claimed.includes(mission.id)) {
      throw new HttpsError("already-exists", "Already claimed.");
    }

    if ((progress[mission.id] ?? 0) < mission.target) {
      throw new HttpsError("failed-precondition", "Mission not complete.");
    }

    tx.set(missionsRef(uid), { day: today, progress, claimed: [...claimed, mission.id] });
    tx.update(walletRef(uid), {
      coins: (wallet.get("coins") ?? 0) + mission.coins,
      gems: (wallet.get("gems") ?? 0) + (mission.gems ?? 0),
    });
    ledger(tx, { uid, type: "mission", missionId: mission.id, coins: mission.coins, gems: mission.gems ?? 0 });
    return { coins: mission.coins, gems: mission.gems ?? 0 };
  });
});
