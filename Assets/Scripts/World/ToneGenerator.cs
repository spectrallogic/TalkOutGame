using UnityEngine;

namespace TalkOut.World
{
    /// Generates a simple honk tone at startup and assigns it to the
    /// AudioSource — no audio assets needed for the MVP.
    [RequireComponent(typeof(AudioSource))]
    public class ToneGenerator : MonoBehaviour
    {
        public float frequency = 392f; // car-horn-ish G4
        public float seconds = 0.45f;

        private void Start()
        {
            int sampleRate = 44100;
            int count = (int)(sampleRate * seconds);
            var samples = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)sampleRate;
                float envelope = Mathf.Clamp01(10f * (seconds - t)) * Mathf.Clamp01(40f * t);
                // two detuned squares = cheap car horn
                float a = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * frequency * t));
                float b = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * frequency * 1.26f * t));
                samples[i] = (a + b) * 0.18f * envelope;
            }
            var clip = AudioClip.Create("honk", count, 1, sampleRate, false);
            clip.SetData(samples, 0);
            GetComponent<AudioSource>().clip = clip;
        }
    }
}
