using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json.Linq;
using LLMUnity;
using TalkOut.Data;
using Debug = UnityEngine.Debug;

namespace TalkOut.Directing
{
    /// The real director: local llama.cpp inference via LLMUnity with a per-turn
    /// GBNF grammar, so parse failures and hallucinated actions are structurally
    /// impossible. Any other failure (timeout, model missing) degrades to an
    /// in-fiction fallback line — a turn always completes.
    [RequireComponent(typeof(LLMAgent))]
    public class LlmDirector : MonoBehaviour, IDirector
    {
        [Tooltip("Auto-found on this GameObject if left empty")]
        public LLMAgent agent;

        private LlmConfig config;
        private bool systemPromptSet;

        private static readonly Regex PartialReplyRegex = new Regex(
            "\"npc_reply\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)", RegexOptions.Compiled);

        public void Configure(LlmConfig llmConfig)
        {
            config = llmConfig;
            if (agent == null) agent = GetComponent<LLMAgent>();
            if (config == null) return;

            agent.temperature = config.temperature;
            agent.numPredict = config.maxReplyTokens;
            agent.cachePrompt = true;

            string modelPath = config.ResolveModelPath();
            if (!File.Exists(modelPath))
            {
                Debug.LogError($"[LlmDirector] Model file not found: {modelPath}. " +
                               "See Assets/StreamingAssets/Models/README.md — all turns will use fallback lines.");
            }
        }

        public async Task WarmupAsync(DirectorRequest contextRequest)
        {
            if (config != null && !config.warmupOnLoad) return;
            try
            {
                EnsureSystemPrompt(contextRequest);
                await agent.llm.WaitUntilReady();
                await agent.Warmup();
                Debug.Log("[LlmDirector] Warmup complete.");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LlmDirector] Warmup failed (will retry per turn): {e.Message}");
            }
        }

        public async Task<DirectorResult> DirectAsync(
            DirectorRequest request,
            Action<string> onPartialReply,
            CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                if (agent == null || agent.llm == null)
                {
                    return FallbackLibrary.GetFallback("LLMAgent not wired");
                }

                EnsureSystemPrompt(request);
                Task ready = agent.llm.WaitUntilReady();
                if (await Task.WhenAny(ready, Task.Delay(TimeSpan.FromSeconds(60), cancellationToken)) != ready)
                {
                    return FallbackLibrary.GetFallback("LLM server never became ready");
                }
                if (agent.llm.failed)
                {
                    return FallbackLibrary.GetFallback("LLM server failed to start");
                }

                string grammar = GrammarBuilder.Build(
                    request.AvailableActions.Select(a => a.id),
                    request.State.Stats.Keys);
                agent.SetGrammar(grammar);

                string turnBlock = PromptBuilder.BuildTurnBlock(request);

                // Stream: LLMUnity's callback delivers accumulated output; we lift
                // out the growing npc_reply string (it's the first JSON field).
                Action<string> streamHandler = accumulated =>
                {
                    if (onPartialReply == null || string.IsNullOrEmpty(accumulated)) return;
                    var match = PartialReplyRegex.Match(accumulated);
                    if (match.Success)
                    {
                        onPartialReply(UnescapeJsonFragment(match.Groups[1].Value));
                    }
                };

                // Our engine owns conversation memory (windowed, inside the turn
                // block) — never LLMUnity's internal history.
                Task<string> chatTask = agent.Chat(turnBlock, streamHandler, null, addToHistory: false);

                float timeoutSeconds = config != null ? config.timeoutSeconds : 45f;
                Task finished = await Task.WhenAny(
                    chatTask, Task.Delay(TimeSpan.FromSeconds(timeoutSeconds), cancellationToken));
                if (finished != chatTask)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Debug.LogWarning($"[LlmDirector] Inference timed out after {timeoutSeconds}s.");
                    return FallbackLibrary.GetFallback("timeout");
                }

                string raw = await chatTask;
                var result = Parse(raw, request);
                result.LatencySeconds = (float)stopwatch.Elapsed.TotalSeconds;
                return result;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                Debug.LogException(e);
                return FallbackLibrary.GetFallback(e.GetType().Name);
            }
        }

        private void EnsureSystemPrompt(DirectorRequest request)
        {
            if (systemPromptSet) return;
            agent.systemPrompt = PromptBuilder.BuildSystemPrompt(request);
            systemPromptSet = true;
        }

        private static DirectorResult Parse(string raw, DirectorRequest request)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return FallbackLibrary.GetFallback("empty model output");
            }
            try
            {
                var json = JObject.Parse(raw);
                string reply = json.Value<string>("npc_reply") ?? "";
                var actions = json["actions"] is JArray arr
                    ? arr.Values<string>().ToList()
                    : new List<string>();
                var deltas = new Dictionary<string, float>();
                if (json["state_changes"] is JObject changes)
                {
                    foreach (var prop in changes.Properties())
                    {
                        if (prop.Value.Type == JTokenType.Integer || prop.Value.Type == JTokenType.Float)
                        {
                            deltas[prop.Name] = prop.Value.Value<float>();
                        }
                    }
                }
                return DirectorValidator.Validate(reply, actions, deltas, request, raw);
            }
            catch (Exception e)
            {
                // Should be unreachable with the grammar active; belt-and-suspenders.
                Debug.LogWarning($"[LlmDirector] Parse failed despite grammar: {e.Message}\nRaw: {raw}");
                return FallbackLibrary.GetFallback("parse error");
            }
        }

        private static string UnescapeJsonFragment(string fragment)
        {
            return fragment
                .Replace("\\n", "\n").Replace("\\t", " ")
                .Replace("\\\"", "\"").Replace("\\\\", "\\");
        }
    }
}
