# Phase 3: Physics, Obstacles and the First Playable Map

**Status: complete** (verified 2026-11-18). Hammer Rush is playable offline from the main menu: **Play Hammer Rush (offline)**.

## 1. What we built

### Interaction layer: `KnockRush.Interaction` (`Scripts/Physics`)
| Type | Purpose |
|---|---|
| `IKnockbackTarget`, `ICourseParticipant` | Contracts obstacles and course volumes use to push, respawn, lock and finish characters (implemented by `PlayerCharacter`; Phase 4 networked players implement them too). |
| `IPointVelocityProvider` | World velocity of a moving obstacle at a point (drives knockback direction and strength). |
| `ImpulseZone` | Trigger hitbox that knocks characters: **ObstacleVelocity** (hammers, sweepers), **Radial** (bumpers), **Directional** (bounce pads). Per-target cooldown; publishes `KnockbackEvent` for audio/VFX. |
| `SurfaceModifier` | Static slippery / sticky / conveyor surfaces (`IMovementSurface`). |

### Obstacle framework: `KnockRush.Obstacles`
| Type | Category | Behaviour |
|---|---|---|
| `TimeDrivenObstacle` (base) | A | Pose is a pure function of shared time → identical on every machine, zero bandwidth. Also carries standing characters and provides point velocity. |
| `RotatingObstacle` | A | Sweepers, spinners, rotating discs, windmills. |
| `PendulumObstacle` | A | Swinging hammers, wrecking balls. |
| `OscillatingObstacle` | A | Moving platforms, pistons (eased, with end pauses). |
| `CyclingPlatform` | A | Appears/disappears on a timer, blinks before vanishing. |
| `FallingTile` | B | Shakes when stepped on, then falls; optional respawn. |
| `ObstacleSimulation` | – | Poses all obstacles at the fixed timestep (physics) and at render time (smooth visuals). Phase 4 sets `ExternallyDriven` and poses from the network tick. |

### Maps: `KnockRush.Maps`
`MapDefinition` (ScriptableObject: id, name, category Race/Survival/Final, fall rule, scene, difficulty, player limits, time limit, theme colour) · `MapController` (spawn points, kill height, fall handling) · `Checkpoint` · `FinishLine` · `KillVolume` · `OfflineRaceSession` (3-2-1 countdown with input lock → timer → finish placement / qualification → time limit) · course events.

### UI
`RaceHud` prefab: countdown, race timer + limit, checkpoint / "Whoops!" toasts, QUALIFIED banner.

### Obstacle prefab library: `Assets/Prefabs/Obstacles`
SwingingHammer · SweeperBar · MovingPlatform · Bumper · BouncePad · FallingTile · CyclingTile · RotatingDisc · WindmillHammer · Hurdle. These are reused by all maps in Phase 6.

## 2. Hammer Rush (`Assets/Scenes/Maps/HammerRush.unity`)

```
z -12..10   START        26 m wide platform, 32 spawn points (8×4), start arch
z  10..50   HAMMER ALLEY 12 m path, 4 swinging hammers (staggered 0.7 s)             CP1 z=11
z  50..80   SWEEPERS     16 m field, 2 ground sweeper bars (95°/s, −80°/s)         CP2 z=51
z  80..102  PLATFORM GAP void with 3 sliding platforms (8 m travel, 4–5 s)          CP3 z=78.5
z 102..140  NARROW BRIDGE 5 m: 2 hurdles, 2 fast hammers (2.2 s), centre bumper     CP4 z=103
z 140..165  FINAL RAMP   10° ramp, windmill hammer, 2 bumpers                        CP5 z=141
z 165..180  FINISH       finish arch + finish line (z=172)
```
Fall rule: respawn at the last checkpoint (kill height −10 m plus a kill volume). Time limit 180 s. Scenery: floating islands, clouds, sea, flags, arches.

## 3. How the pieces connect

1. `ObstacleSimulationDriver` (execution order −100) poses every obstacle at `FixedUpdate` and calls `Physics.SyncTransforms()`, so character queries in `PlayerCharacter.FixedUpdate` see the current pose.
2. A character touching a hammer's `Hitbox` (Trigger layer) triggers `ImpulseZone`, which samples the hammer's velocity and calls `ICourseParticipant.ApplyImpulse`, which runs `CharacterMotor.ApplyImpulse` (stumble when the impulse is ≥ 5 m/s).
3. Standing on a moving platform: the motor's ground probe finds the `TimeDrivenObstacle` (`IMovementSurface`) and carries the character with the platform's velocity.
4. `Checkpoint` raises the participant's respawn point; falling below the kill height or into the `KillVolume` respawns there.
5. `FinishLine` triggers `OfflineRaceSession`, which raises `RaceFinishedEvent`. `RaceHud` shows the banner and the character celebrates.

## 4. Regenerating

```bash
Tools/unity.sh content                                         # all generated content incl. obstacles + Hammer Rush
Tools/unity.sh capture Assets/Scenes/Maps/HammerRush.unity     # overview + one render per checkpoint
```

## 5. Verification

| Check | Result |
|---|---|
| EditMode | 19/19 |
| PlayMode | **24/24**: + obstacle maths (rotation/point velocity, pendulum, oscillation, cycling), registry, impulse zone + cooldown, falling tile; Hammer Rush: 32 grounded spawns, ordered checkpoints, finish after last checkpoint, ≥ 12 obstacles; countdown locks then releases input; checkpoint → fall → respawn at checkpoint; **real hammer knocks a standing player**; crossing the finish line qualifies (#1) |
| Visual | Overview + per-checkpoint renders reviewed (all sections built as designed) |
| Android | Development APK builds |

## 6. Common problems

| Symptom | Fix |
|---|---|
| Obstacle does not move | It must be enabled (registers with `ObstacleSimulation`); its moving part needs the obstacle component, not the static root. |
| Hammer passes through players without knocking them | The `Hitbox` child must be on the Trigger layer with a trigger collider; players need their kinematic Rigidbody. |
| Players slide off moving platforms | The platform root must have the `TimeDrivenObstacle` component (it provides surface velocity) and colliders on its children. |
| A new MonoBehaviour component goes "missing" in prefabs | One MonoBehaviour per file, and the file name must match the class name. |
