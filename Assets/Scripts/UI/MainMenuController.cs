using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using TalkOut.Core;
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

            var save = SaveSystem.Load();
            root.Q<Label>("total-score").text =
                $"TOTAL SCORE  <color=#FFFFFF>{save.TotalScore:N0}</color>";

            var levelsHost = root.Q<VisualElement>("levels");
            foreach (var level in levels)
            {
                levelsHost.Add(BuildLevelCard(level, save));
            }

            WireSettings(root);

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

            var header = new VisualElement();
            header.AddToClassList("level-header");
            var left = new VisualElement();
            var name = new Label(level.title);
            name.AddToClassList("level-name");
            var desc = new Label(level.description);
            desc.AddToClassList("level-desc");
            left.Add(name);
            left.Add(desc);
            header.Add(left);

            save.scenarios.TryGetValue(level.scenarioId, out var record);
            var stats = new Label(record != null && record.timesPlayed > 0
                ? $"BEST {record.BestScore:N0}\n{record.timesWon}/{record.timesPlayed} wins"
                : "NEW");
            stats.AddToClassList("level-stats");
            header.Add(stats);
            card.Add(header);

            // local leaderboard: top runs
            if (record != null && record.topRuns.Count > 0)
            {
                for (int i = 0; i < record.topRuns.Count; i++)
                {
                    var run = record.topRuns[i];
                    var row = new VisualElement();
                    row.AddToClassList("run-row");
                    var rank = new Label($"#{i + 1}");
                    rank.AddToClassList("run-rank");
                    var detail = new Label($"{FormatTime(run.timeSeconds)}  ·  {run.turns} lines  ·  {run.when}");
                    detail.AddToClassList("run-detail");
                    var score = new Label($"{run.score:N0}");
                    score.AddToClassList("run-score");
                    row.Add(rank);
                    row.Add(detail);
                    row.Add(score);
                    card.Add(row);
                }
            }

            card.RegisterCallback<ClickEvent>(_ => SceneManager.LoadScene(level.sceneName));
            return card;
        }

        private void WireSettings(VisualElement root)
        {
            var overlay = root.Q<VisualElement>("settings-overlay");
            var music = root.Q<Slider>("music-slider");
            var sensitivity = root.Q<Slider>("sensitivity-slider");
            var voice = root.Q<Toggle>("voice-toggle");

            music.value = GameSettings.MusicVolume;
            sensitivity.value = GameSettings.MouseSensitivity;
            voice.value = GameSettings.VoiceEnabled;

            music.RegisterValueChangedCallback(evt => GameSettings.MusicVolume = evt.newValue);
            sensitivity.RegisterValueChangedCallback(evt => GameSettings.MouseSensitivity = evt.newValue);
            voice.RegisterValueChangedCallback(evt => GameSettings.VoiceEnabled = evt.newValue);

            root.Q<Button>("settings-button").clicked += () => overlay.style.display = DisplayStyle.Flex;
            root.Q<Button>("settings-close").clicked += () => overlay.style.display = DisplayStyle.None;
        }

        private static string FormatTime(float seconds)
        {
            int total = Mathf.FloorToInt(seconds);
            return $"{total / 60}:{total % 60:00}";
        }
    }
}
