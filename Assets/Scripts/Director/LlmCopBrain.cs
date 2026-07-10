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
        private ScenarioDefinition scenarioData;
        private string npcName = "Officer";
        private string playerLabel = "the driver";

        public void Configure(ScenarioDefinition scenario, LlmConfig llmConfig)
        {
            config = llmConfig;
            scenarioData = scenario;
            playerLabel = scenario.playerLabel;
            var npc = scenario.GetNpc(scenario.respondingNpcId);
            if (npc != null) npcName = npc.displayName;

            agent.systemPrompt = PromptBuilder.BuildCopSystemPrompt(scenario);
            agent.temperature = 0.9f;   // comedy wants spice
            agent.numPredict = 90;      // 1-3 short sentences
            agent.cachePrompt = true;
        }

        public async Task<CopReply> ReplyAsync(EventLog log, SceneStateModel state, string playerLine,
            Action<string> onPartial, CancellationToken ct)
        {
            string query = PromptBuilder.BuildCopReplyQuery(
                log, state, playerLabel, playerLine, WeirdnessDeck.Draw(scenarioData));
            string raw = await RunChat(query, onPartial, ct);
            if (raw == null) return new CopReply { Spoken = FallbackLibrary.GetCopLine("cop reply failed") };
            var reply = SplitReply(raw);
            if (string.IsNullOrEmpty(reply.Spoken))
            {
                reply.Spoken = FallbackLibrary.GetCopLine("empty cop reply");
            }
            return reply;
        }

        public async Task<CopReply> ReactToEventAsync(EventLog log, SceneStateModel state, string eventText,
            int timesHappened, Action<string> onPartial, CancellationToken ct)
        {
            string query = PromptBuilder.BuildCopReactionQuery(
                log, state, eventText, timesHappened, WeirdnessDeck.Draw(scenarioData));
            string raw = await RunChat(query, onPartial, ct);
            if (raw == null) return new CopReply(); // failure on a reaction = officer ignores it
            var reply = SplitReply(raw);
            // "..." (or nothing) means the officer chose not to comment.
            if (reply.Spoken.Replace(".", "").Trim().Length == 0) reply.Spoken = "";
            return reply;
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

        /// Small models decorate their dialogue with narration. Instead of
        /// deleting it (or worse, speaking it), split it out: stage directions
        /// and third-person sentences become silent beat text.
        private CopReply SplitReply(string raw)
        {
            string text = raw.Trim();
            text = Regex.Replace(text, @"^\s*(" + Regex.Escape(npcName) + @"|Officer|Cop)\s*:\s*", "", RegexOptions.IgnoreCase);

            var narration = new System.Text.StringBuilder();

            // *adjusts belt* / (sighs) -> narration
            text = Regex.Replace(text, @"\*([^*]*)\*", m => { narration.Append(m.Groups[1].Value.Trim() + " "); return " "; });
            text = Regex.Replace(text, @"\(([^)]*)\)", m => { narration.Append(m.Groups[1].Value.Trim() + " "); return " "; });

            // Third-person self-narration sentences -> narration
            var spoken = new System.Text.StringBuilder();
            foreach (Match m in Regex.Matches(text, @"[^.!?…]+[.!?…]*"))
            {
                string sentence = m.Value.Trim();
                if (sentence.Length == 0) continue;
                if (Regex.IsMatch(sentence, @"^(The\s+officer|Officer\s+\w+|" + Regex.Escape(npcName) + @")\b(?!\s*[:,])", RegexOptions.IgnoreCase) &&
                    !Regex.IsMatch(sentence, @"\b(I|I'm|I've|my|me)\b"))
                {
                    narration.Append(sentence + " ");
                }
                else
                {
                    spoken.Append(sentence + " ");
                }
            }

            var reply = new CopReply
            {
                Spoken = Tidy(spoken.ToString(), 350),
                Narration = Tidy(narration.ToString(), 200)
            };
            return reply;
        }

        private static string Tidy(string text, int maxLength)
        {
            text = Regex.Replace(text, @"\s+", " ").Trim().Trim('"').Trim();
            if (text.Length > maxLength) text = text.Substring(0, maxLength).TrimEnd() + "…";
            return text;
        }
    }
}
