using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TalkOut.Core;
using TalkOut.Data;

namespace TalkOut.Directing
{
    /// No-model stand-ins so the whole game loop runs on any machine.
    /// Used automatically when the GGUF is missing, or via the GameManager toggle.
    public class MockCopBrain : ICopBrain
    {
        private int turn;

        private static readonly string[] Escalation =
        {
            "Do you know how fast you were going?",
            "Uh-huh. And I'm the tooth fairy. License and registration.",
            "Sir, the radar gun doesn't do 'vibes'. It does numbers.",
            "You're either very funny or very concussed. Which is it?",
            "Okay. Okay. You know what? Fine. Get out of here before I change my mind."
        };

        public async Task<CopReply> ReplyAsync(EventLog log, SceneStateModel state, string playerLine,
            Action<string> onPartial, CancellationToken ct)
        {
            await Task.Delay(900, ct);
            string reply = Escalation[Math.Min(turn, Escalation.Length - 1)];
            turn++;
            return new CopReply { Spoken = reply };
        }

        public async Task<CopReply> ReactToEventAsync(EventLog log, SceneStateModel state, string eventText,
            int timesHappened, Action<string> onPartial, CancellationToken ct)
        {
            await Task.Delay(600, ct);
            if (eventText.Contains("honked"))
            {
                return new CopReply
                {
                    Spoken = timesHappened > 1
                        ? "Okay. Do it a third time. See what happens."
                        : "What the— did you just honk at me?",
                    Narration = timesHappened > 1 ? "The officer's eye twitches." : ""
                };
            }
            if (eventText.Contains("glove")) return new CopReply { Spoken = "Slowly. What's in there?" };
            return new CopReply(); // ignores the rest
        }

        public Task WarmupAsync() => Task.CompletedTask;
    }

    public class MockSidekick : ISidekick
    {
        private int calls;

        private static readonly string[] Lines =
        {
            "...Can I say something? No? Okay.",
            "I'm just gonna... yeah. Never mind.",
            "For the record, I wasn't here.",
        };

        public async Task<string> InterjectAsync(EventLog log, SceneStateModel state, CancellationToken ct)
        {
            await Task.Delay(400, ct);
            calls++;
            return calls % 2 == 0 ? Lines[(calls / 2 - 1) % Lines.Length] : "";
        }

        public async Task<string> ReplyAsync(EventLog log, SceneStateModel state, string playerLine, CancellationToken ct)
        {
            await Task.Delay(500, ct);
            return "Me? Uh. I mean — yes? Whatever you said. Yes.";
        }
    }

    /// Name-match, then gaze, then shrug.
    public class MockAddressee : IAddressee
    {
        public async Task<string> ResolveAsync(EventLog log, string playerLine, string gazedActorId,
            IReadOnlyList<(string id, string name)> candidates, CancellationToken ct)
        {
            await Task.Delay(100, ct);
            foreach (var (id, name) in candidates)
            {
                if (playerLine.IndexOf(name.Split(' ')[0], StringComparison.OrdinalIgnoreCase) >= 0) return id;
            }
            return gazedActorId ?? "";
        }
    }

    public class MockJudge : IJudge
    {
        public async Task<JudgeVerdict> JudgeAsync(EventLog log, SceneStateModel state,
            IReadOnlyList<ActionDefinition> availableActions, CancellationToken ct)
        {
            await Task.Delay(300, ct);
            string lastCopLine = log.LastNpcLine().ToLowerInvariant();

            var verdict = new JudgeVerdict { CopMood = "neutral", RawOutput = "[mock judge]" };
            if (lastCopLine.Contains("get out of here") || lastCopLine.Contains("you can go") ||
                lastCopLine.Contains("on your way") || lastCopLine.Contains("free to go"))
            {
                verdict.Released = true;
                verdict.CopMood = "defeated";
            }
            else if (lastCopLine.Contains("under arrest"))
            {
                verdict.Arrested = true;
                verdict.CopMood = "angry";
            }
            else if (lastCopLine.Contains("funny") || lastCopLine.Contains("tooth fairy"))
            {
                verdict.CopMood = "amused";
                var laugh = availableActions.FirstOrDefault(a => a.id == "OfficerLaugh");
                if (laugh != null) verdict.ActionIds.Add(laugh.id);
            }
            return verdict;
        }
    }
}
