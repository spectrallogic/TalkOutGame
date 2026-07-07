using UnityEngine;
using TalkOut.Data;
using TalkOut.Directing;
using TalkOut.Props;
using TalkOut.Save;
using TalkOut.World;

namespace TalkOut.Core
{
    /// Scene bootstrapper: picks the director implementation, initializes the
    /// turn controller, and kicks off model warmup.
    public class GameManager : MonoBehaviour
    {
        [Header("Data")]
        public ScenarioDefinition scenario;
        public LlmConfig llmConfig;

        [Header("Director")]
        [Tooltip("Use the keyword-based MockDirector instead of the local LLM")]
        public bool useMockDirector = true;

        [Header("Wiring")]
        public TurnController turnController;

        public IDirector Director { get; private set; }

        private void Awake()
        {
            if (turnController == null) turnController = GetComponent<TurnController>();
        }

        private async void Start()
        {
            if (scenario == null)
            {
                Debug.LogError("[GameManager] No scenario assigned.");
                return;
            }

            Director = CreateDirector();
            var performer = FindObjectOfType<WorldPerformer>();
            turnController.Initialize(scenario, Director, performer);
            turnController.SceneEnded += outcome => SaveSystem.RecordOutcome(scenario.scenarioId, outcome);

            var propRegistry = FindObjectOfType<PropRegistry>();
            if (propRegistry != null) propRegistry.ValidateCatalog(scenario);

            // Warm the model up behind the intro beat so turn 1 isn't slow.
            var warmupRequest = new DirectorRequest
            {
                Scenario = scenario,
                State = turnController.State,
                AvailableActions = turnController.ComputeAvailableActions(),
                RespondingNpc = scenario.GetNpc(scenario.respondingNpcId)
            };
            await Director.WarmupAsync(warmupRequest);
        }

        private IDirector CreateDirector()
        {
            if (useMockDirector)
            {
                Debug.Log("[GameManager] Using MockDirector.");
                return new MockDirector();
            }

            var llmDirector = GetComponent<LlmDirector>();
            if (llmDirector == null)
            {
                Debug.LogWarning("[GameManager] No LlmDirector component found — falling back to MockDirector.");
                return new MockDirector();
            }
            llmDirector.Configure(llmConfig);
            return llmDirector;
        }
    }
}
