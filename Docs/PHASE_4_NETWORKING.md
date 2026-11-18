# Phase 4: Multiplayer Networking (Photon Fusion 2)

**Status: networking layer implemented and verified locally.** Online multiplayer (host/client/dedicated server through Photon Cloud) and the 32-player load test are ready to run but **blocked on two items only you can provide** (section 8).

## 1. What we built

| Piece | Description |
|---|---|
| Fusion SDK | Photon Fusion **2.1.4 stable** in `Assets/Photon`. `NetworkProjectConfig`: 32 players, `KnockRush.Multiplayer` woven, lag compensation on. |
| `NetworkSession` | Owns the `NetworkRunner`: starts **Single / Host / Client / Server** sessions, loads the map through Fusion's scene manager, spawns one `NetworkPlayer` per player at the map's spawn points (server only), collects local input each tick, handles leave/shutdown, binds dedicated servers to a public address/port. |
| `NetworkCharacter` | Networked bean. Runs the **same `CharacterMotor`** inside `FixedUpdateNetwork`: the server simulates everyone from inputs, the owning client predicts and is corrected, others see interpolated `NetworkTransform` snapshots. Implements `ICourseParticipant` (checkpoints, finish, knockback; server-only writes) and `ICharacterView` (shared animation). |
| `NetworkInputData` | ~12-byte input: world-space move + Jump/Dive/Grab buttons; press edges derived from the previous tick; move re-clamped on the server. |
| `NetworkMotorState` | Replicated motor state (velocity, mode, timers, ground info); position/yaw come from `NetworkTransform`. |
| `NetworkObstacleClock` | Poses every time-driven obstacle from the **network tick** (also during resimulation), so clients predict obstacles exactly with zero bandwidth. |
| `NetworkSettings` | Player prefab, protocol version (session filter), max players, region; Photon App ID resolution (`-photonAppId`, `KNOCKRUSH_PHOTON_APPID`, or `PhotonAppSettings`). |
| `MapBootstrap` | Map scenes are content-only. Offline → `OfflineRig` (player, camera, controls, HUD, offline race); online → `OnlineRig` (camera, controls, HUD); dedicated server → nothing. |
| `CommandLineLauncher` | Headless roles: `-server`, `-botclient`, `-single`, plus `-session`, `-map`, `-maxPlayers`, `-region`, `-port`, `-publicIp`, `-publicPort`, `-quitAfterSeconds`. Linux `UNITY_SERVER` builds always start as a server. |
| Builds | `Tools/unity.sh windows` (dev player for local server/bot testing), `Tools/unity.sh server` (Linux dedicated server), `Server/Dockerfile` (Edgegap-ready container), `Tools/loadtest.sh` (server + N bots). |
| Main menu (dev) | **Net Practice** (Fusion Single mode), **Host Online**, **Join Online** (enabled once an App ID is set). |

## 2. Authority model (what the server decides)

| Data | Authority |
|---|---|
| Inputs | Client (clamped and edge-decoded on the server) |
| Position, velocity, motor mode | Server simulation; owner predicts; proxies interpolate |
| Knockback impulses, checkpoints, finish, respawn point, input lock | **Server only** (`HasStateAuthority` checks) |
| Obstacles | Deterministic from the tick, identical on all peers |
| Bots | `IsBot` flag on the character (excluded from rewards in Phase 7) |

## 3. Running locally

```bash
Tools/unity.sh windows                        # build KnockRush/Builds/Windows/KnockRush.exe

# Headless local smoke test (no Photon needed):
KnockRush/Builds/Windows/KnockRush.exe -batchmode -nographics -single -quitAfterSeconds 12 -logFile single.log

# With a Photon App ID: dedicated server + bots on this PC
KnockRush/Builds/Windows/KnockRush.exe -batchmode -nographics -server -session dev-01 -photonAppId <ID>
KnockRush/Builds/Windows/KnockRush.exe -batchmode -nographics -botclient -session dev-01 -photonAppId <ID>
# ...or a player: open the game, Main Menu → "Join Online" (session "knockrush-dev")

Tools/loadtest.sh <ID> 31 120                 # 32-player load test (server + 31 bots), summary at the end
```

## 4. Dedicated server deployment (Edgegap)

1. Unity Hub → Installs → 6000.3.28f1 → **Add modules → Linux Dedicated Server Build Support**.
2. `Tools/unity.sh server` → `KnockRush/Builds/LinuxServer/`.
3. `docker build -f Server/Dockerfile -t knockrush-server:0.4 .`
4. Push to Edgegap's container registry; create an application version with UDP port **gameport = 7777** and env `KNOCKRUSH_PHOTON_APPID`.
5. Deploy per region; the server reads `ARBITRIUM_PUBLIC_IP` / `ARBITRIUM_PORT_GAMEPORT_*` and advertises its public address to Photon.

## 5. Verification (performed)

| Check | Result |
|---|---|
| Compile incl. Fusion IL weaving of `KnockRush.Multiplayer` | ✅ 0 errors |
| EditMode | ✅ 19/19 |
| PlayMode | ✅ **27/27**: incl. real Fusion runner (Single mode) on Hammer Rush: server spawns the networked character with state + input authority, online rig camera binds to it, no offline rig spawns, character lands on the start platform through networked simulation, obstacles follow the network tick, falling out respawns at the networked respawn point; input encoding press/hold/clamp |
| Windows player build | ✅ 170 MB, 0 errors |
| **Headless player smoke test** (`-single`) | ✅ boots → launches session → loads Hammer Rush → spawns `[Player:1]` → runs 12 s → clean shutdown, no exceptions |

## 6. Bandwidth notes

Replicated per character: `NetworkTransform` (position/rotation, delta-compressed) + `NetworkMotorState` (~60 bytes, mostly unchanged between ticks) + a few flags. Obstacles: **0 bytes**. Animation: **0 bytes** (derived from state). Actual numbers are measured in the load test (Fusion statistics) once online testing is unblocked.

## 7. Known limitations / next steps

* Knockback is server-authoritative and **not client-predicted**: the owner sees the hit with up to one round trip of delay (hidden by the stumble animation). Client prediction of obstacle hits is a Phase 9 polish item.
* The Grab mechanic (lag-compensated) arrives with the knockout rules in Phase 5.

## 8. Blocked items (need you)

| Item | Why I can't do it | What to do |
|---|---|---|
| **Photon Fusion App ID** | Requires creating an app in *your* Photon account (dashboard.photonengine.com → Create New App → Fusion 2). | Paste the App ID into `Assets/Photon/Fusion/Resources/PhotonAppSettings.asset` → *App Id Fusion* (or pass `-photonAppId`). Then run `Tools/loadtest.sh <ID>`. |
| **Linux Dedicated Server module** | The module installer was removed by the system's security software when downloaded automatically. | Unity Hub → Installs → 6000.3.28f1 → Add modules → *Linux Dedicated Server Build Support*. |
| **Edgegap account** | Paid hosting account. | Needed only for production deployment (Phase 10). |
