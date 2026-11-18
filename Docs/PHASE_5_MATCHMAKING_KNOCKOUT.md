# Phase 5: Matchmaking, Private Rooms and the Knockout System

**Status: implemented and verified locally** (full matches against bots on a real Fusion runner). Online matchmaking runs as soon as a Photon App ID is configured (see Phase 4 §8).

## 1. What we built

| Piece | Description |
|---|---|
| `MatchController` (GameModes) | Server-authoritative networked state machine kept across map loads: **Lobby → Loading → Countdown → Playing → RoundResults → … → MatchResults**. Replicated: phase, round, timer, quota, map, winner, room owner, bot setting, and a 40-slot `PlayerRecord` array (status, placement, rounds cleared, bot flag, disconnect info). |
| Round rules | **Race maps:** finishing qualifies (placement recorded); round ends when the quota qualifies, nobody is running, or time runs out; non-finishers are eliminated. **Survival maps** (`FallRule.Eliminate`): falling eliminates; round ends when only the quota remains or time runs out; survivors qualify. **Final:** one winner (first finisher / last standing). |
| `QualificationRules` | Quotas from ratios with "always eliminate at least one": Classic 32 → 20 → 10 → 5 → 1. Bot fill up to the mode's target (never replacing humans). |
| Game modes | **Classic Knockout** (Race → Survival → Survival → Final, 32 players), **Quick Race** (one race, top half wins, 24 players), **Private Party** (owner-started, bots optional). Assets in `Assets/ScriptableObjects/GameModes`; catalog (modes + map pool) in `Resources/GameModeCatalog.asset`. |
| Map selection | Random map of the round's category, avoiding repeats; falls back to race maps while Phase 6 adds the other categories. |
| Bots | Server-side `BotBrain` produces normal `MotorInput` (same physics as humans): follows checkpoints to the finish, jumps blockers and gaps, dives across gaps; survival bots circle the safe area. Flagged `IsBot`; only fill empty slots. |
| Disconnects | A leaving player keeps their slot for **45 s** (character stays, no input); rejoining with the same user id reclaims it (input authority reassigned); otherwise eliminated. Late joiners spectate. |
| Spectating | Eliminated players' cameras automatically follow a remaining character. |
| Room owner | First human in a private room; `RPC_RequestStart` / `RPC_SetBots` are accepted only from the owner. |
| Matchmaking | `MatchmakingService`: **Quick Play** (joins a session filtered by `mode` + `proto`, or hosts one while there are no dedicated servers), **Create Private Party** (hidden session `PR-<code>`), **Join by code**, **Practice vs bots** (Fusion Single mode). |
| Room codes | 6 characters from `ABCDEFGHJKMNPQRSTUVWXYZ23456789` (no look-alikes), normalised input (case, spaces, dashes). |
| UI (placeholder) | `MatchHud` (lobby count + room code + START, round banner + goal, countdown, qualified counter + timer, QUALIFIED / ELIMINATED / spectating, round results, champion). Main menu: practice Classic / Quick Race, online Play Classic, Create Party, Join code. The styled UI comes in Phase 8. |

## 2. Match flow (server)

```
Lobby (free roam, START by owner or timer) ─► fill bots ─► LoadRound(map by category, quota)
   ─► OnSceneLoadDone: spawn active players ─► Countdown 3.5 s (inputs locked)
   ─► Playing (obstacle clock = 0 at GO) ─► RoundIsOver? ─► RoundResults 4 s
   ─► qualifiers → Active, next round   or   final/≤1 left → MatchResults 10 s → session closes
```

## 3. Verification

| Check | Result |
|---|---|
| EditMode | ✅ **25/25**: Classic quota chain 32 → 20 → 10 → 5 → 1; every quota eliminates ≥ 1 for 2–32 players; bot fill rules; room codes valid/unambiguous; input normalisation |
| PlayMode | ✅ **28/28**, incl. a full **Quick Race match on a real Fusion runner**: 1 human + 23 server bots, quota 12, all 24 characters spawned, countdown → playing, local finisher qualifies as #1, round ends, match results with the local player as winner |

## 4. Known limitations

* Bots are basic: Phase 6 adds hand-authored bot routes per map.
* Online public matchmaking uses peer hosting (`AutoHostOrClient`) until dedicated servers are deployed; switch `MatchmakingService.Backend` to `DedicatedServers` for production.
* Rewards, XP and match history are reported to the backend in Phase 7.
