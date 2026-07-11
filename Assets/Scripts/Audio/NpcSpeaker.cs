using UnityEngine;
using TalkOut.Actors;
using TalkOut.Core;

namespace TalkOut.Audio
{
    /// Gives one NPC a spoken voice: watches the EventLog for their lines and
    /// speaks them — neural Piper first (lifelike, positional), Windows SAPI
    /// as the fallback. Drives the character's talking wobble while audio plays.
    public class NpcSpeaker : MonoBehaviour
    {
        [Tooltip("Must match the display name used in EventLog NpcSaid entries")]
        public string actorDisplayName;

        [Header("Neural voice (preferred)")]
        [Tooltip("Piper model file in StreamingAssets/TTS/voices, e.g. en_US-lessac-medium.onnx")]
        public string piperModel = "";

        [Header("SAPI fallback")]
        [Tooltip("SAPI voice name fragment, e.g. 'David' or 'Zira'")]
        public string voiceName = "David";
        [Range(-10, 10)] public int rate = 0;
        [Range(-10, 10)] public int pitch = 0;

        public WobbleAnimator wobble;

        private PiperVoice piper;
        private SapiVoice sapi;
        private EventLog log;

        private void Awake()
        {
            // positional mouth: 3D audio source on the character
            var source = GetComponent<AudioSource>();
            if (source == null) source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.maxDistance = 18f;

            piper = new PiperVoice(piperModel, source);
            if (!piper.Available)
            {
                sapi = new SapiVoice(voiceName, rate, pitch);
                if (!string.IsNullOrEmpty(piperModel))
                {
                    Debug.LogWarning($"[NpcSpeaker] Piper voice '{piperModel}' unavailable — " +
                                     "using Windows SAPI fallback. See StreamingAssets/TTS/README.md.");
                }
            }
        }

        public void Attach(EventLog eventLog)
        {
            log = eventLog;
            log.EventAdded += OnEvent;
        }

        private void OnDestroy()
        {
            if (log != null) log.EventAdded -= OnEvent;
            piper?.Stop();
            sapi?.Stop();
        }

        private void OnEvent(GameEvent gameEvent)
        {
            if (!GameSettings.VoiceEnabled) return;
            if (gameEvent.kind == EventKind.NpcSaid && gameEvent.actor == actorDisplayName)
            {
                if (piper.Available) piper.Speak(gameEvent.text);
                else sapi?.Speak(gameEvent.text);
            }
        }

        private void Update()
        {
            if (wobble != null)
            {
                wobble.Talking = piper.Available ? piper.IsSpeaking : sapi != null && sapi.IsSpeaking;
            }
        }
    }
}
