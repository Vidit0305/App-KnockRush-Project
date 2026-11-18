using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KnockRush.Core;
using KnockRush.Core.Events;
using KnockRush.Core.Services;
using KnockRush.Utilities;
using UnityEngine;

namespace KnockRush.Backend
{
    public enum BackendState
    {
        NotStarted,
        SigningIn,
        Online,
        Offline
    }

    public readonly struct BackendStateChangedEvent
    {
        public readonly BackendState State;

        public BackendStateChangedEvent(BackendState state)
        {
            State = state;
        }
    }

    public readonly struct ProfileUpdatedEvent
    {
        public readonly PlayerProfile Profile;

        public ProfileUpdatedEvent(PlayerProfile profile)
        {
            Profile = profile;
        }
    }

    /// <summary>Everything the game needs from the backend. All mutations are validated server side.</summary>
    public interface IBackendService
    {
        BackendState State { get; }
        string Uid { get; }
        PlayerProfile Profile { get; }

        Task<PlayerProfile> SignInAsync();
        Task<PlayerProfile> RefreshProfileAsync();
        Task SetUsernameAsync(string username);
        Task<List<CatalogEntry>> GetCatalogAsync();
        Task PurchaseAsync(string itemId);
        Task EquipAsync(Dictionary<string, string> equipped);
        Task<DailyRewardResult> ClaimDailyRewardAsync();
        Task<List<LeaderboardEntry>> GetLeaderboardAsync(int limit);
        Task SendFriendRequestAsync(string username);
        Task RespondFriendRequestAsync(string fromUid, bool accept);
        Task LinkAccountAsync(string providerId, string providerIdToken);
    }

    /// <summary>
    /// Firebase-backed implementation. Startup sign-in runs in the background (never blocks the
    /// splash); if the backend is unreachable the game keeps working offline (practice modes).
    /// </summary>
    public sealed class BackendService : IBackendService, IGameService
    {
        private const string Tag = "Backend";
        private readonly BackendConfig config;
        private readonly FirebaseRestClient client;

        public BackendService(BackendConfig config)
        {
            this.config = config;
            client = new FirebaseRestClient(config);
        }

        public BackendState State { get; private set; } = BackendState.NotStarted;

        public string Uid => client.Uid;

        public PlayerProfile Profile { get; private set; }

        public FirebaseRestClient Client => client;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Install()
        {
            AppRoot.RegisterInstaller(registry =>
            {
                if (!AppRoot.IsDedicatedServer)
                {
                    registry.Add<IBackendService>(new BackendService(BackendConfig.Load()));
                }
            });
        }

        public Task InitializeAsync(CancellationToken cancellationToken)
        {
            if (config.AutoSignIn && !Application.isBatchMode)
            {
                _ = SignInInBackground();
            }

            return Task.CompletedTask;
        }

        private async Task SignInInBackground()
        {
            try
            {
                await SignInAsync();
            }
            catch (Exception exception)
            {
                Log.Warning(Tag, $"Offline: {exception.Message}");
            }
        }

        public async Task<PlayerProfile> SignInAsync()
        {
            SetState(BackendState.SigningIn);
            try
            {
                await client.SignInAsync();
                var profile = await client.CallAsync<PlayerProfile>("ensureProfile", null);
                SetProfile(profile);
                SetState(BackendState.Online);
                Log.Info(Tag, $"Signed in as {profile.DisplayName} ({client.Uid}), level {profile.Level}, {profile.Coins} coins.");
                return profile;
            }
            catch
            {
                SetState(BackendState.Offline);
                throw;
            }
        }

        public async Task<PlayerProfile> RefreshProfileAsync()
        {
            var profile = await client.CallAsync<PlayerProfile>("getProfile", null);
            SetProfile(profile);
            return profile;
        }

        public async Task SetUsernameAsync(string username)
        {
            await client.CallAsync("setUsername", new { username });
            await RefreshProfileAsync();
        }

        public async Task<List<CatalogEntry>> GetCatalogAsync()
        {
            return (await client.CallAsync<CatalogResponse>("getCatalog", null)).Items;
        }

        public async Task PurchaseAsync(string itemId)
        {
            await client.CallAsync("purchaseShopItem", new { itemId });
            await RefreshProfileAsync();
        }

        public async Task EquipAsync(Dictionary<string, string> equipped)
        {
            await client.CallAsync("equipItems", new { equipped });
            await RefreshProfileAsync();
        }

        public async Task<DailyRewardResult> ClaimDailyRewardAsync()
        {
            var result = await client.CallAsync<DailyRewardResult>("claimDailyReward", null);
            await RefreshProfileAsync();
            return result;
        }

        public async Task<List<LeaderboardEntry>> GetLeaderboardAsync(int limit)
        {
            return (await client.CallAsync<LeaderboardResponse>("getLeaderboard", new { limit })).Entries;
        }

        public Task SendFriendRequestAsync(string username) => client.CallAsync("sendFriendRequest", new { username });

        public Task RespondFriendRequestAsync(string fromUid, bool accept) => client.CallAsync("respondFriendRequest", new { fromUid, accept });

        public async Task LinkAccountAsync(string providerId, string providerIdToken)
        {
            await client.LinkWithProviderAsync(providerId, providerIdToken);
            await RefreshProfileAsync();
        }

        private void SetProfile(PlayerProfile profile)
        {
            Profile = profile;
            EventBus.Publish(new ProfileUpdatedEvent(profile));
        }

        private void SetState(BackendState state)
        {
            if (State == state)
            {
                return;
            }

            State = state;
            EventBus.Publish(new BackendStateChangedEvent(state));
        }
    }
}
