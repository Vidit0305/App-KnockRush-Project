import { after, before, test } from "node:test";
import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { assertFails, assertSucceeds, initializeTestEnvironment, RulesTestEnvironment } from "@firebase/rules-unit-testing";
import { doc, getDoc, setDoc, updateDoc } from "firebase/firestore";

let env: RulesTestEnvironment;

before(async () => {
  const [host, port] = (process.env.FIRESTORE_EMULATOR_HOST ?? "127.0.0.1:8080").split(":");
  env = await initializeTestEnvironment({
    projectId: "demo-knockrush-rules",
    firestore: { rules: readFileSync(resolve(process.cwd(), "../firestore.rules"), "utf8"), host, port: Number(port) },
  });

  await env.withSecurityRulesDisabled(async context => {
    const db = context.firestore();
    await setDoc(doc(db, "users/alice"), { displayName: "Alice", level: 3, equipped: { body: "default_bean", color: "default_orange", hat: "none", emote: "emote_celebrate" } });
    await setDoc(doc(db, "users/alice/private/wallet"), { coins: 500, gems: 0 });
    for (const item of ["default_bean", "default_orange", "emote_celebrate", "color_purple"]) {
      await setDoc(doc(db, `users/alice/inventory/${item}`), { source: "test" });
    }
    await setDoc(doc(db, "ledger/entry1"), { uid: "alice", coins: 500 });
    await setDoc(doc(db, "usernames/alice"), { uid: "alice" });
  });
});

after(async () => {
  await env?.cleanup();
});

test("wallet is private and read-only", async () => {
  const alice = env.authenticatedContext("alice").firestore();
  const bob = env.authenticatedContext("bob").firestore();
  await assertSucceeds(getDoc(doc(alice, "users/alice/private/wallet")));
  await assertFails(getDoc(doc(bob, "users/alice/private/wallet")));
  await assertFails(setDoc(doc(alice, "users/alice/private/wallet"), { coins: 999999, gems: 999999 }));
});

test("profiles are public to signed-in players only", async () => {
  await assertSucceeds(getDoc(doc(env.authenticatedContext("bob").firestore(), "users/alice")));
  await assertFails(getDoc(doc(env.unauthenticatedContext().firestore(), "users/alice")));
});

test("owners can equip owned cosmetics only, and nothing else", async () => {
  const alice = env.authenticatedContext("alice").firestore();
  const owned = { body: "default_bean", color: "color_purple", hat: "none", emote: "emote_celebrate" };
  await assertSucceeds(updateDoc(doc(alice, "users/alice"), { equipped: owned }));
  await assertFails(updateDoc(doc(alice, "users/alice"), { equipped: { ...owned, body: "body_dino" } }));
  await assertFails(updateDoc(doc(alice, "users/alice"), { level: 99 }));
  await assertFails(updateDoc(doc(env.authenticatedContext("bob").firestore(), "users/alice"), { equipped: owned }));
});

test("inventory, ledger and usernames cannot be written by clients", async () => {
  const alice = env.authenticatedContext("alice").firestore();
  await assertFails(setDoc(doc(alice, "users/alice/inventory/body_dino"), { source: "hack" }));
  await assertFails(getDoc(doc(alice, "ledger/entry1")));
  await assertSucceeds(getDoc(doc(alice, "usernames/alice")));
  await assertFails(setDoc(doc(alice, "usernames/hacker"), { uid: "alice" }));
  await assertFails(setDoc(doc(alice, "users/mallory"), { displayName: "M" }));
});
