using System;
using System.Collections;
using System.IO;
using System.Linq;
using KnockRush.Core;
using KnockRush.GameModes;
using KnockRush.Maps;
using KnockRush.Multiplayer;
using KnockRush.UI.Framework;
using KnockRush.UI.Screens;
using KnockRush.Utilities;
using UnityEngine;

namespace KnockRush.UI.Diagnostics
{
    /// <summary>
    /// Visual review: launched with <c>-uicapture &lt;folder&gt;</c>, walks through every screen
    /// (menus, then a practice match: lobby, HUD, qualification, elimination, victory, results)
    /// and saves a screenshot of each, then quits. Never active without the flag.
    /// </summary>
    public sealed class UiCaptureRunner : MonoBehaviour
    {
        private const string Tag = "UiCapture";
        private string folder;
        private int index;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var folder = CommandLine.Get("-uicapture");
            if (string.IsNullOrEmpty(folder))
            {
                return;
            }

            var go = new GameObject("[UiCaptureRunner]");
            DontDestroyOnLoad(go);
            go.AddComponent<UiCaptureRunner>().folder = folder;
        }

        private IEnumerator Start()
        {
            Directory.CreateDirectory(folder);
            yield return Run();
            Log.Info(Tag, $"Captured {index} screens to {folder}.");
            Application.Quit(0);
        }

        private IEnumerator Run()
        {
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Capture("splash");

            yield return Until(() => AppUI.Instance != null && AppUI.Instance.Navigator.Top is HomeScreen, 30f);
            var app = AppUI.Instance;
            var nav = app.Navigator;
            yield return new WaitForSecondsRealtime(2.5f);
            yield return Capture("home");

            yield return Page(() => nav.Push<ModeSelectScreen>(), "mode_select");
            yield return Page(() => nav.Push<PrivateRoomScreen>(), "private_room");
            yield return Page(() => nav.Push<FriendsScreen>(), "friends");
            yield return Page(() => nav.Push<CustomizeScreen>(), "customize");
            yield return Page(() => nav.Push<CharacterSelectScreen>(), "character_select");
            yield return Page(() => nav.Push<ShopScreen>(), "shop");
            yield return Page(() => nav.Push<InventoryScreen>(), "inventory");
            yield return Page(() => nav.Push<SeasonPassScreen>(), "season_pass");
            yield return Page(() => nav.Push<LeaderboardScreen>(), "leaderboard");
            yield return Page(() => nav.Push<ProfileScreen>(), "profile");
            yield return Page(() => nav.Push<AchievementsScreen>(), "achievements");
            yield return Page(() => nav.Push<SettingsScreen>(), "settings");
            yield return Page(() => nav.Push<LoginScreen>(), "login");
            yield return Page(() =>
            {
                DialogScreen.Show(app, "LEAVE MATCH?", "You'll be out of this match. Rewards for rounds you cleared still count.", "LEAVE", null, "STAY");
                return nav.Top;
            }, "dialog");

            nav.Push<LoadingScreen>().ShowScene(Core.SceneManagement.SceneNames.MainMenu);
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Capture("loading");
            nav.Remove<LoadingScreen>();
            yield return new WaitForSecondsRealtime(0.3f);

            // A practice Quick Race against bots.
            MatchmakingScreen.Start(app, "quickrace", true);
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Capture("matchmaking");
            yield return Until(() => nav.Top is WaitingLobbyScreen, 30f);
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Capture("waiting_lobby");

            yield return Until(() => MatchController.Current != null && MatchController.Current.Phase == MatchPhase.Countdown, 40f);
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Capture("match_hud_countdown");
            yield return Until(() => MatchController.Current.Phase == MatchPhase.Playing, 20f);
            yield return new WaitForSecondsRealtime(2f);
            yield return Capture("match_hud");

            nav.Push<EliminatedScreen>();
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Capture("elimination");
            nav.Remove<EliminatedScreen>();

            // Cross the finish line → qualification banner, then the victory and results screens.
            var local = FindObjectsByType<NetworkCharacter>(FindObjectsSortMode.None).FirstOrDefault(c => c.HasInputAuthority);
            var finish = FindFirstObjectByType<FinishLine>();
            if (local != null && finish != null)
            {
                local.transform.position = finish.transform.position;
            }

            yield return Until(() => nav.IsShowing<QualifiedScreen>(), 15f);
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Capture("round_qualification");

            yield return Until(() => MatchController.Current != null && MatchController.Current.Phase == MatchPhase.MatchResults, 90f);
            yield return new WaitForSecondsRealtime(1f);
            if (nav.Top is VictoryScreen)
            {
                yield return Capture("final_victory");
                nav.SetRoot<MatchResultsScreen>();
                yield return new WaitForSecondsRealtime(0.5f);
            }

            yield return Capture("match_results");

            var stop = NetworkSession.Current?.StopAsync();
            yield return Until(() => stop == null || stop.IsCompleted, 20f);
            yield return Until(() => nav.Top is HomeScreen, 20f);
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Capture("loading_or_home_after_match");
        }

        private IEnumerator Page(Func<UIScreen> open, string name)
        {
            var nav = AppUI.Instance.Navigator;
            open();
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Capture(name);
            nav.Pop();
            yield return new WaitForSecondsRealtime(0.3f);
        }

        private IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            var path = Path.Combine(folder, $"{++index:00}_{name}.png");
            ScreenCapture.CaptureScreenshot(path);
            yield return null;
            yield return null;
            Log.Info(Tag, $"Captured {path}");
        }

        private static IEnumerator Until(Func<bool> condition, float timeout)
        {
            var start = Time.realtimeSinceStartup;
            while (!SafeCheck(condition) && Time.realtimeSinceStartup - start < timeout)
            {
                yield return null;
            }

            if (!SafeCheck(condition))
            {
                Log.Warning(Tag, "Capture step timed out; continuing.");
            }
        }

        private static bool SafeCheck(Func<bool> condition)
        {
            try
            {
                return condition();
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
