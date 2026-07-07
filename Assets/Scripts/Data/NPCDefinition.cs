using UnityEngine;

namespace TalkOut.Data
{
    [CreateAssetMenu(menuName = "TalkOut/NPC Definition", fileName = "NPC")]
    public class NPCDefinition : ScriptableObject
    {
        public string id;
        public string displayName;

        [Range(0, 100)] public int intelligence = 50;
        [Range(0, 100)] public int ego = 50;
        [Range(0, 100)] public int fear = 50;
        [Range(0, 100)] public int sympathy = 50;
        [Range(0, 100)] public int patience = 50;

        [TextArea(3, 8)] public string personality;

        public FaceSet faceSet;
        public GameObject prefab;

        /// Character sheet block injected into the director prompt.
        public string BuildPromptSheet()
        {
            return $"{displayName} ({id}) — Intelligence {intelligence}, Ego {ego}, Fear {fear}, " +
                   $"Sympathy {sympathy}, Patience {patience}.\nPersonality: {personality}";
        }
    }
}
