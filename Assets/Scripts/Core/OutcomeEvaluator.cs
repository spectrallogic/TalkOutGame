using System.Collections.Generic;
using System.Linq;
using TalkOut.Data;

namespace TalkOut.Core
{
    /// Decides scene endings from state thresholds. Code and data only — the LLM
    /// has no vote here beyond having nudged the stats.
    public static class OutcomeEvaluator
    {
        /// Returns the ended outcome, or null if the scene continues.
        /// forcedOutcomeId comes from an endsScene action (e.g. EndSceneWin).
        public static OutcomeRule Evaluate(
            ScenarioDefinition scenario,
            SceneStateModel state,
            int playerTurnsTaken,
            string forcedOutcomeId = null)
        {
            if (!string.IsNullOrEmpty(forcedOutcomeId))
            {
                var forced = scenario.GetOutcome(forcedOutcomeId);
                if (forced != null) return forced;
            }

            IEnumerable<OutcomeRule> byPriority = scenario.outcomes
                .Where(o => o != null)
                .OrderByDescending(o => o.priority);

            foreach (var rule in byPriority)
            {
                if (rule.Matches(state)) return rule;
            }

            if (playerTurnsTaken >= scenario.maxTurns)
            {
                return scenario.GetOutcome(scenario.maxTurnsOutcomeId);
            }

            return null;
        }
    }
}
