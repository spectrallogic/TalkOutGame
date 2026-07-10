using UnityEngine.SceneManagement;

namespace TalkOut.Core
{
    /// PLAY ALL mode: runs the levels in order, summing the run score.
    /// Static so it survives scene loads; deactivated on returning to the menu.
    public static class PlaylistManager
    {
        public static string[] SceneOrder = System.Array.Empty<string>();
        public static int Index { get; private set; }
        public static bool Active { get; private set; }
        public static int RunScore { get; private set; }

        public static bool IsLastLevel => Index >= SceneOrder.Length - 1;

        public static void StartAll(string[] sceneOrder)
        {
            SceneOrder = sceneOrder;
            Index = 0;
            RunScore = 0;
            Active = sceneOrder.Length > 0;
            if (Active) SceneManager.LoadScene(SceneOrder[0]);
        }

        public static void RecordWin(int levelScore)
        {
            if (Active) RunScore += levelScore;
        }

        public static void LoadNext()
        {
            if (!Active || IsLastLevel) return;
            Index++;
            SceneManager.LoadScene(SceneOrder[Index]);
        }

        public static void Stop()
        {
            Active = false;
            Index = 0;
        }
    }
}
