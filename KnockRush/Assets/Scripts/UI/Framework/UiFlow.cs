using System;
using KnockRush.Core.Events;
using KnockRush.Core.SceneManagement;
using KnockRush.Core.Services;
using KnockRush.GameModes;
using KnockRush.Multiplayer;
using KnockRush.UI.Screens;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KnockRush.UI.Framework
{
    /// <summary>
    /// Maps game state to screens: Boot → splash; MainMenu → home stack; a running match → lobby,
    /// loading, HUD, qualification / elimination, victory and results (driven by the replicated
    /// MatchController); any other scene (dev playground, solo map runs) → a minimal HUD with a way back.
    /// </summary>
    public sealed class UiFlow : IDisposable
    {
        private const string LoginSeenKey = "knockrush.loginSeen";

        public enum Context
        {
            None,
            Boot,
            Menu,
            Match,
            Other
        }

        private readonly AppUI app;
        private MatchPhase lastPhase;
        private RecordStatus lastStatus;
        private bool loginChecked;
        private bool sceneLoading;
        private bool returningToMenu;

        public UiFlow(AppUI app)
        {
            this.app = app;
            EventBus.Subscribe<SceneLoadStartedEvent>(OnSceneLoadStarted);
            EventBus.Subscribe<SceneLoadProgressEvent>(OnSceneLoadProgress);
            EventBus.Subscribe<SceneLoadCompletedEvent>(OnSceneLoadCompleted);
        }

        public Context Current { get; private set; } = Context.None;

        public void Dispose()
        {
            EventBus.Unsubscribe<SceneLoadStartedEvent>(OnSceneLoadStarted);
            EventBus.Unsubscribe<SceneLoadProgressEvent>(OnSceneLoadProgress);
            EventBus.Unsubscribe<SceneLoadCompletedEvent>(OnSceneLoadCompleted);
        }

        public void Update()
        {
            var match = MatchController.Current;
            var hasMatch = match != null && match.Object != null && match.Object.IsValid;
            var scene = SceneManager.GetActiveScene().name;
            var next = scene == SceneNames.Boot ? Context.Boot
                : hasMatch ? Context.Match
                : scene == SceneNames.MainMenu ? Context.Menu
                : Context.Other;

            if (returningToMenu)
            {
                if (scene != SceneNames.MainMenu || hasMatch)
                {
                    return; // the loading screen stays up until the menu is in
                }

                returningToMenu = false;
            }

            // While matchmaking connects, keep its screen up until the match exists.
            if (next != Context.Match && app.Navigator.IsShowing<MatchmakingScreen>())
            {
                return;
            }

            if (Current == Context.Match && next != Context.Match)
            {
                if (NetworkSession.Current != null)
                {
                    return; // round transition: the match object is briefly unavailable while maps swap
                }

                // The session ended (match over, left, or disconnected): back to the menu.
                if (scene != SceneNames.MainMenu)
                {
                    ReturnToMenu();
                    return;
                }
            }

            if (next != Current)
            {
                Enter(next);
            }

            if (Current == Context.Match)
            {
                UpdateMatch(match);
            }
            else if (Current == Context.Menu)
            {
                OfferLoginOnce();
            }
        }

        private async void ReturnToMenu()
        {
            Current = Context.None;
            returningToMenu = true;
            app.Navigator.SetRoot<LoadingScreen>().ShowScene(SceneNames.MainMenu);
            if (ServiceLocator.TryGet<ISceneLoader>(out var loader) && !loader.IsLoading)
            {
                if (!await app.Run(() => loader.LoadSceneAsync(SceneNames.MainMenu)))
                {
                    returningToMenu = false;
                }
            }
            else
            {
                returningToMenu = false;
            }
        }

        private void Enter(Context context)
        {
            Current = context;
            lastPhase = (MatchPhase)255;
            lastStatus = RecordStatus.Empty;
            var nav = app.Navigator;
            switch (context)
            {
                case Context.Boot:
                    nav.SetRoot<SplashScreen>();
                    break;
                case Context.Menu:
                    nav.SetRoot<HomeScreen>();
                    break;
                case Context.Match:
                    break; // UpdateMatch picks the screen for the phase
                default:
                    nav.SetRoot<OfflineHudScreen>();
                    break;
            }
        }

        private void UpdateMatch(MatchController match)
        {
            var nav = app.Navigator;
            var localSlot = match.LocalSlot();
            var status = localSlot >= 0 ? match.Records.Get(localSlot).Status : RecordStatus.Empty;
            var phase = match.Phase;

            if (phase != lastPhase)
            {
                lastPhase = phase;
                switch (phase)
                {
                    case MatchPhase.Lobby:
                        nav.SetRoot<WaitingLobbyScreen>();
                        break;
                    case MatchPhase.Loading:
                        nav.SetRoot<LoadingScreen>().ShowRound(match);
                        break;
                    case MatchPhase.Countdown:
                    case MatchPhase.Playing:
                        if (!(nav.Stack.Count > 0 && nav.Stack[0] is MatchHudScreen))
                        {
                            nav.SetRoot<MatchHudScreen>();
                        }

                        break;
                    case MatchPhase.RoundResults:
                        if (status == RecordStatus.Qualified)
                        {
                            nav.SetRoot<QualifiedScreen>();
                        }
                        else if (localSlot >= 0)
                        {
                            nav.SetRoot<EliminatedScreen>();
                        }

                        break;
                    case MatchPhase.MatchResults:
                        if (localSlot >= 0 && match.WinnerSlot == localSlot)
                        {
                            nav.SetRoot<VictoryScreen>();
                        }
                        else
                        {
                            nav.SetRoot<MatchResultsScreen>();
                        }

                        break;
                }
            }

            // Mid-round status changes pop up over the HUD.
            if (phase == MatchPhase.Playing && status != lastStatus)
            {
                if (status == RecordStatus.Qualified)
                {
                    nav.Push<QualifiedScreen>();
                }
                else if (status == RecordStatus.Eliminated)
                {
                    nav.Push<EliminatedScreen>();
                }
            }

            lastStatus = status;
        }

        private void OfferLoginOnce()
        {
            if (loginChecked || !UiData.Online)
            {
                return;
            }

            loginChecked = true;
            if (string.IsNullOrEmpty(UiData.Profile.Username) && PlayerPrefs.GetInt(LoginSeenKey, 0) == 0)
            {
                PlayerPrefs.SetInt(LoginSeenKey, 1);
                app.Navigator.Push<LoginScreen>();
            }
        }

        private void OnSceneLoadStarted(SceneLoadStartedEvent e)
        {
            sceneLoading = true;
            if (Current != Context.Boot)
            {
                app.Navigator.Push<LoadingScreen>().ShowScene(e.SceneName);
            }
        }

        private void OnSceneLoadProgress(SceneLoadProgressEvent e)
        {
            if (Current == Context.Boot && app.Navigator.Top is SplashScreen splash)
            {
                splash.SetProgress(e.Progress);
            }

            if (sceneLoading && app.Navigator.IsShowing<LoadingScreen>())
            {
                app.Navigator.Get<LoadingScreen>().SetProgress(e.Progress);
            }
        }

        private void OnSceneLoadCompleted(SceneLoadCompletedEvent e)
        {
            sceneLoading = false;
            if (Current != Context.Match || app.Navigator.Top is LoadingScreen && MatchController.Current == null)
            {
                app.Navigator.Remove<LoadingScreen>();
            }
        }
    }
}
