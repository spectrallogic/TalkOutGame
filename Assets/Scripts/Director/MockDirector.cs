using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TalkOut.Directing
{
    /// Keyword-driven fake director. Proves the entire game loop (UI, state,
    /// actions, outcomes) with zero LLM dependency, including a realistic
    /// thinking delay and simulated reply streaming.
    public class MockDirector : IDirector
    {
        private const float FakeDelaySeconds = 1.5f;

        private class Rule
        {
            public string[] Keywords;
            public string Reply;
            public Dictionary<string, float> Deltas;
            public string[] WantedActions;
        }

        private static readonly List<Rule> Rules = new List<Rule>
        {
            new Rule
            {
                Keywords = new[] { "emergency", "hospital", "pregnant", "dying", "vet" },
                Reply = "An emergency, huh? Everyone's got an emergency. Last week a guy told me his goldfish was in labor.",
                Deltas = new Dictionary<string, float> { { "sympathy", 15 }, { "suspicion", 5 } },
                WantedActions = new[] { "OfficerInspectLicense" }
            },
            new Rule
            {
                Keywords = new[] { "idiot", "stupid", "pig", "shut up", "donut" },
                Reply = "Sir. I have a taser and unresolved anger from the academy. Choose your next words carefully.",
                Deltas = new Dictionary<string, float> { { "patience", -20 }, { "suspicion", 10 } },
                WantedActions = new[] { "OfficerTapTicketPad" }
            },
            new Rule
            {
                Keywords = new[] { "earth", "rotate", "physics", "simulation", "alien", "time travel" },
                Reply = "That's not how physics works. ...Actually, hold on. Continue.",
                Deltas = new Dictionary<string, float> { { "amusement", 20 }, { "suspicion", -5 } },
                WantedActions = new[] { "OfficerGetConfused", "OfficerLaugh" }
            },
            new Rule
            {
                Keywords = new[] { "friend was driving", "wasn't me", "not me", "passenger" },
                Reply = "Your friend was driving? From the passenger seat? That's some real teamwork.",
                Deltas = new Dictionary<string, float> { { "suspicion", 15 } },
                WantedActions = new[] { "OfficerWalkToPassengerWindow", "PassengerPanic" }
            },
            new Rule
            {
                Keywords = new[] { "sorry", "my fault", "guilty", "speeding", "i was wrong" },
                Reply = "Honesty. Refreshing. Most people blame the pedals.",
                Deltas = new Dictionary<string, float> { { "sympathy", 10 }, { "suspicion", -10 } },
                WantedActions = new string[0]
            },
            new Rule
            {
                Keywords = new[] { "lawyer", "sue", "rights", "badge number" },
                Reply = "You want my badge number? It's 'zero fun at parties'. License and registration.",
                Deltas = new Dictionary<string, float> { { "patience", -10 }, { "suspicion", 5 } },
                WantedActions = new[] { "OfficerInspectLicense" }
            },
            new Rule
            {
                Keywords = new[] { "joke", "funny", "laugh", "haha" },
                Reply = "I'm not laughing. That sound was a cough. A professional, law-enforcement cough.",
                Deltas = new Dictionary<string, float> { { "amusement", 15 } },
                WantedActions = new[] { "OfficerLaugh" }
            }
        };

        private static readonly Rule DefaultRule = new Rule
        {
            Keywords = Array.Empty<string>(),
            Reply = "Uh-huh. And do you know how fast you were going?",
            Deltas = new Dictionary<string, float> { { "suspicion", 5 }, { "patience", -5 } },
            WantedActions = new string[0]
        };

        public async Task<DirectorResult> DirectAsync(
            DirectorRequest request,
            Action<string> onPartialReply,
            CancellationToken cancellationToken)
        {
            var input = (request.PlayerInput ?? "").ToLowerInvariant();
            var rule = Rules.FirstOrDefault(r => r.Keywords.Any(k => input.Contains(k))) ?? DefaultRule;

            // Escalation pressure so mock playthroughs actually reach outcomes.
            var deltas = new Dictionary<string, float>(rule.Deltas);
            if (request.State.GetStat("suspicion") > 70 && !deltas.ContainsKey("patience"))
            {
                deltas["patience"] = -10;
            }

            var actions = new List<string>(rule.WantedActions);
            if (request.State.GetStat("suspicion") > 80)
            {
                actions.Add("OfficerCallBackup");
            }

            // Fake thinking time + simulated token streaming.
            float elapsed = 0f;
            int shown = 0;
            while (elapsed < FakeDelaySeconds)
            {
                await Task.Delay(100, cancellationToken);
                elapsed += 0.1f;
                int target = (int)(rule.Reply.Length * (elapsed / FakeDelaySeconds));
                if (target > shown && onPartialReply != null)
                {
                    shown = target;
                    onPartialReply(rule.Reply.Substring(0, Math.Min(shown, rule.Reply.Length)));
                }
            }

            var result = DirectorValidator.Validate(
                rule.Reply, actions, deltas, request, "[mock]");
            result.LatencySeconds = FakeDelaySeconds;
            return result;
        }

        public Task WarmupAsync(DirectorRequest contextRequest) => Task.CompletedTask;
    }
}
