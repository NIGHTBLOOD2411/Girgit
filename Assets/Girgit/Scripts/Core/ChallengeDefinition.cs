using UnityEngine;

namespace Girgit
{
    /// <summary>
    /// One challenge = one fixed color the player must match within a time
    /// limit. A level is a sequence of 4-5 of these (see LevelDefinition) —
    /// clear every challenge in order to clear the level.
    /// </summary>
    [System.Serializable]
    public class ChallengeDefinition
    {
        [Tooltip("The color for this challenge. The player matches THIS.")]
        public Color Color = new Color(0.30f, 0.70f, 0.35f, 1f);

        [Tooltip("Seconds allowed to reach the required match.")]
        [Min(1f)] public float TimeLimit = 15f;

        [Tooltip("Match % needed to clear this challenge.")]
        [Range(50f, 100f)] public float RequiredMatch = 90f;
    }
}
