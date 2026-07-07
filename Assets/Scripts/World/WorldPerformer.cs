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

        private void OnDestroy()
        {
            if (turnController != null)
            {
                turnController.CopMoodChanged -= OnCopMood;
            }
        }

        private void OnCopMood(string mood)
        {
            // Judge's mood ruling drives the main NPC's face; the passenger
            // panics whenever things look bad for the car.
            if (actors.TryGetValue("officer", out var officer) && officer.face != null)
            {
                officer.face.SetFace(mood);
                if (officer.wobble != null && (mood == "angry" || mood == "amused"))
                {
                    officer.wobble.Impulse(0.7f);
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

            // Physical emphasis: every action makes the body react.
            if (actor != null && actor.wobble != null)
            {
                actor.wobble.Impulse(ImpulseFor(action.animationKey));
                await Task.Delay(600);
            }
        }

        private static float ImpulseFor(string animationKey)
        {
            switch (animationKey)
            {
                case "panicShake": return 1f;
                case "laugh": return 0.9f;
                case "scribble": return 0.5f;
                default: return 0.4f;
            }
        }
    }
}
