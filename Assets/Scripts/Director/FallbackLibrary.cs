namespace TalkOut.Directing
{
    /// In-fiction canned material used whenever an LLM call fails
    /// (timeout, model missing). The scene always keeps moving.
    public static class FallbackLibrary
    {
        private static int next;

        private static readonly string[] CopLines =
        {
            "Uh-huh. Say that again, slower.",
            "Sir, I've been doing this for twenty-two years. Try me.",
            "That's... something. License and registration.",
            "I'm going to pretend I didn't hear that.",
            "Interesting. The radar gun disagrees."
        };

        public static string GetCopLine(string reason)
        {
            UnityEngine.Debug.LogWarning($"[Fallback] Cop line used: {reason}");
            return CopLines[next++ % CopLines.Length];
        }

        public static JudgeVerdict GetVerdict(string reason)
        {
            UnityEngine.Debug.LogWarning($"[Fallback] Judge verdict used: {reason}");
            return new JudgeVerdict
            {
                Released = false,
                Arrested = false,
                CopMood = "confused",
                IsFallback = true,
                RawOutput = $"[fallback: {reason}]"
            };
        }
    }
}
