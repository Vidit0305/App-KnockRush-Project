using System;
using KnockRush.GameModes;
using KnockRush.Multiplayer;
using UnityEngine;

namespace KnockRush.UI.Hud
{
    /// <summary>
    /// Knockout match overlay (lobby, countdown, qualification counter, round results, winner).
    /// IMGUI placeholder driven entirely by the replicated MatchController state; the styled UI
    /// replaces it in Phase 8 with the same data.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MatchHud : MonoBehaviour
    {
        private GUIStyle big;
        private GUIStyle medium;
        private GUIStyle small;
        private GUIStyle button;

        public static string RoomCode { get; set; }

        private void OnGUI()
        {
            var match = MatchController.Current;
            if (match == null || match.Object == null || !match.Object.IsValid)
            {
                return;
            }

            EnsureStyles();
            var scale = Screen.height / 720f;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
            var width = Screen.width / scale;
            var localSlot = match.LocalSlot();
            var local = localSlot >= 0 ? match.Records.Get(localSlot) : default;
            var mode = match.Mode != null ? match.Mode.DisplayName : "Match";
            var map = match.CurrentMap != null ? match.CurrentMap.DisplayName : string.Empty;

            switch (match.Phase)
            {
                case MatchPhase.Lobby:
                    Center(width, 120f, mode.ToUpperInvariant(), medium);
                    Center(width, 165f, $"Waiting for players  {match.HumanCount()}/{Mathf.Max(1, match.Mode != null ? match.Mode.TargetPlayers : 32)}   ·   starts in {match.TimeRemaining:0}s", small);
                    if (!string.IsNullOrEmpty(RoomCode))
                    {
                        Center(width, 200f, $"Room code: {RoomCode}", medium);
                    }

                    if (match.Runner.LocalPlayer == match.RoomOwner || match.Runner.IsSinglePlayer)
                    {
                        if (GUI.Button(new Rect(width * 0.5f - 110f, 250f, 220f, 54f), "START NOW", button))
                        {
                            match.RPC_RequestStart();
                        }
                    }

                    break;

                case MatchPhase.Loading:
                    Center(width, 300f, $"Round {match.RoundIndex + 1}: {map}", big);
                    break;

                case MatchPhase.Countdown:
                    Center(width, 160f, $"ROUND {match.RoundIndex + 1}  ·  {map.ToUpperInvariant()}", medium);
                    Center(width, 205f, RoundGoal(match), small);
                    Center(width, 290f, Mathf.CeilToInt(match.TimeRemaining).ToString(), big);
                    break;

                case MatchPhase.Playing:
                    Center(width, 20f, $"{RoundGoal(match)}   ·   {FormatTime(match.TimeRemaining)}", small);
                    if (local.Status == RecordStatus.Qualified)
                    {
                        Center(width, 260f, "QUALIFIED!", big);
                    }
                    else if (local.Status == RecordStatus.Eliminated)
                    {
                        Center(width, 260f, "ELIMINATED", big);
                        Center(width, 360f, "Spectating…", small);
                    }

                    break;

                case MatchPhase.RoundResults:
                    Center(width, 240f, local.Status == RecordStatus.Qualified ? "YOU'RE THROUGH!" : localSlot >= 0 ? "KNOCKED OUT" : "ROUND OVER", big);
                    Center(width, 340f, $"{match.QualifiedCount()} players qualified", medium);
                    break;

                case MatchPhase.MatchResults:
                    var winner = match.WinnerSlot;
                    var youWon = winner >= 0 && winner == localSlot;
                    Center(width, 220f, youWon ? "CHAMPION!" : "MATCH OVER", big);
                    Center(width, 320f, winner >= 0 ? (youWon ? "You are the KnockRush champion!" : $"Winner: {Name(match, winner)}") : "No winner this time", medium);
                    break;
            }

            GUI.matrix = Matrix4x4.identity;
        }

        private static string RoundGoal(MatchController match)
        {
            var survival = match.CurrentMap != null && match.CurrentMap.FallRule == KnockRush.Maps.FallRule.Eliminate;
            if (match.CurrentRule.IsFinal)
            {
                return survival ? "Last one standing wins!" : "First to the finish wins!";
            }

            return survival
                ? $"Survive! {match.ActiveCount()} left · {match.Quota} qualify"
                : $"Qualified {match.QualifiedThisRound}/{match.Quota}";
        }

        private static string Name(MatchController match, int slot)
        {
            var record = match.Records.Get(slot);
            return record.IsBot ? $"Bot {slot + 1}" : $"Player {record.Player.PlayerId}";
        }

        private static string FormatTime(float seconds)
        {
            var minutes = Mathf.FloorToInt(seconds / 60f);
            return $"{minutes}:{Mathf.FloorToInt(seconds - minutes * 60f):00}";
        }

        private void Center(float width, float y, string text, GUIStyle style)
        {
            GUI.Label(new Rect(0f, y, width, style.fontSize * 1.6f), text, style);
        }

        private void EnsureStyles()
        {
            if (big != null)
            {
                return;
            }

            big = Style(84, new Color32(0xFF, 0xD1, 0x2B, 0xFF));
            medium = Style(34, Color.white);
            small = Style(22, Color.white);
            button = new GUIStyle(GUI.skin.button) { fontSize = 24, fontStyle = FontStyle.Bold };
        }

        private static GUIStyle Style(int size, Color color)
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            style.normal.textColor = color;
            return style;
        }
    }
}
