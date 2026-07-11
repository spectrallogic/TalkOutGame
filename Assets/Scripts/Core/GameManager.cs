using System.IO;
using UnityEngine;
using TalkOut.Audio;
using TalkOut.Data;
using TalkOut.Directing;
using TalkOut.Props;
using TalkOut.Save;
using TalkOut.World;

namespace TalkOut.Core
{
    /// Scene bootstrapper: picks real vs mock brains, initializes the turn
    /// controller, wires TTS speakers to the event log, records outcomes.
    public class GameManager : MonoBehaviour
    {
        [Header("Data")]
        public ScenarioDefinition scenario;
        public LlmConfig llmConfig;

        [Header("Brains")]
        [Tooltip("Force the no-model mock brains (also used automatically when the GGUF is missing)")]
        public bool useMockBrains = false;

        [Header("Wiring")]
        public TurnController turnController;
        public LlmCopBrain llmCopBrain;
        public LlmJudge llmJudge;
        public LlmSidekick llmSidekick;
        public LlmAddressee llmAddressee;

        private void Awake()
        {
            if (turnController == null) turnController = GetComponent<TurnController>();
        }

        private void Start()
        {
            if (scenario == null)
            {
                Debug.LogError("[GameManager] No scenario assigned.");
                return;
            }

            ICopBrain copBrain;
            IJudge judge;
            ISidekick sidekick = null;
            IAddressee addressee = null;
            bool wantsSidekick = !string.IsNullOrEmpty(scenario.sidekickNpcId);
            bool modelPresent = llmConfig != null && File.Exists(llmConfig.ResolveModelPath());

            if (useMockBrains || !modelPresent || llmCopBrain == null || llmJudge == null)
            {
                if (!useMockBrains)
                {
                    Debug.LogWarning("[GameManager] LLM unavailable " +
                        (modelPresent ? "(brains not wired)" : "(model file missing)") +
                        " — using mock brains.");
                }
                copBrain = new MockCopBrain();
                judge = new MockJudge();
                if (wantsSidekick)
                {
                    sidekick = new MockSidekick();
                    addressee = new MockAddressee();
                }
            }
            else
            {
                llmCopBrain.Configure(scenario, llmConfig);
                llmJudge.Configure(scenario, llmConfig);
                copBrain = llmCopBrain;
                judge = llmJudge;
                if (wantsSidekick && llmSidekick != null)
                {
                    llmSidekick.Configure(scenario, llmConfig);
                    sidekick = llmSidekick;
                }
                if (wantsSidekick && llmAddressee != null)
                {
                    llmAddressee.Configure(llmConfig);
                    addressee = llmAddressee;
                }
            }

            var performer = FindObjectOfType<WorldPerformer>();
            turnController.Initialize(scenario, copBrain, judge, performer, sidekick, addressee);

            var focusTracker = FindObjectOfType<Player.FocusTracker>();
            if (focusTracker != null)
            {
                turnController.GazeProvider = () => focusTracker.GazedActorId;
            }
            // (outcome recording + scoring happens inside TurnController at scene end)

            foreach (var speaker in FindObjectsOfType<NpcSpeaker>())
            {
                speaker.Attach(turnController.Log);
            }

            var propRegistry = FindObjectOfType<PropRegistry>();
            if (propRegistry != null) propRegistry.ValidateCatalog(scenario);
        }
    }
}
