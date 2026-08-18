using System.Collections.Generic;
using UnityEngine;

namespace Girgit
{
    /// <summary>
    /// One level = a sequence of 4-5 Challenges (colors) the player must clear
    /// in order. Make later levels harder by giving their Challenges less
    /// TimeLimit and/or a higher RequiredMatch. Everything here is edited
    /// from the GameManager's Inspector.
    /// </summary>
    [System.Serializable]
    public class LevelDefinition
    {
        [Tooltip("Shown in the HUD. Leave blank to auto-name 'LEVEL n'.")]
        public string Name = "";

        [Tooltip("4-5 colors to clear, in order, to complete this level.")]
        public List<ChallengeDefinition> Challenges = new List<ChallengeDefinition>();
    }
}
