using UnityEngine;
using TalkOut.Core;

namespace TalkOut.World
{
    /// The comedy-club soundscape, fully synthesized: ambient coughs and chair
    /// creaks in the silence, chuckle ripples, full laugh waves scaled by the
    /// crowd's amusement, and low boo swells when the room turns. Reacts to the
    /// judge's mood ruling each turn.
    [RequireComponent(typeof(AudioSource))]
    public class CrowdAudioReactor : MonoBehaviour
    {
        public TurnController turnController;

        private const int SampleRate = 22050;
        private AudioSource source;
        private AudioClip cough, murmur, chuckle, laughWave, boo;
        private float nextAmbientAt;
        private System.Random rng;

        private void Start()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0.55f;
            rng = new System.Random(1234);

            cough = Render("cough", 0.5f, RenderCough);
            murmur = Render("murmur", 1.6f, RenderMurmur);
            chuckle = Render("chuckle", 1.4f, (buf) => RenderLaugh(buf, voices: 3, gain: 0.25f));
            laughWave = Render("laugh", 3.2f, (buf) => RenderLaugh(buf, voices: 14, gain: 0.5f));
            boo = Render("boo", 2.2f, RenderBoo);

            ScheduleAmbient();
            if (turnController != null) turnController.CopMoodChanged += OnMood;
        }

        private void OnDestroy()
        {
            if (turnController != null) turnController.CopMoodChanged -= OnMood;
        }

        private void Update()
        {
            // silence is never fully silent: coughs, shifting chairs
            if (Time.time >= nextAmbientAt && turnController != null &&
                turnController.Phase != TurnPhase.SceneOver && !source.isPlaying)
            {
                source.PlayOneShot(rng.NextDouble() < 0.7 ? cough : murmur, 0.5f);
                ScheduleAmbient();
            }
        }

        private void ScheduleAmbient()
        {
            nextAmbientAt = Time.time + 7f + (float)rng.NextDouble() * 9f;
        }

        private void OnMood(string mood)
        {
            float amusement = turnController.State != null ? turnController.State.GetStat("amusement") : 0f;
            switch (mood)
            {
                case "amused":
                case "warm":
                    source.PlayOneShot(amusement >= 55f ? laughWave : chuckle, Mathf.Lerp(0.5f, 1f, amusement / 100f));
                    break;
                case "angry":
                    source.PlayOneShot(boo, 0.8f);
                    break;
                case "confused":
                case "suspicious":
                    source.PlayOneShot(murmur, 0.6f);
                    break;
            }
        }

        // ------------------------------------------------------------------
        private delegate void Renderer(float[] buffer);

        private AudioClip Render(string clipName, float seconds, Renderer render)
        {
            var buffer = new float[(int)(SampleRate * seconds)];
            render(buffer);
            for (int i = 0; i < buffer.Length; i++) buffer[i] = Mathf.Clamp(buffer[i], -1f, 1f);
            var clip = AudioClip.Create(clipName, buffer.Length, 1, SampleRate, false);
            clip.SetData(buffer, 0);
            return clip;
        }

        /// One dry cough: two sharp lowpassed noise bursts.
        private void RenderCough(float[] buffer)
        {
            float last = 0f;
            foreach (var (start, length, gain) in new[] { (0.05f, 0.12f, 0.5f), (0.24f, 0.09f, 0.35f) })
            {
                int s = (int)(start * SampleRate), n = (int)(length * SampleRate);
                for (int i = 0; i < n && s + i < buffer.Length; i++)
                {
                    float envelope = Mathf.Exp(-6f * i / (float)n);
                    float noise = (float)(rng.NextDouble() * 2 - 1);
                    last += (noise - last) * 0.25f; // cheap lowpass
                    buffer[s + i] += last * gain * envelope;
                }
            }
        }

        /// Room murmur: slow filtered noise swell.
        private void RenderMurmur(float[] buffer)
        {
            float last = 0f;
            for (int i = 0; i < buffer.Length; i++)
            {
                float t = i / (float)buffer.Length;
                float envelope = Mathf.Sin(t * Mathf.PI) * 0.16f;
                float noise = (float)(rng.NextDouble() * 2 - 1);
                last += (noise - last) * 0.045f;
                buffer[i] += last * envelope * 3f;
            }
        }

        /// Laughter: many voices doing "ha" pulses at ~4.3Hz, each detuned and
        /// offset — small counts read as chuckles, large counts as a wave.
        private void RenderLaugh(float[] buffer, int voices, float gain)
        {
            for (int v = 0; v < voices; v++)
            {
                double voiceRate = 3.6 + rng.NextDouble() * 1.6;       // ha's per second
                double pitch = 160 + rng.NextDouble() * 220;           // voice tone
                int offset = (int)(rng.NextDouble() * 0.5 * SampleRate);
                float voiceGain = gain / Mathf.Sqrt(voices);
                float last = 0f;
                for (int i = offset; i < buffer.Length; i++)
                {
                    float t = (i - offset) / (float)SampleRate;
                    float total = (buffer.Length - offset) / (float)SampleRate;
                    float envelope = Mathf.Sin(Mathf.Clamp01(t / total) * Mathf.PI); // swell in+out
                    // pulse train: each "ha" = sharp attack, quick decay
                    float phase = (float)(t * voiceRate % 1.0);
                    float pulse = Mathf.Exp(-7f * phase);
                    // voiced part: tone + breath noise
                    float tone = Mathf.Sin((float)(2 * Mathf.PI * pitch * t));
                    float noise = (float)(rng.NextDouble() * 2 - 1);
                    last += (noise - last) * 0.3f;
                    buffer[i] += (tone * 0.55f + last * 0.45f) * pulse * envelope * voiceGain;
                }
            }
        }

        /// Booing: low detuned saws swelling up.
        private void RenderBoo(float[] buffer)
        {
            foreach (double freq in new[] { 105.0, 118.0, 97.0 })
            {
                double phase = 0;
                for (int i = 0; i < buffer.Length; i++)
                {
                    float t = i / (float)buffer.Length;
                    float envelope = Mathf.Sin(t * Mathf.PI) * 0.12f;
                    phase += freq / SampleRate;
                    float saw = (float)(phase % 1.0) * 2f - 1f;
                    buffer[i] += saw * envelope;
                }
            }
        }
    }
}
