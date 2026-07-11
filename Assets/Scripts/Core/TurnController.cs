using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using TalkOut.Data;
using TalkOut.Directing;

namespace TalkOut.Core
{
    public enum TurnPhase
    {
        NotStarted,
        Intro,
        AwaitingInput,
        CopThinking,
        Judging,
        RunningActions,
        SceneOver
    }

    /// The Referee's spine, v2. Freeform cop replies + a judge LLM that rules on
    /// the outcome from the shared EventLog transcript. Player interactions
    /// (clicking things in the car) flow through the same memory.
    public class TurnController : MonoBehaviour
    {
        public ScenarioDefinition Scenario { get; private set; }
        public SceneStateModel State { get; private set; }
        public EventLog Log { get; private set; }
        public TurnPhase Phase { get; private set; } = TurnPhase.NotStarted;
        public int PlayerTurnsTaken { get; private set; }
        public JudgeVerdict LastVerdict { get; private set; }

        /// Level timer: runs from the moment the player can act until the scene ends.
        public float ElapsedSeconds { get; private set; }
        private bool timerRunning;

        /// Filled at scene end, before SceneEnded fires.
        public int LastRunScore { get; private set; }
        public bool LastRunIsNewBest { get; private set; }

        /// Seconds the player has been silent while input was open.
        public float IdleSeconds { get; private set; }
        private int idleNudgesFired;
        private bool timeoutTriggered;

        public event Action<bool> ThinkingChanged;
        public event Action<string> PartialReply;
        public event Action<string> CopMoodChanged;
        public event Action<OutcomeRule> SceneEnded;

        private ICopBrain copBrain;
        private IJudge judge;
        private ISidekick sidekick;
        private ISceneActionPerformer performer;
        private string npcDisplayName = "Officer";
        private string sidekickDisplayName = "";

        public void Initialize(ScenarioDefinition scenario, ICopBrain copBrain, IJudge judge,
            ISceneActionPerformer performer, ISidekick sidekick = null)
        {
            Scenario = scenario;
            this.copBrain = copBrain;
            this.judge = judge;
            this.sidekick = sidekick;
            this.performer = performer;

            var sidekickNpc = scenario.GetNpc(scenario.sidekickNpcId);
            if (sidekickNpc != null) sidekickDisplayName = sidekickNpc.displayName;
            State = new SceneStateModel(scenario);
            Log = new EventLog { PlayerLabel = scenario.playerTranscriptName };
            PlayerTurnsTaken = 0;
            ElapsedSeconds = 0f;
            timerRunning = false;

            var npc = scenario.GetNpc(scenario.respondingNpcId);
            if (npc != null) npcDisplayName = npc.displayName;

            _ = BeginSceneAsync();
        }

        private async Task BeginSceneAsync()
        {
            Phase = TurnPhase.Intro;
            Log.Add(EventKind.System, "", Scenario.sceneDescription);
            Log.Add(EventKind.System, "", $"Goal: {Scenario.playerGoal}   (hold V to talk, or press Enter to type)");

            try
            {
                // Officer approaches while the model warms up behind the beat.
                var approach = Scenario.GetAction("OfficerWalkToDriverWindow");
                if (approach != null && performer != null)
                {
                    Log.Add(EventKind.SceneBeat, "", approach.narrationText);
                    await performer.PerformAsync(approach);
                }
                await copBrain.WarmupAsync();
            }
            catch (Exception e) { Debug.LogException(e); }

            if (this == null) return;
            Log.Add(EventKind.NpcSaid, npcDisplayName, Scenario.openerLine);
            Phase = TurnPhase.AwaitingInput;
            timerRunning = true; // the clock starts once you can talk
        }

        private void Update()
        {
            if (timerRunning) ElapsedSeconds += Time.deltaTime;

            // idle awareness: silence becomes a scene event the NPC can react to
            if (Phase == TurnPhase.AwaitingInput && timerRunning)
            {
                IdleSeconds += Time.deltaTime;
                if (Scenario.idleNudgeSeconds > 0 && IdleSeconds >= Scenario.idleNudgeSeconds &&
                    idleNudgesFired < 3)
                {
                    idleNudgesFired++;
                    IdleSeconds = -8f; // brief grace before it can fire again
                    if (State.HasStat("annoyance")) State.ApplyStatDelta("annoyance", 4f);
                    if (State.HasStat("awkwardness")) State.ApplyStatDelta("awkwardness", 5f);
                    ReportPlayerInteraction(Scenario.idleEventText, copMayReact: true);
                }
            }
            else if (Phase != TurnPhase.AwaitingInput)
            {
                IdleSeconds = 0f;
            }

            // hard time limit: the NPC ends it themselves
            if (!timeoutTriggered && timerRunning && Scenario != null &&
                Scenario.timeLimitSeconds > 0 && ElapsedSeconds >= Scenario.timeLimitSeconds &&
                Phase == TurnPhase.AwaitingInput)
            {
                timeoutTriggered = true;
                _ = RunTimeoutAsync();
            }
        }

        private async Task RunTimeoutAsync()
        {
            Phase = TurnPhase.RunningActions;
            if (!string.IsNullOrEmpty(Scenario.timeoutLine))
            {
                Log.Add(EventKind.NpcSaid, npcDisplayName, Scenario.timeoutLine);
            }
            foreach (var actionId in Scenario.timeoutActionIds)
            {
                var action = Scenario.GetAction(actionId);
                if (action == null) continue;
                ApplyEngineEffects(action);
                if (!string.IsNullOrEmpty(action.narrationText))
                {
                    Log.Add(EventKind.SceneBeat, "", action.narrationText);
                }
                if (performer != null)
                {
                    try { await performer.PerformAsync(action); }
                    catch (Exception e) { Debug.LogException(e); }
                    if (this == null) return;
                }
            }
            EndScene(Scenario.timeoutOutcomeId);
        }

        public List<ActionDefinition> ComputeAvailableActions()
        {
            return Scenario.actionCatalog
                .Where(a => a != null && a.IsAvailable(State))
                .ToList();
        }

        /// Entry point for both voice-transcribed and typed player speech.
        public async void SubmitPlayerUtterance(string text)
        {
            if (Phase != TurnPhase.AwaitingInput || string.IsNullOrWhiteSpace(text)) return;
            try
            {
                await RunSpeechTurnAsync(text.Trim());
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                Debug.LogException(e);
                Phase = TurnPhase.AwaitingInput; // never soft-lock
            }
        }

        /// Entry point for clickable interactables. Always recorded in memory
        /// (and its immediate emotional effects always land); the cop only gets
        /// a chance to react when the scene is idle.
        public async void ReportPlayerInteraction(string eventText, bool copMayReact = true,
            IEnumerable<StatEffect> immediateEffects = null)
        {
            Log.Add(EventKind.PlayerAction, "", eventText);

            int timesHappened = 0;
            foreach (var e in Log.Events)
            {
                if (e.kind == EventKind.PlayerAction && e.text == eventText) timesHappened++;
            }

            if (immediateEffects != null)
            {
                foreach (var effect in immediateEffects)
                {
                    if (effect.kind == StatEffect.Kind.StatDelta)
                    {
                        // repeat offenses sting more
                        State.ApplyStatDelta(effect.key, effect.amount * Mathf.Min(timesHappened, 3));
                    }
                }
            }

            if (!copMayReact || Phase != TurnPhase.AwaitingInput) return;

            try
            {
                Phase = TurnPhase.CopThinking;
                ThinkingChanged?.Invoke(true);
                var reaction = await copBrain.ReactToEventAsync(
                    Log, State, eventText, timesHappened, p => PartialReply?.Invoke(p), destroyCancellationToken);
                if (this == null) return;
                ThinkingChanged?.Invoke(false);

                if (!string.IsNullOrEmpty(reaction.Narration))
                {
                    Log.Add(EventKind.SceneBeat, "", reaction.Narration);
                }
                if (!string.IsNullOrEmpty(reaction.Spoken))
                {
                    Log.Add(EventKind.NpcSaid, npcDisplayName, reaction.Spoken);
                    await RunJudgePassAsync();
                }
                else
                {
                    Phase = TurnPhase.AwaitingInput;
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                Debug.LogException(e);
                ThinkingChanged?.Invoke(false);
                Phase = TurnPhase.AwaitingInput;
            }
        }

        private async Task RunSpeechTurnAsync(string playerLine)
        {
            PlayerTurnsTaken++;
            Log.Add(EventKind.PlayerSaid, "You", playerLine);

            Phase = TurnPhase.CopThinking;
            ThinkingChanged?.Invoke(true);
            var reply = await copBrain.ReplyAsync(
                Log, State, playerLine, p => PartialReply?.Invoke(p), destroyCancellationToken);
            if (this == null) return;
            ThinkingChanged?.Invoke(false);

            if (!string.IsNullOrEmpty(reply.Narration))
            {
                Log.Add(EventKind.SceneBeat, "", reply.Narration);
            }
            Log.Add(EventKind.NpcSaid, npcDisplayName, reply.Spoken);
            await RunJudgePassAsync();
        }

        private async Task RunJudgePassAsync()
        {
            Phase = TurnPhase.Judging;
            var available = ComputeAvailableActions();
            JudgeVerdict verdict;
            try
            {
                verdict = await judge.JudgeAsync(Log, State, available, destroyCancellationToken);
            }
            catch (OperationCanceledException) { return; }
            catch (Exception e)
            {
                Debug.LogException(e);
                verdict = FallbackLibrary.GetVerdict(e.GetType().Name);
            }
            if (this == null) return;

            LastVerdict = verdict;

            // Judge's emotional ruling: clamped nudges to the officer's meters.
            foreach (var kv in verdict.MoodChanges)
            {
                State.ApplyStatDelta(kv.Key, Mathf.Clamp(kv.Value, -20f, 20f));
            }
            CopMoodChanged?.Invoke(verdict.CopMood);

            // Physical beats picked by the judge.
            Phase = TurnPhase.RunningActions;
            string endsSceneOutcomeId = null;
            foreach (var actionId in verdict.ActionIds)
            {
                var action = Scenario.GetAction(actionId);
                if (action == null) continue;

                ApplyEngineEffects(action);
                if (action.endsScene && endsSceneOutcomeId == null) endsSceneOutcomeId = action.outcomeId;

                if (!string.IsNullOrEmpty(action.narrationText))
                {
                    Log.Add(EventKind.SceneBeat, "", action.narrationText);
                }
                if (performer != null)
                {
                    await performer.PerformAsync(action);
                    if (this == null) return;
                }
            }

            // Outcome resolution: judge verdict first, then scene-ending actions, then turn cap.
            string outcomeId = null;
            if (verdict.Released) outcomeId = Scenario.winOutcomeId;
            else if (verdict.Arrested) outcomeId = Scenario.loseOutcomeId;
            else if (endsSceneOutcomeId != null) outcomeId = endsSceneOutcomeId;
            else if (PlayerTurnsTaken >= Scenario.maxTurns) outcomeId = Scenario.maxTurnsOutcomeId;

            if (outcomeId != null && EndScene(outcomeId)) return;

            // Sidekick chatter: sometimes the second character pipes up
            // (Dennis to the king, Benny to the void).
            if (sidekick != null && !string.IsNullOrEmpty(sidekickDisplayName) &&
                UnityEngine.Random.value < Scenario.sidekickChatterChance)
            {
                try
                {
                    string line = await sidekick.InterjectAsync(Log, State, destroyCancellationToken);
                    if (this == null) return;
                    if (!string.IsNullOrEmpty(line))
                    {
                        Log.Add(EventKind.NpcSaid, sidekickDisplayName, line);
                    }
                }
                catch (OperationCanceledException) { return; }
                catch (Exception e) { Debug.LogException(e); }
            }

            Phase = TurnPhase.AwaitingInput;
            IdleSeconds = 0f;
        }

        private bool EndScene(string outcomeId)
        {
            var outcome = Scenario.GetOutcome(outcomeId);
            if (outcome == null)
            {
                Debug.LogError($"[TurnController] Unknown outcome id '{outcomeId}'");
                return false;
            }
            Phase = TurnPhase.SceneOver;
            timerRunning = false;
            LastRunScore = Scoring.Compute(outcome.isWin, ElapsedSeconds, PlayerTurnsTaken);
            LastRunIsNewBest = Save.SaveSystem.RecordOutcome(
                Scenario.scenarioId, outcome, ElapsedSeconds, PlayerTurnsTaken, LastRunScore);
            Log.Add(EventKind.System, "", $"{outcome.title} — {outcome.resultText}");
            SceneEnded?.Invoke(outcome);
            return true;
        }

        private void ApplyEngineEffects(ActionDefinition action)
        {
            foreach (var effect in action.engineEffects)
            {
                switch (effect.kind)
                {
                    case StatEffect.Kind.StatDelta:
                        State.ApplyStatDelta(effect.key, effect.amount);
                        break;
                    case StatEffect.Kind.SetFlag:
                        State.SetFlag(effect.key, effect.boolValue);
                        break;
                    case StatEffect.Kind.SetLocation:
                        State.SetLocation(effect.key, effect.stringValue);
                        break;
                }
            }
            if (!string.IsNullOrEmpty(action.moveToLocationId) && !string.IsNullOrEmpty(action.actorId))
            {
                State.SetLocation(action.actorId, action.moveToLocationId);
            }
        }
    }
}
