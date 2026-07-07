using TalkOut.Core;

namespace TalkOut.Actors
{
    /// Derives each NPC's ambient face from hidden state. The player never sees
    /// numbers — they read the officer's squint instead.
    public static class ExpressionMapper
    {
        public static string Evaluate(string actorId, SceneStateModel state, bool lastTurnWasFallback)
        {
            if (lastTurnWasFallback) return "confused";

            if (actorId == "passenger")
            {
                if (state.GetFlag("backupCalled") || state.GetStat("suspicion") > 70) return "panicked";
                if (state.GetStat("amusement") > 60) return "amused";
                return "neutral";
            }

            // officer (default rules)
            if (state.GetStat("patience") < 25) return "angry";
            if (state.GetStat("suspicion") > 70) return "suspicious";
            if (state.GetStat("amusement") > 60) return "amused";
            if (state.GetStat("sympathy") > 65) return "warm";
            return "neutral";
        }
    }
}
