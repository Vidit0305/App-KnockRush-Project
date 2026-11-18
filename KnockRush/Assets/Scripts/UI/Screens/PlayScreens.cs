using System;
using System.Threading.Tasks;
using KnockRush.GameModes;
using KnockRush.Matchmaking;
using KnockRush.Multiplayer;
using KnockRush.Player.Presentation;
using KnockRush.UI.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace KnockRush.UI.Screens
{
    /// <summary>5 · Game Mode Selection: public modes with rules summary, online or practice.</summary>
    public sealed class ModeSelectScreen : UIScreen
    {
        private VisualElement list;
        private Toggle practice;

        protected override void Build(VisualElement root)
        {
            root.AddToClassList("kr-backdrop");
            practice = new Toggle("Practice vs bots (offline)");
            practice.AddToClassList("kr-toggle");
            practice.RegisterValueChangedCallback(e => UiData.PracticeMode = e.newValue);
            root.Add(Ui.Header("GAME MODES", () => Nav.Pop(), practice));
            list = Ui.Row("kr-grow", "kr-center");
            root.Add(list);
        }

        public override void OnShow()
        {
            practice.SetValueWithoutNotify(UiData.PracticeMode);
            list.Clear();
            var catalog = GameModeCatalog.Load();
            if (catalog == null)
            {
                return;
            }

            foreach (var mode in catalog.Modes)
            {
                if (mode.ModeId == "private")
                {
                    continue; // reached through Private Room
                }

                var selected = mode.ModeId == UiData.SelectedModeId;
                var card = Ui.Panel("kr-mode-card");
                card.EnableInClassList("kr-card--selected", selected);
                card.Add(Ui.Heading(mode.DisplayName.ToUpperInvariant()));
                card.Add(Ui.Label(mode.Description, "kr-mode-card__text"));
                card.Add(Ui.Label($"{mode.Rounds.Length} round{(mode.Rounds.Length == 1 ? string.Empty : "s")} · up to {mode.TargetPlayers} players", "kr-subtle"));
                card.Add(Ui.Spacer());
                var id = mode.ModeId;
                card.Add(Ui.Button(selected ? "SELECTED" : "SELECT", () =>
                {
                    UiData.SelectedModeId = id;
                    Nav.Pop();
                }, selected ? "kr-btn--good" : "kr-btn"));
                list.Add(card);
            }
        }
    }

    /// <summary>6 · Matchmaking: connecting / finding a match, with cancel.</summary>
    public sealed class MatchmakingScreen : UIScreen
    {
        private Label title;
        private Label status;
        private Label elapsedLabel;
        private VisualElement spinner;
        private Button cancel;
        private float elapsed;
        private bool cancelled;

        public override ScreenLayer Layer => ScreenLayer.Page;

        protected override void Build(VisualElement root)
        {
            root.AddToClassList("kr-backdrop-solid");
            root.Add(SplashScreen.Bubbles());
            var center = Ui.Col("kr-center", "kr-grow");
            spinner = Ui.Element("kr-spinner");
            center.Add(spinner);
            title = Ui.Title("FINDING A MATCH");
            center.Add(title);
            status = Ui.Label(string.Empty, "kr-heading");
            center.Add(status);
            elapsedLabel = Ui.Label("0:00", "kr-subtle");
            center.Add(elapsedLabel);
            cancel = Ui.Button("CANCEL", Cancel, "kr-btn--danger");
            cancel.style.marginTop = 40;
            center.Add(cancel);
            root.Add(center);
        }

        /// <summary>Quick play (online) or practice with bots (on this device).</summary>
        public static void Start(AppUI app, string modeId, bool practice)
        {
            if (!practice && !NetworkSettings.HasAppId)
            {
                practice = true;
                app.Toast("Online play needs a Photon App ID: starting a practice match.");
            }

            var mode = GameModeCatalog.Load() is { } catalog ? HomeScreen.FindMode(catalog, modeId) : null;
            var name = mode != null ? mode.DisplayName : modeId;
            Begin(app, practice ? "PRACTICE" : "FINDING A MATCH", practice ? $"{name} vs bots" : name, async () =>
            {
                var result = practice ? await MatchmakingService.PracticeAsync(modeId) : await MatchmakingService.QuickPlayAsync(modeId);
                return result.Ok ? null : result.ShutdownReason.ToString();
            });
        }

        /// <summary>Shows the screen while <paramref name="connect"/> runs (returns an error or null).</summary>
        public static void Begin(AppUI app, string titleText, string statusText, Func<Task<string>> connect)
        {
            var screen = app.Navigator.Push<MatchmakingScreen>();
            screen.title.text = titleText;
            screen.status.text = statusText;
            screen.Run(connect);
        }

        public override void OnShow()
        {
            elapsed = 0f;
            cancelled = false;
            cancel.SetEnabled(true);
        }

        private async void Run(Func<Task<string>> connect)
        {
            string error;
            try
            {
                error = await connect();
            }
            catch (Exception exception)
            {
                error = Ui.ErrorText(exception);
            }

            if (cancelled)
            {
                if (NetworkSession.Current != null)
                {
                    await NetworkSession.Current.StopAsync();
                }

                return;
            }

            if (error != null)
            {
                App.Toast($"Couldn't join: {error}");
                if (IsVisible)
                {
                    Nav.Pop();
                }
            }
        }

        private void Cancel()
        {
            cancelled = true;
            cancel.SetEnabled(false);
            if (IsVisible)
            {
                Nav.Pop();
            }
        }

        public override void Tick(float deltaTime)
        {
            elapsed += deltaTime;
            elapsedLabel.text = Ui.FormatTime(elapsed);
            spinner.style.rotate = new Rotate((elapsed * 240f) % 360f);
        }

        public override bool OnBack()
        {
            Cancel();
            return true;
        }
    }

    /// <summary>8 · Private Room: host a room (pick a mode, bots on/off) or join with a code.</summary>
    public sealed class PrivateRoomScreen : UIScreen
    {
        private TextField code;
        private Toggle bots;

        protected override void Build(VisualElement root)
        {
            root.AddToClassList("kr-backdrop");
            root.Add(Ui.Header("PRIVATE ROOM", () => Nav.Pop()));
            var row = Ui.Row("kr-grow", "kr-center");

            var host = Ui.Panel("kr-mode-card");
            host.Add(Ui.Heading("HOST A ROOM"));
            host.Add(Ui.Label("Get a code to share with friends. You start the match when everyone's in.", "kr-mode-card__text"));
            bots = new Toggle("Fill empty spots with bots");
            bots.AddToClassList("kr-toggle");
            host.Add(bots);
            host.Add(Ui.Spacer());
            host.Add(Ui.Button("CREATE ROOM", Create, "kr-btn--good"));
            row.Add(host);

            var join = Ui.Panel("kr-mode-card");
            join.Add(Ui.Heading("JOIN A ROOM"));
            join.Add(Ui.Label("Type the 6-character code your friend shared.", "kr-mode-card__text"));
            code = Ui.Input("PR-XXXXXX", 9);
            join.Add(code);
            join.Add(Ui.Spacer());
            join.Add(Ui.Button("JOIN", Join, "kr-btn"));
            row.Add(join);
            root.Add(row);
        }

        private void Create()
        {
            if (!RequireOnline())
            {
                return;
            }

            var withBots = bots.value;
            MatchmakingScreen.Begin(App, "CREATING ROOM", "Private Party", async () =>
            {
                var (result, roomCode) = await MatchmakingService.CreatePrivateRoomAsync("private", withBots);
                UiData.RoomCode = result.Ok ? roomCode : null;
                return result.Ok ? null : result.ShutdownReason.ToString();
            });
        }

        private void Join()
        {
            var typed = code.value;
            if (!RoomCodes.IsValid(RoomCodes.Normalize(typed)))
            {
                App.Toast("That code doesn't look right: 6 letters/numbers.");
                return;
            }

            if (!RequireOnline())
            {
                return;
            }

            MatchmakingScreen.Begin(App, "JOINING ROOM", RoomCodes.Normalize(typed), async () =>
            {
                var result = await MatchmakingService.JoinPrivateRoomAsync(typed);
                UiData.RoomCode = result.Ok ? RoomCodes.Normalize(typed) : null;
                return result.Ok ? null : result.ShutdownReason.ToString();
            });
        }

        private bool RequireOnline()
        {
            if (NetworkSettings.HasAppId)
            {
                return true;
            }

            DialogScreen.Show(App, "ONLINE NOT CONFIGURED", "Private rooms need the Photon App ID to be set (see Docs/PHASE_4_NETWORKING.md). Practice matches work offline.");
            return false;
        }
    }

    /// <summary>7 · Waiting Lobby: players gathering before the first round.</summary>
    public sealed class WaitingLobbyScreen : UIScreen
    {
        private Label mode;
        private Label count;
        private Label timer;
        private Label roomCode;
        private VisualElement codeRow;
        private VisualElement grid;
        private Button start;
        private Toggle bots;
        private int shownPlayers = -1;

        public override ScreenLayer Layer => ScreenLayer.Hud;

        protected override void Build(VisualElement root)
        {
            var panel = Ui.Panel("kr-lobby");
            panel.pickingMode = PickingMode.Position;
            mode = Ui.Title("CLASSIC KNOCKOUT");
            panel.Add(mode);
            count = Ui.Label(string.Empty, "kr-heading");
            panel.Add(count);
            timer = Ui.Label(string.Empty, "kr-subtle");
            panel.Add(timer);

            codeRow = Ui.Row("kr-center");
            codeRow.Add(Ui.Label("ROOM CODE", "kr-subtle"));
            roomCode = Ui.Label(string.Empty, "kr-room-code", "kr-display");
            codeRow.Add(roomCode);
            codeRow.Add(Ui.Button("COPY", () =>
            {
                GUIUtility.systemCopyBuffer = UiData.RoomCode;
                App.Toast("Room code copied: send it to your friends!");
            }, "kr-btn--small"));
            panel.Add(codeRow);

            grid = Ui.Element("kr-wrap");
            grid.AddToClassList("kr-lobby-grid");
            panel.Add(grid);

            var actions = Ui.Row("kr-center");
            bots = new Toggle("Bots");
            bots.AddToClassList("kr-toggle");
            bots.RegisterValueChangedCallback(e => MatchController.Current?.RPC_SetBots(e.newValue));
            actions.Add(bots);
            start = Ui.Button("START NOW", () => MatchController.Current?.RPC_RequestStart(), "kr-btn--good");
            actions.Add(start);
            actions.Add(Ui.Button("LEAVE", Leave, "kr-btn--ghost"));
            panel.Add(actions);
            root.Add(panel);
        }

        public override void OnShow()
        {
            shownPlayers = -1;
        }

        public override void Tick(float deltaTime)
        {
            var match = MatchController.Current;
            if (match == null || match.Object == null || !match.Object.IsValid)
            {
                return;
            }

            mode.text = match.Mode != null ? match.Mode.DisplayName.ToUpperInvariant() : "MATCH";
            var target = Mathf.Max(1, match.Mode != null ? match.Mode.TargetPlayers : 32);
            var players = match.PlayerCount();
            count.text = $"{match.HumanCount()} player{(match.HumanCount() == 1 ? string.Empty : "s")} here · {players}/{target}";
            var isOwner = match.Runner.LocalPlayer == match.RoomOwner || match.Runner.IsSinglePlayer;
            var isPrivate = !string.IsNullOrEmpty(UiData.RoomCode) && NetworkSession.Current != null && NetworkSession.Current.Options != null && NetworkSession.Current.Options.IsPrivate;
            timer.text = isPrivate && match.TimeRemaining > 120f ? (isOwner ? "Start when everyone's ready" : "Waiting for the host to start") : $"Starting in {Mathf.CeilToInt(match.TimeRemaining)}s";
            Ui.SetVisible(codeRow, !string.IsNullOrEmpty(UiData.RoomCode) && match.Mode != null && match.Mode.ModeId == "private");
            roomCode.text = UiData.RoomCode ?? string.Empty;
            Ui.SetVisible(start, isOwner);
            Ui.SetVisible(bots, isOwner && match.Mode != null && match.Mode.ModeId == "private");
            bots.SetValueWithoutNotify(match.BotsEnabled);

            if (players != shownPlayers)
            {
                shownPlayers = players;
                RebuildGrid(match);
            }
        }

        private void RebuildGrid(MatchController match)
        {
            grid.Clear();
            var localSlot = match.LocalSlot();
            for (var slot = 0; slot < MatchController.MaxRecords; slot++)
            {
                var record = match.Records.Get(slot);
                if (!record.InUse)
                {
                    continue;
                }

                var dot = Ui.Element("kr-lobby-dot");
                dot.style.backgroundColor = BeanAppearance.DefaultPalette[record.ColorIndex % BeanAppearance.DefaultPalette.Length];
                dot.EnableInClassList("kr-lobby-dot--you", slot == localSlot);
                dot.EnableInClassList("kr-lobby-dot--bot", record.IsBot);
                grid.Add(dot);
            }
        }

        private static async void Leave()
        {
            UiData.RoomCode = null;
            if (NetworkSession.Current != null)
            {
                await NetworkSession.Current.StopAsync();
            }
        }

        public override bool OnBack()
        {
            Leave();
            return true;
        }
    }
}
