# KnockRush: Development Checklist

Legend: `[x]` done and verified · `[~]` code written, waiting for in-editor verification · `[ ]` not started

Rule: a phase is **complete** only when every item is `[x]` and its milestone test passes. We never start the next phase before that.

---

## PHASE 1: Unity project setup and architecture: ✅ COMPLETE (2026-11-18)

- [x] Technical architecture documented (`Docs/ARCHITECTURE.md`)
- [x] Dependency versions verified (Unity 6.3 LTS, Fusion 2.1.x, Firebase Unity SDK 14.x)
- [x] Git repository, `.gitignore`, `.gitattributes` (Git LFS), pushed to GitHub
- [x] Unity 6000.3.28f1 with Android Build Support (OpenJDK, SDK, NDK). *iOS + Linux Dedicated Server modules are added in Phase 4.*
- [x] Unity project `KnockRush` created from the **Universal 3D** template; template leftovers removed
- [x] URP 17.3.0 embedded to work around the missing-`.meta` bug in 6000.3.28f1 (ARCHITECTURE §14)
- [x] Core architecture scripts: AppRoot, ServiceLocator, EventBus, SceneLoader, GameConfig, LocalSettings, PlatformSettings, DebugOverlay
- [x] Assembly definitions: Utilities, Core, UI, Editor, Tests.EditMode, Tests.PlayMode
- [x] Editor tooling: `KnockRush > Setup` (steps 1–4, Validate, batch entry) and `KnockRush > Build` (Android dev APK)
- [x] Packages: URP 17.3.0, Input System 1.20.1, Addressables 2.10.3, Test Framework 1.6.0
- [x] Folder structure created (40 folders)
- [x] Player settings applied (`com.knockrushstudio.knockrush`, IL2CPP, ARM64, landscape, linear, v0.1.0)
- [x] Boot (0) + MainMenu (1) scenes; GameConfig asset
- [x] Active Input Handling = Input System Package (New)
- [x] `Validate Project`: all checks pass, 0 failures, 0 warnings
- [x] Compile: 0 errors, 0 warnings in KnockRush code
- [x] EditMode tests: **17/17 passing**
- [x] PlayMode milestone tests: **3/3 passing** (AppRoot boot, Boot → MainMenu, settings persistence)
- [x] Android development APK builds: `KnockRush/Builds/Android/KnockRush-dev.apk` (50.8 MB, IL2CPP ARM64, 0 errors)
- [ ] On-device smoke test (no device was connected during automation): `adb install -r KnockRush/Builds/Android/KnockRush-dev.apk`, check landscape, splash → MainMenu, overlay about 60 FPS

## PHASE 2: Character modelling, rigging, movement and animations  ← **NEXT**
- [ ] Movement settings ScriptableObject + deterministic kinematic motor (pure C#)
- [ ] Ground detection, gravity, jump (coyote/buffer), air control, dive, stumble, knockback impulses
- [ ] Mobile controls: virtual joystick, Jump/Dive/Grab buttons (Input System), keyboard/gamepad for editor
- [ ] Cinemachine 3 third-person camera, touch-drag look, sensitivity from settings, collision
- [ ] Placeholder bean character (capsule) → original Blender bean model with shared rig `KR_Bean_Rig`
- [ ] 13 animations: idle, walk, run, jump, dive, fall, land, hit, stumble, grab, eliminated, celebrate, victory dance
- [ ] Animator driven purely from sim state
- [ ] Falling / respawn at checkpoint
- [ ] Movement test scene ("Playground") + PlayMode tests for the motor

## PHASE 3: Physics, obstacles and first playable map
- [ ] Interaction layer: impulses, kill volumes, checkpoints, surfaces (slippery/bouncy/conveyor)
- [ ] Time-driven obstacle framework (category A) + event-driven (category B)
- [ ] Obstacles: rotating hammer, spinner bar, pendulum, moving platform, bounce pad, falling tile, disappearing bridge
- [ ] MapDefinition ScriptableObject, modular map kit
- [ ] **Hammer Rush** first playable map (offline, single player) start → finish

## PHASE 4: Multiplayer networking and 32-player testing
- [ ] Photon account, App ID (dev), Fusion 2.1.x imported & configured
- [ ] Network runner lifecycle (Host for dev, Server mode for prod), player spawning
- [ ] Motor running in FixedUpdateNetwork with prediction; obstacles tick-driven
- [ ] Server-authoritative knockback, grab (lag compensated), kill volumes
- [ ] Linux dedicated server build + Docker + Edgegap test deployment
- [ ] 32-player load test with headless bots; latency/loss testing; bandwidth & CPU budgets measured

## PHASE 5: Matchmaking, private rooms and knockout system
- [ ] Photon Custom Auth via Firebase ID token (minimal Firebase Auth brought forward if needed)
- [ ] MatchController state machine, rounds, qualification quotas, elimination, spectate
- [ ] Public matchmaking with session filters, regions, protocol versioning
- [ ] Private rooms with codes, owner controls
- [ ] Disconnect/reconnect grace window; bots fill
- [ ] Classic Knockout end-to-end with 2–3 maps

## PHASE 6: Eight maps and three game modes
- [ ] Hammer Rush, Slime Mountain, Sky High (race)
- [ ] Spin Attack, Falling Tiles, Pendulum Panic (survival)
- [ ] Crown Climber, Last Platform (final)
- [ ] Classic Knockout, Quick Race, Private Party
- [ ] Bot paths for every map; Addressables groups for maps

## PHASE 7: Firebase backend and player progression
- [ ] Firebase projects (dev/staging/prod), Unity SDK, Auth (guest, Google, Apple, linking)
- [ ] Firestore data model + security rules + emulator tests
- [ ] Cloud Functions: results reporting, XP/levels, match history, stats, leaderboards, friends
- [ ] App Check

## PHASE 8: UI, customisation, shop and monetisation
- [ ] UI framework (screen stack, safe area), all 22 screens
- [ ] Customisation: 20 costumes, 15 colourways, 10 emotes; equip flow
- [ ] Economy: coins/gems, shop, daily rewards, missions, achievements, season pass
- [ ] Unity IAP + server receipt validation; rewarded ads with SSV

## PHASE 9: Audio, visual effects and optimisation
- [ ] Audio mixer, music, SFX set; settings integration
- [ ] VFX set (jump, land, collision, speed trail, finish, victory, elimination, ambient)
- [ ] Quality tiers, LODs, occlusion, pooling, memory & battery profiling on devices

## PHASE 10: Testing, beta and deployment
- [ ] Full QA matrix (section 15 of the brief), security review, crash testing
- [ ] Closed beta (Play internal/closed track, TestFlight)
- [ ] Store listings, privacy labels, age ratings; production release
