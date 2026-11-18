using System.Collections.Generic;
using KnockRush.GameModes;
using KnockRush.Maps;
using KnockRush.Multiplayer;
using KnockRush.Player.Presentation;
using KnockRush.UI.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace KnockRush.UI.Screens
{
    /// <summary>Shared match helpers for the in-match screens.</summary>
    internal static class MatchView
    {
        public static MatchController Match
        {
            get
            {
                var match = MatchController.Current;
                return match != null && match.Object != null && match.Object.IsValid ? match : null;
            }
        }

        public static string Goal(MatchController match)
        {
            var survival = match.CurrentMap != null && match.CurrentMap.FallRule == FallRule.Eliminate;
            if (match.CurrentRule.IsFinal)
            {
                return survival ? "Last bean standing wins!" : "First to the finish wins!";
            }

            return survival ? $"Survive! {match.ActiveCount()} left · {match.Quota} qualify" : $"Qualified {match.QualifiedThisRound}/{match.Quota}";
        }

        public static string Name(MatchController match, int slot)
        {
            if (slot == match.LocalSlot())
            {
                return "You";
            }

            var record = match.Records.Get(slot);
            return record.IsBot ? $"Bot {slot + 1}" : $"Player {record.Player.PlayerId}";
        }

        /// <summary>Final standings, same order as the server's match report.</summary>
        public static List<int> Standings(MatchController match)
        {
            var ranked = new List<int>();
            for (var slot = 0; slot < MatchController.MaxRecords; slot++)
            {
                if (match.Records.Get(slot).InUse)
                {
                    ranked.Add(slot);
                }
            }

            var winner = match.WinnerSlot;
            ranked.Sort((a, b) =>
            {
                if (a == winner) return -1;
                if (b == winner) return 1;
                var ra = match.Records.Get(a);
                var rb = match.Records.Get(b);
                var byRounds = (rb.RoundsCleared + (rb.Status == RecordStatus.Qualified ? 1 : 0)).CompareTo(ra.RoundsCleared + (ra.Status == RecordStatus.Qualified ? 1 : 0));
                return byRounds != 0 ? byRounds : ra.RoundPlacement.CompareTo(rb.RoundPlacement);
            });
            return ranked;
        }

        public static Color ColorOf(MatchController match, int slot)
        {
            var palette = BeanAppearance.DefaultPalette;
            return palette[match.Records.Get(slot).ColorIndex % palette.Length];
        }

        public static async void Leave()
        {
            UiData.RoomCode = null;
            if (NetworkSession.Current != null)
            {
                await NetworkSession.Current.StopAsync();
            }
        }

        public static void ConfirmLeave(AppUI app)
        {
            DialogScreen.Show(app, "LEAVE MATCH?", "You'll be out of this match. Rewards for rounds you cleared still count.", "LEAVE", Leave, "STAY");
        }
    }

    /// <summary>17 · Match HUD: round, goal and timer; countdown; leave button.</summary>
    public sealed class MatchHudScreen : UIScreen
    {
        private Label round;
        private Label goal;
        private Label timer;
        private Label countdown;
        private int lastCount = -1;
        private float goShownUntil;

        public override ScreenLayer Layer => ScreenLayer.Hud;

        protected override void Build(VisualElement root)
        {
            root.AddToClassList("kr-hud");
            var top = Ui.Row("kr-hud-top");
            top.pickingMode = PickingMode.Ignore;
            round = Ui.Label(string.Empty, "kr-hud-chip", "kr-display");
            top.Add(round);
            top.Add(Ui.Spacer());
            goal = Ui.Label(string.Empty, "kr-hud-chip", "kr-display");
            top.Add(goal);
            timer = Ui.Label(string.Empty, "kr-hud-chip", "kr-display");
            timer.style.marginLeft = 12;
            top.Add(timer);
            top.Add(Ui.Spacer());
            top.Add(Ui.Button("LEAVE", () => MatchView.ConfirmLeave(App), "kr-btn--small", "kr-btn--ghost"));
            root.Add(top);

            countdown = Ui.Label(string.Empty, "kr-hud-banner", "kr-display");
            countdown.pickingMode = PickingMode.Ignore;
            countdown.style.position = Position.Absolute;
            countdown.style.left = countdown.style.right = 0;
            countdown.style.top = Length.Percent(30);
            root.Add(countdown);
        }

        public override void OnShow()
        {
            lastCount = -1;
            countdown.text = string.Empty;
        }

        public override void Tick(float deltaTime)
        {
            var match = MatchView.Match;
            if (match == null)
            {
                return;
            }

            var mapName = match.CurrentMap != null ? match.CurrentMap.DisplayName.ToUpperInvariant() : string.Empty;
            round.text = match.CurrentRule.IsFinal ? $"FINAL · {mapName}" : $"ROUND {match.RoundIndex + 1} · {mapName}";
            goal.text = MatchView.Goal(match);

            if (match.Phase == MatchPhase.Countdown)
            {
                timer.text = string.Empty;
                var count = Mathf.CeilToInt(match.TimeRemaining);
                if (count != lastCount && count > 0)
                {
                    lastCount = count;
                    Pop(count.ToString(), false);
                }
            }
            else if (match.Phase == MatchPhase.Playing)
            {
                timer.text = Ui.FormatTime(match.TimeRemaining);
                timer.EnableInClassList("kr-hud-chip--hurry", match.TimeRemaining < 10f);
                if (lastCount > 0)
                {
                    lastCount = 0;
                    Pop("GO!", false);
                    goShownUntil = Time.unscaledTime + 1f;
                }
                else if (goShownUntil > 0f && Time.unscaledTime > goShownUntil)
                {
                    goShownUntil = 0f;
                    countdown.text = string.Empty;
                }
            }
        }

        private void Pop(string text, bool bad)
        {
            countdown.text = text;
            countdown.EnableInClassList("kr-hud-banner--bad", bad);
            countdown.AddToClassList("kr-hud-banner--pop");
            countdown.schedule.Execute(() => countdown.RemoveFromClassList("kr-hud-banner--pop")).StartingIn(60);
        }

        public override bool OnBack()
        {
            MatchView.ConfirmLeave(App);
            return true;
        }
    }

    /// <summary>18 · Round Qualification: banner over the HUD while playing; summary between rounds.</summary>
    public sealed class QualifiedScreen : UIScreen
    {
        private Label banner;
        private Label detail;
        private Label next;
        private float shownAt;

        public override ScreenLayer Layer => ScreenLayer.Hud;

        protected override void Build(VisualElement root)
        {
            var center = Ui.Col("kr-center", "kr-grow");
            center.pickingMode = PickingMode.Ignore;
            banner = Ui.Label("QUALIFIED!", "kr-hud-banner", "kr-display");
            center.Add(banner);
            detail = Ui.Label(string.Empty, "kr-heading");
            center.Add(detail);
            next = Ui.Label(string.Empty, "kr-subtle");
            center.Add(next);
            root.Add(center);
        }

        public override void OnShow()
        {
            shownAt = Time.unscaledTime;
            banner.AddToClassList("kr-hud-banner--pop");
            banner.schedule.Execute(() => banner.RemoveFromClassList("kr-hud-banner--pop")).StartingIn(80);
            Refresh();
        }

        public override void Tick(float deltaTime)
        {
            Refresh();
            var match = MatchView.Match;
            if (match != null && match.Phase == MatchPhase.Playing && Time.unscaledTime - shownAt > 3f)
            {
                Nav.Remove<QualifiedScreen>(); // back to spectating the rest of the round
            }
        }

        private void Refresh()
        {
            var match = MatchView.Match;
            if (match == null)
            {
                return;
            }

            var between = match.Phase == MatchPhase.RoundResults;
            Root.EnableInClassList("kr-backdrop", between);
            banner.text = between ? "YOU'RE THROUGH!" : "QUALIFIED!";
            var slot = match.LocalSlot();
            var placement = slot >= 0 ? match.Records.Get(slot).RoundPlacement : 0;
            detail.text = between ? $"{match.QualifiedCount()} players move on to the next round" : placement > 0 ? $"#{placement} this round" : string.Empty;
            next.text = between ? $"Next round in {Mathf.CeilToInt(match.TimeRemaining)}s" : "Watch the others battle it out...";
        }

        public override bool OnBack()
        {
            MatchView.ConfirmLeave(App);
            return true;
        }
    }

    /// <summary>19 · Elimination: out of the match; spectate or leave.</summary>
    public sealed class EliminatedScreen : UIScreen
    {
        private Label detail;
        private Button spectate;

        public override ScreenLayer Layer => ScreenLayer.Hud;

        protected override void Build(VisualElement root)
        {
            var center = Ui.Col("kr-center", "kr-grow");
            center.pickingMode = PickingMode.Ignore;
            var banner = Ui.Label("ELIMINATED", "kr-hud-banner", "kr-hud-banner--bad", "kr-display");
            center.Add(banner);
            detail = Ui.Label(string.Empty, "kr-heading");
            center.Add(detail);
            var row = Ui.Row("kr-center");
            spectate = Ui.Button("KEEP WATCHING", () => Nav.Remove<EliminatedScreen>(), "kr-btn--ghost");
            row.Add(spectate);
            row.Add(Ui.Button("LEAVE MATCH", MatchView.Leave, "kr-btn--danger"));
            center.Add(row);
            root.Add(center);
        }

        public override void OnShow()
        {
            var match = MatchView.Match;
            if (match == null)
            {
                return;
            }

            var between = match.Phase == MatchPhase.RoundResults;
            Root.EnableInClassList("kr-backdrop", between);
            Ui.SetVisible(spectate, !between);
            var slot = match.LocalSlot();
            var rounds = slot >= 0 ? match.Records.Get(slot).RoundsCleared : 0;
            detail.text = rounds > 0 ? $"You cleared {rounds} round{(rounds == 1 ? string.Empty : "s")}. Great run!" : "Better luck next time. Practice makes perfect!";
        }

        public override bool OnBack()
        {
            MatchView.Leave();
            return true;
        }
    }

    /// <summary>20 · Final Victory: the champion's celebration with confetti.</summary>
    public sealed class VictoryScreen : UIScreen
    {
        private readonly List<(VisualElement piece, Vector2 velocity, float spin)> confetti = new List<(VisualElement, Vector2, float)>();
        private VisualElement confettiLayer;
        private VisualElement crown;
        private float time;

        public override ScreenLayer Layer => ScreenLayer.Hud;

        protected override void Build(VisualElement root)
        {
            confettiLayer = new VisualElement { pickingMode = PickingMode.Ignore };
            confettiLayer.style.position = Position.Absolute;
            confettiLayer.style.left = confettiLayer.style.top = confettiLayer.style.right = confettiLayer.style.bottom = 0;
            root.Add(confettiLayer);

            var center = Ui.Col("kr-center", "kr-grow");
            center.pickingMode = PickingMode.Ignore;
            crown = Ui.Element("kr-crown");
            center.Add(crown);
            center.Add(Ui.Label("CHAMPION!", "kr-hud-banner", "kr-display"));
            center.Add(Ui.Label("You outlasted everyone. The crown is yours!", "kr-heading"));
            var continueButton = Ui.Button("CONTINUE", () => Nav.SetRoot<MatchResultsScreen>(), "kr-btn--primary");
            continueButton.style.marginTop = 30;
            center.Add(continueButton);
            root.Add(center);
        }

        public override void OnShow()
        {
            time = 0f;
            confetti.Clear();
            confettiLayer.Clear();
            Color[] colors = { new Color32(255, 106, 26, 255), new Color32(255, 209, 43, 255), new Color32(34, 197, 139, 255), new Color32(255, 79, 154, 255), new Color32(91, 46, 255, 255) };
            for (var i = 0; i < 90; i++)
            {
                var piece = new VisualElement { pickingMode = PickingMode.Ignore };
                piece.style.position = Position.Absolute;
                piece.style.width = Random.Range(10f, 22f);
                piece.style.height = Random.Range(16f, 30f);
                piece.style.backgroundColor = colors[i % colors.Length];
                piece.style.left = Random.Range(0f, 1920f);
                piece.style.top = Random.Range(-1100f, -20f);
                confettiLayer.Add(piece);
                confetti.Add((piece, new Vector2(Random.Range(-60f, 60f), Random.Range(220f, 420f)), Random.Range(-360f, 360f)));
            }
        }

        public override void Tick(float deltaTime)
        {
            time += deltaTime;
            foreach (var (piece, velocity, spin) in confetti)
            {
                var left = piece.style.left.value.value + velocity.x * deltaTime;
                var top = piece.style.top.value.value + velocity.y * deltaTime;
                if (top > 1150f)
                {
                    top = -30f;
                }

                piece.style.left = left;
                piece.style.top = top;
                piece.style.rotate = new Rotate(time * spin);
            }

            var bob = Mathf.Sin(time * 4f) * 8f;
            crown.style.translate = new Translate(0, bob);
        }

        public override bool OnBack() => false;
    }

    /// <summary>21 · Match Results: final standings and what happens next.</summary>
    public sealed class MatchResultsScreen : UIScreen
    {
        private Label headline;
        private Label summary;
        private VisualElement list;
        private Label closing;

        public override ScreenLayer Layer => ScreenLayer.Hud;

        protected override void Build(VisualElement root)
        {
            root.AddToClassList("kr-backdrop");
            var top = Ui.Row("kr-topbar");
            headline = Ui.Title("MATCH OVER");
            top.Add(headline);
            top.Add(Ui.Spacer());
            closing = Ui.Label(string.Empty, "kr-subtle");
            top.Add(closing);
            root.Add(top);
            summary = Ui.Label(string.Empty, "kr-heading");
            root.Add(summary);
            list = Ui.Scroll();
            root.Add(list);
            var actions = Ui.Row("kr-center");
            actions.Add(Ui.Button("HOME", MatchView.Leave, "kr-btn--ghost"));
            actions.Add(Ui.Button("PLAY AGAIN", PlayAgain, "kr-btn--primary"));
            root.Add(actions);
        }

        public override void OnShow()
        {
            var match = MatchView.Match;
            list.Clear();
            if (match == null)
            {
                return;
            }

            var localSlot = match.LocalSlot();
            var winner = match.WinnerSlot;
            headline.text = winner >= 0 && winner == localSlot ? "VICTORY!" : "MATCH OVER";
            summary.text = winner >= 0 ? $"Champion: {MatchView.Name(match, winner)}" : "No champion this time: everyone fell!";

            var standings = MatchView.Standings(match);
            for (var i = 0; i < standings.Count; i++)
            {
                var slot = standings[i];
                if (i >= 10 && slot != localSlot)
                {
                    continue; // top 10 + you
                }

                var record = match.Records.Get(slot);
                var row = Ui.Row("kr-list-row");
                row.EnableInClassList("kr-list-row--you", slot == localSlot);
                row.EnableInClassList("kr-list-row--top", i < 3);
                var rank = Ui.Label($"#{i + 1}", "kr-display");
                rank.style.width = 90;
                row.Add(rank);
                var dot = Ui.Element("kr-lobby-dot");
                dot.style.backgroundColor = MatchView.ColorOf(match, slot);
                row.Add(dot);
                row.Add(Ui.Label(MatchView.Name(match, slot), "kr-grow"));
                var cleared = record.RoundsCleared + (record.Status == RecordStatus.Qualified ? 1 : 0);
                row.Add(Ui.Label(slot == winner ? "CROWN" : $"{cleared} round{(cleared == 1 ? string.Empty : "s")}", "kr-display"));
                list.Add(row);
            }
        }

        public override void Tick(float deltaTime)
        {
            var match = MatchView.Match;
            closing.text = match != null && match.Phase == MatchPhase.MatchResults ? $"Back to the lobby in {Mathf.CeilToInt(match.TimeRemaining)}s" : string.Empty;
        }

        private async void PlayAgain()
        {
            var mode = NetworkSession.Current?.Options?.GameModeId ?? UiData.SelectedModeId;
            var practice = NetworkSession.Current?.Runner != null && NetworkSession.Current.Runner.IsSinglePlayer;
            if (NetworkSession.Current != null)
            {
                await NetworkSession.Current.StopAsync();
            }

            if (App != null && mode != "private")
            {
                MatchmakingScreen.Start(App, mode, practice);
            }
        }

        public override bool OnBack()
        {
            MatchView.Leave();
            return true;
        }
    }

    /// <summary>Solo map runs and the dev playground: just a way back to the menu (RaceHud shows the race).</summary>
    public sealed class OfflineHudScreen : UIScreen
    {
        public override ScreenLayer Layer => ScreenLayer.Hud;

        protected override void Build(VisualElement root)
        {
            var top = Ui.Row();
            top.pickingMode = PickingMode.Ignore;
            top.Add(Ui.Spacer());
            top.Add(Ui.Button("MENU", Confirm, "kr-btn--small", "kr-btn--ghost"));
            root.Add(top);
        }

        private void Confirm()
        {
            DialogScreen.Show(App, "BACK TO MENU?", "Leave this course and return to the lobby.", "LEAVE", GoHome, "STAY");
        }

        private async void GoHome()
        {
            if (Core.Services.ServiceLocator.TryGet<Core.SceneManagement.ISceneLoader>(out var loader))
            {
                await App.Run(() => loader.LoadSceneAsync(Core.SceneManagement.SceneNames.MainMenu));
            }
        }

        public override bool OnBack()
        {
            Confirm();
            return true;
        }
    }
}
