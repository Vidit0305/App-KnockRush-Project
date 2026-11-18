using System.Collections.Generic;
using KnockRush.Backend;
using KnockRush.UI.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace KnockRush.UI.Screens
{
    /// <summary>Common "you need to be online" placeholder for backend-driven screens.</summary>
    internal static class OfflineNotice
    {
        public static VisualElement Create(string what)
        {
            var box = Ui.Col("kr-center", "kr-grow");
            box.Add(Ui.Heading("YOU'RE OFFLINE"));
            box.Add(Ui.Label($"Connect to the internet to see {what}. Practice matches still work offline.", "kr-subtle"));
            return box;
        }
    }

    /// <summary>9 · Friends &amp; Party: friends list, requests, add by username, invite via room code.</summary>
    public sealed class FriendsScreen : UIScreen
    {
        private VisualElement content;
        private TextField addName;

        protected override void Build(VisualElement root)
        {
            root.AddToClassList("kr-backdrop");
            root.Add(Ui.Header("FRIENDS & PARTY", () => Nav.Pop(), Ui.Button("CREATE PARTY ROOM", () => Nav.Push<PrivateRoomScreen>(), "kr-btn--good", "kr-btn--small")));
            var addRow = Ui.Row();
            addName = Ui.Input("Friend's username", 16);
            addRow.Add(addName);
            addRow.Add(Ui.Button("ADD FRIEND", AddFriend, "kr-btn--small"));
            root.Add(addRow);
            content = Ui.Scroll();
            root.Add(content);
        }

        public override void OnShow() => Load();

        private async void Load()
        {
            content.Clear();
            if (!UiData.Online)
            {
                content.Add(OfflineNotice.Create("your friends"));
                return;
            }

            FriendsResponse response = null;
            if (!await App.Run(async () => response = await UiData.Backend.GetFriendsAsync()) || response == null)
            {
                return;
            }

            content.Clear();
            if (response.Requests.Count > 0)
            {
                content.Add(Ui.Label($"REQUESTS ({response.Requests.Count})", "kr-section__title", "kr-display"));
                foreach (var request in response.Requests)
                {
                    var row = Ui.Row("kr-list-row");
                    row.Add(Avatar(request.DisplayName));
                    row.Add(Ui.Label(request.DisplayName, "kr-grow"));
                    var uid = request.Uid;
                    row.Add(Ui.Button("ACCEPT", () => Respond(uid, true), "kr-btn--small", "kr-btn--good"));
                    row.Add(Ui.Button("DECLINE", () => Respond(uid, false), "kr-btn--small", "kr-btn--ghost"));
                    content.Add(row);
                }
            }

            content.Add(Ui.Label($"FRIENDS ({response.Friends.Count})", "kr-section__title", "kr-display"));
            if (response.Friends.Count == 0)
            {
                content.Add(Ui.Label("No friends yet: add someone by username, or share a private room code.", "kr-subtle"));
            }

            foreach (var friend in response.Friends)
            {
                var row = Ui.Row("kr-list-row");
                row.Add(Avatar(friend.DisplayName));
                row.Add(Ui.Label(friend.DisplayName, "kr-grow"));
                row.Add(Ui.Button("INVITE", () => Invite(friend.DisplayName), "kr-btn--small"));
                content.Add(row);
            }
        }

        private async void AddFriend()
        {
            var name = addName.value.Trim();
            if (name.Length < 3 || !UiData.Online)
            {
                App.Toast(UiData.Online ? "Type your friend's username." : "You're offline.");
                return;
            }

            if (await App.Run(() => UiData.Backend.SendFriendRequestAsync(name), $"Friend request sent to {name}!"))
            {
                addName.value = string.Empty;
            }
        }

        private async void Respond(string uid, bool accept)
        {
            if (await App.Run(() => UiData.Backend.RespondFriendRequestAsync(uid, accept), accept ? "You're now friends!" : null))
            {
                Load();
            }
        }

        private void Invite(string name)
        {
            if (!string.IsNullOrEmpty(UiData.RoomCode))
            {
                GUIUtility.systemCopyBuffer = UiData.RoomCode;
                App.Toast($"Room code {UiData.RoomCode} copied: send it to {name}.");
                return;
            }

            DialogScreen.Show(App, "INVITE " + name.ToUpperInvariant(), "Create a private room, then share its code with your friend.", "CREATE ROOM", () => Nav.Push<PrivateRoomScreen>(), "LATER");
        }

        internal static VisualElement Avatar(string name)
        {
            var avatar = Ui.Label(string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1).ToUpperInvariant(), "kr-avatar", "kr-display");
            var hash = 0;
            foreach (var c in name ?? string.Empty)
            {
                hash = hash * 31 + c;
            }

            avatar.style.backgroundColor = KnockRush.Player.Presentation.BeanAppearance.DefaultPalette[Mathf.Abs(hash) % KnockRush.Player.Presentation.BeanAppearance.DefaultPalette.Length];
            return avatar;
        }
    }

    /// <summary>15 · Leaderboard: global top players by wins.</summary>
    public sealed class LeaderboardScreen : UIScreen
    {
        private VisualElement list;

        protected override void Build(VisualElement root)
        {
            root.AddToClassList("kr-backdrop");
            root.Add(Ui.Header("LEADERBOARD", () => Nav.Pop()));
            var head = Ui.Row("kr-list-row", "kr-list-head");
            head.Add(Cell("#", 80));
            head.Add(Cell("PLAYER", 0));
            head.Add(Cell("LEVEL", 140));
            head.Add(Cell("CROWNS", 160));
            head.Add(Cell("WINS", 140));
            root.Add(head);
            list = Ui.Scroll();
            root.Add(list);
        }

        public override async void OnShow()
        {
            list.Clear();
            if (!UiData.Online)
            {
                list.Add(OfflineNotice.Create("the leaderboard"));
                return;
            }

            List<LeaderboardEntry> entries = null;
            if (!await App.Run(async () => entries = await UiData.Backend.GetLeaderboardAsync(50)) || entries == null)
            {
                return;
            }

            list.Clear();
            if (entries.Count == 0)
            {
                list.Add(Ui.Label("No champions yet. Win a match to be the first!", "kr-subtle"));
            }

            foreach (var entry in entries)
            {
                var row = Ui.Row("kr-list-row");
                row.EnableInClassList("kr-list-row--you", entry.Uid == UiData.Backend.Uid);
                row.EnableInClassList("kr-list-row--top", entry.Rank <= 3);
                row.Add(Cell(entry.Rank.ToString(), 80));
                var who = Ui.Row("kr-grow");
                who.Add(FriendsScreen.Avatar(entry.DisplayName));
                who.Add(Ui.Label(entry.DisplayName));
                row.Add(who);
                row.Add(Cell(entry.Level.ToString(), 140));
                row.Add(Cell(entry.Crowns.ToString(), 160));
                row.Add(Cell(entry.Wins.ToString(), 140));
                list.Add(row);
            }
        }

        private static Label Cell(string text, float width)
        {
            var label = Ui.Label(text, "kr-display");
            if (width > 0)
            {
                label.style.width = width;
            }
            else
            {
                label.style.flexGrow = 1;
            }

            return label;
        }
    }

    /// <summary>16 · Player Profile: name, level, stats and account.</summary>
    public sealed class ProfileScreen : UIScreen
    {
        private Label name;
        private Label level;
        private VisualElement xpFill;
        private Label xpText;
        private VisualElement stats;

        protected override void Build(VisualElement root)
        {
            root.AddToClassList("kr-backdrop");
            root.Add(Ui.Header("PROFILE", () => Nav.Pop()));
            var row = Ui.Row("kr-grow");
            row.style.alignItems = Align.FlexStart;

            var card = Ui.Panel("kr-profile-card");
            level = Ui.Label("1", "kr-level", "kr-level--big", "kr-display");
            card.Add(level);
            name = Ui.Title("Player");
            card.Add(name);
            var bar = Ui.ProgressBar(out xpFill, new Color32(34, 197, 139, 255));
            bar.style.width = 420;
            card.Add(bar);
            xpText = Ui.Label(string.Empty, "kr-subtle");
            card.Add(xpText);
            var actions = Ui.Row("kr-center");
            actions.Add(Ui.Button("CHANGE NAME", () => Nav.Push<LoginScreen>(), "kr-btn--small"));
            actions.Add(Ui.Button("LOCKER", () => Nav.Push<CustomizeScreen>(), "kr-btn--small", "kr-btn--ghost"));
            card.Add(actions);
            row.Add(card);

            stats = Ui.Element("kr-wrap");
            stats.style.flexGrow = 1;
            row.Add(stats);
            root.Add(row);
        }

        public override void OnShow()
        {
            var profile = UiData.Profile;
            name.text = UiData.DisplayName;
            level.text = profile != null ? profile.Level.ToString() : "-";
            Ui.SetProgress(xpFill, profile != null && profile.XpForNextLevel > 0 ? (float)profile.Xp / profile.XpForNextLevel : 0f);
            xpText.text = profile != null ? $"{profile.Xp} / {profile.XpForNextLevel} XP to level {profile.Level + 1}" : "Offline";
            stats.Clear();
            var s = profile?.Stats ?? new PlayerStats();
            stats.Add(Stat("MATCHES", s.Matches));
            stats.Add(Stat("WINS", s.Wins));
            stats.Add(Stat("CROWNS", s.Crowns));
            stats.Add(Stat("ROUNDS CLEARED", s.RoundsCleared));
            stats.Add(Stat("WIN RATE", s.Matches > 0 ? Mathf.RoundToInt(100f * s.Wins / s.Matches) : 0, "%"));
            stats.Add(Stat("ITEMS", profile?.Inventory?.Count ?? 0));
        }

        private static VisualElement Stat(string label, int value, string suffix = "")
        {
            var tile = Ui.Panel("kr-stat");
            tile.Add(Ui.Label(Ui.FormatNumber(value) + suffix, "kr-stat__value", "kr-display"));
            tile.Add(Ui.Label(label, "kr-subtle"));
            return tile;
        }
    }
}
