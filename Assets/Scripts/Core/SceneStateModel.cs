using System.Collections.Generic;
using System.Text;
using UnityEngine;
using TalkOut.Data;

namespace TalkOut.Core
{
    /// Authoritative world state, owned by Unity. The LLM only ever proposes
    /// stat deltas (clamped); flags and locations mutate exclusively through
    /// ActionDefinition engine effects executed by the TurnController.
    public class SceneStateModel
    {
        private readonly Dictionary<string, float> stats = new Dictionary<string, float>();
        private readonly Dictionary<string, bool> flags = new Dictionary<string, bool>();
        private readonly Dictionary<string, string> locations = new Dictionary<string, string>();
        private readonly Dictionary<string, StatDefinition> statDefs = new Dictionary<string, StatDefinition>();

        public SceneStateModel(ScenarioDefinition scenario)
        {
            foreach (var def in scenario.stats)
            {
                statDefs[def.id] = def;
                stats[def.id] = def.initial;
            }
            foreach (var flag in scenario.flags)
            {
                flags[flag.id] = flag.initial;
            }
            foreach (var loc in scenario.initialLocations)
            {
                locations[loc.actorId] = loc.locationId;
            }
        }

        public IReadOnlyDictionary<string, float> Stats => stats;
        public IReadOnlyDictionary<string, bool> Flags => flags;
        public IReadOnlyDictionary<string, string> Locations => locations;

        public bool HasStat(string id) => stats.ContainsKey(id);
        public float GetStat(string id) => stats.TryGetValue(id, out var v) ? v : 0f;
        public bool GetFlag(string id) => flags.TryGetValue(id, out var v) && v;
        public string GetLocation(string actorId) => locations.TryGetValue(actorId, out var v) ? v : "";

        public StatDefinition GetStatDefinition(string id) =>
            statDefs.TryGetValue(id, out var def) ? def : default;

        public void ApplyStatDelta(string id, float delta)
        {
            if (!statDefs.TryGetValue(id, out var def))
            {
                Debug.LogWarning($"[SceneState] Ignoring delta for unknown stat '{id}'");
                return;
            }
            stats[id] = Mathf.Clamp(stats[id] + delta, def.min, def.max);
        }

        public void SetFlag(string id, bool value)
        {
            flags[id] = value;
        }

        public void SetLocation(string actorId, string locationId)
        {
            locations[actorId] = locationId;
        }

        public string Snapshot()
        {
            var sb = new StringBuilder();
            foreach (var kv in stats) sb.Append($"{kv.Key}={kv.Value:0} ");
            foreach (var kv in flags) if (kv.Value) sb.Append($"[{kv.Key}] ");
            foreach (var kv in locations) sb.Append($"{kv.Key}@{kv.Value} ");
            return sb.ToString().TrimEnd();
        }
    }
}
