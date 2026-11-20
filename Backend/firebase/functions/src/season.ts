/**
 * Season pass and daily missions (pure rules). Season XP comes from match XP; every tier needs
 * the same amount. The premium track costs gems (bought with real money or earned), cosmetic only.
 */

export interface TierReward {
  coins?: number;
  gems?: number;
  itemId?: string;
}

export const SEASON = {
  id: "s1",
  name: "Season 1: Rush Hour",
  tiers: 30,
  xpPerTier: 500,
  premiumPriceGems: 950,
  /** UTC end of the season (ISO 8601); clients show the countdown. */
  endsAt: "2027-02-28T00:00:00Z",
} as const;

function track(build: (tier: number) => TierReward): TierReward[] {
  return Array.from({ length: SEASON.tiers }, (_, i) => build(i + 1));
}

const FREE_ITEMS: Record<number, string> = {
  5: "pattern_stripes", 10: "color_coral", 15: "emote_clap", 20: "hat_bunnyears", 25: "body_cowboy", 30: "color_lavender",
};

const PREMIUM_ITEMS: Record<number, string> = {
  1: "hat_propeller", 4: "pattern_hearts", 8: "emote_flex", 10: "body_alien", 14: "pattern_flames", 15: "hat_headphones",
  18: "color_ocean", 20: "color_rainbow", 24: "emote_laugh", 26: "body_golden", 28: "emote_robotdance", 30: "body_dragon",
};

export const FREE_TRACK: TierReward[] = track(tier =>
  FREE_ITEMS[tier] ? { itemId: FREE_ITEMS[tier] } : { coins: tier % 3 === 0 ? 150 : 75 });

export const PREMIUM_TRACK: TierReward[] = track(tier =>
  PREMIUM_ITEMS[tier] ? { itemId: PREMIUM_ITEMS[tier] } : tier % 4 === 0 ? { gems: 50 } : { coins: 200 });

export function tierForXp(seasonXp: number): number {
  return Math.max(0, Math.min(SEASON.tiers, Math.floor(seasonXp / SEASON.xpPerTier)));
}

// -------------------------------------------------------------------------------------------
// Daily missions
// -------------------------------------------------------------------------------------------
export type MissionStat = "matches" | "roundsCleared" | "wins" | "finals";

export interface MissionDef {
  id: string;
  title: string;
  stat: MissionStat;
  target: number;
  coins: number;
  gems?: number;
}

export const MISSION_POOL: ReadonlyArray<MissionDef> = [
  { id: "play_3", title: "Play 3 matches", stat: "matches", target: 3, coins: 100 },
  { id: "play_5", title: "Play 5 matches", stat: "matches", target: 5, coins: 175 },
  { id: "rounds_5", title: "Qualify from 5 rounds", stat: "roundsCleared", target: 5, coins: 150 },
  { id: "rounds_10", title: "Qualify from 10 rounds", stat: "roundsCleared", target: 10, coins: 250 },
  { id: "final_1", title: "Reach a final round", stat: "finals", target: 1, coins: 150 },
  { id: "win_1", title: "Win a match", stat: "wins", target: 1, coins: 100, gems: 5 },
];

/** Three missions per player per UTC day, deterministic (same set if fetched again). */
export function dailyMissions(day: number, uid: string): MissionDef[] {
  let hash = day;
  for (const c of uid) {
    hash = (hash * 31 + c.charCodeAt(0)) >>> 0;
  }

  const pool = [...MISSION_POOL];
  const picked: MissionDef[] = [];
  while (picked.length < 3 && pool.length > 0) {
    hash = (hash * 1103515245 + 12345) >>> 0;
    picked.push(pool.splice(hash % pool.length, 1)[0]);
  }

  return picked;
}

export interface MissionMatchDelta {
  matches: number;
  roundsCleared: number;
  wins: number;
  finals: number;
}

/** Adds one match's stats to a mission progress map (only for today's missions). */
export function applyMissionProgress(progress: Record<string, number>, missions: MissionDef[], delta: MissionMatchDelta): Record<string, number> {
  const next = { ...progress };
  for (const mission of missions) {
    next[mission.id] = Math.min(mission.target, (next[mission.id] ?? 0) + delta[mission.stat]);
  }

  return next;
}
