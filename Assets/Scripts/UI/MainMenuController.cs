using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using TalkOut.Core;
using TalkOut.Data;
using TalkOut.Save;

namespace TalkOut.UI
{
    /// Menu v4: home (PLAY / settings / quit) -> level-select grid + PLAY ALL.
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

        private VisualElement homePanel;
        private VisualElement selectPanel;

        private void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            homePanel = root.Q<VisualElement>("home-panel");
            selectPanel = root.Q<VisualElement>("select-panel");

            root.Q<Button>("play-button").clicked += () => ShowSelect(true);
            root.Q<Button>("back-button").clicked += () => ShowSelect(false);
            root.Q<Button>("quit-button").clicked += Application.Quit;

            var save = SaveSystem.Load();
            root.Q<Label>("total-score").text =
                $"TOTAL SCORE  <color=#FFFFFF>{save.TotalScore:N0}</color>";

            var grid = root.Q<VisualElement>("level-grid");
            for (int i = 0; i < levels.Count; i++)
            {
                grid.Add(BuildGridCard(i, levels[i], save));
            }

            root.Q<VisualElement>("playall-card").RegisterCallback<ClickEvent>(_ =>
                PlaylistManager.StartAll(levels.Select(l => l.sceneName).ToArray()));

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

        private void ShowSelect(bool show)
        {
            homePanel.style.display = show ? DisplayStyle.None : DisplayStyle.Flex;
            selectPanel.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private VisualElement BuildGridCard(int index, LevelEntry level, SaveData save)
        {
            var card = new VisualElement();
            card.AddToClassList("grid-card");

            var number = new Label($"LEVEL {index + 1}");
            number.AddToClassList("grid-number");
            card.Add(number);

            var title = new Label(level.title);
            title.AddToClassList("grid-title");
            card.Add(title);

            var desc = new Label(level.description);
            desc.AddToClassList("grid-desc");
            card.Add(desc);

            save.scenarios.TryGetValue(level.scenarioId, out var record);
            string statsText = "— not attempted —";
            if (record != null && record.timesPlayed > 0)
            {
                statsText = record.BestScore > 0
                    ? $"BEST {record.BestScore:N0}  ·  ⏱ {FormatTime(record.bestTimeSeconds)}  ·  {record.timesWon}/{record.timesPlayed} wins"
                    : $"{record.timesWon}/{record.timesPlayed} wins";
            }
            var stats = new Label(statsText);
            stats.AddToClassList("grid-stats");
            card.Add(stats);

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
