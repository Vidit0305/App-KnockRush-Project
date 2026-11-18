# Phase 2: Character, Movement, Controls, Camera and Animation

**Status: complete** (verified 2026-11-18). Everything below is generated or tested from the command line.

## 1. What we built

| Area | Deliverable |
|---|---|
| Character art | Original **Bean** character built in Blender 4.5 LTS **from code** (`ArtSource/Characters/build_bean.py`): rounded bean body, big expressive eyes with highlights, smile, short arms, stubby feet. About 5.2k triangles, one skinned mesh, one material. |
| Rig | Shared skeleton `BeanRig`: Root → Hips → Chest → Arm.L/R, Hips → Leg.L/R (Generic rig). All future costumes skin to this rig, so every animation is shared. |
| Animations (13) | Idle, Walk, Run, Jump, Fall, Land, Dive, Hit, Stumble, Grab, Eliminated, Celebrate, VictoryDance (keyframed in the same script). |
| Shader | `KnockRush/Toon` (URP HLSL): two-tone ramp, shadows, SH ambient, rim light, cartoon specular, vertex-colour tint mask (body tintable per player, eyes/mouth fixed), SRP Batcher compatible, ShadowCaster / DepthOnly / DepthNormals passes. |
| Movement | `KnockRush.Movement`: deterministic kinematic **CharacterMotor**: collide-and-slide capsule, ground probe + snapping, slope limit, gravity with fall multiplier, variable jump height, coyote time, jump buffering, air control, dive (momentum burst) → belly slide → recover, stumble from impulses, grab slow-down, moving-surface carry and friction hooks (`IMovementSurface`) for Phase 3. |
| Controls | `PlayerInputReader` (Input System, actions built in code): keyboard/mouse, gamepad and on-screen touch controls. Taps are latched until the next tick. Mobile layout: joystick (left), JUMP / DIVE / GRAB (right), touch-drag look area, safe-area aware. |
| Camera | `ThirdPersonCamera`: smooth orbit follow, pitch limits, wall collision (snap in, ease out), sensitivity and invert-Y from local settings, separate scaling for mouse / touch / stick. |
| Driver | `PlayerCharacter`: steps the motor at the fixed timestep, interpolates the visual between ticks, respawns below the kill height, publishes `CharacterMotorEvent`. |
| Animation driver | `CharacterAnimationDriver`: chooses animation states purely from motor state plus one-shot events (no network bandwidth needed for animation later). |
| Test level | `Assets/Scenes/Dev/Playground.unity`: 15°/30° ramps, 60° too-steep ramp, 0.12 m steps, 1.0/1.4/1.8 m jump platforms, 3 m and 5 m dive gaps, wall, pillars, a ledge to fall off. Reachable from the main menu. |

## 2. Why this design

* The motor is a **pure function of (state, input, dt)** with explicit `PhysicsScene` queries, so Phase 4 runs the *same* code inside Fusion's predicted `FixedUpdateNetwork` and on the dedicated server. Nothing about movement needs rewriting for multiplayer.
* Animation is derived from state, so remote players animate correctly from replicated movement state alone.
* The character is **generated from a script**: it's reproducible, reviewable in Git, and costume variants can reuse the same rig.

## 3. Packages and tools

* Input System 1.20.1 (already installed), uGUI 2.0 (on-screen controls), URP 17.3.0.
* Blender **4.5.16 LTS** (portable, in `.tools/`, not committed) only to regenerate the character.
* **Cinemachine is not used:** a ~200-line custom camera gives exact control, has zero dependencies and is trivially stripped from server builds ([ARCHITECTURE.md](ARCHITECTURE.md) updated).

## 4. Physics layers

| Layer | Index | Used for |
|---|---|---|
| Player | 6 | Character capsules (kinematic Rigidbody + CapsuleCollider). Player-vs-Player PhysX collision disabled. |
| Obstacle | 7 | Moving obstacles (Phase 3). Characters and camera collide with it. |
| Trigger | 8 | Finish lines, checkpoints, kill volumes (Phase 3). |

Motor and camera collision mask = Default + Obstacle (`GameLayers.EnvironmentMask`).

## 5. Prefab structure: `Assets/Prefabs/Characters/PlayerCharacter.prefab`

```
PlayerCharacter (layer Player)
├─ CapsuleCollider (r 0.42, h 1.5, centre y 0.75)   ← sized from MovementSettings
├─ Rigidbody (kinematic, no gravity)                ← lets trigger volumes detect the player
├─ PlayerCharacter (settings, visualRoot, inputReader, cameraRig)
├─ PlayerInputReader
└─ Visual  (CharacterAnimationDriver, BeanAppearance)   ← interpolated between ticks
   └─ Bean (FBX instance, rotated 180° to face +Z) + Animator (Bean.controller)
```

## 6. Default tuning (`Assets/ScriptableObjects/Movement/DefaultMovementSettings.asset`)

Max speed 6.5 m/s · accel 45 / decel 38 m/s² · jump height 1.6 m, gravity 28 m/s² (×1.5 falling, ×2.2 jump-cut) · coyote 0.12 s · jump buffer 0.12 s · dive 9.5 m/s + 4.5 m/s up · slide decel 7 m/s², 0.25–1.0 s · recover 0.35 s · stumble at ≥ 5 m/s impulse for 0.8 s · grab speed ×0.45 · max slope 50°.

## 7. Controls

| Action | Keyboard / mouse | Gamepad | Touch |
|---|---|---|---|
| Move | WASD / arrows | Left stick | Left joystick |
| Look | Hold right mouse + move | Right stick | Drag on right side |
| Jump | Space | A / Cross | JUMP |
| Dive | E / Left Ctrl | X / Square | DIVE |
| Grab | F | B / Circle, RT | GRAB |
| Emotes (dev) | 1 / 2 | D-pad up / down | – |
| Respawn / Menu (Playground) | R / Esc | – | Menu button |

## 8. Regenerating content

```bash
Tools/unity.sh bean        # Blender -> Assets/Art/Characters/Bean/Bean.fbx (+ ArtSource/Characters/Bean.blend)
Tools/unity.sh content     # settings, material, Bean.controller, PlayerCharacter + MobileControls prefabs, Playground scene
Tools/unity.sh capture Assets/Scenes/Dev/Playground.unity Run   # PNG renders in .logs/captures
```
In the editor: **KnockRush → Build → All Generated Content**.

## 9. Verification

| Check | Result |
|---|---|
| Compile | 0 errors, 0 warnings |
| Setup validation (now includes layers) | all pass |
| EditMode tests | **19/19** (Phase 1 + MovementSettings) |
| PlayMode tests | **12/12**: Boot flow (3) + CharacterMotor (9): fall & land, full jump height within 12 %, short-hop lower, reaches max speed & faces move dir, wall stops movement without penetration, dive → slide → recover, 70° slope not walkable / 20° walkable, impulse → stumble → recover, moving surface carries at 2 m/s |
| Visual | Batch-rendered captures confirm toon shading, shadows, correct facing and the Playground layout |
| Android | Development APK builds (see checklist) |

## 10. Common problems

| Symptom | Fix |
|---|---|
| Character walks backwards / faces the camera | The model instance under `Visual` must have Y rotation 180°. Rebuild with `Tools/unity.sh content`. |
| Character sinks into / floats above ground | Collider must match MovementSettings (PlayerCharacter does this in `OnValidate`); level colliders must be on Default or Obstacle. |
| Character snags on small ledges | Steps higher than about 0.15 m need a ramp; the rounded capsule climbs only small edges. |
| No touch look | Scene needs an EventSystem with `InputSystemUIInputModule` (the Playground builder adds one). |
| Pink character | `KnockRush/Toon` failed to compile; check the Console for shader errors. |
