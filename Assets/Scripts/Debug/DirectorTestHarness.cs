using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using TalkOut.Core;

namespace TalkOut.Debugging
{
    /// F9: runs the scripted corpus through the REAL loop (cop + judge) and
    /// logs each exchange, the verdicts, and latency. Proves the judge can't
    /// be prompt-injected into releasing the player.
    public class DirectorTestHarness : MonoBehaviour
    {
        public TurnController turnController;

        [Tooltip("One player input per line; lines starting with # are skipped")]
        public TextAsset corpus;

        public bool autoRunOnStart;
        public KeyCode runKey = KeyCode.F9;

        private bool running;

        private void Start()
        {
            if (autoRunOnStart) _ = RunAsync();
        }

        private void Update()
        {
            if (Input.GetKeyDown(runKey) && !running) _ = RunAsync();
        }

        private async Task RunAsync()
        {
            if (turnController == null || corpus == null)
            {
                Debug.LogWarning("[Harness] Missing turnController or corpus.");
                return;
            }

            running = true;
            var log = new StringBuilder();
            log.AppendLine($"=== TalkOut harness run {System.DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
            int turn = 0;

            foreach (var rawLine in corpus.text.Split('\n'))
            {
                var input = rawLine.Trim();
                if (string.IsNullOrEmpty(input) || input.StartsWith("#")) continue;

                while (turnController.Phase != TurnPhase.AwaitingInput)
                {
                    if (turnController.Phase == TurnPhase.SceneOver) goto done;
                    await Task.Delay(100);
                    if (this == null) return;
                }

                turn++;
                float start = Time.realtimeSinceStartup;
                int eventsBefore = turnController.Log.Events.Count;
                turnController.SubmitPlayerUtterance(input);

                // Wait for the full turn (cop + judge + actions) to finish.
                await Task.Delay(200);
                while (turnController.Phase != TurnPhase.AwaitingInput &&
                       turnController.Phase != TurnPhase.SceneOver)
                {
                    await Task.Delay(100);
                    if (this == null) return;
                }

                log.AppendLine($"--- turn {turn} ({Time.realtimeSinceStartup - start:0.0}s) ---");
                for (int i = eventsBefore; i < turnController.Log.Events.Count; i++)
                {
                    var e = turnController.Log.Events[i];
                    log.AppendLine($"  [{e.kind}] {e.actor}: {e.text}");
                }
                var verdict = turnController.LastVerdict;
                if (verdict != null)
                {
                    log.AppendLine($"  verdict: released={verdict.Released} arrested={verdict.Arrested} " +
                                   $"mood={verdict.CopMood} actions=[{string.Join(",", verdict.ActionIds)}] " +
                                   $"fallback={verdict.IsFallback}");
                }

                if (turnController.Phase == TurnPhase.SceneOver) break;
            }

            done:
            log.AppendLine($"=== finished: phase={turnController.Phase}, turns={turn} ===");
            string path = Path.Combine(Application.persistentDataPath,
                $"harness_{System.DateTime.Now:yyyyMMdd_HHmmss}.log");
            File.WriteAllText(path, log.ToString());
            Debug.Log($"[Harness] Done. Log written to {path}\n{log}");
            running = false;
        }
    }
}
