using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using TalkOut.Actors;
using TalkOut.CameraRig;
using TalkOut.Core;
using TalkOut.Data;
using TalkOut.Props;

namespace TalkOut.World
{
    /// Plays the physical side of director actions: camera focus, walking,
    /// gestures, face overrides, prop pulses. Also keeps ambient expressions
    /// in sync with hidden state so the player can read the officer's mood.
    public class WorldPerformer : MonoBehaviour, ISceneActionPerformer
    {
        public TurnController turnController;
        public CameraDirector cameraDirector;
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
                turnController.StateChanged += UpdateAmbientExpressions;
            }
            UpdateAmbientExpressions();
        }

        private void OnDestroy()
        {
            if (turnController != null)
            {
                turnController.StateChanged -= UpdateAmbientExpressions;
            }
        }

        public async Task PerformAsync(ActionDefinition action)
        {
            NPCActor actor = null;
            if (!string.IsNullOrEmpty(action.actorId)) actors.TryGetValue(action.actorId, out actor);

            bool didSomething = false;

            if (actor != null)
            {
                if (cameraDirector != null) cameraDirector.FocusOn(actor.transform);
                if (!string.IsNullOrEmpty(action.expressionOverride) && actor.face != null)
                {
                    actor.face.SetFace(action.expressionOverride);
                }
            }

            if (actor != null && !string.IsNullOrEmpty(action.moveToLocationId) &&
                locations.TryGetValue(action.moveToLocationId, out var point))
            {
                await actor.WalkToAsync(point.transform);
                didSomething = true;
            }

            var prop = propRegistry != null ? propRegistry.Get(action.targetPropId) : null;
            if (prop != null)
            {
                if (cameraDirector != null) cameraDirector.FocusOn(prop.transform);
                await prop.UseAsync();
                didSomething = true;
            }

            if (actor != null && actor.poser != null && !string.IsNullOrEmpty(action.animationKey))
            {
                await actor.poser.PlayPoseAsync(action.animationKey);
                didSomething = true;
            }

            if (!didSomething)
            {
                await Task.Delay(400);
            }

            UpdateAmbientExpressions();
            if (cameraDirector != null && actor != null) cameraDirector.FocusOn(actor.transform);
        }

        private void UpdateAmbientExpressions()
        {
            if (turnController == null || turnController.State == null) return;
            foreach (var kv in actors)
            {
                if (kv.Value.face == null) continue;
                kv.Value.face.SetFace(ExpressionMapper.Evaluate(
                    kv.Key, turnController.State, turnController.LastTurnWasFallback));
            }
        }
    }
}
