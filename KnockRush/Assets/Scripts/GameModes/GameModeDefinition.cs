using System;
using KnockRush.Maps;
using UnityEngine;

namespace KnockRush.GameModes
{
    /// <summary>One round of a game mode.</summary>
    [Serializable]
    public struct RoundRule
    {
        [Tooltip("Which kind of map this round is played on.")]
        public MapCategory Category;

        [Tooltip("Fraction of the round's players that qualify (e.g. 0.625 → 32 → 20).")]
        [Range(0.05f, 1f)] public float QualifyRatio;

        [Tooltip("Minimum number of qualifiers (when enough players remain).")]
        [Min(1)] public int MinQualifiers;

        [Tooltip("Final round: exactly one winner.")]
        public bool IsFinal;

        public RoundRule(MapCategory category, float ratio, int minQualifiers, bool isFinal)
        {
            Category = category;
            QualifyRatio = ratio;
            MinQualifiers = minQualifiers;
            IsFinal = isFinal;
        }
    }

    /// <summary>A playable game mode (Classic Knockout, Quick Race, Private Party, future events).</summary>
    [CreateAssetMenu(menuName = "KnockRush/Game Modes/Game Mode", fileName = "GameMode", order = 0)]
    public sealed class GameModeDefinition : ScriptableObject
    {
        [SerializeField] private string modeId = "classic";
        [SerializeField] private string displayName = "Classic Knockout";
        [SerializeField, TextArea] private string description;
        [SerializeField, Range(1, 32)] private int minPlayers = 2;
        [SerializeField, Range(2, 32)] private int targetPlayers = 32;
        [SerializeField] private bool fillWithBots = true;
        [SerializeField, Min(1f)] private float lobbySeconds = 20f;
        [SerializeField] private RoundRule[] rounds = new RoundRule[0];

        public string ModeId => modeId;
        public string DisplayName => displayName;
        public string Description => description;
        public int MinPlayers => minPlayers;
        public int TargetPlayers => targetPlayers;
        public bool FillWithBots => fillWithBots;
        public float LobbySeconds => lobbySeconds;
        public RoundRule[] Rounds => rounds;

        public void Configure(string id, string name, string text, int min, int target, bool bots, float lobby, RoundRule[] roundRules)
        {
            modeId = id;
            displayName = name;
            description = text;
            minPlayers = min;
            targetPlayers = target;
            fillWithBots = bots;
            lobbySeconds = lobby;
            rounds = roundRules;
        }
    }
}
