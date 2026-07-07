using System.Text;
using TalkOut.Core;

namespace TalkOut.Directing
{
    /// Assembles the director prompt. The static system prompt (scene, characters,
    /// comedy rules, format example) stays byte-identical between turns so
    /// llama.cpp's prompt cache skips re-evaluating it; only the turn block varies.
    public static class PromptBuilder
    {
        public static string BuildSystemPrompt(DirectorRequest request)
        {
            var scenario = request.Scenario;
            var sb = new StringBuilder();

            sb.AppendLine("You are the comedy director of an interactive scene in a video game.");
            sb.AppendLine("You voice the NPCs and pick physical actions for them. You NEVER decide whether the player wins or loses — the game engine does that. You may ONLY pick actions from the list offered each turn.");
            sb.AppendLine();
            sb.AppendLine("SCENE:");
            sb.AppendLine(scenario.sceneDescription);
            sb.AppendLine($"The player's goal: {scenario.playerGoal}");
            sb.AppendLine();
            sb.AppendLine("CHARACTERS:");
            foreach (var npc in scenario.npcs)
            {
                if (npc != null) sb.AppendLine(npc.BuildPromptSheet());
            }
            sb.AppendLine();
            if (scenario.props.Count > 0)
            {
                sb.AppendLine("OBJECTS IN THE SCENE:");
                foreach (var prop in scenario.props)
                {
                    if (prop != null) sb.AppendLine($"- {prop.llmDescription}");
                }
                sb.AppendLine();
            }
            sb.AppendLine("COMEDY RULES:");
            sb.AppendLine(scenario.comedyRules);
            sb.AppendLine();
            sb.AppendLine("OUTPUT FORMAT — reply with EXACTLY this JSON shape and nothing else:");
            sb.AppendLine("{\"npc_reply\": \"what the NPC says, 1-2 sentences, in character\", \"actions\": [\"ActionIdFromTheOfferedList\"], \"state_changes\": {\"statname\": -10}}");
            sb.AppendLine();
            sb.AppendLine("EXAMPLE TURN:");
            sb.AppendLine("Player says: \"My friend was driving.\" (the friend is in the passenger seat)");
            sb.AppendLine("You reply:");
            sb.AppendLine("{\"npc_reply\": \"Your friend was driving? From the passenger seat?\", \"actions\": [\"OfficerWalkToPassengerWindow\", \"PassengerPanic\"], \"state_changes\": {\"suspicion\": 10, \"amusement\": 5}}");

            return sb.ToString();
        }

        public static string BuildTurnBlock(DirectorRequest request)
        {
            var sb = new StringBuilder();

            sb.AppendLine("CURRENT MOOD (secret — never mention numbers or these words directly):");
            foreach (var kv in request.State.Stats)
            {
                var def = request.State.GetStatDefinition(kv.Key);
                string adjective = string.IsNullOrEmpty(def.adjective) ? kv.Key : def.adjective;
                sb.AppendLine($"- {Verbalize(adjective, kv.Value, def.min, def.max)}");
            }
            sb.AppendLine();

            sb.AppendLine("ACTIONS YOU MAY PICK THIS TURN (0 to 3 of these ids, nothing else):");
            foreach (var action in request.AvailableActions)
            {
                sb.AppendLine($"- {action.id}: {action.llmDescription}");
            }
            sb.AppendLine();

            if (request.HistoryWindow != null && request.HistoryWindow.Count > 0)
            {
                sb.AppendLine("RECENT CONVERSATION:");
                foreach (var line in request.HistoryWindow)
                {
                    string who = line.kind == LineKind.Player ? "Player" : line.speaker;
                    sb.AppendLine($"{who}: {line.text}");
                }
                sb.AppendLine();
            }

            string npcName = request.RespondingNpc != null ? request.RespondingNpc.displayName : "the NPC";
            sb.AppendLine($"Player says: \"{request.PlayerInput}\"");
            sb.Append($"Respond as {npcName}. JSON only.");

            return sb.ToString();
        }

        /// Small models handle words better than floats — and words leak less.
        private static string Verbalize(string adjective, float value, float min, float max)
        {
            float t = max > min ? (value - min) / (max - min) : 0f;
            if (t < 0.2f) return $"not {adjective} at all";
            if (t < 0.4f) return $"slightly {adjective}";
            if (t < 0.6f) return $"moderately {adjective}";
            if (t < 0.8f) return $"very {adjective}";
            return $"extremely {adjective}";
        }
    }
}
