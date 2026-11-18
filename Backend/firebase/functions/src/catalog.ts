/**
 * Authoritative cosmetic catalogue (server side). The Unity client mirrors ids for display, but
 * prices and availability are only ever read from here. Cosmetic only: nothing affects gameplay.
 */

export type Currency = "coins" | "gems";
export type Slot = "body" | "hat" | "color" | "pattern" | "emote";

export interface CatalogItem {
  id: string;
  name: string;
  slot: Slot;
  rarity: "common" | "rare" | "epic" | "legendary";
  price: number;
  currency: Currency;
  /** Granted to every new player. */
  starter?: boolean;
}

export const CATALOG: ReadonlyArray<CatalogItem> = [
  // Starter items
  { id: "default_bean", name: "Classic Bean", slot: "body", rarity: "common", price: 0, currency: "coins", starter: true },
  { id: "default_orange", name: "Rush Orange", slot: "color", rarity: "common", price: 0, currency: "coins", starter: true },
  { id: "emote_celebrate", name: "Celebrate", slot: "emote", rarity: "common", price: 0, currency: "coins", starter: true },
  { id: "emote_victory", name: "Victory Dance", slot: "emote", rarity: "common", price: 0, currency: "coins", starter: true },

  // Colours (coins)
  { id: "color_purple", name: "Knock Purple", slot: "color", rarity: "common", price: 300, currency: "coins" },
  { id: "color_mint", name: "Mint", slot: "color", rarity: "common", price: 300, currency: "coins" },
  { id: "color_bubblegum", name: "Bubblegum", slot: "color", rarity: "common", price: 300, currency: "coins" },
  { id: "color_sky", name: "Sky", slot: "color", rarity: "common", price: 300, currency: "coins" },
  { id: "color_sunny", name: "Sunny", slot: "color", rarity: "common", price: 300, currency: "coins" },
  { id: "color_cherry", name: "Cherry", slot: "color", rarity: "rare", price: 600, currency: "coins" },
  { id: "color_lime", name: "Lime", slot: "color", rarity: "rare", price: 600, currency: "coins" },

  // Hats (coins / gems)
  { id: "hat_cap", name: "Rush Cap", slot: "hat", rarity: "common", price: 500, currency: "coins" },
  { id: "hat_crown", name: "Tiny Crown", slot: "hat", rarity: "epic", price: 250, currency: "gems" },
  { id: "hat_party", name: "Party Cone", slot: "hat", rarity: "rare", price: 900, currency: "coins" },

  // Costumes (gems)
  { id: "body_astronaut", name: "Astro Bean", slot: "body", rarity: "epic", price: 400, currency: "gems" },
  { id: "body_dino", name: "Dino Bean", slot: "body", rarity: "legendary", price: 800, currency: "gems" },

  // Emotes
  { id: "emote_wave", name: "Big Wave", slot: "emote", rarity: "common", price: 400, currency: "coins" },
];

export function findItem(id: unknown): CatalogItem | undefined {
  return typeof id === "string" ? CATALOG.find(item => item.id === id) : undefined;
}

export const STARTER_ITEMS = CATALOG.filter(item => item.starter).map(item => item.id);
