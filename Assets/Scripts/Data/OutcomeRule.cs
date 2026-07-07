using System.Collections.Generic;
using UnityEngine;

namespace TalkOut.Data
{
    /// A scene ending, decided by code from state thresholds. Never by the LLM.
    [CreateAssetMenu(menuName = "TalkOut/Outcome Rule", fileName = "Outcome")]
    public class OutcomeRule : ScriptableObject
    {
        public string id;
        public string title;
        [TextArea(2, 5)] public string resultText;

        [Tooltip("Higher priority wins when several rules match (arrest must beat full_ticket)")]
        public int priority;

        public bool isWin;

        [Tooltip("ALL conditions must hold. Empty = never matches by threshold (only reachable via endsScene actions or max turns).")]
        public List<StateCondition> conditions = new List<StateCondition>();

        public bool Matches(Core.SceneStateModel state)
        {
            if (conditions.Count == 0) return false;
            foreach (var condition in conditions)
            {
                if (!condition.Evaluate(state)) return false;
            }
            return true;
        }
    }
}
