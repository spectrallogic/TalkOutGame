using System.IO;
using UnityEngine;

namespace TalkOut.Data
{
    [CreateAssetMenu(menuName = "TalkOut/LLM Config", fileName = "LlmConfig")]
    public class LlmConfig : ScriptableObject
    {
        [Tooltip("GGUF file name inside StreamingAssets/Models/")]
        public string modelFileName = "Dolphin3.0-Llama3.1-8B-Q4_K_M.gguf";

        [Tooltip("Absolute path override — leave empty to use StreamingAssets/Models. Lets the player/dev swap any model in without a code change.")]
        public string overridePath = "";

        public int contextSize = 2048;
        public int maxReplyTokens = 220;
        [Range(0f, 2f)] public float temperature = 0.85f;

        [Tooltip("-1 = offload as many layers to GPU as possible, 0 = CPU only")]
        public int gpuLayers = -1;

        [Tooltip("Evaluate the static system prompt on scene load so turn 1 isn't slow")]
        public bool warmupOnLoad = true;

        [Tooltip("Seconds before an inference call falls back to a canned response")]
        public float timeoutSeconds = 45f;

        public string ResolveModelPath()
        {
            if (!string.IsNullOrEmpty(overridePath)) return overridePath;
            return Path.Combine(Application.streamingAssetsPath, "Models", modelFileName);
        }
    }
}
