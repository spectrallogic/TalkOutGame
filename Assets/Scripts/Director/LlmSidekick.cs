using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using LLMUnity;
using TalkOut.Core;
using TalkOut.Data;

namespace TalkOut.Directing
{
    /// The side character's tiny freeform voice — one short interjection,
    /// or silence. Shares the LLM server on its own agent/slot.
    public class LlmSidekick : MonoBehaviour, ISidekick
    {
        public LLMAgent agent;

        private LlmConfig config;
        private ScenarioDefinition scenarioData;
        private string sidekickName = "";

        public void Configure(ScenarioDefinition scenario, LlmConfig llmConfig)
        {
            config = llmConfig;
            scenarioData = scenario;
            var npc = scenario.GetNpc(scenario.sidekickNpcId);
            if (npc != null) sidekickName = npc.displayName;

            var sceneRng = new System.Random(unchecked(Environment.TickCount * 13 + scenario.scenarioId.GetHashCode()));
            agent.systemPrompt = PromptBuilder.BuildSidekickSystemPrompt(scenario, sceneRng);
            agent.temperature = 0.95f;
            agent.numPredict = 45; // one short line
            agent.cachePrompt = true;
        }

        public Task<string> InterjectAsync(EventLog log, SceneStateModel state, CancellationToken ct)
        {
            return RunAsync(PromptBuilder.BuildSidekickQuery(log, state, scenarioData), 160, ct);
        }

        public Task<string> ReplyAsync(EventLog log, SceneStateModel state, string playerLine, CancellationToken ct)
        {
            return RunAsync(PromptBuilder.BuildSidekickReplyQuery(log, scenarioData, playerLine), 240, ct);
        }

        private async Task<string> RunAsync(string query, int maxLength, CancellationToken ct)
        {
            try
            {
                Task ready = agent.llm.WaitUntilReady();
                if (await Task.WhenAny(ready, Task.Delay(TimeSpan.FromSeconds(30), ct)) != ready) return "";
                if (agent.llm.failed) return "";

                Task<string> chat = agent.Chat(query, null, null, addToHistory: false);
                float timeout = config != null ? config.timeoutSeconds : 45f;
                if (await Task.WhenAny(chat, Task.Delay(TimeSpan.FromSeconds(timeout), ct)) != chat)
                {
                    ct.ThrowIfCancellationRequested();
                    return "";
                }

                string raw = (await chat ?? "").Trim();
                raw = Regex.Replace(raw, @"^\s*(" + Regex.Escape(sidekickName) + @")\s*:\s*", "", RegexOptions.IgnoreCase);
                raw = Regex.Replace(raw, @"\*[^*]*\*|\([^)]*\)", "").Trim().Trim('"').Trim();
                if (raw.Replace(".", "").Trim().Length == 0) return ""; // "..." = staying quiet
                if (raw.Length > maxLength) raw = raw.Substring(0, maxLength).TrimEnd() + "…";
                return raw;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                Debug.LogWarning($"[Sidekick] Call failed: {e.Message}");
                return "";
            }
        }
    }
}
