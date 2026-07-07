using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;
using TalkOut.Data;

namespace TalkOut.Save
{
    [Serializable]
    public class RunRecord
    {
        public int score;
        public float timeSeconds;
        public int turns;
        public string outcomeId;
        public string when; // yyyy-MM-dd HH:mm
    }

    [Serializable]
    public class ScenarioRecord
    {
        public string scenarioId;
        public int timesPlayed;
        public int timesWon;
        public Dictionary<string, int> outcomeCounts = new Dictionary<string, int>();
        public string bestOutcomeId;

        public float bestTimeSeconds; // fastest win; 0 = never won
        public float lastTimeSeconds;

        /// Winning runs only, sorted by score desc, capped at MaxRuns.
        public List<RunRecord> topRuns = new List<RunRecord>();

        public int BestScore => topRuns.Count > 0 ? topRuns[0].score : 0;
    }

    [Serializable]
    public class SaveData
    {
        public Dictionary<string, ScenarioRecord> scenarios = new Dictionary<string, ScenarioRecord>();

        public int TotalScore
        {
            get
            {
                int total = 0;
                foreach (var record in scenarios.Values) total += record.BestScore;
                return total;
            }
        }
    }

    /// Plain JSON save at persistentDataPath: outcomes, best times, leaderboards.
    public static class SaveSystem
    {
        public const int MaxRuns = 5;

        private static SaveData cached;

        private static string PathOnDisk =>
            Path.Combine(Application.persistentDataPath, "talkout_save.json");

        public static SaveData Load()
        {
            if (cached != null) return cached;
            try
            {
                if (File.Exists(PathOnDisk))
                {
                    cached = JsonConvert.DeserializeObject<SaveData>(File.ReadAllText(PathOnDisk));
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] Failed to load save: {e.Message}");
            }
            return cached ??= new SaveData();
        }

        /// Records a finished run. Returns true when this run is the new #1
        /// score for the scenario.
        public static bool RecordOutcome(
            string scenarioId, OutcomeRule outcome, float elapsedSeconds, int turns, int score)
        {
            var data = Load();
            if (!data.scenarios.TryGetValue(scenarioId, out var record))
            {
                record = new ScenarioRecord { scenarioId = scenarioId };
                data.scenarios[scenarioId] = record;
            }

            record.timesPlayed++;
            record.lastTimeSeconds = elapsedSeconds;
            record.outcomeCounts.TryGetValue(outcome.id, out int count);
            record.outcomeCounts[outcome.id] = count + 1;
            if (outcome.isWin || string.IsNullOrEmpty(record.bestOutcomeId))
            {
                record.bestOutcomeId = outcome.id;
            }

            bool newBest = false;
            if (outcome.isWin)
            {
                record.timesWon++;
                if (record.bestTimeSeconds <= 0f || elapsedSeconds < record.bestTimeSeconds)
                {
                    record.bestTimeSeconds = elapsedSeconds;
                }

                record.topRuns.Add(new RunRecord
                {
                    score = score,
                    timeSeconds = elapsedSeconds,
                    turns = turns,
                    outcomeId = outcome.id,
                    when = DateTime.Now.ToString("yyyy-MM-dd HH:mm")
                });
                record.topRuns.Sort((a, b) => b.score.CompareTo(a.score));
                if (record.topRuns.Count > MaxRuns)
                {
                    record.topRuns.RemoveRange(MaxRuns, record.topRuns.Count - MaxRuns);
                }
                newBest = record.topRuns[0].score == score && score > 0;
            }

            Write(data);
            return newBest;
        }

        private static void Write(SaveData data)
        {
            try
            {
                File.WriteAllText(PathOnDisk, JsonConvert.SerializeObject(data, Formatting.Indented));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] Failed to write save: {e.Message}");
            }
        }
    }
}
