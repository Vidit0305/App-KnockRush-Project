using UnityEngine;

namespace KnockRush.GameModes
{
    /// <summary>Pure round-rule maths (unit tested).</summary>
    public static class QualificationRules
    {
        /// <summary>
        /// Number of players that qualify from a round with <paramref name="activePlayers"/>.
        /// Always eliminates at least one player when more than one remains; finals have one winner.
        /// Classic Knockout with 32: 32 → 20 → 10 → 5 → 1.
        /// </summary>
        public static int ComputeQuota(int activePlayers, RoundRule rule)
        {
            if (activePlayers <= 1)
            {
                return activePlayers;
            }

            if (rule.IsFinal)
            {
                return 1;
            }

            var quota = Mathf.RoundToInt(activePlayers * rule.QualifyRatio);
            var floor = Mathf.Max(1, Mathf.Min(rule.MinQualifiers, activePlayers - 1));
            return Mathf.Clamp(quota, floor, activePlayers - 1);
        }

        /// <summary>
        /// Total players wanted for a match: the mode's target, capped by the session size,
        /// never below the humans present (bots only fill free slots).
        /// </summary>
        public static int ComputeFill(int humans, int targetPlayers, int sessionCapacity, bool fillWithBots)
        {
            if (!fillWithBots)
            {
                return humans;
            }

            return Mathf.Max(humans, Mathf.Min(targetPlayers, sessionCapacity));
        }
    }
}
