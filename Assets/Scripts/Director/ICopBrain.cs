using System;
using System.Threading;
using System.Threading.Tasks;
using TalkOut.Core;

namespace TalkOut.Directing
{
    /// The cop's voice: freeform, in-character dialogue with no structural
    /// constraints — this is what makes him feel like a person, not a form.
    public interface ICopBrain
    {
        /// Reply to the player's latest line. Transcript comes from the EventLog.
        Task<string> ReplyAsync(EventLog log, string playerLine,
            Action<string> onPartial, CancellationToken ct);

        /// React to something the player DID (honked, opened the glove box...).
        /// Returns empty/null if the officer chooses not to comment.
        Task<string> ReactToEventAsync(EventLog log, string eventText,
            Action<string> onPartial, CancellationToken ct);

        Task WarmupAsync();
    }

    /// The story referee: reads the transcript, rules on the outcome, sets the
    /// cop's mood, and picks physical scene actions. Grammar-constrained JSON.
    public interface IJudge
    {
        Task<JudgeVerdict> JudgeAsync(EventLog log,
            System.Collections.Generic.IReadOnlyList<Data.ActionDefinition> availableActions,
            CancellationToken ct);
    }
}
