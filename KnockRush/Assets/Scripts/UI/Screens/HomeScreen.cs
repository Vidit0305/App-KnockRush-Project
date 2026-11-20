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
    /// 4 · Home: the player's bean on the festival stage, framed by the profile card (live avatar,
    /// level, XP), the logo, wallet, settings and inbox; the season card and main navigation on the
    /// left; daily rewards and missions on the right; the mode cards and the big PLAY button below.
    /// </summary>
    public sealed class HomeScreen : UIScreen
    {
        private const string InboxReadKey = "knockrush.inbox.s1";

        private static SeasonInfo season;

        private VisualElement avatarImage;
        private Label nameLabel;
        private Label levelLabel;
        private Label xpLabel;
        private VisualElement xpFill;
        private Label coins;
        private Label gems;
        private VisualElement mailBadge;
        private Label seasonNumber;
        private Label seasonName;
        private Label seasonTime;
        private VisualElement seasonImage;
        private Label dailyTime;
        private VisualElement dailyBadge;
        private Label missionsCount;
        private VisualElement missionsBadge;
        private VisualElement offline;
        private readonly System.Collections.Generic.Dictionary<string, Button> modeCards = new System.Collections.Generic.Dictionary<string, Button>();
        private bool subscribed;
        private float clock;

        public override bool ShowsStage => true;

        protected override void Build(VisualElement root)
        {
            root.AddToClassList("kr-home");

            // ---- Top: profile card | logo | wallet, settings, inbox ----
            var top = Ui.Row("kr-h-top");
            top.pickingMode = PickingMode.Ignore;
            top.Add(ProfileCard());
            top.Add(Ui.Spacer());
            top.Add(Wallet("kr-ico--coin", "kr-h-plus--yellow", out coins, () => Nav.Push<ShopScreen>()));
            top.Add(Wallet("kr-ico--gem", "kr-h-plus--green", out gems, () => Nav.Push<ShopScreen>()));
            top.Add(IconButton("kr-ico--settings", () => Nav.Push<SettingsScreen>(), out _));
            top.Add(IconButton("kr-ico--mail", OpenInbox, out mailBadge));
            root.Add(top);

            var logo = Ui.Element("kr-h-logo");
            logo.pickingMode = PickingMode.Ignore;
            root.Add(logo);

            offline = Ui.Row("kr-h-offline");
            offline.Add(Ui.Label("OFFLINE  ·  PRACTICE MODES", "kr-display"));
            root.Add(offline);

            // ---- Left: season card + main navigation ----
            var left = Ui.Col("kr-h-left");
            left.pickingMode = PickingMode.Ignore;
            left.Add(SeasonCard());
            var nav = Ui.Col("kr-h-nav");
            nav.pickingMode = PickingMode.Ignore;
            nav.Add(NavButton("Shop", "kr-ico--cart", "kr-h-nav--shop", () => Nav.Push<ShopScreen>()));
            nav.Add(NavButton("Customize", "kr-ico--hanger", null, () => Nav.Push<CustomizeScreen>()));
            nav.Add(NavButton("Leaderboard", "kr-ico--trophy", null, () => Nav.Push<LeaderboardScreen>()));
            nav.Add(NavButton("Friends", "kr-ico--friends", null, () => Nav.Push<FriendsScreen>()));
            left.Add(nav);
            root.Add(left);

            // ---- Right: daily rewards + missions ----
            var right = Ui.Col("kr-h-right");
            right.pickingMode = PickingMode.Ignore;
            right.Add(InfoCard("Daily Rewards", "kr-ico--gift", "kr-ico--clock", out dailyTime, out dailyBadge, ClaimDaily));
            right.Add(InfoCard("Missions", "kr-ico--missions", "kr-ico--coin", out missionsCount, out missionsBadge, () => Nav.Push<MissionsScreen>()));
            root.Add(right);

            // ---- Bottom: mode cards + PLAY ----
            var bottom = Ui.Row("kr-h-bottom");
            bottom.pickingMode = PickingMode.Ignore;
            var catalog = GameModeCatalog.Load();
            bottom.Add(ModeCard("classic", "kr-ico--crown", catalog, false));
            bottom.Add(ModeCard("quickrace", "kr-ico--stopwatch", catalog, false));
            bottom.Add(ModeCard("private", "kr-ico--party", catalog, true));
            bottom.Add(Ui.Spacer());

            var playGroup = Ui.Col("kr-h-playgroup");
            playGroup.pickingMode = PickingMode.Ignore;
            var tag = Ui.Row("kr-h-tag");
            tag.pickingMode = PickingMode.Ignore;
            tag.Add(Ui.Label("Let's Knock it Out!", "kr-h-tag__text", "kr-display"));
            tag.Add(Ui.Element("kr-ico", "kr-ico--crown", "kr-h-tag__crown"));
            playGroup.Add(tag);
            var play = HButton(() => MatchmakingScreen.Start(App, UiData.SelectedModeId, UiData.PracticeMode), "kr-h-btn", "kr-h-play", "kr-btn--play");
            play.Add(Ui.Label("PLAY", "kr-h-play__text", "kr-display"));
            play.Add(Ui.Element("kr-ico", "kr-ico--play", "kr-h-play__arrows"));
            playGroup.Add(play);
            bottom.Add(playGroup);
            root.Add(bottom);
        }

        // -----------------------------------------------------------------------------------------
        // Building blocks
        // -----------------------------------------------------------------------------------------
        private VisualElement ProfileCard()
        {
            var card = HButton(() => Nav.Push<ProfileScreen>(), "kr-h-btn", "kr-h-profile");
            var frame = Ui.Element("kr-h-avatar");
            var inner = Ui.Element("kr-h-avatar__inner");
            avatarImage = Ui.Element("kr-h-avatar__image");
            inner.Add(avatarImage);
            frame.Add(inner);
            card.Add(frame);

            var info = Ui.Col("kr-h-profile__info");
            var nameRow = Ui.Row();
            nameLabel = Ui.Label("Player", "kr-h-name", "kr-display");
            nameRow.Add(nameLabel);
            nameRow.Add(Ui.Element("kr-ico", "kr-ico--edit", "kr-h-name__edit"));
            info.Add(nameRow);

            var levelRow = Ui.Row("kr-h-levelrow");
            levelLabel = Ui.Label("Lv. 1", "kr-h-level", "kr-display");
            levelRow.Add(levelLabel);
            var bar = Ui.Element("kr-h-xp");
            xpFill = Ui.Element("kr-h-xp__fill");
            bar.Add(xpFill);
            xpLabel = Ui.Label("0 / 0", "kr-h-xp__text", "kr-display");
            bar.Add(xpLabel);
            levelRow.Add(bar);
            info.Add(levelRow);
            card.Add(info);
            return card;
        }

        private static VisualElement Wallet(string iconClass, string plusClass, out Label value, Action onAdd)
        {
            var pill = Ui.Row("kr-h-wallet");
            pill.Add(Ui.Element("kr-ico", iconClass, "kr-h-wallet__icon"));
            value = Ui.Label("0", "kr-h-wallet__value", "kr-display");
            pill.Add(value);
            var plus = HButton(onAdd, "kr-h-btn", "kr-h-plus", plusClass);
            plus.Add(Ui.Element("kr-ico", "kr-ico--plus", "kr-h-plus__icon"));
            pill.Add(plus);
            return pill;
        }

        private static Button IconButton(string iconClass, Action onClick, out VisualElement badge)
        {
            var button = HButton(onClick, "kr-h-btn", "kr-h-square");
            button.Add(Ui.Element("kr-ico", iconClass, "kr-h-square__icon"));
            badge = Ui.Element("kr-ico", "kr-ico--alert", "kr-h-badge");
            badge.style.display = DisplayStyle.None;
            button.Add(badge);
            return button;
        }

        private VisualElement SeasonCard()
        {
            var card = HButton(() => Nav.Push<SeasonPassScreen>(), "kr-h-btn", "kr-h-season");
            seasonImage = Ui.Element("kr-h-season__image");
            card.Add(seasonImage);
            var text = Ui.Col("kr-h-season__text");
            seasonNumber = Ui.Label("Season 1", "kr-h-season__number", "kr-display");
            seasonName = Ui.Label("Rush Hour", "kr-h-season__name", "kr-display");
            text.Add(seasonNumber);
            text.Add(seasonName);
            var timeRow = Ui.Row("kr-h-season__timer");
            timeRow.Add(Ui.Element("kr-ico", "kr-ico--clock", "kr-h-season__clock"));
            seasonTime = Ui.Label("--", "kr-h-season__time", "kr-display");
            timeRow.Add(seasonTime);
            text.Add(timeRow);
            card.Add(text);
            var badge = Ui.Element("kr-ico", "kr-ico--alert", "kr-h-badge", "kr-h-badge--season");
            card.Add(badge);
            return card;
        }

        private static Button NavButton(string title, string iconClass, string variant, Action onClick)
        {
            var button = HButton(onClick, "kr-h-btn", "kr-h-nav__btn");
            if (variant != null)
            {
                button.AddToClassList(variant);
            }

            button.Add(Ui.Element("kr-ico", iconClass, "kr-h-nav__icon"));
            button.Add(Ui.Label(title, "kr-h-nav__label", "kr-display"));
            return button;
        }

        private static Button InfoCard(string title, string iconClass, string subIconClass, out Label sub, out VisualElement badge, Action onClick)
        {
            var card = HButton(onClick, "kr-h-btn", "kr-h-info");
            card.Add(Ui.Element("kr-ico", iconClass, "kr-h-info__icon"));
            var col = Ui.Col("kr-h-info__text");
            col.Add(Ui.Label(title, "kr-h-info__title", "kr-display"));
            var row = Ui.Row("kr-h-info__row");
            row.Add(Ui.Element("kr-ico", subIconClass, "kr-h-info__subicon"));
            sub = Ui.Label("--", "kr-h-info__sub", "kr-display");
            row.Add(sub);
            col.Add(row);
            card.Add(col);
            badge = Ui.Element("kr-ico", "kr-ico--alert", "kr-h-badge");
            badge.style.display = DisplayStyle.None;
            card.Add(badge);
            return card;
        }

        private Button ModeCard(string modeId, string iconClass, GameModeCatalog catalog, bool opensRoom)
        {
            var mode = catalog != null ? FindMode(catalog, modeId) : null;
            var title = mode != null ? mode.DisplayName : modeId;
            var players = mode == null ? string.Empty
                : modeId == "classic" ? $"{mode.TargetPlayers} Players"
                : $"{Mathf.Max(2, mode.MinPlayers)}–{mode.TargetPlayers} Players";

            var card = HButton(() => OnModeCard(modeId, opensRoom), "kr-h-btn", "kr-h-mode");
            if (opensRoom)
            {
                card.AddToClassList("kr-h-mode--party");
            }

            card.Add(Ui.Element("kr-ico", iconClass, "kr-h-mode__icon"));
            var col = Ui.Col("kr-h-mode__text");
            col.Add(Ui.Label(title, "kr-h-mode__title", "kr-display"));
            var row = Ui.Row("kr-h-mode__row");
            row.Add(Ui.Element("kr-ico", "kr-ico--friends", "kr-h-mode__people"));
            row.Add(Ui.Label(players, "kr-h-mode__players", "kr-display"));
            col.Add(row);
            card.Add(col);
            modeCards[modeId] = card;
            return card;
        }

        private void OnModeCard(string modeId, bool opensRoom)
        {
            if (opensRoom)
            {
                Nav.Push<PrivateRoomScreen>();
                return;
            }

            if (UiData.SelectedModeId == modeId)
            {
                Nav.Push<ModeSelectScreen>(); // second tap: details and practice toggle
                return;
            }

            UiData.SelectedModeId = modeId;
            RefreshModes();
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
            levelLabel.text = profile != null ? $"Lv. {profile.Level}" : "Lv. 1";
            var xpNeeded = profile != null ? profile.XpForNextLevel : 0;
            Ui.SetProgress(xpFill, profile != null && xpNeeded > 0 ? (float)profile.Xp / xpNeeded : 0f);
            xpLabel.text = profile != null && xpNeeded > 0 ? $"{Ui.FormatNumber(profile.Xp)} / {Ui.FormatNumber(xpNeeded)}" : "0 / 100";
            coins.text = profile != null ? Ui.FormatNumber(profile.Coins) : "0";
            gems.text = profile != null ? Ui.FormatNumber(profile.Gems) : "0";
            mailBadge.style.display = PlayerPrefs.GetInt(InboxReadKey, 0) == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            dailyBadge.style.display = profile != null && profile.DailyClaimable ? DisplayStyle.Flex : DisplayStyle.None;
            if (profile == null)
            {
                missionsCount.text = "Go online";
            }

            RefreshSeason();
            RefreshTimers();
            RefreshModes();
        }

        private void RefreshModes()
        {
            foreach (var pair in modeCards)
            {
                pair.Value.EnableInClassList("kr-h-mode--selected", pair.Key == UiData.SelectedModeId);
            }
        }

        private void RefreshSeason()
        {
            var name = season != null && !string.IsNullOrEmpty(season.Name) ? season.Name : "Season 1: Rush Hour";
            var split = name.IndexOf(": ", StringComparison.Ordinal);
            seasonNumber.text = split > 0 ? name.Substring(0, split) : "Season 1";
            seasonName.text = split > 0 ? name.Substring(split + 2) : name;
        }

        private void RefreshTimers()
        {
            var now = DateTime.UtcNow;
            seasonTime.text = season != null && DateTime.TryParse(season.EndsAt, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var ends)
                ? FormatSpan(ends - now, true)
                : "Play to level up!";

            var profile = UiData.Profile;
            if (profile == null)
            {
                dailyTime.text = "Go online";
            }
            else if (profile.DailyClaimable)
            {
                dailyTime.text = "Claim now!";
            }
            else
            {
                dailyTime.text = FormatSpan(now.Date.AddDays(1) - now, false);
            }
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
                    season = (await UiData.Backend.GetSeasonAsync())?.Season;
                    RefreshSeason();
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

                    missionsCount.text = $"{done}/{missions.Missions.Count}";
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
            // The stage creates its portrait textures on Start (after the screen may be built).
            if (avatarImage.style.backgroundImage.value.renderTexture == null && MenuBeanStage.Current != null)
            {
                if (MenuBeanStage.Current.Portrait != null)
                {
                    avatarImage.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(MenuBeanStage.Current.Portrait));
                }

                if (MenuBeanStage.Current.SeasonPortrait != null)
                {
                    seasonImage.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(MenuBeanStage.Current.SeasonPortrait));
                }
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
            // Portrait textures belong to the menu scene; re-bind on the next show.
            avatarImage.style.backgroundImage = StyleKeyword.Null;
            seasonImage.style.backgroundImage = StyleKeyword.Null;
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
                App.Toast($"Day {profile.DailyStreak} claimed! Next reward in {dailyTime.text}.");
                return;
            }

            DailyRewardResult result = null;
            if (await App.Run(async () => result = await UiData.Backend.ClaimDailyRewardAsync()) && result != null)
            {
                App.Toast(result.Gems > 0 ? $"Day {result.Streak}: +{result.Coins} coins, +{result.Gems} gems!" : $"Day {result.Streak}: +{result.Coins} coins!");
            }
        }

        private void OpenInbox()
        {
            PlayerPrefs.SetInt(InboxReadKey, 1);
            mailBadge.style.display = DisplayStyle.None;
            DialogScreen.Show(App, "INBOX",
                "Welcome to KnockRush Season 1: Rush Hour!\n\nRace, dodge and knock your way to the crown. Finish daily missions for coins and gems, and level up the season pass for new costumes.");
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
