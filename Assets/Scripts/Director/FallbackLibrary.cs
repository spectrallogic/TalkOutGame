using System.Collections.Generic;
using TalkOut.Data;

namespace TalkOut.Directing
{
    /// In-fiction canned material used whenever an LLM call fails
    /// (timeout, model missing). Lines come from the scenario's data;
    /// the generic pool only backstops scenarios that define none.
    public static class FallbackLibrary
    {
        private static int next;

        private static readonly List<string> GenericLines = new List<string>
        {
            "...Say that again. Slower.",
            "Mmm.",
            "I'm going to pretend I didn't hear that.",
            "That's... something.",
            "Interesting. Continue.",
        };

        public static string GetLine(ScenarioDefinition scenario, string reason)
        {
            UnityEngine.Debug.LogWarning($"[Fallback] NPC line used: {reason}");
            var pool = scenario != null && scenario.fallbackLines != null && scenario.fallbackLines.Count > 0
                ? scenario.fallbackLines
                : GenericLines;
            return pool[next++ % pool.Count];
        }

        public static JudgeVerdict GetVerdict(string reason)
        {
            UnityEngine.Debug.LogWarning($"[Fallback] Judge verdict used: {reason}");
            return new JudgeVerdict
            {
                Released = false,
                Arrested = false,
                CopMood = "confused",
                IsFallback = true,
                RawOutput = $"[fallback: {reason}]"
            };
        }
    }
}
