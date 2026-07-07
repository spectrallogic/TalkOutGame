using System;
using System.Collections.Generic;
using System.Text;

namespace TalkOut.Core
{
    public enum EventKind
    {
        PlayerSaid,
        NpcSaid,
        PlayerAction, // "The player opened the glove compartment (inside: a live hamster)."
        SceneBeat,    // "The officer walks to the window."
        System        // intro, outcome text
    }

    public struct GameEvent
    {
        public EventKind kind;
        public string actor; // display name for speech ("Officer Glazer"), empty otherwise
        public string text;

        public GameEvent(EventKind kind, string actor, string text)
        {
            this.kind = kind;
            this.actor = actor;
            this.text = text;
        }
    }

    /// The scene's single source of memory: everything anyone said or did,
    /// in order. Both the cop LLM and the judge LLM read from this transcript.
    public class EventLog
    {
        private readonly List<GameEvent> events = new List<GameEvent>();

        public event Action<GameEvent> EventAdded;

        public IReadOnlyList<GameEvent> Events => events;

        public void Add(GameEvent gameEvent)
        {
            events.Add(gameEvent);
            EventAdded?.Invoke(gameEvent);
        }

        public void Add(EventKind kind, string actor, string text) =>
            Add(new GameEvent(kind, actor, text));

        /// Formats the last maxEvents entries as a screenplay-style transcript
        /// for LLM prompts. System lines are excluded (they're meta, not scene).
        public string ToTranscript(int maxEvents = 24)
        {
            var sb = new StringBuilder();
            int start = Math.Max(0, events.Count - maxEvents);
            for (int i = start; i < events.Count; i++)
            {
                var e = events[i];
                switch (e.kind)
                {
                    case EventKind.PlayerSaid:
                        sb.AppendLine($"Driver: \"{e.text}\"");
                        break;
                    case EventKind.NpcSaid:
                        sb.AppendLine($"{e.actor}: \"{e.text}\"");
                        break;
                    case EventKind.PlayerAction:
                    case EventKind.SceneBeat:
                        sb.AppendLine($"[{e.text}]");
                        break;
                }
            }
            return sb.ToString();
        }

        public string LastNpcLine()
        {
            for (int i = events.Count - 1; i >= 0; i--)
            {
                if (events[i].kind == EventKind.NpcSaid) return events[i].text;
            }
            return "";
        }
    }
}
