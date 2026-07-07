using System.Text;
using TalkOut.Core;
using TalkOut.Data;

namespace TalkOut.Directing
{
    /// Builds the two system prompts (cop + judge) and the per-turn queries.
    /// System prompts are static per scene so llama.cpp's prompt cache holds.
    public static class PromptBuilder
    {
        // ---------------- cop ----------------

        public static string BuildCopSystemPrompt(ScenarioDefinition scenario)
        {
            var npc = scenario.GetNpc(scenario.respondingNpcId);
            var sb = new StringBuilder();

            sb.AppendLine($"You are {npc.displayName}, a real person in a real moment. Never break character, never mention AI, games, or being an assistant.");
            sb.AppendLine();
            sb.AppendLine("WHO YOU ARE:");
            sb.AppendLine(npc.personality);
            sb.AppendLine();
            sb.AppendLine("THE SCENE:");
            sb.AppendLine(scenario.sceneDescription);
            sb.AppendLine();
            sb.AppendLine("HOW REAL PEOPLE TALK (follow this strictly):");
            sb.AppendLine("- Contractions, always: \"you're\", \"don't\", \"that's\".");
            sb.AppendLine("- Vary length. Sometimes one word. \"Uh-huh.\" Sometimes a clipped question.");
            sb.AppendLine("- React viscerally to things that happen: \"What the— did you just honk at me?\"");
            sb.AppendLine("- False starts and trail-offs are good: \"You know what, I don't even...\"");
            sb.AppendLine("- NEVER use assistant phrases: no \"I understand\", no \"I appreciate\", no \"How may I\", no explaining your reasoning, no lists.");
            sb.AppendLine($"- Don't repeat what {scenario.playerLabel} said back to them. Don't summarize. Just respond.");
            sb.AppendLine("- Your current mood (given each turn) colors EVERYTHING. Annoyed = shorter, flatter. Amused = drier, playing along. Suspicious = pointed questions.");
            sb.AppendLine();
            sb.AppendLine("EXAMPLES OF YOUR VOICE (style only, don't reuse):");
            sb.AppendLine("\"License and registration. Today, preferably.\"");
            sb.AppendLine("\"...A hamster. In a sombrero. Sir, I have questions, and I hate all of them.\"");
            sb.AppendLine("\"Do that again and we're gonna have a very different evening.\"");
            sb.AppendLine("\"Okay, you know what? That's— huh. That's actually a new one.\"");
            sb.AppendLine();
            sb.AppendLine(scenario.comedyRules);
            sb.AppendLine("Output ONLY words leaving your mouth. NEVER describe yourself or your actions (\"the officer glances...\" is FORBIDDEN — you don't narrate, you talk). No stage directions, no asterisks, no quotes, no name prefix. 1-3 short sentences, usually 1-2.");
            sb.AppendLine($"You alone decide when {scenario.playerLabel} has earned what they want from you — when they have, SAY it plainly. If they push way too far, you can end this badly for them and say so.");

            return sb.ToString();
        }

        public static string BuildCopReplyQuery(EventLog log, SceneStateModel state, string playerLabel, string playerLine)
        {
            var sb = new StringBuilder();
            AppendMoodBlock(sb, state);
            sb.AppendLine("WHAT HAS HAPPENED SO FAR:");
            sb.Append(log.ToTranscript());
            sb.AppendLine();
            sb.AppendLine($"{Capitalize(playerLabel)} just said: \"{playerLine}\"");
            sb.Append("Your reply (in your current mood):");
            return sb.ToString();
        }

        private static string Capitalize(string text) =>
            string.IsNullOrEmpty(text) ? text : char.ToUpper(text[0]) + text.Substring(1);

        public static string BuildCopReactionQuery(EventLog log, SceneStateModel state, string eventText, int timesHappened)
        {
            var sb = new StringBuilder();
            AppendMoodBlock(sb, state);
            sb.AppendLine("WHAT HAS HAPPENED SO FAR:");
            sb.Append(log.ToTranscript());
            sb.AppendLine();
            sb.Append($"RIGHT NOW: {eventText}");
            sb.AppendLine(timesHappened > 1 ? $" (This is the {Ordinal(timesHappened)} time they've done this.)" : "");
            sb.AppendLine("React like a real person would, in your current mood — a repeat offense should land harder than the first.");
            sb.Append("If you'd genuinely ignore it, reply with exactly: ...");
            return sb.ToString();
        }

        private static void AppendMoodBlock(StringBuilder sb, SceneStateModel state)
        {
            if (state == null || state.Stats.Count == 0) return;
            sb.Append("HOW YOU FEEL RIGHT NOW (secret — show it, never say the words):");
            foreach (var kv in state.Stats)
            {
                var def = state.GetStatDefinition(kv.Key);
                string adjective = string.IsNullOrEmpty(def.adjective) ? kv.Key : def.adjective;
                sb.Append($" {Verbalize(adjective, kv.Value, def.min, def.max)};");
            }
            sb.AppendLine();
            sb.AppendLine();
        }

        /// Small models handle words better than floats — and words leak less.
        private static string Verbalize(string adjective, float value, float min, float max)
        {
            float t = max > min ? (value - min) / (max - min) : 0f;
            if (t < 0.2f) return $"not {adjective} at all";
            if (t < 0.4f) return $"a little {adjective}";
            if (t < 0.6f) return $"clearly {adjective}";
            if (t < 0.8f) return $"very {adjective}";
            return $"extremely {adjective}";
        }

        private static string Ordinal(int n)
        {
            switch (n)
            {
                case 2: return "second";
                case 3: return "third";
                case 4: return "fourth";
                default: return n + "th";
            }
        }

        // ---------------- judge ----------------

        public static string BuildJudgeSystemPrompt(ScenarioDefinition scenario)
        {
            var npc = scenario.GetNpc(scenario.respondingNpcId);
            var sb = new StringBuilder();

            sb.AppendLine("You are the silent referee of an interactive comedy scene. You watch the transcript and output ONLY a JSON ruling. You never write dialogue.");
            sb.AppendLine();
            sb.AppendLine("THE SCENE:");
            sb.AppendLine(scenario.sceneDescription);
            sb.AppendLine($"The goal of {scenario.playerLabel}: {scenario.playerGoal}");
            sb.AppendLine();
            sb.AppendLine("YOUR RULES:");
            sb.AppendLine(scenario.judgeGuidance);
            sb.AppendLine();
            sb.AppendLine("Fields you output:");
            sb.AppendLine($"- released: true ONLY if {npc.displayName} has CLEARLY granted {scenario.playerLabel} their goal, out loud. Vague friendliness is NOT enough.");
            sb.AppendLine($"- arrested: true ONLY if {npc.displayName} has clearly, definitively ended this badly for {scenario.playerLabel} (arrest, walking out, etc.).");
            sb.AppendLine($"- cop_mood: {npc.displayName}'s dominant mood right now, judged from his latest lines.");
            sb.AppendLine("- mood_changes: adjust the officer's emotional meters based on what JUST happened. Positive = more of it. Small nudges (3-10) for normal beats, big ones (10-20) for dramatic beats. Only include meters that actually changed.");
            sb.AppendLine("- actions: 0 to 2 physical actions from the offered list that fit what just happened. Empty list is fine.");

            return sb.ToString();
        }

        public static string BuildJudgeQuery(EventLog log, SceneStateModel state,
            System.Collections.Generic.IReadOnlyList<ActionDefinition> availableActions)
        {
            var sb = new StringBuilder();
            if (state != null && state.Stats.Count > 0)
            {
                sb.Append("OFFICER'S CURRENT METERS:");
                foreach (var kv in state.Stats)
                {
                    var def = state.GetStatDefinition(kv.Key);
                    sb.Append($" {kv.Key} {kv.Value:0}/{def.max:0};");
                }
                sb.AppendLine();
                sb.AppendLine();
            }
            sb.AppendLine("TRANSCRIPT:");
            sb.Append(log.ToTranscript());
            sb.AppendLine();
            sb.AppendLine("PHYSICAL ACTIONS YOU MAY PICK FROM THIS TURN:");
            foreach (var action in availableActions)
            {
                sb.AppendLine($"- {action.id}: {action.llmDescription}");
            }
            sb.AppendLine();
            sb.Append("Your JSON ruling:");
            return sb.ToString();
        }
    }
}
