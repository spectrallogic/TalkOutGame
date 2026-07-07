using UnityEngine;
using TalkOut.Core;

namespace TalkOut.Debugging
{
    /// F1 toggles the hidden Referee state (stats, flags, locations, phase).
    /// Dev tool only — the shipped player infers emotion from behavior.
    public class DebugStatsOverlay : MonoBehaviour
    {
        public TurnController turnController;
        public KeyCode toggleKey = KeyCode.F1;

        private bool visible;

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey)) visible = !visible;
        }

        private void OnGUI()
        {
            if (!visible || turnController == null || turnController.State == null) return;

            var state = turnController.State;
            GUILayout.BeginArea(new Rect(10, 10, 320, 480), GUI.skin.box);
            GUILayout.Label($"<b>REFEREE STATE</b> (F1 to hide)", RichLabel());
            GUILayout.Label($"Phase: {turnController.Phase}   Turn: {turnController.PlayerTurnsTaken}/{turnController.Scenario.maxTurns}", RichLabel());
            if (turnController.LastTurnWasFallback)
            {
                GUILayout.Label("<color=orange>last turn: FALLBACK</color>", RichLabel());
            }
            GUILayout.Space(6);
            foreach (var kv in state.Stats)
            {
                var def = state.GetStatDefinition(kv.Key);
                GUILayout.Label($"{kv.Key}: {kv.Value:0} / {def.max:0}", RichLabel());
                GUILayout.HorizontalSlider(kv.Value, def.min, def.max);
            }
            GUILayout.Space(6);
            foreach (var kv in state.Flags)
            {
                GUILayout.Label($"{(kv.Value ? "☑" : "☐")} {kv.Key}", RichLabel());
            }
            GUILayout.Space(6);
            foreach (var kv in state.Locations)
            {
                GUILayout.Label($"{kv.Key} @ {kv.Value}", RichLabel());
            }
            GUILayout.EndArea();
        }

        private static GUIStyle RichLabel()
        {
            var style = new GUIStyle(GUI.skin.label) { richText = true };
            return style;
        }
    }
}
