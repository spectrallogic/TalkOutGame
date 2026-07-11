using System.Collections.Generic;
using UnityEngine;

namespace TalkOut.Data
{
    /// Root data asset for one playable scenario. Future scenarios are new assets,
    /// not new code: NPCs, actions, props, goals, and outcome rules all live here.
    [CreateAssetMenu(menuName = "TalkOut/Scenario Definition", fileName = "Scenario")]
    public class ScenarioDefinition : ScriptableObject
    {
        public string scenarioId;
        public string title;

        [TextArea(3, 8)] public string sceneDescription;
        [TextArea(2, 4)] public string playerGoal;
        [TextArea(3, 8)] public string comedyRules;

        [Tooltip("Fixed line the main NPC always opens the scene with")]
        [TextArea(2, 4)] public string openerLine;

        [Tooltip("How the NPC/judge refer to the player, e.g. 'the driver', 'your date'")]
        public string playerLabel = "the driver";

        [Tooltip("Short label used in transcripts, e.g. 'Driver', 'Date'")]
        public string playerTranscriptName = "Driver";

        [Tooltip("Outcome id when the judge rules the player won (released)")]
        public string winOutcomeId = "talked_out";

        [Tooltip("Outcome id when the judge rules the situation ended badly (arrested)")]
        public string loseOutcomeId = "arrest";

        [Tooltip("Instructions for the judge LLM: what counts as the player winning (released) or losing (arrested)")]
        [TextArea(3, 8)] public string judgeGuidance;

        [Tooltip("Primary NPC the reply is voiced by (the LLM may still narrate others via actions)")]
        public string respondingNpcId = "officer";

        public List<StatDefinition> stats = new List<StatDefinition>();
        public List<FlagDefinition> flags = new List<FlagDefinition>();
        public List<ActorLocation> initialLocations = new List<ActorLocation>();

        public List<NPCDefinition> npcs = new List<NPCDefinition>();
        public List<ActionDefinition> actionCatalog = new List<ActionDefinition>();
        public List<PropDefinition> props = new List<PropDefinition>();
        public List<OutcomeRule> outcomes = new List<OutcomeRule>();

        [Tooltip("Scene ends with maxTurnsOutcomeId if no rule fired after this many player turns")]
        public int maxTurns = 20;
        public string maxTurnsOutcomeId = "full_ticket";

        [Header("Hard time limit")]
        [Tooltip("Seconds before the NPC ends the scene themselves (0 = no limit)")]
        public float timeLimitSeconds = 300f;
        [Tooltip("What the NPC says when time runs out")]
        [TextArea(2, 3)] public string timeoutLine;
        [Tooltip("Actions performed for the timeout ending (e.g. write ticket, storm off)")]
        public List<string> timeoutActionIds = new List<string>();
        [Tooltip("Outcome forced when time runs out")]
        public string timeoutOutcomeId = "full_ticket";

        [Tooltip("Variant pools for all instructional prompt text; null falls back to defaults")]
        public PromptStyleLibrary promptStyle;

        [Tooltip("In-fiction canned NPC lines used when an LLM call fails")]
        public List<string> fallbackLines = new List<string>();

        [Header("Sidekick chatter")]
        [Tooltip("NPC id of a second character who occasionally interjects (empty = none)")]
        public string sidekickNpcId = "";
        [Tooltip("Chance after each exchange that the sidekick pipes up")]
        [Range(0f, 1f)] public float sidekickChatterChance = 0.3f;

        [Header("Weirdness (ITYSL dial)")]
        [Tooltip("Chance per NPC turn that a secret weird directive is injected (0-1)")]
        [Range(0f, 1f)] public float weirdnessChance = 0.25f;
        [Tooltip("Scenario-flavored weird directives, mixed into the global deck")]
        public List<string> weirdSpice = new List<string>();

        [Header("Idle player")]
        [Tooltip("Seconds of player silence before it becomes a scene event the NPC can react to")]
        public float idleNudgeSeconds = 20f;
        [Tooltip("Event text describing the player's silence")]
        [TextArea(2, 3)] public string idleEventText = "The player just sits there in silence.";

        [Tooltip("Per-turn clamp on any single stat delta proposed by the LLM")]
        public float maxStatDeltaPerTurn = 20f;

        public NPCDefinition GetNpc(string npcId)
        {
            foreach (var npc in npcs)
            {
                if (npc != null && npc.id == npcId) return npc;
            }
            return null;
        }

        public ActionDefinition GetAction(string actionId)
        {
            foreach (var action in actionCatalog)
            {
                if (action != null && action.id == actionId) return action;
            }
            return null;
        }

        public OutcomeRule GetOutcome(string outcomeIdToFind)
        {
            foreach (var outcome in outcomes)
            {
                if (outcome != null && outcome.id == outcomeIdToFind) return outcome;
            }
            return null;
        }
    }
}
