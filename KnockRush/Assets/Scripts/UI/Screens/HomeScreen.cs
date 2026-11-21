using System;
using System.Globalization;
using KnockRush.Backend;
using KnockRush.Core.Events;
using KnockRush.GameModes;
using KnockRush.UI.Framework;
using KnockRush.UI.Stage;
using UnityEngine;
using UnityEngine.UIElements;

namespace KnockRush.UI.Screens
{
    /// <summary>
    /// 4 · Home (lobby): the player's character large in the middle of the arena, framed by
    /// floating icon buttons — profile plate and wallet on top, Shop / Characters / Friends / Party
    /// on the left, Missions / Rewards / Modes on the right, the Rush Pass banner, Leaderboard and
    /// Achievements along the bottom, and the big slanted PLAY button with the selected mode.
    /// </summary>
    public sealed class HomeScreen : UIScreen
    {
        private static SeasonResponse season;

        private VisualElement avatarImage;
        private Label nameLabel;
        private Label levelLabel;
        private VisualElement xpFill;
        private Label coins;
        private Label gems;
        private Label rewardsTime;
        private VisualElement rewardsBadge;
        private Label missionsCount;
        private VisualElement missionsBadge;
        private Label passTier;
        private Label passTime;
        private Label modeLabel;
        private VisualElement offline;
        private bool subscribed;
        private float clock;

        public override bool ShowsStage => true;

        protected override void Build(VisualElement root)
        {
            root.AddToClassList("kr-home");

            // ---- Top-left: avatar, level hexagon, name plate, XP ----
            root.Add(ProfilePlate());

            // ---- Top-right: wallet + settings ----
            var top = Ui.Row("kr-h2-topright");
            top.pickingMode = PickingMode.Ignore;
            top.Add(Wallet("kr-ico--coin", out coins));
            top.Add(Wallet("kr-ico--gem", out gems));
            var settings = HButton(() => Nav.Push<SettingsScreen>(), "kr-h-btn", "kr-h2-gear");
            settings.Add(Ui.Element("kr-ico", "kr-ico--settings", "kr-h2-gear__icon"));
            top.Add(settings);
            root.Add(top);

            offline = Ui.Row("kr-h-offline");
            offline.Add(Ui.Label("OFFLINE  ·  PRACTICE MODES", "kr-display"));
            root.Add(offline);

            // ---- Left column ----
            var left = Ui.Col("kr-h2-left");
            left.pickingMode = PickingMode.Ignore;
            left.Add(Tile("Shop", "kr-ico--cart", () => Nav.Push<ShopScreen>(), out _, out _));
            left.Add(Tile("Characters", "kr-ico--hanger", () => Nav.Push<CharacterSelectScreen>(), out _, out _));
            left.Add(Tile("Friends", "kr-ico--friends", () => Nav.Push<FriendsScreen>(), out _, out _));
            left.Add(Tile("Party", "kr-ico--party", () => Nav.Push<PrivateRoomScreen>(), out _, out _));
            root.Add(left);

            // ---- Right column ----
            var right = Ui.Col("kr-h2-right");
            right.pickingMode = PickingMode.Ignore;
            right.Add(Tile("Missions", "kr-ico--missions", () => Nav.Push<MissionsScreen>(), out missionsCount, out missionsBadge));
            right.Add(Tile("Rewards", "kr-ico--gift", ClaimDaily, out rewardsTime, out rewardsBadge));
            right.Add(Tile("Modes", "kr-ico--modes", () => Nav.Push<ModeSelectScreen>(), out _, out _));
            root.Add(right);

            // ---- Bottom: Rush Pass banner, leaderboard + achievements, PLAY ----
            root.Add(PassBanner());

            var middle = Ui.Row("kr-h2-bottomtiles");
            middle.pickingMode = PickingMode.Ignore;
            middle.Add(Tile("Ranks", "kr-ico--trophy", () => Nav.Push<LeaderboardScreen>(), out _, out _));
            middle.Add(Tile("Awards", "kr-ico--medal", () => Nav.Push<AchievementsScreen>(), out _, out _));
            root.Add(middle);

            var playGroup = Ui.Col("kr-h2-playgroup");
            playGroup.pickingMode = PickingMode.Ignore;
            var modePlate = HButton(() => Nav.Push<ModeSelectScreen>(), "kr-h-btn", "kr-h2-modeplate");
            modePlate.Add(Ui.Element("kr-ico", "kr-ico--crown", "kr-h2-modeplate__icon"));
            modeLabel = Ui.Label("Classic Knockout", "kr-h2-modeplate__text", "kr-display");
            modePlate.Add(modeLabel);
            playGroup.Add(modePlate);
            var play = HButton(() => MatchmakingScreen.Start(App, UiData.SelectedModeId, UiData.PracticeMode), "kr-h-btn", "kr-h2-play", "kr-btn--play");
            play.Add(Ui.Label("PLAY!", "kr-h2-play__text", "kr-h-play__text", "kr-display"));
            playGroup.Add(play);
            root.Add(playGroup);
        }

        // -----------------------------------------------------------------------------------------
        // Building blocks
        // -----------------------------------------------------------------------------------------
        private VisualElement ProfilePlate()
        {
            var plate = HButton(() => Nav.Push<ProfileScreen>(), "kr-h-btn", "kr-h2-profile");
            var frame = Ui.Element("kr-h-avatar", "kr-h2-avatar");
            var inner = Ui.Element("kr-h-avatar__inner");
            avatarImage = Ui.Element("kr-h-avatar__image");
            inner.Add(avatarImage);
            frame.Add(inner);
            plate.Add(frame);

            var hex = Ui.Element("kr-h2-hex");
            levelLabel = Ui.Label("1", "kr-h2-hex__text", "kr-display");
            hex.Add(levelLabel);
            plate.Add(hex);

            var info = Ui.Col("kr-h2-profile__info");
            var namePlate = Ui.Row("kr-h2-nameplate");
            nameLabel = Ui.Label("Player", "kr-h2-nameplate__text", "kr-display");
            namePlate.Add(nameLabel);
            info.Add(namePlate);
            var bar = Ui.Element("kr-h2-xp");
            xpFill = Ui.Element("kr-h2-xp__fill");
            bar.Add(xpFill);
            info.Add(bar);
            plate.Add(info);
            return plate;
        }

        private VisualElement Wallet(string iconClass, out Label value)
        {
            var plate = HButton(() => Nav.Push<ShopScreen>(), "kr-h-btn", "kr-h2-wallet");
            plate.Add(Ui.Element("kr-ico", iconClass, "kr-h2-wallet__icon"));
            value = Ui.Label("0", "kr-h2-wallet__value", "kr-display");
            plate.Add(value);
            var plus = Ui.Element("kr-h2-wallet__plus");
            plus.Add(Ui.Element("kr-ico", "kr-ico--plus", "kr-h2-wallet__plusicon"));
            plate.Add(plus);
            return plate;
        }

        /// <summary>A floating icon with a slanted label (and an optional sub line / alert badge).</summary>
        private static Button Tile(string title, string iconClass, Action onClick, out Label sub, out VisualElement badge)
        {
            var tile = HButton(onClick, "kr-h-btn", "kr-h2-tile");
            tile.Add(Ui.Element("kr-ico", iconClass, "kr-h2-tile__icon"));
            tile.Add(Ui.Label(title, "kr-h2-tile__label", "kr-display"));
            sub = Ui.Label(string.Empty, "kr-h2-tile__sub", "kr-display");
            sub.style.display = DisplayStyle.None;
            tile.Add(sub);
            badge = Ui.Element("kr-ico", "kr-ico--alert", "kr-h2-tile__badge");
            badge.style.display = DisplayStyle.None;
            tile.Add(badge);
            return tile;
        }

        private VisualElement PassBanner()
        {
            var banner = HButton(() => Nav.Push<SeasonPassScreen>(), "kr-h-btn", "kr-h2-pass");
            banner.Add(Ui.Element("kr-ico", "kr-ico--pass", "kr-h2-pass__star"));
            var text = Ui.Col("kr-h2-pass__text");
            text.Add(Ui.Label("Rush Pass", "kr-h2-pass__title", "kr-display"));
            passTime = Ui.Label("Season 1", "kr-h2-pass__time", "kr-display");
            text.Add(passTime);
            banner.Add(text);
            var hex = Ui.Element("kr-h2-hex", "kr-h2-pass__hex");
            passTier = Ui.Label("1", "kr-h2-hex__text", "kr-display");
            hex.Add(passTier);
            banner.Add(hex);
            return banner;
        }

        private static void SetSub(Label label, string text)
        {
            label.text = text;
            label.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        // -----------------------------------------------------------------------------------------
        // Data
        // -----------------------------------------------------------------------------------------
        public override void OnShow()
        {
            if (!subscribed)
            {
                subscribed = true;
                EventBus.Subscribe<ProfileUpdatedEvent>(_ => { if (IsVisible) Refresh(); });
                EventBus.Subscribe<BackendStateChangedEvent>(_ => { if (IsVisible) { Refresh(); LoadRemote(); } });
            }

            Refresh();
            LoadRemote();
        }

        private void Refresh()
        {
            var profile = UiData.Profile;
            Ui.SetVisible(offline, profile == null);
            nameLabel.text = UiData.DisplayName;
            levelLabel.text = profile != null ? profile.Level.ToString(CultureInfo.InvariantCulture) : "1";
            var xpNeeded = profile != null ? profile.XpForNextLevel : 0;
            Ui.SetProgress(xpFill, profile != null && xpNeeded > 0 ? (float)profile.Xp / xpNeeded : 0f);
            coins.text = profile != null ? Ui.FormatNumber(profile.Coins) : "0";
            gems.text = profile != null ? Ui.FormatNumber(profile.Gems) : "0";
            rewardsBadge.style.display = profile != null && profile.DailyClaimable ? DisplayStyle.Flex : DisplayStyle.None;
            if (profile == null)
            {
                SetSub(missionsCount, string.Empty);
            }

            var catalog = GameModeCatalog.Load();
            var mode = catalog != null ? FindMode(catalog, UiData.SelectedModeId) : null;
            modeLabel.text = (mode != null ? mode.DisplayName : "Classic Knockout") + (UiData.PracticeMode ? " · Practice" : string.Empty);
            RefreshPass();
            RefreshTimers();
        }

        private void RefreshPass()
        {
            passTier.text = season != null ? Mathf.Max(1, season.Player.Tier).ToString(CultureInfo.InvariantCulture) : "1";
        }

        private void RefreshTimers()
        {
            var now = DateTime.UtcNow;
            passTime.text = season != null && DateTime.TryParse(season.Season.EndsAt, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var ends)
                ? FormatSpan(ends - now, true)
                : "Season 1";

            var profile = UiData.Profile;
            SetSub(rewardsTime, profile == null ? string.Empty : profile.DailyClaimable ? "Claim!" : FormatSpan(now.Date.AddDays(1) - now, false));
        }

        /// <summary>"34d 12h" for long spans, "12h 45m" for short ones.</summary>
        internal static string FormatSpan(TimeSpan span, bool days)
        {
            if (span <= TimeSpan.Zero)
            {
                return "Ended";
            }

            return days && span.TotalDays >= 1 ? $"{(int)span.TotalDays}d {span.Hours}h" : $"{(int)span.TotalHours}h {span.Minutes:00}m";
        }

        private async void LoadRemote()
        {
            if (!UiData.Online)
            {
                return;
            }

            try
            {
                if (season == null)
                {
                    season = await UiData.Backend.GetSeasonAsync();
                    RefreshPass();
                    RefreshTimers();
                }

                var missions = await UiData.Backend.GetMissionsAsync();
                if (missions != null)
                {
                    var done = 0;
                    var claimable = false;
                    foreach (var mission in missions.Missions)
                    {
                        if (mission.Complete || mission.Claimed)
                        {
                            done++;
                        }

                        claimable |= mission.Complete && !mission.Claimed;
                    }

                    SetSub(missionsCount, $"{done}/{missions.Missions.Count}");
                    missionsBadge.style.display = claimable ? DisplayStyle.Flex : DisplayStyle.None;
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Home] Season/missions unavailable: {exception.GetBaseException().Message}");
            }
        }

        public override void Tick(float deltaTime)
        {
            // The stage creates its portrait texture on Start (after the screen may be built).
            if (avatarImage.style.backgroundImage.value.renderTexture == null && MenuBeanStage.Current != null && MenuBeanStage.Current.Portrait != null)
            {
                avatarImage.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(MenuBeanStage.Current.Portrait));
            }

            clock += deltaTime;
            if (clock >= 1f)
            {
                clock = 0f;
                RefreshTimers();
            }
        }

        public override void OnHide()
        {
            // The portrait texture belongs to the menu scene; re-bind on the next show.
            avatarImage.style.backgroundImage = StyleKeyword.Null;
        }

        /// <summary>A bare button skinned only by its classes (no default .kr-btn look).</summary>
        private static Button HButton(Action onClick, params string[] classes)
        {
            var button = new Button(onClick);
            foreach (var c in classes)
            {
                button.AddToClassList(c);
            }

            return button;
        }

        internal static GameModeDefinition FindMode(GameModeCatalog catalog, string modeId)
        {
            var index = catalog.IndexOfMode(modeId);
            return index >= 0 ? catalog.Modes[index] : null;
        }

        private async void ClaimDaily()
        {
            var profile = UiData.Profile;
            if (profile == null)
            {
                App.Toast("Daily rewards need an internet connection.");
                return;
            }

            if (!profile.DailyClaimable)
            {
                App.Toast($"Day {profile.DailyStreak} claimed! Next reward in {rewardsTime.text}.");
                return;
            }

            DailyRewardResult result = null;
            if (await App.Run(async () => result = await UiData.Backend.ClaimDailyRewardAsync()) && result != null)
            {
                App.Toast(result.Gems > 0 ? $"Day {result.Streak}: +{result.Coins} coins, +{result.Gems} gems!" : $"Day {result.Streak}: +{result.Coins} coins!");
            }
        }

        public override bool OnBack()
        {
            DialogScreen.Show(App, "QUIT KNOCKRUSH?", "See you on the course soon!", "QUIT", Application.Quit, "STAY");
            return true;
        }
    }

    /// <summary>Daily missions (overlay from the Home missions card): progress and claim.</summary>
    public sealed class MissionsScreen : UIScreen
    {
        private VisualElement list;

        public override ScreenLayer Layer => ScreenLayer.Overlay;

        protected override void Build(VisualElement root)
        {
            root.AddToClassList("kr-dim");
            root.AddToClassList("kr-center");
            var panel = Ui.Panel("kr-dialog", "kr-missions");
            panel.Add(Ui.Heading("DAILY MISSIONS"));
            list = Ui.Col("kr-missions__list");
            panel.Add(list);
            panel.Add(Ui.Button("CLOSE", () => Nav.Pop(), "kr-btn--ghost"));
            root.Add(panel);
        }

        public override void OnShow() => Load();

        private async void Load()
        {
            list.Clear();
            if (!UiData.Online)
            {
                list.Add(Ui.Label("Connect to get daily missions.", "kr-subtle"));
                return;
            }

            MissionsResponse response = null;
            if (!await App.Run(async () => response = await UiData.Backend.GetMissionsAsync()) || response == null)
            {
                return;
            }

            list.Clear();
            foreach (var mission in response.Missions)
            {
                list.Add(Row(mission));
            }
        }

        private VisualElement Row(Mission mission)
        {
            var row = Ui.Col("kr-mission");
            var top = Ui.Row();
            top.Add(Ui.Label(mission.Title, "kr-grow"));
            var reward = mission.Gems > 0 ? $"{mission.Coins}c +{mission.Gems}g" : $"{mission.Coins}c";
            if (mission.Claimed)
            {
                top.Add(Ui.Label("DONE", "kr-subtle"));
            }
            else if (mission.Complete)
            {
                top.Add(Ui.Button($"CLAIM {reward}", async () =>
                {
                    if (await App.Run(() => UiData.Backend.ClaimMissionAsync(mission.Id), $"+{reward}"))
                    {
                        Load();
                    }
                }, "kr-btn--small", "kr-btn--good"));
            }
            else
            {
                top.Add(Ui.Label(reward, "kr-subtle"));
            }

            row.Add(top);
            var bar = Ui.ProgressBar(out var fill);
            bar.style.height = 12;
            Ui.SetProgress(fill, mission.Target > 0 ? (float)mission.Progress / mission.Target : 0f);
            row.Add(bar);
            return row;
        }
    }
}
