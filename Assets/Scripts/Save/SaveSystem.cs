using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;
using TalkOut.Data;

namespace TalkOut.Save
{
    [Serializable]
    public class ScenarioRecord
    {
        public string scenarioId;
        public int timesPlayed;
        public int timesWon;
        public Dictionary<string, int> outcomeCounts = new Dictionary<string, int>();
        public string bestOutcomeId;
    }

    [Serializable]
    public class SaveData
    {
        public Dictionary<string, ScenarioRecord> scenarios = new Dictionary<string, ScenarioRecord>();
    }

    /// Plain JSON save at persistentDataPath. Records playthroughs and outcomes.
    public static class SaveSystem
    {
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

        public static void RecordOutcome(string scenarioId, OutcomeRule outcome)
        {
            var data = Load();
            if (!data.scenarios.TryGetValue(scenarioId, out var record))
            {
                record = new ScenarioRecord { scenarioId = scenarioId };
                data.scenarios[scenarioId] = record;
            }

            record.timesPlayed++;
            if (outcome.isWin) record.timesWon++;
            record.outcomeCounts.TryGetValue(outcome.id, out int count);
            record.outcomeCounts[outcome.id] = count + 1;
            if (outcome.isWin || string.IsNullOrEmpty(record.bestOutcomeId))
            {
                record.bestOutcomeId = outcome.id;
            }

            Write(data);
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
