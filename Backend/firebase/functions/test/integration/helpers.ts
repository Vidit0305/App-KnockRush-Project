/** Helpers for integration tests against the Firebase Emulator Suite (project demo-knockrush). */
export const PROJECT = "demo-knockrush";
export const REGION = "europe-west1";
export const AUTH = process.env.FIREBASE_AUTH_EMULATOR_HOST ?? "127.0.0.1:9099";
export const FUNCTIONS = "127.0.0.1:5001";
export const SERVER_KEY = "dev-report-key";

export interface TestUser {
  uid: string;
  token: string;
}

export async function signUpAnonymous(): Promise<TestUser> {
  const response = await fetch(`http://${AUTH}/identitytoolkit.googleapis.com/v1/accounts:signUp?key=fake-api-key`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ returnSecureToken: true }),
  });
  const json = await response.json() as { localId: string; idToken: string };
  return { uid: json.localId, token: json.idToken };
}

export class CallError extends Error {
  constructor(public status: string, message: string) {
    super(`${status}: ${message}`);
  }
}

/** Invokes a callable function the way the Unity client does (HTTP + Firebase ID token). */
export async function call<T = any>(name: string, data: unknown, user?: TestUser): Promise<T> {
  const headers: Record<string, string> = { "Content-Type": "application/json" };
  if (user) {
    headers.Authorization = `Bearer ${user.token}`;
  }

  const response = await fetch(`http://${FUNCTIONS}/${PROJECT}/${REGION}/${name}`, {
    method: "POST", headers, body: JSON.stringify({ data: data ?? {} }),
  });
  const json = await response.json() as { result?: T; error?: { status: string; message: string } };
  if (json.error) {
    throw new CallError(json.error.status, json.error.message);
  }

  return json.result as T;
}

export async function report(body: unknown, key: string | null = SERVER_KEY): Promise<{ status: number; json?: any; text?: string }> {
  const headers: Record<string, string> = { "Content-Type": "application/json" };
  if (key) {
    headers["x-knockrush-server-key"] = key;
  }

  const response = await fetch(`http://${FUNCTIONS}/${PROJECT}/${REGION}/reportMatchResult`, {
    method: "POST", headers, body: JSON.stringify(body),
  });
  const text = await response.text();
  try {
    return { status: response.status, json: JSON.parse(text) };
  } catch {
    return { status: response.status, text };
  }
}

export async function expectCallError(promise: Promise<unknown>, status: string): Promise<void> {
  try {
    await promise;
  } catch (error) {
    if (error instanceof CallError && error.status === status) {
      return;
    }

    throw error;
  }

  throw new Error(`Expected ${status} error`);
}
