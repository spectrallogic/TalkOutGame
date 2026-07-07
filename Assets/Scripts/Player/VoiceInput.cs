using System;
using UnityEngine;
using Whisper;
using TalkOut.Core;

namespace TalkOut.Player
{
    /// Push-to-talk: hold V, speak, release. Local Whisper (whisper.unity)
    /// transcribes offline and the text goes through the normal turn loop.
    public class VoiceInput : MonoBehaviour
    {
        public WhisperManager whisper;
        public TurnController turnController;
        public KeyCode pushToTalkKey = KeyCode.V;

        [Tooltip("Hard cap so a stuck key can't record forever")]
        public int maxSeconds = 15;

        public event Action<bool> ListeningChanged;
        public event Action<bool> TranscribingChanged;

        private const int SampleRate = 16000;
        private AudioClip recording;
        private string micDevice;
        private bool available;
        private bool listening;
        private bool transcribing;

        private void Start()
        {
            if (Microphone.devices.Length == 0)
            {
                Debug.LogWarning("[VoiceInput] No microphone found — voice input disabled (typing still works).");
                return;
            }
            micDevice = Microphone.devices[0];
            available = whisper != null;
            if (!available)
            {
                Debug.LogWarning("[VoiceInput] No WhisperManager wired — voice input disabled.");
            }
        }

        private void Update()
        {
            if (!available || transcribing) return;

            if (!listening && Input.GetKeyDown(pushToTalkKey) &&
                turnController != null && turnController.Phase == TurnPhase.AwaitingInput)
            {
                StartListening();
            }
            else if (listening && (Input.GetKeyUp(pushToTalkKey) ||
                     Microphone.GetPosition(micDevice) >= SampleRate * maxSeconds - 1))
            {
                _ = StopAndTranscribeAsync();
            }
        }

        private void StartListening()
        {
            recording = Microphone.Start(micDevice, false, maxSeconds, SampleRate);
            listening = true;
            ListeningChanged?.Invoke(true);
        }

        private async System.Threading.Tasks.Task StopAndTranscribeAsync()
        {
            listening = false;
            ListeningChanged?.Invoke(false);

            int position = Microphone.GetPosition(micDevice);
            Microphone.End(micDevice);
            if (recording == null || position < SampleRate / 4) return; // < 0.25s = accidental tap

            var samples = new float[position * recording.channels];
            recording.GetData(samples, 0);

            transcribing = true;
            TranscribingChanged?.Invoke(true);
            try
            {
                var result = await whisper.GetTextAsync(samples, SampleRate, recording.channels);
                if (this == null) return;
                string text = CleanTranscript(result?.Result);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    turnController.SubmitPlayerUtterance(text);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[VoiceInput] Transcription failed: {e.Message}");
            }
            finally
            {
                transcribing = false;
                TranscribingChanged?.Invoke(false);
            }
        }

        /// Whisper emits artifacts like [BLANK_AUDIO], (wind blowing), music notes.
        private static string CleanTranscript(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            string text = System.Text.RegularExpressions.Regex.Replace(raw, @"\[[^\]]*\]|\([^)]*\)", "");
            return text.Trim();
        }
    }
}
