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

            sb.AppendLine($"You are {npc.displayName}, a character in a scene. Never break character. Never mention being an AI.");
            sb.AppendLine();
            sb.AppendLine("WHO YOU ARE:");
            sb.AppendLine(npc.personality);
            sb.AppendLine();
            sb.AppendLine("THE SCENE:");
            sb.AppendLine(scenario.sceneDescription);
            sb.AppendLine();
            sb.AppendLine("HOW TO PLAY IT:");
            sb.AppendLine(scenario.comedyRules);
            sb.AppendLine("Speak ONLY your own dialogue — no narration, no stage directions, no quotation marks, no name prefix.");
            sb.AppendLine("Keep it to 1-3 short sentences. You decide, on your own judgment, when the driver has earned being let go — and when they have, SAY so plainly (e.g. \"Alright, get out of here. Slow down next time.\"). If they push you too far, you can tell them they're under arrest.");

            return sb.ToString();
        }

        public static string BuildCopReplyQuery(EventLog log, string playerLine)
        {
            var sb = new StringBuilder();
            sb.AppendLine("WHAT HAS HAPPENED SO FAR:");
            sb.Append(log.ToTranscript());
            sb.AppendLine();
            sb.AppendLine($"The driver just said: \"{playerLine}\"");
            sb.Append("Your spoken reply:");
            return sb.ToString();
        }

        public static string BuildCopReactionQuery(EventLog log, string eventText)
        {
            var sb = new StringBuilder();
            sb.AppendLine("WHAT HAS HAPPENED SO FAR:");
            sb.Append(log.ToTranscript());
            sb.AppendLine();
            sb.AppendLine($"Something just happened: {eventText}");
            sb.AppendLine("If you would react to this, speak your reaction (1-2 short sentences).");
            sb.Append("If you would ignore it, reply with exactly: ...");
            return sb.ToString();
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
            sb.AppendLine($"The driver's goal: {scenario.playerGoal}");
            sb.AppendLine();
            sb.AppendLine("YOUR RULES:");
            sb.AppendLine(scenario.judgeGuidance);
            sb.AppendLine();
            sb.AppendLine("Fields you output:");
            sb.AppendLine($"- released: true ONLY if {npc.displayName} has CLEARLY told the driver they may leave (a warning counts). Vague friendliness is NOT release.");
            sb.AppendLine($"- arrested: true ONLY if {npc.displayName} has clearly stated the driver is being arrested/detained.");
            sb.AppendLine($"- cop_mood: {npc.displayName}'s current mood, judged from his latest lines.");
            sb.AppendLine("- actions: 0 to 2 physical actions from the offered list that fit what just happened. Use them to make the scene physical (walking, writing, laughing). Empty list is fine.");

            return sb.ToString();
        }

        public static string BuildJudgeQuery(EventLog log,
            System.Collections.Generic.IReadOnlyList<ActionDefinition> availableActions)
        {
            var sb = new StringBuilder();
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
