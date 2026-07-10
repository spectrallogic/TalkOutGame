using System;
using UnityEngine;
using TalkOut.Core;

namespace TalkOut.Audio
{
    public enum MusicStyle { Menu, Night, Romantic, Royal }

    /// Procedurally synthesized background loop — no audio assets. A jazzy
    /// ii-V-I-ish progression with triangle bass, soft chord stabs, a seeded
    /// pentatonic melody and brushed hats. Deliberately lo-fi and quiet.
    [RequireComponent(typeof(AudioSource))]
    public class MusicPlayer : MonoBehaviour
    {
        public MusicStyle style = MusicStyle.Menu;

        private const int SampleRate = 44100;
        private AudioSource source;

        private void Start()
        {
            source = GetComponent<AudioSource>();
            source.clip = Compose();
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            ApplyVolume();
            source.Play();
            GameSettings.Changed += ApplyVolume;
        }

        private void OnDestroy()
        {
            GameSettings.Changed -= ApplyVolume;
        }

        private void ApplyVolume()
        {
            if (source != null) source.volume = GameSettings.MusicVolume;
        }

        // ------------------------------------------------------------------
        private AudioClip Compose()
        {
            float bpm = style == MusicStyle.Menu ? 104f
                : style == MusicStyle.Night ? 96f
                : style == MusicStyle.Royal ? 92f : 82f;
            double beat = 60.0 / bpm;
            int bars = 8;
            int totalSamples = (int)(SampleRate * beat * 4 * bars);
            var buffer = new float[totalSamples];
            var rng = new System.Random((int)style * 7919 + 42); // deterministic per style

            // C-rooted: Cmaj7 / Am7 / Dm7 / G7 twice around — except Royal,
            // which walks a minor court progression: Am / F / C / E
            int[] barRoots = style == MusicStyle.Royal
                ? new[] { -3, 5, 0, 4, -3, 5, 0, 4 }
                : new[] { 0, -3, 2, -5, 0, -3, 2, -5 };
            int[][] chordIntervals = style == MusicStyle.Royal
                ? new[]
                {
                    new[] { 0, 3, 7, 12 },  // minor
                    new[] { 0, 4, 7, 11 },  // maj7
                    new[] { 0, 4, 7, 12 },  // major
                    new[] { 0, 4, 7, 10 },  // dom (E7 pull back to Am)
                }
                : new[]
                {
                    new[] { 0, 4, 7, 11 },  // maj7
                    new[] { 0, 3, 7, 10 },  // m7
                    new[] { 0, 3, 7, 10 },  // m7
                    new[] { 0, 4, 7, 10 },  // dom7
                };
            int[] pentatonic = { 0, 2, 4, 7, 9, 12, 14 };

            const double bassRoot = 65.41;   // C2
            const double chordRoot = 130.81; // C3
            const double melodyRoot = 261.63; // C4

            for (int bar = 0; bar < bars; bar++)
            {
                double barStart = bar * 4 * beat;
                int root = barRoots[bar];
                int[] chord = chordIntervals[bar % 4];

                // bass: root on 1, fifth on 3 (Night walks quarters)
                if (style == MusicStyle.Night)
                {
                    int[] walk = { 0, 7, 10, 7 };
                    for (int b = 0; b < 4; b++)
                    {
                        Note(buffer, barStart + b * beat, beat * 0.9, Freq(bassRoot, root + walk[b]), 0.20f, Wave.Triangle);
                    }
                }
                else
                {
                    Note(buffer, barStart, beat * 1.8, Freq(bassRoot, root), 0.22f, Wave.Triangle);
                    Note(buffer, barStart + 2 * beat, beat * 1.8, Freq(bassRoot, root + 7), 0.18f, Wave.Triangle);
                }

                // chords
                if (style == MusicStyle.Romantic || style == MusicStyle.Royal)
                {
                    foreach (int interval in chord) // sustained pad
                    {
                        Note(buffer, barStart, beat * 3.9, Freq(chordRoot, root + interval), 0.045f, Wave.Soft);
                    }
                }
                else
                {
                    foreach (double offbeat in new[] { 1.5, 3.5 }) // stabs on the and-of-2 / and-of-4
                    {
                        foreach (int interval in chord)
                        {
                            Note(buffer, barStart + offbeat * beat, beat * 0.4, Freq(chordRoot, root + interval), 0.05f, Wave.Soft);
                        }
                    }
                }

                // melody: sparse seeded eighths, pentatonic over the root
                for (int eighth = 0; eighth < 8; eighth++)
                {
                    if (rng.NextDouble() > (style == MusicStyle.Romantic || style == MusicStyle.Royal ? 0.30 : 0.42)) continue;
                    int degree = pentatonic[rng.Next(pentatonic.Length)];
                    double length = beat * (rng.NextDouble() > 0.7 ? 1.0 : 0.5) * 0.9;
                    Note(buffer, barStart + eighth * 0.5 * beat, length, Freq(melodyRoot, root + degree), 0.085f, Wave.Sine);
                }

                // hats: brushed 8ths (not for the mellow styles)
                if (style != MusicStyle.Romantic && style != MusicStyle.Royal)
                {
                    for (int eighth = 0; eighth < 8; eighth++)
                    {
                        float gain = eighth % 2 == 0 ? 0.020f : 0.035f;
                        Noise(buffer, barStart + eighth * 0.5 * beat, 0.025, gain, rng);
                    }
                }
            }

            // gentle soft-clip so overlaps never crackle
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = (float)Math.Tanh(buffer[i] * 1.4) * 0.8f;
            }

            var clip = AudioClip.Create($"talkout_{style}", totalSamples, 1, SampleRate, false);
            clip.SetData(buffer, 0);
            return clip;
        }

        private enum Wave { Sine, Triangle, Soft }

        private static double Freq(double root, int semitones) => root * Math.Pow(2.0, semitones / 12.0);

        private static void Note(float[] buffer, double startSec, double durSec, double freq, float gain, Wave wave)
        {
            int start = (int)(startSec * SampleRate);
            int length = (int)(durSec * SampleRate);
            for (int i = 0; i < length; i++)
            {
                int index = start + i;
                if (index >= buffer.Length) break;
                double t = i / (double)SampleRate;
                double phase = 2.0 * Math.PI * freq * t;

                double sample;
                switch (wave)
                {
                    case Wave.Triangle:
                        sample = 2.0 / Math.PI * Math.Asin(Math.Sin(phase));
                        break;
                    case Wave.Soft: // rounded square: fundamental + a little 3rd harmonic
                        sample = Math.Sin(phase) + 0.35 * Math.Sin(3 * phase);
                        break;
                    default:
                        sample = Math.Sin(phase + 0.06 * Math.Sin(2.0 * Math.PI * 5.0 * t)); // light vibrato
                        break;
                }

                double attack = Math.Min(1.0, t / 0.012);
                double release = Math.Min(1.0, (durSec - t) / 0.06);
                buffer[index] += (float)(sample * gain * attack * Math.Max(0, release));
            }
        }

        private static void Noise(float[] buffer, double startSec, double durSec, float gain, System.Random rng)
        {
            int start = (int)(startSec * SampleRate);
            int length = (int)(durSec * SampleRate);
            for (int i = 0; i < length; i++)
            {
                int index = start + i;
                if (index >= buffer.Length) break;
                float envelope = 1f - i / (float)length;
                buffer[index] += (float)(rng.NextDouble() * 2 - 1) * gain * envelope * envelope;
            }
        }
    }
}
