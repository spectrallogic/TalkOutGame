using System;
using System.Threading;
using System.Threading.Tasks;
using TalkOut.Core;

namespace TalkOut.Directing
{
    /// One cop turn, separated so narration is never spoken by the TTS:
    /// Spoken goes to the voice + chat bubble; Narration becomes an italic,
    /// silent beat line ("The officer glances over.").
    public class CopReply
    {
        public string Spoken = "";
        public string Narration = "";

        public bool IsSilent => string.IsNullOrEmpty(Spoken) && string.IsNullOrEmpty(Narration);
    }

    /// The cop's voice: freeform, in-character dialogue with no structural
    /// constraints — this is what makes him feel like a person, not a form.
    /// His current emotional state (SceneStateModel meters) colors every reply.
    public interface ICopBrain
    {
        /// Reply to the player's latest line.
        Task<CopReply> ReplyAsync(EventLog log, SceneStateModel state, string playerLine,
            Action<string> onPartial, CancellationToken ct);

        /// React to something the player DID (honked, opened the glove box...).
        /// timesHappened lets a repeat offense land harder than the first.
        /// Returns a silent CopReply if the officer chooses not to comment.
        Task<CopReply> ReactToEventAsync(EventLog log, SceneStateModel state, string eventText,
            int timesHappened, Action<string> onPartial, CancellationToken ct);

        Task WarmupAsync();
    }

    /// The story referee: reads the transcript, rules on the outcome, sets the
    /// cop's mood + emotional meter deltas, and picks physical scene actions.
    public interface IJudge
    {
        Task<JudgeVerdict> JudgeAsync(EventLog log, SceneStateModel state,
            System.Collections.Generic.IReadOnlyList<Data.ActionDefinition> availableActions,
            CancellationToken ct);
    }

    /// A second character who occasionally pipes up with ONE short line
    /// (Dennis to the king, Benny to nobody in particular).
    /// Returns empty when they'd stay quiet.
    public interface ISidekick
    {
        Task<string> InterjectAsync(EventLog log, SceneStateModel state, CancellationToken ct);

        /// The player addressed THEM directly — they must actually answer.
        Task<string> ReplyAsync(EventLog log, SceneStateModel state, string playerLine, CancellationToken ct);
    }

    /// Decides who the player's line is addressed to, weighing the words
    /// against gaze evidence. Returns an actor id from the candidates,
    /// or "" when unclear.
    public interface IAddressee
    {
        Task<string> ResolveAsync(EventLog log, string playerLine, string gazedActorId,
            System.Collections.Generic.IReadOnlyList<(string id, string name)> candidates,
            CancellationToken ct);
    }
}
