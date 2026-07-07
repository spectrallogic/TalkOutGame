using System.Collections.Generic;
using UnityEngine;

namespace TalkOut.Data
{
    /// One approved scene action the LLM director may pick. Everything the engine
    /// does in response (flags, movement, animation, narration) lives here as data.
    [CreateAssetMenu(menuName = "TalkOut/Action Definition", fileName = "Action")]
    public class ActionDefinition : ScriptableObject
    {
        [Tooltip("Token the LLM emits, e.g. OfficerWriteTicket")]
        public string id;

        [Tooltip("One-line description sent to the LLM, e.g. 'officer starts writing the ticket'")]
        public string llmDescription;

        [Tooltip("Italic beat line shown in the dialogue history, e.g. 'The officer taps his ticket pad.'")]
        public string narrationText;

        [Tooltip("officer / passenger / empty for scene-level actions")]
        public string actorId;

        [Tooltip("Optional prop this action targets (highlight + animate)")]
        public string targetPropId;

        [Tooltip("Pose key for SimplePoseAnimator, e.g. scribble, headBob, panicShake")]
        public string animationKey;

        [Tooltip("If set, the actor walks to this LocationPoint id")]
        public string moveToLocationId;

        [Tooltip("Face texture shown while this action executes, e.g. amused")]
        public string expressionOverride;

        [Tooltip("Engine-side state mutations. Flags/locations change ONLY here, never from the LLM.")]
        public List<StatEffect> engineEffects = new List<StatEffect>();

        [Tooltip("Action is offered to the LLM this turn only if ALL conditions hold")]
        public List<StateCondition> availabilityConditions = new List<StateCondition>();

        [Tooltip("Hard scene ending (EndSceneWin/EndSceneFail style actions)")]
        public bool endsScene;

        [Tooltip("Outcome rule id forced when endsScene is true")]
        public string outcomeId;

        public bool IsAvailable(Core.SceneStateModel state)
        {
            foreach (var condition in availabilityConditions)
            {
                if (!condition.Evaluate(state)) return false;
            }
            return true;
        }
    }
}
