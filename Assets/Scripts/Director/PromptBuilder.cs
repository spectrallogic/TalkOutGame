using System.Text;
using UnityEngine;
using TalkOut.Core;
using TalkOut.Data;

namespace TalkOut.Directing
{
    /// Assembles prompts entirely from PromptStyleLibrary variant pools — no
    /// hardcoded instructional text. Scene-stable sections use a seeded RNG
    /// (picked once per scene, so llama.cpp's prompt cache stays warm);
    /// per-turn sections re-roll on every call.
    public static class PromptBuilder
    {
        private static PromptStyleLibrary fallbackStyle;

        /// Field-initializer defaults double as the no-asset fallback.
        private static PromptStyleLibrary Resolve(PromptStyleLibrary style)
        {
            if (style != null) return style;
            if (fallbackStyle == null)
            {
                fallbackStyle = ScriptableObject.CreateInstance<PromptStyleLibrary>();
            }
            return fallbackStyle;
        }

        private static string Tokens(string text, NPCDefinition npc, ScenarioDefinition scenario)
        {
            return text.Replace("{name}", npc != null ? npc.displayName : "the character")
                       .Replace("{player}", scenario != null ? scenario.playerLabel : "the player");
        }

        // ---------------- cop (system prompt: scene-stable) ----------------

        public static string BuildCopSystemPrompt(ScenarioDefinition scenario, System.Random rng)
        {
            var style = Resolve(scenario.promptStyle);
            var npc = scenario.GetNpc(scenario.respondingNpcId);
            var sb = new StringBuilder();

            sb.AppendLine(Tokens(PromptStyleLibrary.Pick(style.roleIntros, rng), npc, scenario));
            sb.AppendLine();
            sb.AppendLine("WHO YOU ARE:");
            sb.AppendLine(npc.personality);
            sb.AppendLine();
            sb.AppendLine("THE SCENE:");
            sb.AppendLine(scenario.sceneDescription);
            sb.AppendLine();

            sb.AppendLine(PromptStyleLibrary.Pick(style.speechSectionHeaders, rng));
            foreach (var rule in PromptStyleLibrary.Sample(style.speechRules, style.speechRuleCount, rng))
            {
                sb.AppendLine("- " + Tokens(rule, npc, scenario));
            }
            sb.AppendLine();
            sb.AppendLine(Tokens(PromptStyleLibrary.Pick(style.weirdRules, rng), npc, scenario));

            if (!string.IsNullOrEmpty(npc.edgeProfile))
            {
                sb.AppendLine();
                sb.AppendLine(PromptStyleLibrary.Pick(style.maskHeaders, rng));
                sb.AppendLine(npc.edgeProfile);
                sb.AppendLine(PromptStyleLibrary.Pick(style.maskFooters, rng));
            }

            if (npc.voiceExamples != null && npc.voiceExamples.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine(PromptStyleLibrary.Pick(style.voiceExampleHeaders, rng));
                foreach (var example in PromptStyleLibrary.Sample(npc.voiceExamples, style.voiceExampleCount, rng))
                {
                    sb.AppendLine($"\"{example}\"");
                }
            }

            sb.AppendLine();
            sb.AppendLine(scenario.comedyRules);
            sb.AppendLine(Tokens(PromptStyleLibrary.Pick(style.outputRules, rng), npc, scenario));
            sb.AppendLine(Tokens(PromptStyleLibrary.Pick(style.concessionLines, rng), npc, scenario));

            return sb.ToString();
        }

        // ---------------- cop (turn queries: re-rolled every turn) ----------------

        public static string BuildCopReplyQuery(EventLog log, SceneStateModel state, ScenarioDefinition scenario,
            string playerLine, string weirdDirective)
        {
            var style = Resolve(scenario.promptStyle);
            var sb = new StringBuilder();
            AppendMoodBlock(sb, state, style);
            AppendWeirdDirective(sb, weirdDirective, style);
            sb.AppendLine(PromptStyleLibrary.Pick(style.historyHeaders));
            sb.Append(log.ToTranscript());
            sb.AppendLine();
            sb.AppendLine($"{Capitalize(scenario.playerLabel)} just said: \"{playerLine}\"");
            sb.Append(PromptStyleLibrary.Pick(style.replyCues));
            return sb.ToString();
        }

        public static string BuildCopReactionQuery(EventLog log, SceneStateModel state, ScenarioDefinition scenario,
            string eventText, int timesHappened, string weirdDirective)
        {
            var style = Resolve(scenario.promptStyle);
            var sb = new StringBuilder();
            AppendMoodBlock(sb, state, style);
            AppendWeirdDirective(sb, weirdDirective, style);
            sb.AppendLine(PromptStyleLibrary.Pick(style.historyHeaders));
            sb.Append(log.ToTranscript());
            sb.AppendLine();
            sb.Append($"RIGHT NOW: {eventText}");
            sb.AppendLine(timesHappened > 1 ? $" (This is the {Ordinal(timesHappened)} time they've done this.)" : "");
            sb.AppendLine("React like a real person would, in your current mood — a repeat offense should land harder than the first.");
            // contract line: LlmCopBrain treats "..." as staying silent — keep exact
            sb.Append("If you'd genuinely ignore it, reply with exactly: ...");
            return sb.ToString();
        }

        private static void AppendMoodBlock(StringBuilder sb, SceneStateModel state, PromptStyleLibrary style)
        {
            if (state == null || state.Stats.Count == 0) return;
            sb.Append(PromptStyleLibrary.Pick(style.moodHeaders));
            foreach (var kv in state.Stats)
            {
                var def = state.GetStatDefinition(kv.Key);
                string adjective = string.IsNullOrEmpty(def.adjective) ? kv.Key : def.adjective;
                sb.Append($" {Verbalize(style, adjective, kv.Value, def.min, def.max)};");
            }
            sb.AppendLine();
            AppendEdgeDirective(sb, state, style);
            sb.AppendLine();
        }

        private static void AppendWeirdDirective(StringBuilder sb, string directive, PromptStyleLibrary style)
        {
            if (string.IsNullOrEmpty(directive)) return;
            sb.AppendLine($"{PromptStyleLibrary.Pick(style.weirdIntros)} {directive}");
            sb.AppendLine();
        }

        /// The mask-slip dial, phrased differently every turn.
        private static void AppendEdgeDirective(StringBuilder sb, SceneStateModel state, PromptStyleLibrary style)
        {
            float annoyance = state.GetStat("annoyance");
            float amusement = state.GetStat("amusement");
            float awkwardness = state.GetStat("awkwardness");

            if (annoyance >= 85) sb.AppendLine(PromptStyleLibrary.Pick(style.edgeAnnoyedMax));
            else if (annoyance >= 60) sb.AppendLine(PromptStyleLibrary.Pick(style.edgeAnnoyedHigh));
            else if (amusement >= 70) sb.AppendLine(PromptStyleLibrary.Pick(style.edgeAmusedHigh));
            else if (awkwardness >= 70) sb.AppendLine(PromptStyleLibrary.Pick(style.edgeAwkwardHigh));
        }

        /// Words, not numbers — with the intensity phrasing re-rolled per turn.
        private static string Verbalize(PromptStyleLibrary style, string adjective, float value, float min, float max)
        {
            float t = max > min ? (value - min) / (max - min) : 0f;
            var pool = t < 0.2f ? style.intensityNone
                : t < 0.4f ? style.intensityLow
                : t < 0.6f ? style.intensityMid
                : t < 0.8f ? style.intensityHigh
                : style.intensityMax;
            string template = PromptStyleLibrary.Pick(pool);
            return string.IsNullOrEmpty(template) ? adjective : template.Replace("{0}", adjective);
        }

        private static string Capitalize(string text) =>
            string.IsNullOrEmpty(text) ? text : char.ToUpper(text[0]) + text.Substring(1);

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

        // ---------------- sidekick ----------------

        public static string BuildSidekickSystemPrompt(ScenarioDefinition scenario, System.Random rng)
        {
            var style = Resolve(scenario.promptStyle);
            var sidekick = scenario.GetNpc(scenario.sidekickNpcId);
            var main = scenario.GetNpc(scenario.respondingNpcId);
            var sb = new StringBuilder();

            sb.AppendLine(Tokens(PromptStyleLibrary.Pick(style.roleIntros, rng), sidekick, scenario));
            sb.AppendLine();
            sb.AppendLine("WHO YOU ARE:");
            sb.AppendLine(sidekick.personality);
            sb.AppendLine();
            sb.AppendLine("THE SCENE:");
            sb.AppendLine(scenario.sceneDescription);
            sb.AppendLine();
            sb.AppendLine($"YOUR JOB: you are the side character. You speak RARELY, and when you do it is EXACTLY ONE short line — " +
                          $"often addressed to {main.displayName}, sometimes muttered to yourself. Never more than one sentence or two tiny ones. " +
                          "You never drive the scene; you punctuate it.");
            sb.AppendLine(Tokens(PromptStyleLibrary.Pick(style.weirdRules, rng), sidekick, scenario));

            if (sidekick.voiceExamples != null && sidekick.voiceExamples.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine(PromptStyleLibrary.Pick(style.voiceExampleHeaders, rng));
                foreach (var example in PromptStyleLibrary.Sample(sidekick.voiceExamples, style.voiceExampleCount, rng))
                {
                    sb.AppendLine($"\"{example}\"");
                }
            }

            sb.AppendLine();
            sb.AppendLine(Tokens(PromptStyleLibrary.Pick(style.outputRules, rng), sidekick, scenario));

            return sb.ToString();
        }

        public static string BuildSidekickReplyQuery(EventLog log, ScenarioDefinition scenario, string playerLine)
        {
            var style = Resolve(scenario.promptStyle);
            var sb = new StringBuilder();
            sb.AppendLine(PromptStyleLibrary.Pick(style.historyHeaders));
            sb.Append(log.ToTranscript());
            sb.AppendLine();
            sb.AppendLine($"{Capitalize(scenario.playerLabel)} just spoke directly TO YOU: \"{playerLine}\"");
            sb.Append("Answer them — one or two short lines, fully in character:");
            return sb.ToString();
        }

        public static string BuildSidekickQuery(EventLog log, SceneStateModel state, ScenarioDefinition scenario)
        {
            var style = Resolve(scenario.promptStyle);
            var sb = new StringBuilder();
            sb.AppendLine(PromptStyleLibrary.Pick(style.historyHeaders));
            sb.Append(log.ToTranscript());
            sb.AppendLine();
            sb.AppendLine("If you have ONE short line worth saying right now, say it.");
            // contract line: "..." means staying quiet — keep exact
            sb.Append("If you'd stay quiet (which is most of the time), reply with exactly: ...");
            return sb.ToString();
        }

        // ---------------- addressee arbitration ----------------

        public static string BuildAddresseeSystemPrompt()
        {
            return "You determine who a line of dialogue is addressed to in a scene. " +
                   "Consider the words first (names, titles, content only one character could answer), " +
                   "then where the speaker is LOOKING as physical evidence. " +
                   "Output ONLY one id from the offered list, or unclear.";
        }

        public static string BuildAddresseeQuery(EventLog log, string playerLine, string gazedActorId,
            System.Collections.Generic.IReadOnlyList<(string id, string name)> candidates)
        {
            var sb = new StringBuilder();
            sb.AppendLine("CHARACTERS PRESENT:");
            foreach (var (id, name) in candidates)
            {
                sb.AppendLine($"- {id}: {name}");
            }
            sb.AppendLine();
            if (!string.IsNullOrEmpty(gazedActorId))
            {
                var gazed = System.Linq.Enumerable.FirstOrDefault(candidates, c => c.id == gazedActorId);
                sb.AppendLine($"THE SPEAKER IS CURRENTLY LOOKING AT: {gazed.name ?? gazedActorId}");
            }
            else
            {
                sb.AppendLine("THE SPEAKER IS NOT LOOKING AT ANYONE IN PARTICULAR.");
            }
            sb.AppendLine();
            sb.AppendLine("RECENT LINES:");
            sb.Append(log.ToTranscript(8));
            sb.AppendLine();
            sb.AppendLine($"The speaker says: \"{playerLine}\"");
            sb.Append("Addressed to:");
            return sb.ToString();
        }

        // ---------------- judge ----------------

        public static string BuildJudgeSystemPrompt(ScenarioDefinition scenario, System.Random rng)
        {
            var style = Resolve(scenario.promptStyle);
            var npc = scenario.GetNpc(scenario.respondingNpcId);
            var sb = new StringBuilder();

            sb.AppendLine(PromptStyleLibrary.Pick(style.judgeIntros, rng));
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
            sb.AppendLine("- mood_changes: adjust the emotional meters based on what JUST happened. Positive = more of it. Small nudges (3-10) for normal beats, big ones (10-20) for dramatic beats. Only include meters that actually changed.");
            sb.AppendLine("- actions: 0 to 2 physical actions from the offered list that fit what just happened. Empty list is fine.");
            sb.AppendLine();
            sb.AppendLine($"NOTE: {npc.displayName} sometimes says odd, tangential things. That is normal for this world — never count the NPC's own weirdness for or against the player.");

            return sb.ToString();
        }

        public static string BuildJudgeQuery(EventLog log, SceneStateModel state,
            System.Collections.Generic.IReadOnlyList<ActionDefinition> availableActions)
        {
            var sb = new StringBuilder();
            if (state != null && state.Stats.Count > 0)
            {
                sb.Append("CURRENT METERS:");
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
