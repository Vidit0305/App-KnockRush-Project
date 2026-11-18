/** Shared Firebase setup and helpers for all functions. */
import { initializeApp, getApps } from "firebase-admin/app";
import { FieldValue, getFirestore, Transaction } from "firebase-admin/firestore";
import { HttpsError, CallableRequest } from "firebase-functions/v2/https";
import { setGlobalOptions } from "firebase-functions/v2";

if (getApps().length === 0) {
  initializeApp();
}

setGlobalOptions({ region: "europe-west1", maxInstances: 20 });

export const db = getFirestore();

export function requireUid(request: CallableRequest<unknown>): string {
  const uid = request.auth?.uid;
  if (!uid) {
    throw new HttpsError("unauthenticated", "Sign in first.");
  }

  return uid;
}

export const userRef = (uid: string) => db.collection("users").doc(uid);
export const walletRef = (uid: string) => userRef(uid).collection("private").doc("wallet");
export const progressRef = (uid: string) => userRef(uid).collection("private").doc("progress");
export const missionsRef = (uid: string) => userRef(uid).collection("private").doc("missions");
export const inventoryRef = (uid: string, itemId: string) => userRef(uid).collection("inventory").doc(itemId);

export function ledger(tx: Transaction, entry: Record<string, unknown>): void {
  tx.create(db.collection("ledger").doc(), { ...entry, at: FieldValue.serverTimestamp() });
}

export const isEmulator = () => process.env.FUNCTIONS_EMULATOR === "true";
