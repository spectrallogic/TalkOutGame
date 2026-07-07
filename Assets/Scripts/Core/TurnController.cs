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
        AwaitingInput,
        Thinking,
        RunningActions,
        SceneOver
    }

    /// The Referee's spine: player input -> director -> validated result ->
    /// state deltas -> action queue -> outcome check. Owns all authoritative state.
    public class TurnController : MonoBehaviour
    {
        public ScenarioDefinition Scenario { get; private set; }
        public SceneStateModel State { get; private set; }
        public TurnPhase Phase { get; private set; } = TurnPhase.NotStarted;
        public IReadOnlyList<DialogueLine> History => history;
        public int PlayerTurnsTaken { get; private set; }
        public bool LastTurnWasFallback { get; private set; }

        public event Action<DialogueLine> LineAdded;
        public event Action<bool> ThinkingChanged;
        public event Action<string> PartialReply;
        public event Action StateChanged;
        public event Action<OutcomeRule> SceneEnded;

        [Tooltip("How many recent lines are sent to the director as history")]
        public int historyWindowSize = 6;

        [Tooltip("Beat pacing when no 3D performer is attached")]
        public float defaultBeatSeconds = 0.6f;

        private readonly List<DialogueLine> history = new List<DialogueLine>();
        private IDirector director;
        private ISceneActionPerformer performer;

        public void Initialize(ScenarioDefinition scenario, IDirector director, ISceneActionPerformer performer = null)
        {
            Scenario = scenario;
            this.director = director;
            this.performer = performer;
            State = new SceneStateModel(scenario);
            history.Clear();
            PlayerTurnsTaken = 0;
            Phase = TurnPhase.AwaitingInput;

            AddLine(new DialogueLine(LineKind.System, "", scenario.sceneDescription));
            AddLine(new DialogueLine(LineKind.System, "", $"Goal: {scenario.playerGoal}"));
            StateChanged?.Invoke();
        }

        public void SetPerformer(ISceneActionPerformer newPerformer) => performer = newPerformer;

        public List<ActionDefinition> ComputeAvailableActions()
        {
            return Scenario.actionCatalog
                .Where(a => a != null && a.IsAvailable(State))
                .ToList();
        }

        /// UI entry point. Fire-and-forget by design; all failures are contained.
        public async void SubmitPlayerInput(string text)
        {
            if (Phase != TurnPhase.AwaitingInput || string.IsNullOrWhiteSpace(text)) return;
            try
            {
                await RunTurnAsync(text.Trim());
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Phase = TurnPhase.AwaitingInput; // never soft-lock the game
            }
        }

        private async Task RunTurnAsync(string playerInput)
        {
            PlayerTurnsTaken++;
            AddLine(new DialogueLine(LineKind.Player, "You", playerInput));

            var request = new DirectorRequest
            {
                Scenario = Scenario,
                State = State,
                AvailableActions = ComputeAvailableActions(),
                HistoryWindow = history.Where(l => l.kind == LineKind.Player || l.kind == LineKind.Npc)
                                       .TakeLast(historyWindowSize).ToList(),
                PlayerInput = playerInput,
                RespondingNpc = Scenario.GetNpc(Scenario.respondingNpcId)
            };

            Phase = TurnPhase.Thinking;
            ThinkingChanged?.Invoke(true);

            DirectorResult result;
            try
            {
                result = await director.DirectAsync(
                    request, p => PartialReply?.Invoke(p), destroyCancellationToken);
            }
            catch (OperationCanceledException) { return; }
            catch (Exception e)
            {
                Debug.LogException(e);
                result = FallbackLibrary.GetFallback(e.GetType().Name);
            }

            if (this == null) return; // scene torn down while awaiting
            ThinkingChanged?.Invoke(false);
            LastTurnWasFallback = result.IsFallback;

            // 1. LLM-proposed stat deltas (already validated + clamped).
            foreach (var kv in result.StatChanges)
            {
                State.ApplyStatDelta(kv.Key, kv.Value);
            }
            StateChanged?.Invoke();

            // 2. NPC reply.
            string speaker = request.RespondingNpc != null ? request.RespondingNpc.displayName : "???";
            AddLine(new DialogueLine(LineKind.Npc, speaker, result.NpcReply));

            // 3. Action queue — engine effects + staged visual beats.
            Phase = TurnPhase.RunningActions;
            string forcedOutcomeId = null;
            foreach (var actionId in result.ActionIds)
            {
                var action = Scenario.GetAction(actionId);
                if (action == null) continue;

                ApplyEngineEffects(action);
                if (action.endsScene && forcedOutcomeId == null)
                {
                    forcedOutcomeId = action.outcomeId;
                }

                if (!string.IsNullOrEmpty(action.narrationText))
                {
                    AddLine(new DialogueLine(LineKind.Beat, "", action.narrationText));
                }

                if (performer != null)
                {
                    await performer.PerformAsync(action);
                }
                else
                {
                    await Task.Delay(TimeSpan.FromSeconds(defaultBeatSeconds), destroyCancellationToken);
                }
            }
            StateChanged?.Invoke();

            // 4. Outcome check — code and data only.
            var outcome = OutcomeEvaluator.Evaluate(Scenario, State, PlayerTurnsTaken, forcedOutcomeId);
            if (outcome != null)
            {
                Phase = TurnPhase.SceneOver;
                AddLine(new DialogueLine(LineKind.System, "", $"{outcome.title} — {outcome.resultText}"));
                SceneEnded?.Invoke(outcome);
                return;
            }

            Phase = TurnPhase.AwaitingInput;
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

        private void AddLine(DialogueLine line)
        {
            history.Add(line);
            LineAdded?.Invoke(line);
        }
    }
}
