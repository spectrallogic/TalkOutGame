using System;
using UnityEngine;

namespace TalkOut.Core
{
    /// PlayerPrefs-backed settings, read by the music player, first-person rig,
    /// and TTS speakers. Changed() fires so live scenes react immediately.
    public static class GameSettings
    {
        public static event Action Changed;

        public static float MusicVolume
        {
            get => PlayerPrefs.GetFloat("talkout_music", 0.35f);
            set { PlayerPrefs.SetFloat("talkout_music", Mathf.Clamp01(value)); Notify(); }
        }

        public static float MouseSensitivity
        {
            get => PlayerPrefs.GetFloat("talkout_sensitivity", 1f);
            set { PlayerPrefs.SetFloat("talkout_sensitivity", Mathf.Clamp(value, 0.3f, 3f)); Notify(); }
        }

        public static bool VoiceEnabled
        {
            get => PlayerPrefs.GetInt("talkout_voice", 1) == 1;
            set { PlayerPrefs.SetInt("talkout_voice", value ? 1 : 0); Notify(); }
        }

        private static void Notify()
        {
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
