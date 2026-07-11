using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using TalkOut.Actors;
using TalkOut.Core;
using TalkOut.Data;
using TalkOut.Props;

namespace TalkOut.World
{
    /// Plays the physical side of judge-picked actions (walking, prop pulses,
    /// wobble flails, face overrides) and applies the judge's cop-mood to faces.
    public class WorldPerformer : MonoBehaviour, ISceneActionPerformer
    {
        public TurnController turnController;
        public PropRegistry propRegistry;

        private readonly Dictionary<string, NPCActor> actors = new Dictionary<string, NPCActor>();
        private readonly Dictionary<string, LocationPoint> locations = new Dictionary<string, LocationPoint>();

        private void Awake()
        {
            foreach (var actor in FindObjectsOfType<NPCActor>())
            {
                if (!string.IsNullOrEmpty(actor.actorId)) actors[actor.actorId] = actor;
            }
            foreach (var point in FindObjectsOfType<LocationPoint>())
            {
                if (!string.IsNullOrEmpty(point.id)) locations[point.id] = point;
            }
        }

        private void Start()
        {
            if (turnController != null)
            {
                turnController.CopMoodChanged += OnCopMood;
            }
        }

        private void Update()
        {
            // expectant head tilt when the player is keeping them waiting
            if (turnController == null || turnController.Scenario == null) return;
            bool waiting = turnController.Phase == TurnPhase.AwaitingInput &&
                           turnController.IdleSeconds > 8f;
            if (actors.TryGetValue(turnController.Scenario.respondingNpcId, out var main) &&
                main.wobble != null)
            {
                main.wobble.WaitingForPlayer = waiting;
            }
        }

        private void OnDestroy()
        {
            if (turnController != null)
            {
                turnController.CopMoodChanged -= OnCopMood;
            }
        }

        private void OnCopMood(string mood)
        {
            // Judge's mood ruling drives the main NPC's face; a sidekick
            // (Benny-style "passenger" actor) panics whenever things look bad.
            string mainId = turnController != null && turnController.Scenario != null
                ? turnController.Scenario.respondingNpcId : "officer";
            if (actors.TryGetValue(mainId, out var main) && main.face != null)
            {
                main.face.SetFace(mood);
                if (main.wobble != null)
                {
                    if (mood == "angry") main.wobble.Impulse(1f);       // full flail
                    else if (mood == "amused") main.wobble.Impulse(0.8f);
                    else if (mood == "suspicious") main.wobble.Impulse(0.35f);
                }
            }
            if (actors.TryGetValue("passenger", out var passenger) && passenger.face != null)
            {
                bool bad = mood == "angry" || mood == "suspicious";
                passenger.face.SetFace(bad ? "panicked" : "neutral");
                if (bad && passenger.wobble != null) passenger.wobble.Impulse(1f);
            }
        }

        public async Task PerformAsync(ActionDefinition action)
        {
            NPCActor actor = null;
            if (!string.IsNullOrEmpty(action.actorId)) actors.TryGetValue(action.actorId, out actor);

            if (actor != null && !string.IsNullOrEmpty(action.expressionOverride) && actor.face != null)
            {
                actor.face.SetFace(action.expressionOverride);
            }

            if (actor != null && !string.IsNullOrEmpty(action.moveToLocationId) &&
                locations.TryGetValue(action.moveToLocationId, out var point))
            {
                await actor.WalkToAsync(point.transform);
                if (this == null) return;
            }

            var prop = propRegistry != null ? propRegistry.Get(action.targetPropId) : null;
            if (prop != null)
            {
                await prop.UseAsync();
                if (this == null) return;
            }

            // Physical emphasis: coded gesture if the key names one, else a flail.
            if (actor != null && actor.wobble != null)
            {
                if (!actor.wobble.PlayGesture(action.animationKey))
                {
                    actor.wobble.Impulse(0.4f);
                }
                await Task.Delay(600);
            }
        }
    }
}
