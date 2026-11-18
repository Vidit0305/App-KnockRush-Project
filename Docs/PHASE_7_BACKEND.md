# Phase 7: Firebase Backend and Player Progression

**Status: implemented and verified on the Firebase Emulator Suite.** Going live needs only a Firebase project (section 6).

## 1. Architecture

```
Unity client ──(REST: Firebase Auth + Callable Functions)──► Cloud Functions ──► Firestore
Dedicated server ──(HTTPS + server key)──► reportMatchResult
Photon Cloud ──(custom auth: Firebase ID token)──► photonAuth  → verified UID = Photon UserId
```

* **Cloud Functions are the only writers** of currency, XP, levels, inventory, match history, leaderboards and friendships. Firestore rules deny everything else.
* The Unity client uses a **lightweight REST client** (no native Firebase SDK): smaller builds, one code path on every platform, and fully testable against the emulators.

## 2. Server (`Backend/firebase`)

| Function | Type | Purpose |
|---|---|---|
| `ensureProfile` | callable | First sign-in: profile, wallet (500 coins), progress, starter cosmetics (idempotent) |
| `getProfile` | callable | Profile + wallet + XP/level + inventory + achievements + daily status |
| `setUsername` | callable | Unique, case-insensitive, validated (3–16 chars), 7-day rename cooldown |
| `getCatalog` / `purchaseShopItem` / `equipItems` | callable | Server-priced cosmetic shop (transactional, ledger), ownership-checked equipping |
| `claimDailyReward` | callable | UTC-day streak rewards (day 7 gives gems) |
| `reportMatchResult` | HTTPS (server key) | Validated, **idempotent per matchId**; grants coins/XP/crowns, levels, achievements, match history, leaderboard |
| `getLeaderboard` | callable | Global leaderboard by wins |
| `sendFriendRequest` / `respondFriendRequest` | callable | Friends by username |
| `photonAuth` | HTTPS | Photon custom authentication: verifies the Firebase ID token and returns the UID |

`rules.ts` holds the pure economy rules: rewards, level curve, username rules, daily streaks, achievements and match-report validation. Private parties grant no coins or crowns (no farming). `catalog.ts` is the authoritative price list.

**Firestore rules:** deny by default. Players read public profiles and their own private data, and may only update `equipped` with items they own. Inventory, wallet, usernames, leaderboards, matches and ledger are not client-writable.

## 3. Unity client (`KnockRush.Backend`)

| Type | Purpose |
|---|---|
| `FirebaseRestClient` | Anonymous guest sign-in (session persisted, ID-token refresh), Google/Apple account linking from platform ID tokens (`signInWithIdp`), callable invocation with error mapping |
| `BackendService` (`IBackendService`) | Registered automatically through `AppRoot.RegisterInstaller`; background sign-in at startup (never blocks the splash); offline fallback; profile/shop/daily/equip/leaderboard/friends; `ProfileUpdatedEvent`, `BackendStateChangedEvent` |
| `BackendConfig` | Emulator (development) or production endpoints (`Resources/BackendConfig.asset`) |
| `MatchReportClient` | Dedicated server → `reportMatchResult` with retries; the key comes only from the server environment |
| Matchmaking identity | Signed-in players join sessions with their Firebase ID token (Photon custom auth); the match server reports results against the verified UID |

## 4. Verification

| Suite | Result |
|---|---|
| Functions unit tests (`node:test`) | ✅ 7/7 |
| Functions + rules integration tests (Emulator Suite) | ✅ **12/12**: profile bootstrap, auth required, unique usernames, server-priced purchases + equip checks, daily reward, server-key match reports (forged/invalid rejected, idempotent, rewards/levels/achievements/leaderboard), friends, Photon custom auth, wallet tampering / unowned equip / foreign writes denied |
| Unity client against the emulators | ✅ guest sign-in → profile → catalog → purchase → daily → equip → rejected purchase → session restore |
| Unity PlayMode total | ✅ **38/38** |

Run everything with `Tools/backend-test.sh`.

## 5. Not included / next phases

* **Google Play Games / Google Sign-In and Sign in with Apple** need platform plugins plus store and Apple developer configuration; the client already links accounts from their ID tokens (`LinkAccountAsync`). Plugin wiring comes with store setup in Phase 10.
* **In-app purchase receipt validation and rewarded-ad SSV** are Phase 8 (monetisation).
* **App Check** (Play Integrity / App Attest) is enabled when the real project is created.

## 6. Going live (needs you)

1. Create a Firebase project (Blaze plan for Cloud Functions), enable **Authentication → Anonymous** (and Google/Apple later).
2. `cd Backend/firebase && firebase use --add <project-id>`.
3. `firebase functions:secrets:set MATCH_REPORT_KEY` (any long random string), and give the same value to dedicated servers as `KNOCKRUSH_MATCH_REPORT_KEY`.
4. `firebase deploy --only functions,firestore`.
5. In Unity: `BackendConfig.asset` → project id, Web API key, region `europe-west1`, **Use Emulator off** for production builds.
6. Photon dashboard → your Fusion app → Authentication → Custom server URL = `https://europe-west1-<project-id>.cloudfunctions.net/photonAuth`, query parameter `token`.
