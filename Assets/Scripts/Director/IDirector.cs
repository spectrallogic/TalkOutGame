using System;
using System.Threading;
using System.Threading.Tasks;

namespace TalkOut.Directing
{
    public interface IDirector
    {
        /// Runs one director turn. onPartialReply streams the growing npc_reply
        /// text for live typewriter display (may never fire for mock/fallback).
        /// Implementations must never throw — they return a fallback result instead.
        Task<DirectorResult> DirectAsync(
            DirectorRequest request,
            Action<string> onPartialReply,
            CancellationToken cancellationToken);

        /// Optional pre-load (model warmup). Safe to call once on scene load.
        Task WarmupAsync(DirectorRequest contextRequest);
    }
}
