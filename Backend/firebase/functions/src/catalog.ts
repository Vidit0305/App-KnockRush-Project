/**
 * Authoritative cosmetic catalogue (server side). The Unity client mirrors ids for visuals, but
 * prices and availability are only ever read from here. Everything is cosmetic: nothing affects
 * gameplay (no pay-to-win).
 */

export type Currency = "coins" | "gems";
export type Slot = "body" | "hat" | "color" | "pattern" | "emote";
export type Rarity = "common" | "rare" | "epic" | "legendary";

export interface CatalogItem {
  id: string;
  name: string;
  slot: Slot;
  rarity: Rarity;
  price: number;
  currency: Currency;
  /** Granted to every new player. */
  starter?: boolean;
  /** Only obtainable through the season pass / events (not sold in the shop). */
  exclusive?: boolean;
}

const PRICE: Record<Rarity, { price: number; currency: Currency }> = {
  common: { price: 400, currency: "coins" },
  rare: { price: 900, currency: "coins" },
  epic: { price: 300, currency: "gems" },
  legendary: { price: 700, currency: "gems" },
};

function item(id: string, name: string, slot: Slot, rarity: Rarity, extra: Partial<CatalogItem> = {}): CatalogItem {
  return { id, name, slot, rarity, ...PRICE[rarity], ...extra };
}

const starter = { starter: true, price: 0, currency: "coins" as Currency };
const exclusive = { exclusive: true };

export const CATALOG: ReadonlyArray<CatalogItem> = [
  // 20 costumes (full-body looks: accessory set + pattern + palette, defined in the client)
  item("default_bean", "Classic Rusher", "body", "common", starter),
  item("body_astronaut", "Astro Rusher", "body", "epic"),
  item("body_dino", "Dino Rusher", "body", "legendary"),
  item("body_chef", "Chef Rusher", "body", "rare"),
  item("body_pirate", "Pirate Rusher", "body", "rare"),
  item("body_ninja", "Ninja Rusher", "body", "epic"),
  item("body_robot", "Robo Rusher", "body", "epic"),
  item("body_knight", "Knight Rusher", "body", "legendary"),
  item("body_wizard", "Wizard Rusher", "body", "epic"),
  item("body_bunny", "Bunny Rusher", "body", "rare"),
  item("body_cat", "Kitty Rusher", "body", "rare"),
  item("body_pumpkin", "Pumpkin Rusher", "body", "rare"),
  item("body_snowman", "Snow Rusher", "body", "rare"),
  item("body_superhero", "Super Rusher", "body", "legendary"),
  item("body_viking", "Viking Rusher", "body", "epic"),
  item("body_cowboy", "Cowboy Rusher", "body", "common"),
  item("body_clown", "Clown Rusher", "body", "common"),
  item("body_alien", "Alien Rusher", "body", "epic", exclusive),
  item("body_dragon", "Dragon Rusher", "body", "legendary", exclusive),
  item("body_golden", "Golden Rusher", "body", "legendary", exclusive),

  // 15 colourways
  item("default_orange", "Rush Orange", "color", "common", starter),
  item("color_purple", "Knock Purple", "color", "common"),
  item("color_mint", "Mint", "color", "common"),
  item("color_bubblegum", "Bubblegum", "color", "common"),
  item("color_sky", "Sky", "color", "common"),
  item("color_sunny", "Sunny", "color", "common"),
  item("color_cherry", "Cherry", "color", "rare"),
  item("color_lime", "Lime", "color", "rare"),
  item("color_midnight", "Midnight", "color", "rare"),
  item("color_coral", "Coral", "color", "common"),
  item("color_lavender", "Lavender", "color", "common"),
  item("color_ocean", "Ocean", "color", "rare"),
  item("color_cocoa", "Cocoa", "color", "common"),
  item("color_snow", "Snow", "color", "rare"),
  item("color_rainbow", "Rainbow Shift", "color", "legendary", exclusive),

  // 8 patterns
  item("pattern_none", "Plain", "pattern", "common", starter),
  item("pattern_stripes", "Stripes", "pattern", "common"),
  item("pattern_dots", "Polka Dots", "pattern", "common"),
  item("pattern_zigzag", "Zigzag", "pattern", "rare"),
  item("pattern_stars", "Stars", "pattern", "rare"),
  item("pattern_hearts", "Hearts", "pattern", "rare"),
  item("pattern_checker", "Checkers", "pattern", "epic"),
  item("pattern_flames", "Flames", "pattern", "epic", exclusive),

  // 10 hats / head accessories
  item("hat_none", "No Hat", "hat", "common", starter),
  item("hat_cap", "Rush Cap", "hat", "common"),
  item("hat_crown", "Tiny Crown", "hat", "epic"),
  item("hat_party", "Party Cone", "hat", "common"),
  item("hat_tophat", "Top Hat", "hat", "rare"),
  item("hat_propeller", "Propeller Cap", "hat", "rare"),
  item("hat_horns", "Horns", "hat", "rare"),
  item("hat_halo", "Halo", "hat", "epic"),
  item("hat_bunnyears", "Bunny Ears", "hat", "common"),
  item("hat_headphones", "Headphones", "hat", "rare", exclusive),

  // 10 emotes (incl. victory dances)
  item("emote_celebrate", "Celebrate", "emote", "common", starter),
  item("emote_victory", "Victory Dance", "emote", "common", starter),
  item("emote_wave", "Big Wave", "emote", "common"),
  item("emote_bounce", "Bounce", "emote", "common"),
  item("emote_spin", "Spin", "emote", "rare"),
  item("emote_clap", "Clap", "emote", "common"),
  item("emote_bow", "Bow", "emote", "rare"),
  item("emote_flex", "Flex", "emote", "rare"),
  item("emote_laugh", "Laugh", "emote", "epic"),
  item("emote_robotdance", "Robot Dance", "emote", "legendary", exclusive),
];

export function findItem(id: unknown): CatalogItem | undefined {
  return typeof id === "string" ? CATALOG.find(entry => entry.id === id) : undefined;
}

export const STARTER_ITEMS = CATALOG.filter(entry => entry.starter).map(entry => entry.id);

/** Items that can be bought in the shop (not starter, not season/event exclusive). */
export const SHOP_ITEMS = CATALOG.filter(entry => !entry.starter && !entry.exclusive);

export interface Bundle {
  id: string;
  name: string;
  items: string[];
  price: number;
  currency: Currency;
}

/** Premium cosmetic bundles (discounted sets, still cosmetic only). */
export const BUNDLES: ReadonlyArray<Bundle> = [
  { id: "bundle_space", name: "Space Cadet", items: ["body_astronaut", "color_midnight", "pattern_stars"], price: 450, currency: "gems" },
  { id: "bundle_medieval", name: "Castle Crew", items: ["body_knight", "hat_crown", "color_cherry"], price: 950, currency: "gems" },
  { id: "bundle_party", name: "Party Pack", items: ["hat_party", "pattern_dots", "emote_bounce", "color_bubblegum"], price: 1500, currency: "coins" },
];

export function findBundle(id: unknown): Bundle | undefined {
  return typeof id === "string" ? BUNDLES.find(bundle => bundle.id === id) : undefined;
}

/** Deterministic daily shop: 6 featured items chosen by UTC day (same for everyone). */
export function featuredItems(day: number, count = 6): CatalogItem[] {
  const pool = [...SHOP_ITEMS];
  let seed = (day * 2654435761) >>> 0;
  const next = () => {
    seed = (seed * 1664525 + 1013904223) >>> 0;
    return seed / 0x100000000;
  };
  for (let i = pool.length - 1; i > 0; i--) {
    const j = Math.floor(next() * (i + 1));
    [pool[i], pool[j]] = [pool[j], pool[i]];
  }
  return pool.slice(0, count);
}
