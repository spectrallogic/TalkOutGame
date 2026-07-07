using UnityEngine;

namespace TalkOut.Data
{
    [CreateAssetMenu(menuName = "TalkOut/Prop Definition", fileName = "Prop")]
    public class PropDefinition : ScriptableObject
    {
        public string id;
        public string displayName;

        [Tooltip("One-line description sent to the LLM, e.g. 'radio: the officer's shoulder radio'")]
        public string llmDescription;

        [Tooltip("Emission pulse color when the prop is used")]
        public Color highlightColor = Color.yellow;
    }
}
