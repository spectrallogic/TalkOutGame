using System.Collections.Generic;
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

        [Tooltip("How this character's professional mask slips as the scene goes south — fed to the prompt as 'THE MASK'")]
        [TextArea(3, 8)] public string edgeProfile;

        [Tooltip("Pool of example lines in this character's voice; a few are sampled per scene as style guidance")]
        public List<string> voiceExamples = new List<string>();

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
