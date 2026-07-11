using System.Collections.Generic;
using UnityEngine;

namespace TalkOut.Actors
{
    /// Inflatable-tube-man body language, upgraded: Perlin torso sway and
    /// breathing, talking amplification, impulse flails — plus coded gestures
    /// (nod, head shake, lean) and head LOOK-AT so characters actually turn
    /// their eyes to the player while talking, waiting, or just glancing.
    /// Physics-jointed arms can be shoved with real forces for gestures.
    public class WobbleAnimator : MonoBehaviour
    {
        [Tooltip("Pivot at the hips — everything above wobbles")]
        public Transform torsoPivot;
        public Transform head;

        [Header("Idle sway")]
        public float idleDegrees = 3.5f;
        public float idleSpeed = 0.6f;

        [Header("Talking sway")]
        public float talkDegrees = 11f;
        public float talkSpeed = 2.2f;
        public float headBobDegrees = 7f;

        [Header("Look-at")]
        [Tooltip("Defaults to the main camera (the player)")]
        public Transform lookTarget;
        public float maxLookAngle = 65f;

        /// Set by NpcSpeaker while TTS audio is playing.
        public bool Talking { get; set; }

        /// Set while the player is keeping them waiting: expectant head tilt.
        public bool WaitingForPlayer { get; set; }

        private float impulse;
        private float seed;
        private float talkBlend;
        private float waitBlend;
        private float lookBlend;
        private float nextGlanceAt;
        private float glanceUntil;

        private string gesture = "";
        private float gestureT;
        private readonly List<Rigidbody> armBodies = new List<Rigidbody>();

        private void Start()
        {
            seed = GetInstanceID() * 0.137f;
            if (lookTarget == null && Camera.main != null) lookTarget = Camera.main.transform;
            foreach (var rb in GetComponentsInChildren<Rigidbody>())
            {
                if (!rb.isKinematic) armBodies.Add(rb);
            }
            ScheduleGlance();
        }

        /// Something dramatic happened — flail for a moment.
        public void Impulse(float strength = 1f)
        {
            impulse = Mathf.Max(impulse, Mathf.Clamp01(strength));
        }

        /// Coded gestures usable from actions/templates. Returns false when
        /// the key isn't a gesture (caller can fall back to Impulse).
        public bool PlayGesture(string key)
        {
            switch (key)
            {
                case "nod":
                case "headShake":
                case "leanIn":
                case "leanBack":
                    gesture = key;
                    gestureT = 0f;
                    return true;
                case "armRaise":
                    if (armBodies.Count > 0)
                    {
                        armBodies[0].AddForce(Vector3.up * 3.2f + transform.forward * 0.8f, ForceMode.Impulse);
                    }
                    return true;
                case "armFlail":
                case "panicShake":
                    foreach (var arm in armBodies)
                    {
                        arm.AddForce(Random.insideUnitSphere * 2.6f + Vector3.up * 2f, ForceMode.Impulse);
                    }
                    Impulse(0.9f);
                    return true;
                case "laugh":
                    gesture = "nod";
                    gestureT = 0f;
                    Impulse(0.8f);
                    return true;
                case "scribble":
                    if (armBodies.Count > 1)
                    {
                        armBodies[1].AddForce(Vector3.up * 1.5f, ForceMode.Impulse);
                    }
                    Impulse(0.4f);
                    return true;
                default:
                    return false;
            }
        }

        private void ScheduleGlance()
        {
            nextGlanceAt = Time.time + 5f + (seed * 7919f % 1f) * 6f + Random.value * 5f;
        }

        private void Update()
        {
            impulse = Mathf.MoveTowards(impulse, 0f, Time.deltaTime * 0.7f);
            talkBlend = Mathf.MoveTowards(talkBlend, Talking ? 1f : 0f, Time.deltaTime * 4f);
            waitBlend = Mathf.MoveTowards(waitBlend, WaitingForPlayer ? 1f : 0f, Time.deltaTime * 1.5f);

            // random idle glances at the player
            if (Time.time >= nextGlanceAt)
            {
                glanceUntil = Time.time + 1.2f + Random.value * 1.8f;
                ScheduleGlance();
            }
            bool wantsLook = Talking || WaitingForPlayer || Time.time < glanceUntil;
            lookBlend = Mathf.MoveTowards(lookBlend, wantsLook ? 1f : 0f, Time.deltaTime * 2.5f);

            float energy = Mathf.Max(talkBlend, impulse);
            float degrees = Mathf.Lerp(idleDegrees, talkDegrees, energy) * (1f + impulse);
            float speed = Mathf.Lerp(idleSpeed, talkSpeed, energy) * (1f + impulse * 1.5f);

            float t = Time.time * speed + seed;
            float gesturePitch = 0f, gestureYaw = 0f, torsoGesturePitch = 0f;
            UpdateGesture(ref gesturePitch, ref gestureYaw, ref torsoGesturePitch);

            if (torsoPivot != null)
            {
                float sideLean = (Mathf.PerlinNoise(t, seed) - 0.5f) * 2f * degrees;
                float frontLean = (Mathf.PerlinNoise(seed, t * 0.9f) - 0.5f) * 2f * degrees * 0.7f;
                float breathe = Mathf.Sin(Time.time * 1.6f + seed) * 1.2f;
                torsoPivot.localRotation = Quaternion.Euler(
                    frontLean + breathe * 0.4f + torsoGesturePitch, 0f, sideLean);

                float squash = 1f + Mathf.Sin(Time.time * 1.6f + seed) * 0.015f * (1f + energy);
                torsoPivot.localScale = new Vector3(1f, squash, 1f);
            }

            if (head != null)
            {
                float nod = (Mathf.PerlinNoise(t * 1.3f, seed + 5f) - 0.5f) * 2f * headBobDegrees * energy;
                float tilt = (Mathf.PerlinNoise(seed + 9f, t * 1.1f) - 0.5f) * 2f * headBobDegrees * 0.8f * (0.3f + energy);
                tilt += waitBlend * 16f;
                nod += waitBlend * Mathf.Sin(Time.time * 0.8f + seed) * 2.5f;

                var wobbleRotation = Quaternion.Euler(nod + gesturePitch, gestureYaw, tilt);

                // look-at: blend the head toward the player, clamped so necks stay attached
                if (lookBlend > 0.01f && lookTarget != null && head.parent != null)
                {
                    Vector3 localDir = head.parent.InverseTransformDirection(
                        (lookTarget.position - head.position).normalized);
                    var lookRotation = Quaternion.LookRotation(localDir);
                    if (Quaternion.Angle(Quaternion.identity, lookRotation) <= maxLookAngle)
                    {
                        head.localRotation = Quaternion.Slerp(wobbleRotation, lookRotation * Quaternion.Euler(gesturePitch, gestureYaw, 0), lookBlend * 0.85f);
                        return;
                    }
                }
                head.localRotation = wobbleRotation;
            }
        }

        private void UpdateGesture(ref float pitch, ref float yaw, ref float torsoPitch)
        {
            if (string.IsNullOrEmpty(gesture)) return;
            gestureT += Time.deltaTime;
            switch (gesture)
            {
                case "nod": // two clear nods
                    if (Done(1.0f)) return;
                    pitch = Mathf.Sin(gestureT * Mathf.PI * 4f) * 14f * Envelope(1.0f);
                    break;
                case "headShake": // three shakes
                    if (Done(1.2f)) return;
                    yaw = Mathf.Sin(gestureT * Mathf.PI * 5f) * 18f * Envelope(1.2f);
                    break;
                case "leanIn":
                    if (Done(1.5f)) return;
                    torsoPitch = 13f * Envelope(1.5f);
                    break;
                case "leanBack":
                    if (Done(1.5f)) return;
                    torsoPitch = -11f * Envelope(1.5f);
                    break;
            }
        }

        private bool Done(float duration)
        {
            if (gestureT < duration) return false;
            gesture = "";
            return true;
        }

        private float Envelope(float duration) =>
            Mathf.Sin(Mathf.Clamp01(gestureT / duration) * Mathf.PI);
    }
}
