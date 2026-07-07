using System.Collections.Generic;
using TalkOut.Core;
using TalkOut.Data;

namespace TalkOut.Directing
{
    /// Everything the director needs to run one turn. The engine decides what
    /// goes in here; the director never reaches back into live game objects.
    public class DirectorRequest
    {
        public ScenarioDefinition Scenario;
        public SceneStateModel State;
        public List<ActionDefinition> AvailableActions;
        public List<DialogueLine> HistoryWindow;
        public string PlayerInput;
        public NPCDefinition RespondingNpc;
    }

    /// The validated result of one director call — the only thing the engine consumes.
    public class DirectorResult
    {
        public string NpcReply = "";
        public List<string> ActionIds = new List<string>();
        public Dictionary<string, float> StatChanges = new Dictionary<string, float>();
        public bool IsFallback;
        public string RawOutput = "";       // for the test harness / logs
        public float LatencySeconds;
    }
}
