using UnityEngine;
using TalkOut.Actors;
using TalkOut.Core;

namespace TalkOut.Audio
{
    /// Gives one NPC a spoken voice: watches the EventLog for their lines,
    /// speaks them, and drives the character's talking wobble while audio plays.
    public class NpcSpeaker : MonoBehaviour
    {
        [Tooltip("Must match the display name used in EventLog NpcSaid entries")]
        public string actorDisplayName;

        [Tooltip("SAPI voice name fragment, e.g. 'David' or 'Zira'")]
        public string voiceName = "David";
        [Range(-10, 10)] public int rate = 0;
        [Range(-10, 10)] public int pitch = 0;

        public WobbleAnimator wobble;

        private SapiVoice voice;
        private EventLog log;

        private void Awake()
        {
            voice = new SapiVoice(voiceName, rate, pitch);
        }

        public void Attach(EventLog eventLog)
        {
            log = eventLog;
            log.EventAdded += OnEvent;
        }

        private void OnDestroy()
        {
            if (log != null) log.EventAdded -= OnEvent;
            voice?.Stop();
        }

        private void OnEvent(GameEvent gameEvent)
        {
            if (!GameSettings.VoiceEnabled) return;
            if (gameEvent.kind == EventKind.NpcSaid && gameEvent.actor == actorDisplayName)
            {
                voice.Speak(gameEvent.text);
            }
        }

        private void Update()
        {
            if (wobble != null) wobble.Talking = voice != null && voice.IsSpeaking;
        }
    }
}
