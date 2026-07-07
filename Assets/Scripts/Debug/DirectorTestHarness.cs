using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using TalkOut.Core;

namespace TalkOut.Debugging
{
    /// Runs a scripted corpus of player inputs through the REAL turn loop and
    /// logs every turn (raw director output, applied deltas, state, latency).
    /// Press F9 in play mode, or enable autoRunOnStart. Proves M1 (mock) and
    /// M2 (grammar: zero parse failures, zero out-of-catalog actions).
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
            var lines = corpus.text.Split('\n');
            int turn = 0;

            foreach (var rawLine in lines)
            {
                var input = rawLine.Trim();
                if (string.IsNullOrEmpty(input) || input.StartsWith("#")) continue;

                // Wait for the controller to accept input (or stop if scene ended).
                while (turnController.Phase != TurnPhase.AwaitingInput)
                {
                    if (turnController.Phase == TurnPhase.SceneOver) goto done;
                    await Task.Delay(100);
                    if (this == null) return;
                }

                turn++;
                float start = Time.realtimeSinceStartup;
                int historyBefore = turnController.History.Count;
                turnController.SubmitPlayerInput(input);

                while (turnController.Phase == TurnPhase.Thinking ||
                       turnController.Phase == TurnPhase.RunningActions)
                {
                    await Task.Delay(100);
                    if (this == null) return;
                }

                float elapsed = Time.realtimeSinceStartup - start;
                log.AppendLine($"--- turn {turn} ({elapsed:0.0}s) ---");
                log.AppendLine($"input: {input}");
                for (int i = historyBefore; i < turnController.History.Count; i++)
                {
                    var line = turnController.History[i];
                    log.AppendLine($"  [{line.kind}] {line.speaker}: {line.text}");
                }
                log.AppendLine($"fallback: {turnController.LastTurnWasFallback}");
                log.AppendLine($"state: {turnController.State.Snapshot()}");

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
