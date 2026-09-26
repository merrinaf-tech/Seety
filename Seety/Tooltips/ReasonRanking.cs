using System.Collections.Generic;

namespace Seety.Tooltips
{
    /// <summary>One thing holding a building back, before it is turned into a tooltip line.</summary>
    public struct ReasonCandidate
    {
        /// <summary>A notification prefab name, or an EfficiencyFactor name.</summary>
        public string Key;

        /// <summary>True for a notification the game raised on the building.</summary>
        public bool IsNotification;

        /// <summary>
        /// For a notification, its IconPriority as a number: higher is worse. For an efficiency
        /// factor, the multiplier the game applies: lower is worse.
        /// </summary>
        public float Severity;
    }

    /// <summary>
    /// Chooses which reasons a building's tooltip shows.
    ///
    /// Game-free on purpose, so the choice can be tested without the game. The rules:
    ///
    /// - What the game itself flagged comes first. A notification is the game saying, in its own
    ///   words, that something is wrong here; an efficiency factor is a number the player has to
    ///   interpret. Worst notification first.
    /// - Then efficiency factors that actually cost something, largest loss first. Anything within
    ///   a couple of percent of full is noise, not a reason.
    /// - At most <see cref="MaxLines"/>. A tooltip is read in the half-second the cursor rests; a
    ///   fourth line is a panel, and the building's own panel already exists.
    /// - Nothing at all when nothing is wrong. A building with no reason gets no tooltip.
    /// </summary>
    public static class ReasonRanking
    {
        public const int MaxLines = 3;

        /// <summary>Factors at or above this multiplier are not worth a line.</summary>
        public const float FactorThreshold = 0.98f;

        public static void Pick(List<ReasonCandidate> notifications, List<ReasonCandidate> factors,
            List<ReasonCandidate> picked)
        {
            picked.Clear();

            notifications.Sort((a, b) => b.Severity.CompareTo(a.Severity));
            foreach (var note in notifications)
            {
                if (picked.Count >= MaxLines)
                {
                    return;
                }

                if (!Contains(picked, note.Key))
                {
                    picked.Add(note);
                }
            }

            factors.Sort((a, b) => a.Severity.CompareTo(b.Severity));
            foreach (var factor in factors)
            {
                if (picked.Count >= MaxLines)
                {
                    return;
                }

                if (factor.Severity < FactorThreshold && !Contains(picked, factor.Key))
                {
                    picked.Add(factor);
                }
            }
        }

        /// <summary>The loss a factor causes, as a whole percentage: 0.68 is 32.</summary>
        public static int LossPercent(float multiplier)
        {
            var loss = (1f - multiplier) * 100f;
            return loss < 1f ? 1 : (int)(loss + 0.5f);
        }

        private static bool Contains(List<ReasonCandidate> list, string key)
        {
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i].Key == key)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
