using UnityEngine;

namespace TalkOut.Actors
{
    /// Inflatable-tube-man body language: the torso sways on Perlin noise,
    /// breathes, and gets much more animated while talking or after an impulse
    /// (something dramatic happened). Physics-jointed arms flop along for free.
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

        /// Set by NpcSpeaker while TTS audio is playing.
        public bool Talking { get; set; }

        private float impulse;   // 0..1 spike that decays
        private float seed;
        private float talkBlend; // smoothed 0..1

        private void Start()
        {
            seed = GetInstanceID() * 0.137f;
        }

        /// Something dramatic happened — flail for a moment.
        public void Impulse(float strength = 1f)
        {
            impulse = Mathf.Max(impulse, Mathf.Clamp01(strength));
        }

        private void Update()
        {
            impulse = Mathf.MoveTowards(impulse, 0f, Time.deltaTime * 0.7f);
            talkBlend = Mathf.MoveTowards(talkBlend, Talking ? 1f : 0f, Time.deltaTime * 4f);

            float energy = Mathf.Max(talkBlend, impulse);
            float degrees = Mathf.Lerp(idleDegrees, talkDegrees, energy) * (1f + impulse);
            float speed = Mathf.Lerp(idleSpeed, talkSpeed, energy) * (1f + impulse * 1.5f);

            float t = Time.time * speed + seed;
            if (torsoPivot != null)
            {
                float sideLean = (Mathf.PerlinNoise(t, seed) - 0.5f) * 2f * degrees;
                float frontLean = (Mathf.PerlinNoise(seed, t * 0.9f) - 0.5f) * 2f * degrees * 0.7f;
                float breathe = Mathf.Sin(Time.time * 1.6f + seed) * 1.2f;
                torsoPivot.localRotation = Quaternion.Euler(frontLean + breathe * 0.4f, 0f, sideLean);

                float squash = 1f + Mathf.Sin(Time.time * 1.6f + seed) * 0.015f * (1f + energy);
                torsoPivot.localScale = new Vector3(1f, squash, 1f);
            }

            if (head != null)
            {
                float nod = (Mathf.PerlinNoise(t * 1.3f, seed + 5f) - 0.5f) * 2f * headBobDegrees * energy;
                float tilt = (Mathf.PerlinNoise(seed + 9f, t * 1.1f) - 0.5f) * 2f * headBobDegrees * 0.8f * (0.3f + energy);
                head.localRotation = Quaternion.Euler(nod, 0f, tilt);
            }
        }
    }
}
