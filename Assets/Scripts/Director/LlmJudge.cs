using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json.Linq;
using LLMUnity;
using TalkOut.Core;
using TalkOut.Data;

namespace TalkOut.Directing
{
    /// The referee LLM: rules on release/arrest, sets the cop's mood, and picks
    /// physical actions — grammar-constrained so its JSON can never be invalid
    /// and it can never invent actions.
    public class LlmJudge : MonoBehaviour, IJudge
    {
        public LLMAgent agent;

        private LlmConfig config;

        public void Configure(ScenarioDefinition scenario, LlmConfig llmConfig)
        {
            config = llmConfig;
            agent.systemPrompt = PromptBuilder.BuildJudgeSystemPrompt(scenario);
            agent.temperature = 0.3f;  // the referee should be consistent, not creative
            agent.numPredict = 80;
            agent.cachePrompt = true;
        }

        public async Task<JudgeVerdict> JudgeAsync(EventLog log,
            IReadOnlyList<ActionDefinition> availableActions, CancellationToken ct)
        {
            try
            {
                Task ready = agent.llm.WaitUntilReady();
                if (await Task.WhenAny(ready, Task.Delay(TimeSpan.FromSeconds(60), ct)) != ready)
                {
                    return FallbackLibrary.GetVerdict("LLM never ready");
                }
                if (agent.llm.failed) return FallbackLibrary.GetVerdict("LLM failed to start");

                agent.SetGrammar(GrammarBuilder.BuildJudgeGrammar(availableActions.Select(a => a.id)));
                string query = PromptBuilder.BuildJudgeQuery(log, availableActions);

                Task<string> chat = agent.Chat(query, null, null, addToHistory: false);
                float timeout = config != null ? config.timeoutSeconds : 45f;
                if (await Task.WhenAny(chat, Task.Delay(TimeSpan.FromSeconds(timeout), ct)) != chat)
                {
                    ct.ThrowIfCancellationRequested();
                    return FallbackLibrary.GetVerdict("judge timeout");
                }

                return Parse(await chat, availableActions);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                Debug.LogException(e);
                return FallbackLibrary.GetVerdict(e.GetType().Name);
            }
        }

        private static JudgeVerdict Parse(string raw, IReadOnlyList<ActionDefinition> availableActions)
        {
            if (string.IsNullOrWhiteSpace(raw)) return FallbackLibrary.GetVerdict("empty judge output");
            try
            {
                var json = JObject.Parse(raw);
                var verdict = new JudgeVerdict
                {
                    Released = json.Value<bool?>("released") ?? false,
                    Arrested = json.Value<bool?>("arrested") ?? false,
                    CopMood = json.Value<string>("cop_mood") ?? "neutral",
                    RawOutput = raw
                };

                if (!JudgeVerdict.Moods.Contains(verdict.CopMood)) verdict.CopMood = "neutral";

                var offered = new HashSet<string>(availableActions.Select(a => a.id));
                if (json["actions"] is JArray actions)
                {
                    foreach (var token in actions.Values<string>())
                    {
                        if (verdict.ActionIds.Count >= 2) break;
                        if (!string.IsNullOrEmpty(token) && offered.Contains(token) &&
                            !verdict.ActionIds.Contains(token))
                        {
                            verdict.ActionIds.Add(token);
                        }
                    }
                }

                // Contradiction: release wins (comedy over cruelty).
                if (verdict.Released && verdict.Arrested) verdict.Arrested = false;
                return verdict;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Judge] Parse failed despite grammar: {e.Message}\nRaw: {raw}");
                return FallbackLibrary.GetVerdict("parse error");
            }
        }
    }
}
