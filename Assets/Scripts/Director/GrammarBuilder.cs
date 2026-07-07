using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TalkOut.Directing
{
    /// Generates a per-turn GBNF grammar for llama.cpp. The grammar makes invalid
    /// JSON and out-of-catalog actions STRUCTURALLY impossible, even from a tiny
    /// model: the only producible action tokens are this turn's offered ids, the
    /// only stat keys are the scenario's stats, and deltas are bounded to ±99.
    public static class GrammarBuilder
    {
        public static string Build(IEnumerable<string> availableActionIds, IEnumerable<string> statIds)
        {
            var actions = availableActionIds.Where(id => !string.IsNullOrEmpty(id)).ToList();
            var stats = statIds.Where(id => !string.IsNullOrEmpty(id)).ToList();

            var sb = new StringBuilder();

            sb.AppendLine("root ::= \"{\" ws \"\\\"npc_reply\\\":\" ws reply \",\" ws \"\\\"actions\\\":\" ws actionlist \",\" ws \"\\\"state_changes\\\":\" ws changes ws \"}\"");
            sb.AppendLine("reply ::= \"\\\"\" replychar{1,400} \"\\\"\"");
            sb.AppendLine("replychar ::= [^\"\\\\\\x00-\\x1F] | \"\\\\\" [\"\\\\bfnrt]");

            if (actions.Count > 0)
            {
                sb.AppendLine("actionlist ::= \"[\" ws \"]\" | \"[\" ws action (\",\" ws action){0,2} ws \"]\"");
                sb.AppendLine("action ::= " + string.Join(" | ", actions.Select(Quoted)));
            }
            else
            {
                sb.AppendLine("actionlist ::= \"[\" ws \"]\"");
            }

            if (stats.Count > 0)
            {
                sb.AppendLine("changes ::= \"{\" ws \"}\" | \"{\" ws change (\",\" ws change){0,3} ws \"}\"");
                sb.AppendLine("change ::= statkey ws \":\" ws delta");
                sb.AppendLine("statkey ::= " + string.Join(" | ", stats.Select(Quoted)));
                sb.AppendLine("delta ::= \"-\"? [1-9] [0-9]?");
            }
            else
            {
                sb.AppendLine("changes ::= \"{\" ws \"}\"");
            }

            sb.AppendLine("ws ::= [ \\t\\n]{0,4}");

            return sb.ToString();
        }

        /// GBNF literal for a JSON string token, e.g. "\"OfficerLaugh\""
        private static string Quoted(string id) => $"\"\\\"{id}\\\"\"";
    }
}
