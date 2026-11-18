using System.Collections.Generic;
using KnockRush.Backend;
using KnockRush.Core.Events;
using KnockRush.GameModes;
using KnockRush.Inventory;
using KnockRush.UI.Framework;
using KnockRush.UI.Stage;
using UnityEngine;
using UnityEngine.UIElements;

namespace KnockRush.UI.Screens
{
    /// <summary>
    /// 4 · Home: the player's bean on the 3D stage, currencies, level, daily missions and reward,
    /// navigation to every menu, the selected mode and the big PLAY button.
    /// </summary>
    public sealed class HomeScreen : UIScreen
    {
        private Label levelLabel;
        private Label nameLabel;
        private VisualElement xpFill;
        private Label coins;
        private Label gems;
        private Label modeLabel;
        private Label offlineBanner;
        private VisualElement missionList;
        private Button dailyButton;
        private bool subscribed;

        public override bool ShowsStage => true;

        protected override void Build(VisualElement root)
        {
            root.AddToClassList("kr-home");

            // ---- Top bar: profile chip, currencies, settings ----
            var top = Ui.Row("kr-topbar");
            var profile = Ui.Row("kr-profile-chip");
            levelLabel = Ui.Label("1", "kr-level", "kr-display");
            profile.Add(levelLabel);
            var nameCol = Ui.Col();
            nameCol.style.marginLeft = 12;
            nameLabel = Ui.Label("Player", "kr-display");
            nameLabel.style.fontSize = 30;
            nameCol.Add(nameLabel);
            var xpBar = Ui.ProgressBar(out xpFill, new Color32(34, 197, 139, 255));
            xpBar.style.width = 220;
            xpBar.style.height = 14;
            nameCol.Add(xpBar);
            profile.Add(nameCol);
            profile.RegisterCallback<ClickEvent>(_ => Nav.Push<ProfileScreen>());
            top.Add(profile);
            top.Add(Ui.Spacer());

            var coinPill = Ui.CurrencyPill("kr-coin", out coins);
            var gemPill = Ui.CurrencyPill("kr-gem", out gems);
            gemPill.RegisterCallback<ClickEvent>(_ => Nav.Push<ShopScreen>());
            top.Add(coinPill);
            top.Add(gemPill);
            top.Add(Ui.Button("SETTINGS", () => Nav.Push<SettingsScreen>(), "kr-btn--icon", "kr-btn--ghost"));
            root.Add(top);

            offlineBanner = Ui.Label("OFFLINE · practice modes are available", "kr-offline");
            root.Add(offlineBanner);

            // ---- Middle: menu column | stage | missions ----
            var middle = Ui.Row("kr-grow");
            middle.style.alignItems = Align.Stretch;

            var menu = Ui.Col("kr-home-menu");
            menu.Add(MenuButton("SHOP", "Daily deals", () => Nav.Push<ShopScreen>(), "kr-btn--shop"));
            menu.Add(MenuButton("LOCKER", "Costumes & colours", () => Nav.Push<CustomizeScreen>(), "kr-btn--locker"));
            menu.Add(MenuButton("SEASON", "Rush Hour pass", () => Nav.Push<SeasonPassScreen>(), "kr-btn--season"));
            menu.Add(MenuButton("FRIENDS", "Party up", () => Nav.Push<FriendsScreen>(), "kr-btn--friends"));
            middle.Add(menu);

            var stageArea = Ui.Col("kr-grow");
            stageArea.pickingMode = PickingMode.Ignore;
            stageArea.Add(Ui.Spacer());
            var emoteRow = Ui.Row("kr-center");
            emoteRow.Add(Ui.Button("EMOTE", () => MenuBeanStage.Current?.PlayEquippedEmote(), "kr-btn--small", "kr-btn--ghost"));
            emoteRow.Add(Ui.Button("CHANGE OUTFIT", () => Nav.Push<CharacterSelectScreen>(), "kr-btn--small", "kr-btn--ghost"));
            stageArea.Add(emoteRow);
            middle.Add(stageArea);

            var side = Ui.Col("kr-home-side");
            var missions = Ui.Panel();
            missions.Add(Ui.Label("DAILY MISSIONS", "kr-section__title", "kr-display"));
            missionList = Ui.Col();
            missions.Add(missionList);
            side.Add(missions);
            dailyButton = Ui.Button("DAILY REWARD", ClaimDaily, "kr-btn--good");
            side.Add(dailyButton);
            var socialRow = Ui.Row();
            socialRow.Add(Ui.Button("RANKS", () => Nav.Push<LeaderboardScreen>(), "kr-btn--small"));
            socialRow.Add(Ui.Button("ITEMS", () => Nav.Push<InventoryScreen>(), "kr-btn--small"));
            side.Add(socialRow);
            middle.Add(side);
            root.Add(middle);

            // ---- Bottom: mode + PLAY ----
            var bottom = Ui.Row("kr-home-bottom");
            bottom.Add(Ui.Button("PRIVATE ROOM", () => Nav.Push<PrivateRoomScreen>(), "kr-btn--ghost"));
            bottom.Add(Ui.Spacer());
            var modeChip = Ui.Col("kr-mode-chip");
            modeChip.Add(Ui.Label("MODE", "kr-subtle"));
            modeLabel = Ui.Label("Classic Knockout", "kr-display");
            modeLabel.style.fontSize = 30;
            modeChip.Add(modeLabel);
            modeChip.RegisterCallback<ClickEvent>(_ => Nav.Push<ModeSelectScreen>());
            bottom.Add(modeChip);
            bottom.Add(Ui.Button("PLAY", () => MatchmakingScreen.Start(App, UiData.SelectedModeId, UiData.PracticeMode), "kr-btn--primary", "kr-btn--play"));
            root.Add(bottom);
        }

        public override void OnShow()
        {
            if (!subscribed)
            {
                subscribed = true;
                EventBus.Subscribe<ProfileUpdatedEvent>(_ => { if (IsVisible) Refresh(); });
                EventBus.Subscribe<BackendStateChangedEvent>(_ => { if (IsVisible) Refresh(); });
            }

            Refresh();
            LoadMissions();
        }

        private void Refresh()
        {
            var profile = UiData.Profile;
            Ui.SetVisible(offlineBanner, profile == null);
            levelLabel.text = profile != null ? profile.Level.ToString() : "-";
            nameLabel.text = UiData.DisplayName;
            Ui.SetProgress(xpFill, profile != null && profile.XpForNextLevel > 0 ? (float)profile.Xp / profile.XpForNextLevel : 0f);
            coins.text = profile != null ? Ui.FormatNumber(profile.Coins) : "-";
            gems.text = profile != null ? Ui.FormatNumber(profile.Gems) : "-";
            dailyButton.SetEnabled(profile != null && profile.DailyClaimable);
            dailyButton.text = profile != null && !profile.DailyClaimable ? $"STREAK {profile.DailyStreak} · COME BACK TOMORROW" : "DAILY REWARD";

            var catalog = GameModeCatalog.Load();
            var mode = catalog != null ? FindMode(catalog, UiData.SelectedModeId) : null;
            modeLabel.text = (mode != null ? mode.DisplayName : "Classic Knockout") + (UiData.PracticeMode ? " · Practice" : string.Empty);
        }

        internal static GameModeDefinition FindMode(GameModeCatalog catalog, string modeId)
        {
            var index = catalog.IndexOfMode(modeId);
            return index >= 0 ? catalog.Modes[index] : null;
        }

        private async void LoadMissions()
        {
            missionList.Clear();
            if (!UiData.Online)
            {
                missionList.Add(Ui.Label("Connect to get daily missions.", "kr-subtle"));
                return;
            }

            MissionsResponse response = null;
            if (!await App.Run(async () => response = await UiData.Backend.GetMissionsAsync()) || response == null)
            {
                return;
            }

            missionList.Clear();
            foreach (var mission in response.Missions)
            {
                missionList.Add(MissionRow(mission));
            }
        }

        private VisualElement MissionRow(Mission mission)
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
                        LoadMissions();
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

        private async void ClaimDaily()
        {
            DailyRewardResult result = null;
            if (await App.Run(async () => result = await UiData.Backend.ClaimDailyRewardAsync()) && result != null)
            {
                App.Toast(result.Gems > 0 ? $"Day {result.Streak}: +{result.Coins} coins, +{result.Gems} gems!" : $"Day {result.Streak}: +{result.Coins} coins!");
            }
        }

        private static Button MenuButton(string title, string subtitle, System.Action onClick, string variant)
        {
            var button = Ui.Button(string.Empty, onClick, "kr-menu-btn", variant);
            button.Add(Ui.Label(title, "kr-menu-btn__title", "kr-display"));
            button.Add(Ui.Label(subtitle, "kr-menu-btn__sub"));
            return button;
        }

        public override bool OnBack()
        {
            DialogScreen.Show(App, "QUIT KNOCKRUSH?", "See you on the course soon!", "QUIT", Application.Quit, "STAY");
            return true;
        }
    }
}
