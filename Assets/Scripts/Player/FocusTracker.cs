using System.Collections.Generic;
using UnityEngine;
using TalkOut.Actors;

namespace TalkOut.Player
{
    /// Tracks which character the player is visually focused on: smallest
    /// view angle under a threshold, smoothed with dwell time so a flick of
    /// the mouse doesn't count as attention. The addressee arbitration uses
    /// this as gaze evidence alongside the words themselves.
    public class FocusTracker : MonoBehaviour
    {
        public Camera playerCamera;

        [Tooltip("Max view angle (degrees) for a character to count as looked-at")]
        public float maxAngle = 30f;

        [Tooltip("Seconds of sustained gaze before it counts as focus")]
        public float dwellSeconds = 0.4f;

        /// Actor id currently focused ("" = nobody).
        public string GazedActorId { get; private set; } = "";
        public string GazedDisplayName { get; private set; } = "";

        private class Candidate
        {
            public NPCActor actor;
            public Transform head;
            public string displayName;
            public float dwell;
        }

        private readonly List<Candidate> candidates = new List<Candidate>();

        private void Start()
        {
            if (playerCamera == null) playerCamera = GetComponent<Camera>();
            foreach (var actor in FindObjectsOfType<NPCActor>())
            {
                if (string.IsNullOrEmpty(actor.actorId)) continue;
                var head = FindHead(actor.transform);
                candidates.Add(new Candidate
                {
                    actor = actor,
                    head = head != null ? head : actor.transform,
                    displayName = actor.name.Replace("_", " "),
                });
            }
        }

        private static Transform FindHead(Transform root)
        {
            var torso = root.Find("TorsoPivot");
            return torso != null ? torso.Find("Head") : null;
        }

        private void Update()
        {
            if (playerCamera == null || candidates.Count == 0) return;

            Candidate best = null;
            float bestAngle = maxAngle;
            foreach (var candidate in candidates)
            {
                if (candidate.actor == null) continue;
                Vector3 to = candidate.head.position - playerCamera.transform.position;
                float angle = Vector3.Angle(playerCamera.transform.forward, to);
                if (angle < bestAngle)
                {
                    bestAngle = angle;
                    best = candidate;
                }
            }

            foreach (var candidate in candidates)
            {
                if (candidate == best)
                {
                    candidate.dwell += Time.deltaTime;
                }
                else
                {
                    candidate.dwell = Mathf.Max(0f, candidate.dwell - Time.deltaTime * 2f);
                }
            }

            if (best != null && best.dwell >= dwellSeconds)
            {
                GazedActorId = best.actor.actorId;
                GazedDisplayName = best.displayName;
            }
            else if (best == null)
            {
                GazedActorId = "";
                GazedDisplayName = "";
            }
            // (keep last focus while dwell builds — brief glances don't clear it)
        }
    }
}
