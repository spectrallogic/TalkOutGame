using System.Collections.Generic;
using UnityEngine;
using TalkOut.Data;

namespace TalkOut.Directing
{
    /// The ITYSL dial: some turns, a secret directive nudges the NPC into
    /// deadpan absurdity — misheard words, pointless anecdotes, tiny personal
    /// laws, dead air. The character never acknowledges that anything is off.
    public static class WeirdnessDeck
    {
        private static readonly string[] Global =
        {
            "Open with just 'Mmm.' — a beat of dead air — then your actual reply. The pause IS the joke.",
            "Misunderstand exactly ONE word they said, with total confidence. If corrected, do not acknowledge it.",
            "Bring up an oddly specific personal anecdote that helps no one. ('My cousin Gary had a boat. Doesn't matter now.')",
            "State a tiny personal rule and enforce it like it's the law. ('I don't do metric. Ever.')",
            "Trail off mid-sentence and just... start a completely different thought.",
            "Use a word that is ALMOST right for what you mean. Commit to it completely.",
            "Say something completely normal, but phrase it like a veiled threat. Or something threatening, phrased like a compliment.",
            "Get briefly, deeply fascinated by one specific word they used. Repeat it once, quietly. Then move on like nothing happened.",
            "Answer the question they SHOULD have asked, not the one they did.",
            "Reference an event from earlier tonight that definitely did not happen. Be casual about it.",
            "Give one piece of genuinely good advice about something completely unrelated. Then return to business.",
            "Count something out loud for no reason. ('That's the third time tonight. Third.')",
        };

        /// Rolls the scenario's weirdness chance; returns a directive or null.
        public static string Draw(ScenarioDefinition scenario)
        {
            if (scenario == null || Random.value > scenario.weirdnessChance) return null;

            int globalCount = Global.Length;
            int spiceCount = scenario.weirdSpice?.Count ?? 0;
            int pick = Random.Range(0, globalCount + spiceCount);
            return pick < globalCount ? Global[pick] : scenario.weirdSpice[pick - globalCount];
        }
    }
}
