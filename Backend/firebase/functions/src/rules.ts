/**
 * Pure game-economy rules (no Firebase imports) so they are unit-testable and shared by all
 * functions. Every reward, price and level is computed here on the server, never on clients.
 */

export const STARTING_COINS = 500;
export const STARTING_GEMS = 0;
export const MAX_PLAYERS_PER_MATCH = 32;

export interface PlacementInput {
  uid?: string;
  isBot: boolean;
  /** Final placement in the match, 1 = champion. */
  placement: number;
  /** Rounds the player qualified from. */
  roundsCleared: number;
  won: boolean;
}

export interface Reward {
  coins: number;
  xp: number;
  crowns: number;
}

/**
 * Coins and XP for one match. Everyone gets participation XP; each round cleared adds more; the
 * champion gets a crown and a bonus. Values are tuned for ~15–25 matches per level early on.
 */
export function computeReward(p: PlacementInput, modeId: string): Reward {
  const rounds = clampInt(p.roundsCleared, 0, 5);
  const modeMultiplier = modeId === "quickrace" ? 0.6 : modeId === "private" ? 0 : 1;
  const coins = Math.round((10 + rounds * 20 + (p.won ? 100 : 0)) * modeMultiplier);
  const xp = Math.round((50 + rounds * 40 + (p.won ? 200 : 0)) * Math.max(modeMultiplier, 0.25));
  return { coins, xp, crowns: p.won && modeMultiplier > 0 ? 1 : 0 };
}

/** Total XP needed to reach `level` (level 1 = 0 XP). Gently increasing curve. */
export function xpForLevel(level: number): number {
  if (level <= 1) {
    return 0;
  }

  let total = 0;
  for (let l = 1; l < level; l++) {
    total += 250 + (l - 1) * 50;
  }

  return total;
}

export function levelForXp(xp: number): number {
  let level = 1;
  while (level < 200 && xp >= xpForLevel(level + 1)) {
    level++;
  }

  return level;
}

/** Usernames: 3–16 characters, letters, digits and underscore, must start with a letter. */
export function validateUsername(name: unknown): string | null {
  if (typeof name !== "string") {
    return null;
  }

  const trimmed = name.trim();
  return /^[A-Za-z][A-Za-z0-9_]{2,15}$/.test(trimmed) ? trimmed : null;
}

/** Daily reward streak: day 1..7 rewards, then repeats the last. Gems on day 7. */
export const DAILY_REWARDS: ReadonlyArray<{ coins: number; gems: number }> = [
  { coins: 50, gems: 0 },
  { coins: 75, gems: 0 },
  { coins: 100, gems: 0 },
  { coins: 125, gems: 0 },
  { coins: 150, gems: 0 },
  { coins: 200, gems: 0 },
  { coins: 250, gems: 10 },
];

export function utcDayNumber(nowMs: number): number {
  return Math.floor(nowMs / 86_400_000);
}

/**
 * Next daily-reward state. Claimable once per UTC day; claiming on consecutive days grows the
 * streak, missing a day resets it to 1.
 */
export function nextDailyClaim(lastClaimDay: number | undefined, streak: number | undefined, today: number):
  { claimable: boolean; newStreak: number; reward: { coins: number; gems: number } } {
  if (lastClaimDay === today) {
    return { claimable: false, newStreak: streak ?? 0, reward: { coins: 0, gems: 0 } };
  }

  const newStreak = lastClaimDay === today - 1 ? Math.min((streak ?? 0) + 1, 365) : 1;
  const reward = DAILY_REWARDS[Math.min(newStreak, DAILY_REWARDS.length) - 1];
  return { claimable: true, newStreak, reward };
}

export interface Achievement {
  id: string;
  title: string;
}

export const ACHIEVEMENTS: ReadonlyArray<Achievement & { test: (s: PlayerStats) => boolean }> = [
  { id: "first_match", title: "Into the Rush", test: s => s.matches >= 1 },
  { id: "first_round", title: "Survivor", test: s => s.roundsCleared >= 1 },
  { id: "first_win", title: "Crowned", test: s => s.wins >= 1 },
  { id: "matches_25", title: "Regular", test: s => s.matches >= 25 },
  { id: "rounds_50", title: "Seasoned", test: s => s.roundsCleared >= 50 },
  { id: "wins_10", title: "Royalty", test: s => s.wins >= 10 },
];

export interface PlayerStats {
  matches: number;
  wins: number;
  roundsCleared: number;
}

export function newAchievements(before: PlayerStats, after: PlayerStats, owned: ReadonlyArray<string>): string[] {
  return ACHIEVEMENTS
    .filter(a => !owned.includes(a.id) && !a.test(before) && a.test(after))
    .map(a => a.id);
}

export function clampInt(value: unknown, min: number, max: number): number {
  const n = typeof value === "number" && Number.isFinite(value) ? Math.trunc(value) : min;
  return Math.max(min, Math.min(max, n));
}

/** Validates a server match report body; returns an error message or null. */
export function validateMatchReport(body: any): string | null {
  if (!body || typeof body !== "object") return "Body must be an object";
  if (typeof body.matchId !== "string" || !/^[A-Za-z0-9_-]{6,64}$/.test(body.matchId)) return "Invalid matchId";
  if (!["classic", "quickrace", "private"].includes(body.modeId)) return "Invalid modeId";
  if (!Array.isArray(body.players) || body.players.length < 1 || body.players.length > MAX_PLAYERS_PER_MATCH) return "Invalid players";

  let winners = 0;
  const seen = new Set<string>();
  for (const p of body.players) {
    if (typeof p !== "object" || typeof p.isBot !== "boolean") return "Invalid player entry";
    if (!p.isBot) {
      if (typeof p.uid !== "string" || p.uid.length < 6 || p.uid.length > 128) return "Invalid uid";
      if (seen.has(p.uid)) return "Duplicate uid";
      seen.add(p.uid);
    }
    if (!Number.isInteger(p.placement) || p.placement < 1 || p.placement > MAX_PLAYERS_PER_MATCH) return "Invalid placement";
    if (!Number.isInteger(p.roundsCleared) || p.roundsCleared < 0 || p.roundsCleared > 5) return "Invalid roundsCleared";
    if (p.won === true) winners++;
  }

  if (winners > 1) return "More than one winner";
  return null;
}
