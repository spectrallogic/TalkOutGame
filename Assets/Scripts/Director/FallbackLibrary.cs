using System.Collections.Generic;

namespace TalkOut.Directing
{
    /// In-fiction canned replies used whenever the real director fails
    /// (timeout, parse error, model missing). The turn always completes.
    public static class FallbackLibrary
    {
        private static int next;

        private static readonly List<string> Lines = new List<string>
        {
            "The officer stares at you, blinking slowly.",
            "The officer opens his mouth, thinks better of it, and adjusts his belt instead.",
            "A long silence. Somewhere, a cricket files a noise complaint.",
            "The officer squints at you like you're an eye chart he can't read.",
            "The officer taps his pen against his notepad, saying nothing."
        };

        public static DirectorResult GetFallback(string reason)
        {
            var result = new DirectorResult
            {
                NpcReply = Lines[next % Lines.Count],
                IsFallback = true,
                RawOutput = $"[fallback: {reason}]"
            };
            next++;
            return result;
        }
    }
}
