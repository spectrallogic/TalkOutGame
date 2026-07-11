using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using LLMUnity;
using TalkOut.Core;
using TalkOut.Data;

namespace TalkOut.Directing
{
    /// Tiny grammar-locked LLM call: given the words + where the player is
    /// looking, decide which character is being addressed. The grammar means
    /// it can only ever emit a candidate id or "unclear".
    public class LlmAddressee : MonoBehaviour, IAddressee
    {
        public LLMAgent agent;

        public void Configure(LlmConfig llmConfig)
        {
            agent.systemPrompt = PromptBuilder.BuildAddresseeSystemPrompt();
            agent.temperature = 0f; // arbitration should be deterministic
            agent.numPredict = 8;
            agent.cachePrompt = true;
        }

        public async Task<string> ResolveAsync(EventLog log, string playerLine, string gazedActorId,
            IReadOnlyList<(string id, string name)> candidates, CancellationToken ct)
        {
            try
            {
                Task ready = agent.llm.WaitUntilReady();
                if (await Task.WhenAny(ready, Task.Delay(TimeSpan.FromSeconds(20), ct)) != ready) return "";
                if (agent.llm.failed) return "";

                agent.SetGrammar(GrammarBuilder.BuildAddresseeGrammar(candidates.Select(c => c.id)));
                string query = PromptBuilder.BuildAddresseeQuery(log, playerLine, gazedActorId, candidates);

                Task<string> chat = agent.Chat(query, null, null, addToHistory: false);
                if (await Task.WhenAny(chat, Task.Delay(TimeSpan.FromSeconds(20), ct)) != chat)
                {
                    ct.ThrowIfCancellationRequested();
                    return "";
                }

                string verdict = (await chat ?? "").Trim().Trim('"').ToLowerInvariant();
                return candidates.Any(c => c.id == verdict) ? verdict : "";
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                Debug.LogWarning($"[Addressee] Arbitration failed: {e.Message}");
                return "";
            }
        }
    }
}
