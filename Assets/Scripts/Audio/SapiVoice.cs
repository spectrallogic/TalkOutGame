using System;
using System.Reflection;
using UnityEngine;

namespace TalkOut.Audio
{
    /// Windows SAPI text-to-speech via COM reflection — offline, instant, and
    /// built into every Windows box. The flat robotic delivery is a feature:
    /// it's funny. Swappable later for a neural TTS (Piper/Kokoro) behind the
    /// same surface. No-ops gracefully on machines without SAPI.
    public class SapiVoice
    {
        private const int SVSFlagsAsync = 1;
        private const int SVSFPurgeBeforeSpeak = 2;
        private const int SVSFIsXML = 8;

        private readonly Type voiceType;
        private readonly object voice;
        private readonly int pitch; // -10..10

        public bool Available => voice != null;

        public SapiVoice(string preferredVoiceName, int rate, int pitch)
        {
            this.pitch = Mathf.Clamp(pitch, -10, 10);
            try
            {
                voiceType = Type.GetTypeFromProgID("SAPI.SpVoice");
                if (voiceType == null) return;
                voice = Activator.CreateInstance(voiceType);
                Set("Rate", Mathf.Clamp(rate, -10, 10));
                Set("Volume", 100);
                TrySelectVoice(preferredVoiceName);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SapiVoice] TTS unavailable: {e.Message}");
                voice = null;
            }
        }

        public void Speak(string text)
        {
            if (!Available || string.IsNullOrWhiteSpace(text)) return;
            try
            {
                string xml = $"<pitch absmiddle='{pitch}'>{EscapeXml(text)}</pitch>";
                Invoke("Speak", xml, SVSFlagsAsync | SVSFPurgeBeforeSpeak | SVSFIsXML);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SapiVoice] Speak failed: {e.Message}");
            }
        }

        public bool IsSpeaking
        {
            get
            {
                if (!Available) return false;
                try
                {
                    // WaitUntilDone(0) returns true when idle.
                    return !(bool)Invoke("WaitUntilDone", 0);
                }
                catch { return false; }
            }
        }

        public void Stop()
        {
            if (!Available) return;
            try { Invoke("Speak", "", SVSFlagsAsync | SVSFPurgeBeforeSpeak); }
            catch { }
        }

        private void TrySelectVoice(string nameFragment)
        {
            if (string.IsNullOrEmpty(nameFragment)) return;
            try
            {
                object voices = Invoke("GetVoices", "", "");
                int count = (int)voices.GetType().InvokeMember(
                    "Count", BindingFlags.GetProperty, null, voices, null);
                for (int i = 0; i < count; i++)
                {
                    object token = voices.GetType().InvokeMember(
                        "Item", BindingFlags.InvokeMethod, null, voices, new object[] { i });
                    string description = (string)token.GetType().InvokeMember(
                        "GetDescription", BindingFlags.InvokeMethod, null, token, new object[] { 0 });
                    if (description.IndexOf(nameFragment, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        Set("Voice", token);
                        return;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SapiVoice] Voice select failed (using default): {e.Message}");
            }
        }

        private object Invoke(string member, params object[] args) =>
            voiceType.InvokeMember(member, BindingFlags.InvokeMethod, null, voice, args);

        private void Set(string property, object value) =>
            voiceType.InvokeMember(property, BindingFlags.SetProperty, null, voice, new[] { value });

        private static string EscapeXml(string text) =>
            text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    }
}
