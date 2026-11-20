using System;
using System.Collections;
using KnockRush.Core;
using KnockRush.Core.SceneManagement;
using KnockRush.GameModes;
using KnockRush.Inventory;
using KnockRush.Multiplayer;
using KnockRush.UI.Framework;
using KnockRush.UI.Screens;
using KnockRush.UI.Stage;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace KnockRush.Tests.PlayMode
{
    /// <summary>
    /// Phase 8 UI: the app UI follows the game state (splash → home → lobby → HUD → results → home),
    /// every menu screen builds and opens without errors, and the stage bean wears the loadout.
    /// </summary>
    public sealed class UiFlowTests
    {
        private const float Timeout = 30f;

        [UnityTearDown]
        public IEnumerator StopSession()
        {
            if (NetworkSession.Current != null)
            {
                var stop = NetworkSession.Current.StopAsync();
                while (!stop.IsCompleted)
                {
                    yield return null;
                }
            }
        }

        [UnityTest]
        public IEnumerator Boot_ShowsSplash_ThenHomeWithStageBean()
        {
            yield return WaitUntil(() => AppRoot.IsInitialized, "AppRoot");
            yield return SceneManager.LoadSceneAsync(SceneNames.Boot, LoadSceneMode.Single);
            yield return null;

            var app = AppUI.Instance;
            Assert.IsNotNull(app, "AppUI should be created at startup.");
            Assert.IsInstanceOf<SplashScreen>(app.Navigator.Top, "Boot shows the splash.");

            yield return WaitUntil(() => SceneManager.GetActiveScene().name == SceneNames.MainMenu && app.Navigator.Top is HomeScreen, "home screen");
            Assert.IsTrue(app.Navigator.Top.IsVisible);
            Assert.AreEqual(1, app.Navigator.Stack.Count);

            yield return WaitUntil(() => MenuBeanStage.Current != null, "stage bean");
            yield return null;
            Assert.IsTrue(MenuBeanStage.Current.Shown.Equals(LocalLoadoutStore.Current), "Stage bean wears the equipped loadout.");
            Assert.IsTrue(MenuBeanStage.Current.GetComponentInChildren<CosmeticApplier>().HasApplied);

            // Layout: screens fill the panel (regression: NaN safe-area insets collapsed everything into a corner).
            var panelWidth = app.Root.layout.width;
            var homeRect = app.Navigator.Top.Root.worldBound;
            Assert.Greater(panelWidth, 0f);
            Assert.IsFalse(float.IsNaN(homeRect.width), "Screen layout is NaN.");
            Assert.Greater(homeRect.width, panelWidth * 0.9f, "Home should fill the screen width.");
            Assert.Greater(homeRect.height, app.Root.layout.height * 0.9f, "Home should fill the screen height.");

            // Theme + fonts are applied: the root carries the stylesheet and display text uses Lilita One.
            Assert.IsTrue(app.Root.ClassListContains("kr-root"));
            var playButton = app.Navigator.Top.Root.Q<Button>(className: "kr-btn--play");
            Assert.IsNotNull(playButton, "Home has a PLAY button.");
            var playLabel = playButton.Q<Label>(className: "kr-h-play__text");
            Assert.IsNotNull(playLabel, "PLAY button has its label.");
            Assert.AreEqual("PLAY", playLabel.text);
            Assert.IsNotNull(playLabel.resolvedStyle.unityFontDefinition.font ?? (UnityEngine.Object)playLabel.resolvedStyle.unityFontDefinition.fontAsset, "Display font resolved.");
        }

        [UnityTest]
        public IEnumerator EveryMenuScreen_OpensAndCloses()
        {
            yield return LoadMainMenu();
            var nav = AppUI.Instance.Navigator;

            var pushes = new Func<UIScreen>[]
            {
                () => nav.Push<ModeSelectScreen>(),
                () => nav.Push<PrivateRoomScreen>(),
                () => nav.Push<FriendsScreen>(),
                () => nav.Push<CustomizeScreen>(),
                () => nav.Push<CharacterSelectScreen>(),
                () => nav.Push<ShopScreen>(),
                () => nav.Push<InventoryScreen>(),
                () => nav.Push<SeasonPassScreen>(),
                () => nav.Push<LeaderboardScreen>(),
                () => nav.Push<ProfileScreen>(),
                () => nav.Push<AchievementsScreen>(),
                () => nav.Push<SettingsScreen>(),
                () => nav.Push<LoginScreen>()
            };

            foreach (var push in pushes)
            {
                var screen = push();
                yield return null;
                yield return null;
                Assert.AreSame(screen, nav.Top, $"{screen.GetType().Name} should be on top.");
                Assert.IsTrue(screen.IsVisible);
                Assert.AreEqual(DisplayStyle.Flex, screen.Root.resolvedStyle.display);
                Assert.Greater(screen.Root.childCount, 0, $"{screen.GetType().Name} built no content.");
                Assert.IsFalse(nav.Get<HomeScreen>().IsVisible, "Pages hide the home screen.");
                nav.Pop();
                yield return null;
                Assert.IsInstanceOf<HomeScreen>(nav.Top);
                Assert.IsTrue(nav.Get<HomeScreen>().IsVisible, "Home is revealed again after Pop.");
            }

            // Locker preview: selecting an item dresses the stage bean, leaving reverts it.
            var locker = nav.Push<CustomizeScreen>();
            yield return null;
            locker.SelectTab(CosmeticSlot.Color);
            yield return null;
            var card = locker.Root.Query<Button>(className: "kr-item").AtIndex(3);
            Assert.IsNotNull(card, "Locker grid has items.");
            using (var click = NavigationSubmitEvent.GetPooled())
            {
                click.target = card;
                card.SendEvent(click);
            }

            yield return null;
            nav.Pop();
            yield return null;
            Assert.IsTrue(MenuBeanStage.Current.Shown.Equals(LocalLoadoutStore.Current), "Leaving the locker restores the equipped outfit.");

            // Dialog overlay keeps the page underneath visible.
            DialogScreen.Show(AppUI.Instance, "Test", "Overlay", "OK", null, "CANCEL");
            yield return null;
            Assert.IsInstanceOf<DialogScreen>(nav.Top);
            Assert.IsTrue(nav.Get<HomeScreen>().IsVisible, "Overlays keep the page visible.");
            Assert.IsTrue(nav.Back(), "Back closes the dialog.");
            Assert.IsInstanceOf<HomeScreen>(nav.Top);
        }

        [UnityTest]
        public IEnumerator PracticeMatch_DrivesLobbyHudAndResults_ThenReturnsHome()
        {
            yield return LoadMainMenu();
            var app = AppUI.Instance;

            var start = NetworkSession.StartAsync(new NetworkLaunchOptions
            {
                Role = SessionRole.Single,
                SessionName = "ui-test",
                GameModeId = "quickrace",
                FillWithBots = true,
                LobbySeconds = 1.5f,
                RoundTimeLimitOverride = 4f
            });
            yield return WaitUntil(() => start.IsCompleted, "session start");
            Assert.IsTrue(start.Result.Ok);

            yield return WaitUntil(() => app.Navigator.Top is WaitingLobbyScreen || app.Navigator.Top is MatchHudScreen, "lobby screen");
            yield return WaitUntil(() => MatchController.Current != null && MatchController.Current.Phase == MatchPhase.Countdown, "countdown");
            // The round's loading bar first runs to 100% (well under a second), then the HUD takes over.
            yield return WaitUntil(() => app.Navigator.Stack.Count > 0 && app.Navigator.Stack[0] is MatchHudScreen, "match HUD");
            Assert.AreEqual(MatchPhase.Countdown, MatchController.Current.Phase, "The HUD is up while the countdown is still running.");
            Assert.AreEqual(PickingMode.Ignore, app.Navigator.Stack[0].Root.pickingMode, "HUD must not block touch controls.");

            yield return WaitUntil(() => MatchController.Current != null && MatchController.Current.Phase == MatchPhase.MatchResults, "match results");
            yield return null;
            var top = app.Navigator.Stack[0];
            Assert.IsTrue(top is MatchResultsScreen || top is VictoryScreen, $"Results expected, got {top?.GetType().Name}.");

            var stop = NetworkSession.Current.StopAsync();
            yield return WaitUntil(() => stop.IsCompleted, "stop");
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == SceneNames.MainMenu && app.Navigator.Top is HomeScreen, "home after match");
        }

        private static IEnumerator LoadMainMenu()
        {
            yield return WaitUntil(() => AppRoot.IsInitialized, "AppRoot");
            yield return SceneManager.LoadSceneAsync(SceneNames.MainMenu, LoadSceneMode.Single);
            yield return WaitUntil(() => AppUI.Instance != null && AppUI.Instance.Navigator.Top is HomeScreen, "home");
            while (AppUI.Instance.Navigator.Stack.Count > 1)
            {
                AppUI.Instance.Navigator.Pop();
            }
        }

        private static IEnumerator WaitUntil(Func<bool> condition, string what)
        {
            var elapsed = 0f;
            while (!condition() && elapsed < Timeout)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.IsTrue(condition(), $"Timed out waiting for {what}.");
        }
    }
}
