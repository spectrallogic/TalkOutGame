using UnityEngine;

namespace TalkOut.Core
{
    /// One score formula for every level: fast, concise wins rank highest.
    public static class Scoring
    {
        public const int Base = 10000;
        public const int PerSecond = 30;
        public const int PerTurn = 200;
        public const int Floor = 250;

        public static int Compute(bool isWin, float seconds, int turns)
        {
            if (!isWin) return 0;
            int score = Base - Mathf.RoundToInt(seconds * PerSecond) - turns * PerTurn;
            return Mathf.Max(Floor, score);
        }
    }
}
