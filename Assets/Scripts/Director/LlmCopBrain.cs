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
    /// The cop's freeform voice: local llama.cpp chat with NO output grammar,
    /// so replies read like a person. All memory comes from the EventLog
    /// transcript we inject each turn — LLMUnity's own history stays empty.
    public class LlmCopBrain : MonoBehaviour, ICopBrain
    {
        public LLMAgent agent;

        private LlmConfig config;
        private string npcName = "Officer";

        public void Configure(ScenarioDefinition scenario, LlmConfig llmConfig)
        {
            config = llmConfig;
            var npc = scenario.GetNpc(scenario.respondingNpcId);
            if (npc != null) npcName = npc.displayName;

            agent.systemPrompt = PromptBuilder.BuildCopSystemPrompt(scenario);
            agent.temperature = 0.9f;   // comedy wants spice
            agent.numPredict = 90;      // 1-3 short sentences
            agent.cachePrompt = true;
        }

        public async Task<string> ReplyAsync(EventLog log, string playerLine,
            Action<string> onPartial, CancellationToken ct)
        {
            string query = PromptBuilder.BuildCopReplyQuery(log, playerLine);
            string raw = await RunChat(query, onPartial, ct);
            if (raw == null) return FallbackLibrary.GetCopLine("cop reply failed");
            string cleaned = CleanReply(raw);
            return string.IsNullOrEmpty(cleaned) ? FallbackLibrary.GetCopLine("empty cop reply") : cleaned;
        }

        public async Task<string> ReactToEventAsync(EventLog log, string eventText,
            Action<string> onPartial, CancellationToken ct)
        {
            string query = PromptBuilder.BuildCopReactionQuery(log, eventText);
            string raw = await RunChat(query, onPartial, ct);
            if (raw == null) return ""; // failure on a reaction = officer ignores it
            string cleaned = CleanReply(raw);
            // "..." (or nothing) means the officer chose not to comment.
            if (cleaned.Length < 4 && cleaned.Replace(".", "").Trim().Length == 0) return "";
            return cleaned;
        }

        public async Task WarmupAsync()
        {
            try
            {
                await agent.llm.WaitUntilReady();
                if (!agent.llm.failed) await agent.Warmup();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CopBrain] Warmup failed: {e.Message}");
            }
        }

        private async Task<string> RunChat(string query, Action<string> onPartial, CancellationToken ct)
        {
            try
            {
                Task ready = agent.llm.WaitUntilReady();
                if (await Task.WhenAny(ready, Task.Delay(TimeSpan.FromSeconds(60), ct)) != ready) return null;
                if (agent.llm.failed) return null;

                Task<string> chat = agent.Chat(query, onPartial, null, addToHistory: false);
                float timeout = config != null ? config.timeoutSeconds : 45f;
                if (await Task.WhenAny(chat, Task.Delay(TimeSpan.FromSeconds(timeout), ct)) != chat)
                {
                    ct.ThrowIfCancellationRequested();
                    Debug.LogWarning("[CopBrain] Reply timed out.");
                    return null;
                }
                return await chat;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                Debug.LogException(e);
                return null;
            }
        }

        /// Small models decorate: strip name prefixes, quotes, stage directions.
        private string CleanReply(string raw)
        {
            string text = raw.Trim();
            text = Regex.Replace(text, @"^\s*(" + Regex.Escape(npcName) + @"|Officer|Cop)\s*:\s*", "", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\*[^*]*\*", "");      // *adjusts belt*
            text = Regex.Replace(text, @"\([^)]*\)", "");       // (sighs)
            text = text.Trim().Trim('"').Trim();
            if (text.Length > 350) text = text.Substring(0, 350).TrimEnd() + "…";
            return text;
        }
    }
}
