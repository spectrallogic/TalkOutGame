using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using TalkOut.Data;
using TalkOut.Save;

namespace TalkOut.UI
{
    [RequireComponent(typeof(UIDocument))]
    public class MainMenuController : MonoBehaviour
    {
        public LlmConfig llmConfig;
        public string playSceneName = "TrafficStop";
        public string scenarioIdForStats = "traffic_stop";

        private void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;

            root.Q<Button>("play-button").clicked += () => SceneManager.LoadScene(playSceneName);
            root.Q<Button>("quit-button").clicked += Application.Quit;

            var stats = root.Q<Label>("stats-label");
            var save = SaveSystem.Load();
            if (save.scenarios.TryGetValue(scenarioIdForStats, out var record))
            {
                stats.text = $"Traffic stops: {record.timesPlayed}   Talked out of: {record.timesWon}   " +
                             $"Best outcome: {record.bestOutcomeId ?? "none yet"}";
            }
            else
            {
                stats.text = "No traffic stops survived yet.";
            }

            var modelStatus = root.Q<Label>("model-status");
            if (llmConfig != null)
            {
                bool found = File.Exists(llmConfig.ResolveModelPath());
                modelStatus.text = found
                    ? $"Brain: {llmConfig.modelFileName} ✓"
                    : "Brain missing — see StreamingAssets/Models/README.md (game will use canned lines)";
                modelStatus.style.color = found
                    ? new StyleColor(new Color(0.47f, 0.75f, 0.55f))
                    : new StyleColor(new Color(0.85f, 0.6f, 0.3f));
            }
        }
    }
}
