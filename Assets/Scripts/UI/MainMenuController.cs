using System;
using System.Collections.Generic;
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
        [Serializable]
        public struct LevelEntry
        {
            public string sceneName;
            public string scenarioId;
            public string title;
            public string description;
        }

        public LlmConfig llmConfig;
        public List<LevelEntry> levels = new List<LevelEntry>();

        private void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            root.Q<Button>("quit-button").clicked += Application.Quit;

            var levelsHost = root.Q<VisualElement>("levels");
            var save = SaveSystem.Load();
            foreach (var level in levels)
            {
                levelsHost.Add(BuildLevelCard(level, save));
            }

            var modelStatus = root.Q<Label>("model-status");
            if (llmConfig != null)
            {
                bool found = File.Exists(llmConfig.ResolveModelPath());
                modelStatus.text = found
                    ? $"Brain loaded: {llmConfig.modelFileName} ✓"
                    : "Brain missing — see StreamingAssets/Models/README.md (canned lines only)";
                modelStatus.style.color = found
                    ? new StyleColor(new Color(0.47f, 0.75f, 0.55f))
                    : new StyleColor(new Color(0.9f, 0.6f, 0.3f));
            }
        }

        private VisualElement BuildLevelCard(LevelEntry level, SaveData save)
        {
            var card = new VisualElement();
            card.AddToClassList("level-card");

            var left = new VisualElement();
            var name = new Label(level.title);
            name.AddToClassList("level-name");
            var desc = new Label(level.description);
            desc.AddToClassList("level-desc");
            left.Add(name);
            left.Add(desc);
            card.Add(left);

            var stats = new Label(StatsFor(level.scenarioId, save));
            stats.AddToClassList("level-stats");
            card.Add(stats);

            card.RegisterCallback<ClickEvent>(_ => SceneManager.LoadScene(level.sceneName));
            return card;
        }

        private static string StatsFor(string scenarioId, SaveData save)
        {
            if (save.scenarios.TryGetValue(scenarioId, out var record) && record.timesPlayed > 0)
            {
                string best = record.bestTimeSeconds > 0f
                    ? $"⏱ best {(int)record.bestTimeSeconds / 60}:{(int)record.bestTimeSeconds % 60:00}"
                    : "no win yet";
                return $"{record.timesWon}/{record.timesPlayed} wins\n{best}";
            }
            return "NEW";
        }
    }
}
