using System.Collections.Generic;

namespace TalkOut.Directing
{
    /// Structured ruling from the judge LLM after each exchange.
    /// The engine — not the cop — acts on it.
    public class JudgeVerdict
    {
        /// The officer has clearly, verbally let the player go.
        public bool Released;

        /// The situation has escalated to an arrest.
        public bool Arrested;

        /// Officer's current mood — drives face texture + wobble energy.
        public string CopMood = "neutral";

        /// 0-2 physical scene actions from the approved catalog.
        public List<string> ActionIds = new List<string>();

        public bool IsFallback;
        public string RawOutput = "";

        public static readonly string[] Moods =
        {
            "neutral", "suspicious", "angry", "amused", "confused", "warm", "defeated"
        };
    }
}
