using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace TalkOut.Audio
{
    /// Neural local TTS via the Piper CLI (StreamingAssets/TTS). Each utterance
    /// is synthesized to a temp WAV by a short-lived subprocess, parsed, and
    /// played through the character's own AudioSource — so voices are lifelike
    /// AND positional. No cloud, no keys.
    public class PiperVoice
    {
        private readonly string exePath;
        private readonly string modelPath;
        private readonly AudioSource source;
        private bool synthesizing;
        private int generation; // invalidates stale synth results

        public bool Available { get; }

        public bool IsSpeaking => synthesizing || (source != null && source.isPlaying);

        public PiperVoice(string voiceModelFileName, AudioSource output)
        {
            source = output;
            string ttsRoot = Path.Combine(Application.streamingAssetsPath, "TTS");
            exePath = Path.Combine(ttsRoot, "piper", "piper.exe");
            modelPath = Path.Combine(ttsRoot, "voices", voiceModelFileName ?? "");
            Available = !string.IsNullOrEmpty(voiceModelFileName)
                        && File.Exists(exePath) && File.Exists(modelPath) && source != null;
        }

        public async void Speak(string text)
        {
            if (!Available || string.IsNullOrWhiteSpace(text)) return;
            int myGeneration = ++generation;
            synthesizing = true;
            try
            {
                string wavPath = Path.Combine(Application.temporaryCachePath, $"piper_{myGeneration}.wav");
                // punctuation drives pacing; ellipses become real beats via sentence silence
                string line = text.Replace("\r", " ").Replace("\n", " ").Replace("…", "...").Trim();

                var clipData = await Task.Run(() => Synthesize(line, wavPath));
                if (clipData == null || myGeneration != generation || source == null) return;

                var clip = AudioClip.Create("piper", clipData.samples.Length, 1, clipData.sampleRate, false);
                clip.SetData(clipData.samples, 0);
                source.Stop();
                source.clip = clip;
                source.Play();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PiperVoice] Synthesis failed: {e.Message}");
            }
            finally
            {
                if (myGeneration == generation) synthesizing = false;
            }
        }

        public void Stop()
        {
            generation++;
            synthesizing = false;
            if (source != null) source.Stop();
        }

        private class WavData
        {
            public float[] samples;
            public int sampleRate;
        }

        private WavData Synthesize(string text, string wavPath)
        {
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = $"--model \"{modelPath}\" --output_file \"{wavPath}\" --sentence_silence 0.45",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                WorkingDirectory = Path.GetDirectoryName(exePath),
            };
            using (var process = Process.Start(psi))
            {
                process.StandardInput.WriteLine(text);
                process.StandardInput.Close();
                if (!process.WaitForExit(20000))
                {
                    try { process.Kill(); } catch { }
                    Debug.LogWarning("[PiperVoice] Synthesis timed out.");
                    return null;
                }
            }

            if (!File.Exists(wavPath)) return null;
            var data = ParseWav(File.ReadAllBytes(wavPath));
            try { File.Delete(wavPath); } catch { }
            return data;
        }

        /// Minimal 16-bit PCM WAV parser (piper outputs 22.05kHz mono PCM16).
        private static WavData ParseWav(byte[] bytes)
        {
            if (bytes.Length < 44) return null;
            int sampleRate = BitConverter.ToInt32(bytes, 24);
            short bitsPerSample = BitConverter.ToInt16(bytes, 34);
            if (bitsPerSample != 16) return null;

            // find the "data" chunk (piper may include extra chunks)
            int pos = 12;
            while (pos + 8 <= bytes.Length)
            {
                string chunkId = System.Text.Encoding.ASCII.GetString(bytes, pos, 4);
                int chunkSize = BitConverter.ToInt32(bytes, pos + 4);
                if (chunkId == "data")
                {
                    int count = Mathf.Min(chunkSize, bytes.Length - pos - 8) / 2;
                    var samples = new float[count];
                    for (int i = 0; i < count; i++)
                    {
                        samples[i] = BitConverter.ToInt16(bytes, pos + 8 + i * 2) / 32768f;
                    }
                    return new WavData { samples = samples, sampleRate = sampleRate };
                }
                pos += 8 + chunkSize + (chunkSize & 1);
            }
            return null;
        }
    }
}
