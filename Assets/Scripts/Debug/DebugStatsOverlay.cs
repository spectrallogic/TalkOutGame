using UnityEngine;
using TalkOut.Core;

namespace TalkOut.Debugging
{
    /// F1 shows the Referee's internals: phase, turn count, the judge's last
    /// ruling, flags, and the transcript tail both LLMs actually see.
    public class DebugStatsOverlay : MonoBehaviour
    {
        public TurnController turnController;
        public KeyCode toggleKey = KeyCode.F1;

        private bool visible;
        private Vector2 scroll;

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey)) visible = !visible;
        }

        private void OnGUI()
        {
            if (!visible || turnController == null || turnController.Log == null) return;

            GUILayout.BeginArea(new Rect(10, 10, 420, 560), GUI.skin.box);
            GUILayout.Label("<b>REFEREE STATE</b> (F1 to hide)", Rich());
            GUILayout.Label($"Phase: {turnController.Phase}   Turn: {turnController.PlayerTurnsTaken}/{turnController.Scenario.maxTurns}", Rich());

            var verdict = turnController.LastVerdict;
            if (verdict != null)
            {
                GUILayout.Label($"Last verdict: released={verdict.Released} arrested={verdict.Arrested} mood={verdict.CopMood}" +
                                (verdict.IsFallback ? " <color=orange>(FALLBACK)</color>" : ""), Rich());
                if (verdict.ActionIds.Count > 0)
                {
                    GUILayout.Label($"Actions: {string.Join(", ", verdict.ActionIds)}", Rich());
                }
            }

            if (turnController.State != null)
            {
                GUILayout.Label($"Flags/locations: {turnController.State.Snapshot()}", Rich());
            }

            GUILayout.Space(6);
            GUILayout.Label("<b>TRANSCRIPT (what the LLMs see)</b>", Rich());
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(380));
            GUILayout.Label(turnController.Log.ToTranscript(40), Rich());
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private static GUIStyle Rich() => new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true };
    }
}
