using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TalkOut.Directing
{
    /// Semantic validation of director output. With grammar-constrained generation
    /// most of this is belt-and-suspenders, but the Referee trusts nothing:
    /// unknown actions dropped, max 3 actions, deltas clamped, contradictions resolved.
    public static class DirectorValidator
    {
        public const int MaxActions = 3;
        public const int MaxReplyLength = 500;

        public static DirectorResult Validate(
            string npcReply,
            IEnumerable<string> proposedActionIds,
            IReadOnlyDictionary<string, float> proposedStatChanges,
            DirectorRequest request,
            string rawOutput)
        {
            var result = new DirectorResult { RawOutput = rawOutput };

            // Reply: required, length-capped.
            if (string.IsNullOrWhiteSpace(npcReply))
            {
                return FallbackLibrary.GetFallback("empty npc_reply");
            }
            result.NpcReply = npcReply.Length > MaxReplyLength
                ? npcReply.Substring(0, MaxReplyLength) + "…"
                : npcReply.Trim();

            // Actions: must exist in this turn's offered list, dedupe, cap at 3,
            // and at most one scene-ending action (first wins).
            var offered = new HashSet<string>(request.AvailableActions.Select(a => a.id));
            bool sceneEndTaken = false;
            if (proposedActionIds != null)
            {
                foreach (var id in proposedActionIds)
                {
                    if (result.ActionIds.Count >= MaxActions) break;
                    if (string.IsNullOrEmpty(id) || result.ActionIds.Contains(id)) continue;
                    if (!offered.Contains(id))
                    {
                        Debug.LogWarning($"[Validator] Dropped out-of-catalog action '{id}'");
                        continue;
                    }
                    var def = request.Scenario.GetAction(id);
                    if (def != null && def.endsScene)
                    {
                        if (sceneEndTaken)
                        {
                            Debug.LogWarning($"[Validator] Dropped contradictory scene-ending action '{id}'");
                            continue;
                        }
                        sceneEndTaken = true;
                    }
                    result.ActionIds.Add(id);
                }
            }

            // Stat changes: known stats only, clamped to the scenario's per-turn limit.
            float limit = request.Scenario.maxStatDeltaPerTurn;
            if (proposedStatChanges != null)
            {
                foreach (var kv in proposedStatChanges)
                {
                    if (!request.State.HasStat(kv.Key))
                    {
                        Debug.LogWarning($"[Validator] Dropped delta for unknown stat '{kv.Key}'");
                        continue;
                    }
                    result.StatChanges[kv.Key] = Mathf.Clamp(kv.Value, -limit, limit);
                }
            }

            return result;
        }
    }
}
