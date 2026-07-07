using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TalkOut.Directing
{
    /// GBNF grammar for the JUDGE's structured ruling. (The cop speaks freely —
    /// no grammar — that's what makes him feel alive.) The grammar makes invalid
    /// JSON, unknown moods, and out-of-catalog actions structurally impossible.
    public static class GrammarBuilder
    {
        public static string BuildJudgeGrammar(IEnumerable<string> availableActionIds, IEnumerable<string> statIds)
        {
            var actions = availableActionIds.Where(id => !string.IsNullOrEmpty(id)).ToList();
            var stats = statIds?.Where(id => !string.IsNullOrEmpty(id)).ToList() ?? new List<string>();
            var sb = new StringBuilder();

            sb.AppendLine("root ::= \"{\" ws \"\\\"released\\\":\" ws boolean \",\" ws \"\\\"arrested\\\":\" ws boolean \",\" ws \"\\\"cop_mood\\\":\" ws mood \",\" ws \"\\\"mood_changes\\\":\" ws changes \",\" ws \"\\\"actions\\\":\" ws actionlist ws \"}\"");
            sb.AppendLine("boolean ::= \"true\" | \"false\"");
            sb.AppendLine("mood ::= " + string.Join(" | ", JudgeVerdict.Moods.Select(Quoted)));

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

            if (actions.Count > 0)
            {
                sb.AppendLine("actionlist ::= \"[\" ws \"]\" | \"[\" ws action (\",\" ws action)? ws \"]\"");
                sb.AppendLine("action ::= " + string.Join(" | ", actions.Select(Quoted)));
            }
            else
            {
                sb.AppendLine("actionlist ::= \"[\" ws \"]\"");
            }

            sb.AppendLine("ws ::= [ \\t\\n]{0,4}");
            return sb.ToString();
        }

        /// GBNF literal for a JSON string token, e.g. "\"amused\""
        private static string Quoted(string id) => $"\"\\\"{id}\\\"\"";
    }
}
