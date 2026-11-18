using System;
using System.Collections.Generic;
using Fusion;
using KnockRush.CameraRig;
using KnockRush.Core.Events;
using KnockRush.Interaction;
using KnockRush.Maps;
using KnockRush.Multiplayer;
using UnityEngine;
using Log = KnockRush.Utilities.Log;

namespace KnockRush.GameModes
{
    /// <summary>
    /// Server-authoritative knockout match: lobby → rounds (race / survival / final) with
    /// qualification quotas → winner. Spawned by the server once per session and kept across map
    /// loads. All outcomes (qualification, elimination, winner) are decided here on the server;
    /// clients only read the replicated state for UI and spectating.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MatchController : NetworkBehaviour, ISessionRules
    {
        public const int MaxRecords = 40;

        /// <summary>Humans + bots per match (bots are server objects, not Fusion players).</summary>
        public const int MaxParticipants = 32;
        private const string Tag = nameof(MatchController);
        private const float CountdownSeconds = 3.5f;
        private const float RoundResultsSeconds = 4f;
        private const float MatchResultsSeconds = 10f;
        private const float ReconnectGraceSeconds = 45f;

        private readonly Dictionary<int, NetworkCharacter> characters = new Dictionary<int, NetworkCharacter>();
        private readonly HashSet<int> playedMaps = new HashSet<int>();
        private GameModeCatalog catalog;
        private System.Random random;
        private bool startRequested;
        private int finishCounter;
        private bool sceneReady;

        [Networked] public MatchPhase Phase { get; set; }
        [Networked] public int RoundIndex { get; set; }
        [Networked] public TickTimer Timer { get; set; }
        [Networked] public int Quota { get; set; }
        [Networked] public int QualifiedThisRound { get; set; }
        [Networked] public int MapIndex { get; set; }
        [Networked] public int ModeIndex { get; set; }
        [Networked] public int WinnerSlot { get; set; }
        [Networked] public int RoundStartTick { get; set; }
        [Networked] public PlayerRef RoomOwner { get; set; }
        [Networked] public NetworkBool BotsEnabled { get; set; }
        [Networked] public float RoundTimeLimitOverride { get; set; }
        [Networked, Capacity(MaxRecords)] public NetworkArray<PlayerRecord> Records => default;

        public static MatchController Current { get; private set; }

        public GameModeDefinition Mode => catalog != null && ModeIndex >= 0 && ModeIndex < catalog.Modes.Count ? catalog.Modes[ModeIndex] : null;

        public MapDefinition CurrentMap => catalog != null && MapIndex >= 0 && MapIndex < catalog.Maps.Count ? catalog.Maps[MapIndex] : null;

        public RoundRule CurrentRule => Mode != null && Mode.Rounds.Length > 0
            ? Mode.Rounds[Mathf.Clamp(RoundIndex, 0, Mode.Rounds.Length - 1)]
            : new RoundRule(MapCategory.Race, 0.5f, 1, false);

        public float TimeRemaining => Timer.RemainingTime(Runner) ?? 0f;

        // -------------------------------------------------------------------------------------
        // Lifecycle
        // -------------------------------------------------------------------------------------
        public override void Spawned()
        {
            Current = this;
            catalog = GameModeCatalog.Load();
            Runner.MakeDontDestroyOnLoad(gameObject);

            if (!HasStateAuthority)
            {
                return;
            }

            var session = NetworkSession.Current;
            var options = session.Options;
            random = new System.Random(Environment.TickCount);
            ModeIndex = Mathf.Max(0, catalog.IndexOfMode(options.GameModeId));
            BotsEnabled = options.FillWithBots && Mode.FillWithBots;
            RoundTimeLimitOverride = options.RoundTimeLimitOverride;
            WinnerSlot = -1;
            MapIndex = -1;
            Phase = MatchPhase.Lobby;
            Timer = TickTimer.CreateFromSeconds(Runner, options.LobbySeconds > 0f ? options.LobbySeconds : Mode.LobbySeconds);

            session.Rules = this;
            MapController.FallInterceptor = HandleFall;
            EventBus.Subscribe<FinishLineCrossedEvent>(OnFinishLineCrossed);

            sceneReady = MapController.Current != null;
            foreach (var player in Runner.ActivePlayers)
            {
                OnPlayerJoined(player);
            }

            Log.Info(Tag, $"Match created: {Mode.DisplayName}, bots {(bool)BotsEnabled}, lobby {TimeRemaining:0}s.");
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (Current == this)
            {
                Current = null;
            }

            EventBus.Unsubscribe<FinishLineCrossedEvent>(OnFinishLineCrossed);
            MapController.FallInterceptor = null;
        }

        public override void Render()
        {
            NetworkObstacleClock.RoundStartTick = RoundStartTick;
            KeepSpectatorCameraOnAPlayer();
        }

        // -------------------------------------------------------------------------------------
        // ISessionRules (server)
        // -------------------------------------------------------------------------------------
        public void OnPlayerJoined(PlayerRef player)
        {
            if (!HasStateAuthority || FindSlot(player) >= 0)
            {
                return;
            }

            var userHash = UserHash(player);
            if (Phase != MatchPhase.Lobby)
            {
                TryReconnect(player, userHash);
                return; // late joiners spectate
            }

            var slot = AddRecord(player, userHash, isBot: false);
            if (slot < 0)
            {
                return;
            }

            if (RoomOwner == PlayerRef.None)
            {
                RoomOwner = player;
            }

            if (sceneReady)
            {
                SpawnFor(slot);
            }
        }

        public void OnPlayerLeft(PlayerRef player)
        {
            if (!HasStateAuthority)
            {
                return;
            }

            var slot = FindSlot(player);
            if (slot < 0)
            {
                return;
            }

            if (Phase == MatchPhase.Lobby)
            {
                DespawnSlot(slot);
                SetRecord(slot, default);
                return;
            }

            var record = Records.Get(slot);
            record.Disconnected = true;
            record.LeftTick = Runner.Tick.Raw;
            SetRecord(slot, record);
            Log.Info(Tag, $"{player} disconnected; holding their spot for {ReconnectGraceSeconds}s.");
        }

        public void OnSceneLoadDone()
        {
            sceneReady = true;
            if (!HasStateAuthority)
            {
                return;
            }

            for (var slot = 0; slot < MaxRecords; slot++)
            {
                var record = Records.Get(slot);
                if (record.Status == RecordStatus.Active || (Phase == MatchPhase.Lobby && record.InUse))
                {
                    SpawnFor(slot);
                }
            }

            if (Phase == MatchPhase.Loading)
            {
                BeginCountdown();
            }
        }

        // -------------------------------------------------------------------------------------
        // Simulation (server)
        // -------------------------------------------------------------------------------------
        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority)
            {
                return;
            }

            ExpireDisconnectedPlayers();

            switch (Phase)
            {
                case MatchPhase.Lobby:
                    if ((Timer.Expired(Runner) && HumanCount() >= Mathf.Min(Mode.MinPlayers, Runner.SessionInfo.MaxPlayers)) || startRequested)
                    {
                        StartMatch();
                    }

                    break;

                case MatchPhase.Countdown:
                    if (Timer.Expired(Runner))
                    {
                        BeginPlaying();
                    }

                    break;

                case MatchPhase.Playing:
                    if (RoundIsOver())
                    {
                        EndRound();
                    }

                    break;

                case MatchPhase.RoundResults:
                    if (Timer.Expired(Runner))
                    {
                        NextRoundOrFinish();
                    }

                    break;

                case MatchPhase.MatchResults:
                    if (Timer.Expired(Runner))
                    {
                        Log.Info(Tag, "Match finished; closing session.");
                        Timer = TickTimer.None;
                        Runner.Shutdown();
                    }

                    break;
            }
        }

        private void StartMatch()
        {
            startRequested = false;
            var humans = HumanCount();
            var total = QualificationRules.ComputeFill(humans, Mode.TargetPlayers, MaxParticipants, BotsEnabled);
            for (var i = humans; i < total; i++)
            {
                AddRecord(PlayerRef.None, 0, isBot: true);
            }

            Runner.SessionInfo.IsOpen = false;
            RoundIndex = 0;
            Log.Info(Tag, $"Match starting with {humans} players + {total - humans} bots.");
            LoadRound();
        }

        private void LoadRound()
        {
            var rule = CurrentRule;
            MapIndex = catalog.PickMap(rule.Category, random, playedMaps);
            playedMaps.Add(MapIndex);
            Quota = QualificationRules.ComputeQuota(ActiveCount(), rule);
            QualifiedThisRound = 0;
            finishCounter = 0;
            Phase = MatchPhase.Loading;
            sceneReady = false;
            DespawnAll();

            var map = CurrentMap;
            Log.Info(Tag, $"Round {RoundIndex + 1}/{Mode.Rounds.Length}: {map.DisplayName} ({rule.Category}), {ActiveCount()} players, quota {Quota}.");
            Runner.LoadScene(SceneRef.FromIndex(NetworkSession.BuildIndexOf(map.SceneName)));
        }

        private void BeginCountdown()
        {
            Phase = MatchPhase.Countdown;
            Timer = TickTimer.CreateFromSeconds(Runner, CountdownSeconds);
            foreach (var character in characters.Values)
            {
                if (character != null)
                {
                    character.InputLocked = true;
                }
            }
        }

        private void BeginPlaying()
        {
            Phase = MatchPhase.Playing;
            RoundStartTick = Runner.Tick.Raw;
            var limit = RoundTimeLimitOverride > 0f ? RoundTimeLimitOverride : (CurrentMap != null ? CurrentMap.TimeLimit : 180f);
            Timer = TickTimer.CreateFromSeconds(Runner, limit);
            foreach (var character in characters.Values)
            {
                if (character != null)
                {
                    character.InputLocked = false;
                }
            }
        }

        private bool RoundIsOver()
        {
            if (Timer.Expired(Runner))
            {
                return true;
            }

            var eliminateOnFall = CurrentMap != null && CurrentMap.FallRule == FallRule.Eliminate;
            if (eliminateOnFall)
            {
                // Survival: ends when only the quota remains alive.
                return ActiveCount() <= Quota;
            }

            // Race: ends when the quota has qualified or nobody is left running.
            return QualifiedThisRound >= Quota || ActiveCount() == 0;
        }

        private void EndRound()
        {
            var survival = CurrentMap != null && CurrentMap.FallRule == FallRule.Eliminate;
            for (var slot = 0; slot < MaxRecords; slot++)
            {
                var record = Records.Get(slot);
                if (record.Status != RecordStatus.Active)
                {
                    continue;
                }

                // Race: didn't finish in time → out. Survival: still standing → through.
                record.Status = survival ? RecordStatus.Qualified : RecordStatus.Eliminated;
                if (survival)
                {
                    QualifiedThisRound++;
                }

                SetRecord(slot, record);
                if (!survival)
                {
                    DespawnSlot(slot);
                }
            }

            foreach (var character in characters.Values)
            {
                if (character != null)
                {
                    character.InputLocked = true;
                }
            }

            Phase = MatchPhase.RoundResults;
            Timer = TickTimer.CreateFromSeconds(Runner, RoundResultsSeconds);
            Log.Info(Tag, $"Round {RoundIndex + 1} over: {QualifiedThisRound} qualified.");
        }

        private void NextRoundOrFinish()
        {
            var qualifiedSlots = new List<int>();
            for (var slot = 0; slot < MaxRecords; slot++)
            {
                if (Records.Get(slot).Status == RecordStatus.Qualified)
                {
                    qualifiedSlots.Add(slot);
                }
            }

            var lastRound = CurrentRule.IsFinal || RoundIndex + 1 >= Mode.Rounds.Length || qualifiedSlots.Count <= 1;
            if (lastRound)
            {
                WinnerSlot = qualifiedSlots.Count > 0 ? BestPlaced(qualifiedSlots) : -1;
                Phase = MatchPhase.MatchResults;
                Timer = TickTimer.CreateFromSeconds(Runner, MatchResultsSeconds);
                Log.Info(Tag, $"Match over. Winner slot {WinnerSlot}, qualifiers {qualifiedSlots.Count}.");
                return;
            }

            foreach (var slot in qualifiedSlots)
            {
                var record = Records.Get(slot);
                record.Status = RecordStatus.Active;
                record.RoundsCleared++;
                record.RoundPlacement = 0;
                SetRecord(slot, record);
            }

            RoundIndex++;
            LoadRound();
        }

        // -------------------------------------------------------------------------------------
        // Course events (server)
        // -------------------------------------------------------------------------------------
        private void OnFinishLineCrossed(FinishLineCrossedEvent crossed)
        {
            if (!HasStateAuthority || Phase != MatchPhase.Playing || !(crossed.Participant is NetworkCharacter character))
            {
                return;
            }

            var slot = SlotOf(character);
            if (slot < 0 || Records.Get(slot).Status != RecordStatus.Active)
            {
                return;
            }

            var record = Records.Get(slot);
            record.Status = RecordStatus.Qualified;
            record.RoundPlacement = (short)(++finishCounter);
            SetRecord(slot, record);
            character.MarkFinished();
            QualifiedThisRound++;
            Log.Info(Tag, $"Slot {slot} qualified (#{finishCounter}).");
        }

        /// <summary>Server fall rule: survival maps eliminate, race maps respawn at the checkpoint.</summary>
        private bool HandleFall(ICourseParticipant participant)
        {
            if (!HasStateAuthority || !(participant is NetworkCharacter character))
            {
                return false;
            }

            var eliminate = Phase == MatchPhase.Playing && CurrentMap != null && CurrentMap.FallRule == FallRule.Eliminate;
            if (!eliminate)
            {
                return false;
            }

            var slot = SlotOf(character);
            if (slot >= 0)
            {
                var record = Records.Get(slot);
                record.Status = RecordStatus.Eliminated;
                record.RoundPlacement = (short)ActiveCount();
                SetRecord(slot, record);
                DespawnSlot(slot);
                Log.Info(Tag, $"Slot {slot} eliminated by falling.");
            }

            return true;
        }

        // -------------------------------------------------------------------------------------
        // Room owner controls (private parties)
        // -------------------------------------------------------------------------------------
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_RequestStart(RpcInfo info = default)
        {
            if (Phase == MatchPhase.Lobby && (info.Source == RoomOwner || info.Source == PlayerRef.None || Runner.IsSinglePlayer))
            {
                startRequested = true;
            }
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_SetBots(NetworkBool enabled, RpcInfo info = default)
        {
            if (Phase == MatchPhase.Lobby && (info.Source == RoomOwner || Runner.IsSinglePlayer))
            {
                BotsEnabled = enabled;
            }
        }

        // -------------------------------------------------------------------------------------
        // Records & characters (server)
        // -------------------------------------------------------------------------------------
        private int AddRecord(PlayerRef player, int userHash, bool isBot)
        {
            for (var slot = 0; slot < MaxRecords; slot++)
            {
                if (Records.Get(slot).InUse)
                {
                    continue;
                }

                SetRecord(slot, new PlayerRecord
                {
                    Player = player,
                    UserHash = userHash,
                    Status = RecordStatus.Active,
                    ColorIndex = (byte)(slot % 8),
                    IsBot = isBot
                });
                return slot;
            }

            Log.Warning(Tag, "Match is full.");
            return -1;
        }

        private void SpawnFor(int slot)
        {
            if (characters.TryGetValue(slot, out var existing) && existing != null)
            {
                return;
            }

            var record = Records.Get(slot);
            var character = NetworkSession.Current.SpawnCharacter(record.Player, slot, record.IsBot, record.ColorIndex);
            characters[slot] = character;
            if (Phase == MatchPhase.Countdown || Phase == MatchPhase.Loading)
            {
                character.InputLocked = true;
            }
        }

        private void DespawnSlot(int slot)
        {
            if (characters.TryGetValue(slot, out var character))
            {
                if (character != null && character.Object != null && character.Object.IsValid)
                {
                    Runner.Despawn(character.Object);
                }

                characters.Remove(slot);
            }

            var record = Records.Get(slot);
            if (!record.IsBot && record.Player != PlayerRef.None)
            {
                NetworkSession.Current.ForgetCharacter(record.Player);
            }
        }

        private void DespawnAll()
        {
            foreach (var slot in new List<int>(characters.Keys))
            {
                DespawnSlot(slot);
            }
        }

        private void TryReconnect(PlayerRef player, int userHash)
        {
            if (userHash == 0)
            {
                return;
            }

            for (var slot = 0; slot < MaxRecords; slot++)
            {
                var record = Records.Get(slot);
                if (!record.Disconnected || record.UserHash != userHash || record.Status == RecordStatus.Eliminated)
                {
                    continue;
                }

                record.Player = player;
                record.Disconnected = false;
                SetRecord(slot, record);
                if (characters.TryGetValue(slot, out var character) && character != null)
                {
                    character.Object.AssignInputAuthority(player);
                    Runner.SetPlayerObject(player, character.Object);
                }

                Log.Info(Tag, $"{player} reconnected into slot {slot}.");
                return;
            }
        }

        private void ExpireDisconnectedPlayers()
        {
            var graceTicks = Mathf.CeilToInt(ReconnectGraceSeconds / Runner.DeltaTime);
            for (var slot = 0; slot < MaxRecords; slot++)
            {
                var record = Records.Get(slot);
                if (!record.Disconnected || record.Status == RecordStatus.Eliminated || Runner.Tick.Raw - record.LeftTick < graceTicks)
                {
                    continue;
                }

                record.Status = RecordStatus.Eliminated;
                SetRecord(slot, record);
                DespawnSlot(slot);
            }
        }

        private void SetRecord(int slot, PlayerRecord record)
        {
            Records.Set(slot, record);
        }

        private int FindSlot(PlayerRef player)
        {
            for (var slot = 0; slot < MaxRecords; slot++)
            {
                var record = Records.Get(slot);
                if (record.InUse && !record.IsBot && record.Player == player)
                {
                    return slot;
                }
            }

            return -1;
        }

        private int SlotOf(NetworkCharacter character)
        {
            foreach (var pair in characters)
            {
                if (pair.Value == character)
                {
                    return pair.Key;
                }
            }

            return -1;
        }

        private int UserHash(PlayerRef player)
        {
            var id = Runner.GetPlayerUserId(player);
            return string.IsNullOrEmpty(id) ? 0 : StableHash(id);
        }

        private static int StableHash(string text)
        {
            unchecked
            {
                var hash = (int)2166136261;
                foreach (var c in text)
                {
                    hash = (hash ^ c) * 16777619;
                }

                return hash == 0 ? 1 : hash;
            }
        }

        private int BestPlaced(List<int> slots)
        {
            var best = slots[0];
            foreach (var slot in slots)
            {
                var placement = Records.Get(slot).RoundPlacement;
                var bestPlacement = Records.Get(best).RoundPlacement;
                if (placement > 0 && (bestPlacement == 0 || placement < bestPlacement))
                {
                    best = slot;
                }
            }

            return best;
        }

        // -------------------------------------------------------------------------------------
        // Queries (all peers)
        // -------------------------------------------------------------------------------------
        public int ActiveCount() => Count(r => r.Status == RecordStatus.Active);

        public int HumanCount() => Count(r => r.InUse && !r.IsBot);

        public int PlayerCount() => Count(r => r.InUse);

        public int QualifiedCount() => Count(r => r.Status == RecordStatus.Qualified);

        private int Count(Func<PlayerRecord, bool> predicate)
        {
            var count = 0;
            for (var slot = 0; slot < MaxRecords; slot++)
            {
                if (predicate(Records.Get(slot)))
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>The local player's record slot, or -1 (spectator / dedicated server).</summary>
        public int LocalSlot()
        {
            if (Runner == null || !Runner.LocalPlayer.IsRealPlayer)
            {
                return -1;
            }

            for (var slot = 0; slot < MaxRecords; slot++)
            {
                var record = Records.Get(slot);
                if (record.InUse && !record.IsBot && record.Player == Runner.LocalPlayer)
                {
                    return slot;
                }
            }

            return -1;
        }

        private void KeepSpectatorCameraOnAPlayer()
        {
            if (Runner == null || Runner.IsServer && !Runner.LocalPlayer.IsRealPlayer)
            {
                return;
            }

            var cameraRig = ThirdPersonCamera.Active;
            if (cameraRig == null || (cameraRig.Target != null && cameraRig.Target.gameObject.activeInHierarchy))
            {
                return;
            }

            var any = FindFirstObjectByType<NetworkCharacter>();
            if (any != null)
            {
                cameraRig.Target = any.transform;
            }
        }
    }
}
