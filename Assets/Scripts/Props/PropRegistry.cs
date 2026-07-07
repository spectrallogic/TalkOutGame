using System.Collections.Generic;
using UnityEngine;
using TalkOut.Data;

namespace TalkOut.Props
{
    /// Collects the scene's props and validates that every action's TargetPropId
    /// actually exists — loudly, at startup, so broken data can't ship quietly.
    public class PropRegistry : MonoBehaviour
    {
        private readonly Dictionary<string, SceneProp> props = new Dictionary<string, SceneProp>();

        private void Awake()
        {
            foreach (var prop in FindObjectsOfType<SceneProp>(includeInactive: true))
            {
                if (prop.definition == null || string.IsNullOrEmpty(prop.definition.id))
                {
                    Debug.LogWarning($"[PropRegistry] SceneProp '{prop.name}' has no definition/id.", prop);
                    continue;
                }
                props[prop.definition.id] = prop;
            }
        }

        public SceneProp Get(string id)
        {
            return !string.IsNullOrEmpty(id) && props.TryGetValue(id, out var prop) ? prop : null;
        }

        public void ValidateCatalog(ScenarioDefinition scenario)
        {
            foreach (var action in scenario.actionCatalog)
            {
                if (action != null && !string.IsNullOrEmpty(action.targetPropId) && Get(action.targetPropId) == null)
                {
                    Debug.LogError($"[PropRegistry] Action '{action.id}' targets missing prop '{action.targetPropId}'.");
                }
            }
        }
    }
}
