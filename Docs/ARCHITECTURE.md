# KnockRush: Technical Architecture

> Living document. Versions verified on **2026-11-18**. Update this file whenever an architectural decision changes.

---

## 0. Technology stack (verified versions)

| Layer | Choice | Version policy | Why |
|---|---|---|---|
| Engine | **Unity 6.3 LTS** | Project pinned to `6000.3.28f1`; move to newer 6000.3 patches between phases | Current LTS line (supported to Dec 2027). Unity 6.0 LTS support ended Oct 2026; 6.4–6.6 are not LTS. Photon Fusion 2 officially lists 6.3.x support. |
| Rendering | **URP** | Core package, version locked to the editor | Mobile-first, scalable quality tiers, SRP Batcher, Shader Graph toon shading. |
| Netcode | **Photon Fusion 2** | Pin `2.1.x` (2.1.4 stable, Oct 2026). Upgrade only between phases. | Tick-based state sync, client-side prediction & resimulation, lag compensation, dedicated **Server Mode**, up to 200 players/room. |
| Dedicated server hosting | **Edgegap** (primary), Gameye (fallback) | Decided after Phase 4 load test | Photon does not host Fusion dedicated servers. Unity Multiplay shut down 31 Mar 2026. Edgegap has a documented Fusion 2 integration. |
| Backend | **Firebase**: Auth, Cloud Firestore, Cloud Functions (2nd gen, TypeScript), Remote Config, Crashlytics, Analytics, Cloud Messaging, App Check | Firebase Unity SDK `14.x` (14.1.0, Oct 2026; min Unity 2022.3) | Managed auth with Google/Apple/guest, real-time document DB, server-side logic for every trusted operation. |
| Input | **Input System** package | Version resolved by editor | Touch + on-screen controls, gamepad/keyboard for testing. |
| Camera | **Cinemachine 3** | Phase 2 | Smooth third-person follow with collision. |
| Content delivery | **Addressables** | Installed in Phase 1, used from Phase 6 | Maps & cosmetics as downloadable content; smaller install size. |
| UI | **uGUI + TextMeshPro** (part of `com.unity.ugui` 2.x) | Phase 8 | Most mature path for animated, world-space and screen-space mobile game UI. |
| IAP | **Unity IAP** (`com.unity.purchasing`) | Phase 8 | Google Play Billing + StoreKit, receipts validated server-side. |
| Ads | LevelPlay or AdMob (decide Phase 8) | Phase 8 | Must support **server-side verification** of rewarded ads. |
| 3D | **Blender** (current LTS) | Phase 2 | Original characters, rigs, animations, map kits. |
| VCS | **Git + Git LFS** | Now | Text-serialised Unity assets, LFS for binaries. |

> **iOS note:** iOS builds require **macOS + Xcode** (or a cloud build service such as Unity Build Automation). Development and Android builds can happen entirely on Windows. Plan for Mac access before Phase 10 (ideally before Phase 7 for Apple Sign-In testing).

---

## 1. System overview

```
┌──────────────────────────── MOBILE CLIENT (Unity, Android / iOS) ────────────────────────────┐
│  Presentation: UI · Audio · VFX · Animation   ◄── EventBus / networked state change callbacks │
│  Gameplay: input sampling ─► local prediction of OWN character (Fusion client)                │
│  Backend facade: Firebase Auth · Firestore reads · Callable Functions                         │
└───────────┬──────────────────────────────────────────────┬───────────────────────────────────┘
            │ UDP (Photon Cloud transport)                  │ HTTPS / gRPC (Firebase)
            ▼                                               ▼
┌─────────────────────────────┐   custom auth   ┌──────────────────────────────────────────────┐
│ PHOTON CLOUD                │ ──────────────► │ FIREBASE                                      │
│ Name/Master servers,        │  (verifies      │ Auth · Firestore · Cloud Functions            │
│ regions, session discovery, │  Firebase ID    │ Remote Config · Crashlytics · FCM · App Check │
│ matchmaking filters, relay  │  token)         └──────────────▲───────────────▲───────────────┘
└──────────────┬──────────────┘                                │ signed match  │ allocate server
               │                                               │ result report │ (private rooms,
               ▼                                               │               │  fleet scaling)
┌──────────────────────────────────────────────────────────────┴──┐   ┌─────────┴─────────────┐
│ DEDICATED GAME SERVER  (Unity headless Linux build,             │◄──│ Edgegap orchestration │
│ Fusion GameMode.Server, one match per process, per region)      │   │ (containers, regions) │
│ AUTHORITATIVE FOR: movement simulation, obstacles, collisions,  │   └───────────────────────┘
│ knockback, grabs, qualification, elimination, round flow, bots, │
│ winner, match result report                                     │
└─────────────────────────────────────────────────────────────────┘
```

### Trust boundaries (the most important rule in the project)

| Data | Who decides | Client role |
|---|---|---|
| Player inputs (move, camera yaw, jump/dive/grab) | Client | Sends inputs only |
| Character position, velocity, state | **Dedicated server** | Predicts own character, renders others |
| Obstacle state | **Server** (deterministic from server tick) | Predicts identically |
| Qualification / elimination / winner | **Server** | Displays |
| XP, coins, gems, inventory, season progress | **Cloud Functions** | Reads only |
| Purchases (IAP) | **Cloud Functions** after store receipt validation | Initiates purchase, sends receipt |
| Rewarded-ad rewards | **Cloud Functions** via ad network SSV callback | Shows ad |
| Cosmetic equip | Firestore rules check ownership | Writes equip choice |
| Local settings (volume, sensitivity) | Client | Owns (PlayerPrefs) |

---

## 2. Core architectural principles

1. **Server decides, client predicts and presents.** No client message can grant a win, a qualification, currency or items.
2. **Simulation is separate from presentation.** Movement/obstacle logic is plain, deterministic C# stepped with a fixed delta. Animation, audio, VFX and UI *observe* the simulation and never change it.
3. **One code path for offline, host and dedicated server.** Logic is gated by authority (`HasStateAuthority`, `Runner.IsServer`), not by build type, so we can iterate offline (Phase 2–3), in Host mode (Phase 4 dev) and on dedicated servers (production) with the same code.
4. **Data-driven content.** Movement tuning, maps, game modes, rounds, cosmetics and shop items are ScriptableObjects (client) mirrored by catalog documents (server).
5. **Small, focused assemblies.** One responsibility per script; one assembly per feature folder; dependencies flow one way.
6. **Measure, don't guess.** Every performance and network decision has a budget (section 11) checked by profiling on real devices.

---

## 3. Client architecture

### 3.1 Runtime composition

```
BeforeSceneLoad ─► AppRoot (DontDestroyOnLoad "[KnockRush App]")
                     ├─ loads GameConfig (Resources)
                     ├─ PlatformSettings.Apply (fps, orientation, sleep)
                     ├─ registers + initialises services in order:
                     │     ILocalSettingsService, ISceneLoader          ← Phase 1
                     │     IAudioService                                ← Phase 9
                     │     IAuthService, IProfileService, IEconomyService, IInventoryService ← Phase 7/8
                     │     INetworkSessionService, IMatchmakingService  ← Phase 4/5
                     └─ publishes AppInitializedEvent
Boot scene ─► waits for AppRoot.InitializationTask ─► loads MainMenu
```

* **`AppRoot`** is created by `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]`, so pressing Play in *any* scene gives a fully initialised game.
* **Services** are plain C# objects behind interfaces, resolved through `ServiceLocator`. They are application-lifetime only. Per-match objects (runner, match state, players) live in the **match scope** owned by the networking layer, never in the locator.
* **`EventBus`** carries local, presentation-side signals (scene loaded, app paused, settings changed, "player landed" for VFX). It is never a source of truth for networked game state.
* Static state resets on `SubsystemRegistration`, so **Enter Play Mode Options (no domain reload)** can be enabled for fast iteration.

### 3.2 Assemblies and namespaces

| Folder (`Assets/Scripts/…`) | Assembly | Root namespace | Phase | May reference |
|---|---|---|---|---|
| `Utilities` | `KnockRush.Utilities` | `KnockRush.Utilities` | 1 | – |
| `Core` | `KnockRush.Core` | `KnockRush.Core` | 1 | Utilities |
| `Movement` | `KnockRush.Movement` | `KnockRush.Movement` | 2 | Core, Utilities (**no Fusion, no MonoBehaviour in the motor**) |
| `Camera` | `KnockRush.CameraRig` | `KnockRush.CameraRig` | 2 | Core, Cinemachine, Input System |
| `Player` | `KnockRush.Player` | `KnockRush.Player` | 2 (+Fusion in 4) | Movement, Interaction, Core |
| `Physics` | `KnockRush.Interaction` | `KnockRush.Interaction` | 3 | Movement, Core |
| `Obstacles` | `KnockRush.Obstacles` | `KnockRush.Obstacles` | 3 | Interaction, Core |
| `Maps` | `KnockRush.Maps` | `KnockRush.Maps` | 3 | Obstacles, Core |
| `Multiplayer` | `KnockRush.Multiplayer` | `KnockRush.Multiplayer` | 4 | Fusion, Player, Maps, Core |
| `Matchmaking` | `KnockRush.Matchmaking` | `KnockRush.Matchmaking` | 5 | Multiplayer, Core |
| `GameModes` | `KnockRush.GameModes` | `KnockRush.GameModes` | 5 | Multiplayer, Player, Maps, Core |
| `Backend` | `KnockRush.Backend` | `KnockRush.Backend` | 7 | Firebase, Core |
| `Economy` | `KnockRush.Economy` | `KnockRush.Economy` | 7–8 | Backend, Core |
| `Inventory` | `KnockRush.Inventory` | `KnockRush.Inventory` | 8 | Economy, Backend, Core |
| `Audio` | `KnockRush.Audio` | `KnockRush.Audio` | 9 | Core |
| `UI` | `KnockRush.UI` | `KnockRush.UI` | 1 (placeholders), 8 | everything above |
| `Editor` | `KnockRush.Editor` | `KnockRush.EditorTools` | 1 | everything (Editor only) |
| `Assets/Tests/EditMode` | `KnockRush.Tests.EditMode` | `KnockRush.Tests.EditMode` | 1 | as needed |

**Namespace rule:** never create a namespace segment named `Camera`, `Physics`, `Editor`, `Input`, `Debug`, `Random`, `Time`, `Application` or `Animation`. Inside `KnockRush.*`, a namespace `KnockRush.Physics` would shadow `UnityEngine.Physics` and break every `Physics.Raycast` call. This is why `Camera/` maps to `KnockRush.CameraRig` and `Physics/` to `KnockRush.Interaction`.

An assembly is created when its folder gets its first script, so empty assemblies never exist.

---

## 4. Gameplay simulation

### 4.1 Character movement: deterministic kinematic motor

Party-knockout physics must feel bouncy and chaotic, but **networked rigidbodies with 32 players and client prediction are unreliable** (PhysX is not deterministic across machines, and resimulating a shared physics scene per tick is expensive on mobile). KnockRush therefore uses a **custom kinematic character motor**:

```
MotorState  Step(MotorState state, MotorInput input, MovementSettings settings, float dt, PhysicsScene scene)
```

* Pure function of (state, input, dt), plus capsule casts and overlap tests against the physics scene for collision.
* **Phase 2:** driven offline from `FixedUpdate`.
* **Phase 4:** the *same* motor runs inside Fusion's `FixedUpdateNetwork()`. Its state is stored in `[Networked]` properties, so Fusion can roll back and resimulate the local player's predicted ticks.
* Features: ground detection and slope limit, gravity, variable jump, coyote time, jump buffering, air control, dive (momentum burst + belly slide), stumble, external impulses (knockback with decay), moving-platform carry, rotating-platform carry, slippery surfaces (per-surface friction from `SurfaceDefinition`), player-to-player soft push.
* **Ragdolls are presentation only.** When a character tumbles, the visual model may ragdoll, but the simulated capsule stays authoritative. This is cheap, deterministic and impossible to exploit.

Character simulation states: `Grounded`, `Airborne`, `Diving`, `Sliding`, `Stumbled`, `Tumbling`, `Grabbing`, `Grabbed`, `Respawning`, `Qualified`, `Eliminated`.

### 4.2 Obstacles: three categories

| Category | Examples | Sync strategy | Bandwidth |
|---|---|---|---|
| **A. Time-driven deterministic** | Rotating hammers, spin bars, pendulums, moving platforms, conveyors, scripted rolling balls | `pose = f(roundTime, parameters)` evaluated from the **network tick**. Every client computes the exact pose the server has. | **Zero** per tick |
| **B. Event-driven state** | Falling tiles, disappearing bridges, collapsing platforms, shrinking safe zones | Server sets compact networked state (bit arrays, trigger ticks); clients play predicted visuals. | Bytes per event |
| **C. Dynamic physics props** | Free-rolling balls (only if scripted paths don't feel right) | Server-simulated, interpolated on clients; strict per-map count cap. | Highest; avoid |

Category A is the default. A designer authors a `HammerObstacle` by choosing axis, amplitude, period and phase offset in the Inspector, with no network code.

### 4.3 Interactions

* **Knockback:** obstacles report a hit (contact normal × strength, from `ObstacleImpactDefinition`) and the motor adds an external impulse plus a stumble timer. The server applies it authoritatively; clients predict their own.
* **Grab:** server-side lag-compensated hitbox query (`Runner.LagCompensation`) so grabs register fairly despite latency. The result is networked (`GrabbedBy`), and the grabbed player is slowed.
* **Fall / kill volumes:** a kill height per map plus trigger volumes. Race maps respawn at the last checkpoint; survival and final maps eliminate. Evaluated **on the server only**.

---

## 5. Networking (Photon Fusion 2)

### 5.1 Topology

* **Production:** Fusion **Server Mode**. Each match is one headless Unity Linux server process (`GameMode.Server`); clients join with `GameMode.Client`.
* **Development:** **Host Mode** (one editor/device hosts) and Fusion's multi-peer mode (several peers inside one editor) for fast iteration. Same gameplay code because authority is checked, not assumed.
* **Initial tick rate:** 30 Hz simulation and send rate, re-evaluated with data in Phase 4 (60 Hz is the upgrade path if feel requires it and bandwidth allows).

### 5.2 What is replicated

* **Input (client → server), roughly 8 bytes/tick:** quantised move vector, camera yaw, button bitset (jump, dive, grab, emote).
* **Player state (server → clients):** position, yaw, velocity, sim state enum, timers, grab target, qualification flag. Fusion delta-compresses unchanged values.
* **Match state:** a persistent `MatchController` network object (survives map scene loads): phase, round index, timers, qualification counters, player records.
* **Not replicated:** category A obstacles (derived from tick), animation parameters (derived from sim state), cosmetics (sent once on join as IDs).

### 5.3 Prediction and smoothing

* Local player: predicted with resimulation; visual smoothing hides corrections.
* Remote players: snapshot interpolation.
* Obstacles: predicted (deterministic) so collisions agree with the server.

### 5.4 Anti-cheat

1. **Identity:** Photon **Custom Authentication** calls a Cloud Function that verifies the Firebase ID token; the Photon `UserId` *is* the Firebase UID. Players cannot impersonate others.
2. **Inputs only:** the server simulates every character from inputs, so speed, teleport and fly hacks have nothing to change. Input values are clamped and sanity-checked.
3. **Results:** only the dedicated server reports results, over a server-to-server credential (secret injected into the container, never shipped in the client). The Cloud Function cross-checks the match allocation record, participants and placements, and is **idempotent per matchId**.
4. **App Check** (Play Integrity / App Attest) protects callable Functions from scripted abuse.
5. **Telemetry** flags statistically impossible results for review.

### 5.5 Disconnects and reconnection

* Mobile backgrounding drops the socket. On resume the client reconnects with the same `UserId` into the same session within a **grace window** (target 30–60 s, tuned in Phase 5).
* During the grace window the character stays in the world with neutral input; after it expires the player is eliminated.
* A client crash relaunches into a "rejoin match?" prompt if the match is still running.

### 5.6 Bots

* Server-only `BotController` produces the same input struct as a human, from per-map **bot paths** (waypoints + jump/dive hints authored in Phase 6).
* `PlayerRecord.IsBot` marks bots in every system. Bots never generate rewards, stats, leaderboard entries or match history.
* Used only to fill public matches after a matchmaking timeout, and in private rooms only if the owner enables them.

---

## 6. Match lifecycle (server state machine)

```
WaitingForPlayers ─► LobbyCountdown ─► RoundIntro ─► RoundPlaying ─► RoundResults ─┐
        ▲                                   ▲                                       │
        │                                   └────── next round (qualified only) ◄───┤
        │                                                                           ▼
   (server pool)  ◄── Shutdown ◄── RewardsReported ◄── MatchResults ◄── FinalRound complete
```

* **`GameModeDefinition`** (ScriptableObject) lists rounds: map category pool (Race / Survival / Final), qualification quota as a ratio with min/max, time limit, respawn rules.
* **Classic Knockout:** 32 → 20 → 10 → 5 → 1 (quotas scale proportionally for smaller lobbies; a round always eliminates at least one player).
* **Quick Race:** 16–32 players, a single timed race round with a qualification quota.
* **Private Party:** owner picks mode, map rotation, round count, bots on/off and max players where supported.
* Map scenes load per round through Fusion's network scene management; the `MatchController` persists across loads.
* Eliminated players spectate (cycle through remaining players) or leave. Their placement is already locked in.

---

## 7. Matchmaking, private rooms and server orchestration

* **Public modes:** pre-warmed dedicated servers per region register Fusion sessions with properties `{mode, protocolVersion, state}`. Clients call `StartGame(GameMode.Client)` with session property filters; Photon fills the fullest open session. When a match starts the session is closed (`IsOpen = false`). An autoscaler keeps N idle servers warm per region on Edgegap.
* **Regions:** best-ping Photon region, matched to the same Edgegap location.
* **Private rooms:** a Cloud Function `createPrivateRoom` generates a **6-character code** from an unambiguous alphabet (no 0/O/1/I/L), reserves `rooms/{code}` in Firestore with a TTL, allocates a server through the Edgegap API, and the server opens a hidden session named by the code. Friends join by code.
* **Friends and parties:** Firestore `parties/{partyId}`; the leader matchmakes and members follow into the same session. Invites are delivered through Firestore listeners and FCM push notifications.
* **Protocol versioning:** every session carries `protocolVersion`; clients only see compatible servers. Forced update via Remote Config when the minimum version changes.

---

## 8. Backend (Firebase)

### 8.1 Authentication

* Guest (anonymous) on first launch: zero friction.
* Link to **Google** (Android/iOS) or **Apple** (required on iOS when other social logins exist) without losing progress.
* Username chosen once (uniqueness enforced by a Function using `usernames/{lowercase}`) and changeable with a cooldown.

### 8.2 Firestore data model (initial)

```
users/{uid}                         public profile: displayName, level, equipped cosmetics, stats summary
users/{uid}/private/wallet          coins, gems                     (Functions write only)
users/{uid}/private/progress        xp, level, seasonPass progress  (Functions write only)
users/{uid}/inventory/{itemId}      owned cosmetics                  (Functions write only)
users/{uid}/matchHistory/{matchId}  placement, rewards               (Functions write only)
users/{uid}/friends/{friendUid}     friendship edges
usernames/{nameLower}               → uid (uniqueness)
matches/{matchId}                   allocation + authoritative results
catalog/{itemId}                    shop items, prices, availability (read only)
seasons/{seasonId}                  season pass tiers
leaderboards/{boardId}/entries/{uid}
rooms/{code}                        private room reservations (TTL)
ledger/{txId}                       immutable economy audit log
purchases/{storeTransactionId}      IAP idempotency and audit
```

**Security rules:** deny by default. Clients may read their own private docs and public profiles, and write only low-risk fields (equipped cosmetics, validated with `exists()` against their inventory; friend requests). Every currency, XP, inventory or result change goes through Cloud Functions using Firestore transactions.

### 8.3 Trusted server flows

* `reportMatchResult` (server-to-server): validate, then in one transaction grant XP and coins, advance season pass, write match history and ledger entries, update leaderboards.
* `purchaseShopItem` (callable): price from `catalog`, balance check, grant item, ledger entry.
* `validatePurchase` (callable): verify the receipt with the **Google Play Developer API** or **App Store Server API**, then grant gems once per transaction ID.
* `rewardedAdCallback` (HTTPS): ad-network server-side verification, then grant the reward.
* `claimDailyReward`, `claimMission`: computed with **server time**.

### 8.4 Environments

`Development`, `Staging` and `Production` (`GameConfig.Environment`) map to separate Firebase projects, Photon App IDs and server fleets. Production credentials never appear in development builds.

---

## 9. Content pipeline

* **Characters:** one shared Blender skeleton (`KR_Bean_Rig`, Generic rig, not Humanoid, because the bean proportions don't map to Humanoid well). Every costume is skinned to the same skeleton or attaches to sockets (head, face, back, hands), so **all animations are shared**. Budget: LOD0 ≈ 4k triangles, LOD1 ≈ 1.5k, LOD2 ≈ 600; one material per character plus an atlas (512–1024 px).
* **Toon material:** URP Shader Graph (ramp lighting, rim light, colour mask for customisation), SRP Batcher compatible.
* **Animation:** Mecanim with a small layered controller (locomotion blend tree, actions, upper-body emotes). Animator parameters are **derived from the replicated sim state**, so animation costs no bandwidth.
* **Maps:** modular kit prefabs (platforms, rails, bumpers, decor) plus obstacle prefabs. Each map is a scene with a `MapDefinition` (category, spawn points, checkpoints, finish volumes, kill height, bot paths, music, difficulty). Maps are delivered via Addressables, and new maps can ship as remote content.
* **Cosmetics:** `CosmeticDefinition` (id, slot, rarity, Addressable prefab or material) whose `id` matches `catalog/{itemId}` on the server.

---

## 10. UI, audio and VFX

* **UI:** uGUI + TextMeshPro with a screen-stack navigator (push/pop, modal layer, toasts). Each screen is a View plus a Presenter that talks to services. A `SafeAreaFitter` on every root, Canvas split by update frequency (static vs. dynamic HUD), and screens loaded via Addressables.
* **Audio:** AudioMixer groups (Master / Music / SFX / UI / Voice-emotes) driven by local settings, pooled AudioSources, and 3D attenuation tuned for 32 characters with a voice-count cap.
* **VFX:** Particle System (Shuriken), not VFX Graph, because VFX Graph needs compute shaders that many low-end Android GPUs lack. Everything is pooled.

---

## 11. Performance budgets (targets checked on real devices)

| Metric | High tier | Low tier |
|---|---|---|
| Frame rate | 60 FPS | 30 FPS (stable) |
| SetPass calls / batches | < 150 | < 100 |
| Triangles on screen | < 300k | < 120k |
| Runtime memory | < 1.2 GB | < 750 MB |
| Download per client during a round | ≤ 25 KB/s average (verified Phase 4) | same |
| Server | ≥ 1 full 32-player match per vCPU (verified Phase 4) | – |

Tools: Unity Profiler, Frame Debugger, Memory Profiler, Fusion network statistics, Android GPU Inspector, Xcode Instruments.

Techniques: URP quality tiers per device class, LODs, occlusion culling for race maps, GPU instancing / SRP Batcher, texture compression (ASTC), object pooling, physics layer collision matrix, fixed-timestep tuning, no per-frame allocations in gameplay code, dynamic resolution on low tier.

---

## 12. Testing strategy

| Level | Tooling | From |
|---|---|---|
| Unit (pure logic: services, motor maths, quotas, economy rules) | Unity Test Framework, EditMode | Phase 1 |
| Gameplay (motor, obstacles, triggers) | PlayMode tests | Phase 2–3 |
| Multiplayer | Fusion multi-peer in editor, multiple builds on LAN | Phase 4 |
| 32-player load | Headless bot clients (batchmode) against a cloud server | Phase 4 |
| Network conditions | Fusion / OS network simulation (latency, loss, jitter) | Phase 4 |
| Backend | Firebase Emulator Suite: rules and function tests | Phase 7 |
| Devices | Low / mid / high Android + iPhone matrix | Phase 9–10 |
| Release | Closed beta (Play Console internal / closed track, TestFlight) | Phase 10 |

---

## 13. Repository layout

```
App-KnockRush-Project/
├── KnockRush/              Unity project (created in Phase 1 via Unity Hub)
├── Backend/firebase/       Cloud Functions (TypeScript), firestore.rules, indexes   (Phase 7)
├── Server/                 Dedicated server Dockerfile & deploy scripts              (Phase 4–5)
├── ArtSource/              .blend sources, reference sheets (Git LFS)                (Phase 2)
└── Docs/                   Architecture, checklist, phase guides
```

---

## 14. Known engine issues and workarounds

| Issue | Workaround | Remove when |
|---|---|---|
| Unity **6000.3.28f1**'s built-in URP 17.3.0 ships two editor scripts (`ReadonlyMaterialConverter.MaterialReferenceBuilder.cs`, `…MaterialReferenceChanger.cs`) **without `.meta` files**. Unity ignores them, so `Unity.RenderPipelines.Universal.Editor` fails to compile (`CS0246 MaterialReferenceChanger`). | URP 17.3.0 is **embedded** at `KnockRush/Packages/com.unity.render-pipelines.universal` (copied from the editor's built-in package, metas generated and committed). | The project moves to a 6000.3 patch whose built-in URP includes the metas: delete the embedded folder and let the Package Manager use the built-in copy. |

---

## 15. Risks and open decisions

| Risk / decision | Mitigation / when decided |
|---|---|
| Custom motor is more work than rigidbody movement | Pays for itself in correct prediction; built and tuned offline in Phase 2 before networking. |
| Running costs (Photon CCU + server hours) | Cost model in Phase 5 using Phase 4 measurements; bots allow smaller real-player matches. |
| Hosting provider | Edgegap vs Gameye after the Phase 4 load test. |
| iOS build machine | Mac or cloud build required by Phase 10. |
| Application identifier | Must be final before Phase 7 (baked into Firebase config). |
| Ads SDK | Phase 8, must support SSV. |
| Fusion upgrades | Pin `2.1.x`; upgrade between phases only, with full regression. |
| Originality | No third-party game IP; all characters, maps, names and audio are original or properly licensed. |
