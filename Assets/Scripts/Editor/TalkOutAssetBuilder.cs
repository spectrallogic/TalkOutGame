using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using TalkOut.Data;

namespace TalkOut.EditorTools
{
    /// Authors every Traffic Stop data asset (NPCs, actions, props, outcomes,
    /// scenario, faces, materials, panel settings) idempotently — safe to re-run;
    /// existing assets are updated in place so scene references survive.
    public static class TalkOutAssetBuilder
    {
        private const string Root = "Assets/GameData/Scenarios/TrafficStop";

        public static readonly Color OfficerSkin = new Color(0.87f, 0.70f, 0.55f);
        public static readonly Color PassengerSkin = new Color(0.70f, 0.52f, 0.38f);

        [MenuItem("Tools/TalkOut/1. Generate Face Textures")]
        public static void GenerateFaces()
        {
            FaceTextureGenerator.GenerateFor("Officer", OfficerSkin);
            FaceTextureGenerator.GenerateFor("Passenger", PassengerSkin);
            AssetDatabase.SaveAssets();
            Debug.Log("[TalkOut] Face textures generated.");
        }

        [MenuItem("Tools/TalkOut/2. Build Scenario Assets")]
        public static void BuildAssets()
        {
            BuildMaterials();
            var officerFaces = BuildFaceSet("Officer");
            var passengerFaces = BuildFaceSet("Passenger");

            // --- NPCs (stats straight from the TDD) ---
            var officer = CreateOrLoad<NPCDefinition>($"{Root}/NPCs/Officer.asset");
            officer.id = "officer";
            officer.displayName = "Officer Glazer";
            officer.intelligence = 80; officer.ego = 40; officer.fear = 20;
            officer.sympathy = 60; officer.patience = 70;
            officer.personality =
                "By-the-book traffic cop, 22 years on the force. Seen everything, surprised by nothing — until tonight. " +
                "Dry as toast, secretly desperate for entertainment. Speaks in short, clipped cop sentences.";
            officer.faceSet = officerFaces;
            EditorUtility.SetDirty(officer);

            var passenger = CreateOrLoad<NPCDefinition>($"{Root}/NPCs/Passenger.asset");
            passenger.id = "passenger";
            passenger.displayName = "Benny";
            passenger.intelligence = 50; passenger.ego = 30; passenger.fear = 90;
            passenger.sympathy = 40; passenger.patience = 40;
            passenger.personality =
                "The player's best friend, riding shotgun. Loyal but a complete coward with zero poker face. " +
                "Panics under the slightest pressure and overshares catastrophically.";
            passenger.faceSet = passengerFaces;
            EditorUtility.SetDirty(passenger);

            // --- Props ---
            var props = new List<PropDefinition>
            {
                Prop("ticketPad", "Ticket Pad", "ticketPad: the officer's ticket pad, hangs off his belt", new Color(1f, 0.9f, 0.3f)),
                Prop("radio", "Shoulder Radio", "radio: the officer's shoulder radio, used to call backup", new Color(0.4f, 0.8f, 1f)),
                Prop("flashlight", "Flashlight", "flashlight: the officer's heavy flashlight", new Color(1f, 1f, 0.7f)),
                Prop("license", "Driver's License", "license: the player's driver's license on the dashboard", new Color(0.6f, 1f, 0.6f)),
                Prop("coffee", "Coffee Cup", "coffee: the officer's coffee cup resting on the police car", new Color(0.9f, 0.6f, 0.3f)),
                Prop("horn", "Car Horn", "horn: the player's car horn on the steering wheel", new Color(1f, 0.5f, 0.5f)),
            };

            // --- Actions (TDD catalog + prop actions) ---
            var actions = new List<ActionDefinition>
            {
                Action("OfficerWalkToDriverWindow", "officer walks to the driver's window",
                    "The officer walks up to your window.", "officer",
                    move: "DriverWindow"),

                Action("OfficerWalkToPassengerWindow", "officer walks around to the passenger window to question the passenger",
                    "The officer walks around to the passenger window.", "officer",
                    move: "PassengerWindow",
                    effects: E(StatEffect.Flag("passengerQuestioned", true))),

                Action("OfficerInspectLicense", "officer takes a hard look at the driver's license",
                    "The officer inspects your license.", "officer",
                    prop: "license", anim: "leanIn"),

                Action("OfficerWriteTicket", "officer starts writing the ticket",
                    "The officer starts writing a ticket.", "officer",
                    prop: "ticketPad", anim: "scribble",
                    effects: E(StatEffect.Flag("ticketWritten", true)),
                    conditions: C(StateCondition.Flag("ticketWritten", false))),

                Action("OfficerLaugh", "officer cracks up despite himself",
                    "The officer bursts out laughing, then tries to disguise it as a cough.", "officer",
                    anim: "laugh", expression: "amused"),

                Action("OfficerGetConfused", "officer is genuinely baffled by what he just heard",
                    "The officer looks deeply, existentially confused.", "officer",
                    anim: "headTilt", expression: "confused"),

                Action("OfficerCallBackup", "officer radios for backup — serious escalation",
                    "The officer steps back and calls for backup.", "officer",
                    prop: "radio", anim: "armRaiseL",
                    effects: E(StatEffect.Flag("backupCalled", true)),
                    conditions: C(StateCondition.Stat("suspicion", Comparator.Greater, 60),
                                  StateCondition.Flag("backupCalled", false))),

                Action("OfficerTapTicketPad", "officer taps his ticket pad meaningfully — a warning",
                    "The officer slowly taps his ticket pad.", "officer",
                    prop: "ticketPad", anim: "point"),

                Action("OfficerShineFlashlight", "officer shines his flashlight at the player's face",
                    "The officer shines his flashlight directly in your eyes.", "officer",
                    prop: "flashlight", anim: "armRaise"),

                Action("OfficerSipCoffee", "officer takes a long, judgmental sip of coffee",
                    "The officer takes a long sip of coffee, maintaining eye contact.", "officer",
                    prop: "coffee", anim: "sipCoffee",
                    effects: E(StatEffect.Delta("patience", 5))),

                Action("PassengerPanic", "the passenger visibly panics",
                    "Benny starts hyperventilating in the passenger seat.", "passenger",
                    anim: "panicShake", expression: "panicked"),

                Action("PassengerBlamePlayer", "the passenger throws the player under the bus",
                    "Benny points at you. \"IT WAS ALL THEIR IDEA, OFFICER.\"", "passenger",
                    anim: "point", expression: "panicked"),

                Action("PassengerStaySilent", "the passenger goes suspiciously silent",
                    "Benny stares straight ahead, perfectly still, like a very sweaty statue.", "passenger"),

                Action("PassengerHonkHorn", "the passenger accidentally honks the horn",
                    "Benny leans over and honks the horn. Nobody knows why. Not even Benny.", "passenger",
                    prop: "horn", anim: "armRaise",
                    effects: E(StatEffect.Delta("suspicion", 10))),

                Action("EndSceneWin", "END the scene: officer lets the player go — ONLY when genuinely persuaded",
                    "The officer sighs and waves you off.", "officer",
                    ends: true, outcomeId: "talked_out",
                    conditions: C(StateCondition.Stat("sympathy", Comparator.GreaterOrEqual, 55))),

                Action("EndSceneFail", "END the scene: officer is done talking and hands over the full ticket",
                    "The officer tears off the ticket and hands it over.", "officer",
                    ends: true, outcomeId: "full_ticket",
                    conditions: C(StateCondition.Flag("ticketWritten", true))),
            };

            // --- Outcomes (priority: arrest must beat full_ticket) ---
            var outcomes = new List<OutcomeRule>
            {
                Outcome("arrest", "ARRESTED", 100, false,
                    "Backup arrives. You're going downtown. Benny is already composing the group-chat message.",
                    C(StateCondition.Stat("suspicion", Comparator.GreaterOrEqual, 90),
                      StateCondition.Flag("backupCalled", true))),

                Outcome("talked_out", "TALKED YOUR WAY OUT", 90, true,
                    "The officer lets you off with a warning. You absolute legend.",
                    C(StateCondition.Stat("sympathy", Comparator.GreaterOrEqual, 80))),

                Outcome("officer_distracted", "OFFICER DISTRACTED", 85, true,
                    "The officer is laughing too hard to remember why he pulled you over. Drive away slowly.",
                    C(StateCondition.Stat("amusement", Comparator.GreaterOrEqual, 90))),

                Outcome("officer_gives_up", "OFFICER GAVE UP", 80, true,
                    "The officer decides this traffic stop is above his pay grade and walks away muttering.",
                    C(StateCondition.Stat("patience", Comparator.LessOrEqual, 0))),

                Outcome("full_ticket", "FULL TICKET", 70, false,
                    "Full ticket. Court date. The works. Benny says he 'knew it the whole time'.",
                    C(StateCondition.Stat("suspicion", Comparator.GreaterOrEqual, 100))),

                Outcome("reduced_ticket", "REDUCED TICKET", 60, false,
                    "The officer got bored and wrote you a smaller ticket just to end the conversation.",
                    null),
            };

            // --- Scenario root ---
            var scenario = CreateOrLoad<ScenarioDefinition>($"{Root}/TrafficStop_Scenario.asset");
            scenario.scenarioId = "traffic_stop";
            scenario.title = "Traffic Stop";
            scenario.sceneDescription =
                "Night. A quiet highway shoulder. Red and blue lights flash behind a beat-up sedan. " +
                "The player has been pulled over for speeding. Officer Glazer approaches the driver's window, " +
                "flashlight in hand. The player's friend Benny sits in the passenger seat, sweating.";
            scenario.playerGoal = "Talk your way out of this ticket.";
            scenario.comedyRules =
                "Stay in character no matter what. React to absurd claims with complete seriousness — that's the joke. " +
                "Never break the fourth wall or mention being an AI or a game. Keep replies to 1-2 sentences. " +
                "Be occasionally, unexpectedly persuaded by truly creative nonsense. " +
                "If the player is rude, get colder and more bureaucratic, never shouty.";
            scenario.respondingNpcId = "officer";
            scenario.stats = new List<StatDefinition>
            {
                Stat("suspicion", 40, 0, 120, "suspicious of the player"),
                Stat("patience", 70, -20, 100, "patient"),
                Stat("sympathy", 30, 0, 100, "sympathetic toward the player"),
                Stat("amusement", 10, 0, 100, "amused"),
            };
            scenario.flags = new List<FlagDefinition>
            {
                new FlagDefinition { id = "ticketWritten", initial = false },
                new FlagDefinition { id = "backupCalled", initial = false },
                new FlagDefinition { id = "passengerQuestioned", initial = false },
                new FlagDefinition { id = "playerArrested", initial = false },
            };
            scenario.initialLocations = new List<ActorLocation>
            {
                new ActorLocation { actorId = "officer", locationId = "DriverWindow" },
                new ActorLocation { actorId = "passenger", locationId = "PassengerSeat" },
            };
            scenario.npcs = new List<NPCDefinition> { officer, passenger };
            scenario.actionCatalog = actions;
            scenario.props = props;
            scenario.outcomes = outcomes;
            scenario.maxTurns = 20;
            scenario.maxTurnsOutcomeId = "reduced_ticket";
            scenario.maxStatDeltaPerTurn = 20f;
            EditorUtility.SetDirty(scenario);

            // --- LLM config ---
            var llmConfig = CreateOrLoad<LlmConfig>("Assets/GameData/LlmConfig.asset");
            llmConfig.modelFileName = "Phi-3.5-mini-instruct-Q4_K_M.gguf";
            EditorUtility.SetDirty(llmConfig);

            BuildPanelSettings();

            AssetDatabase.SaveAssets();
            Debug.Log("[TalkOut] Scenario assets built.");
        }

        // ---- helpers -----------------------------------------------------------

        private static StatDefinition Stat(string id, float initial, float min, float max, string adjective) =>
            new StatDefinition { id = id, initial = initial, min = min, max = max, adjective = adjective };

        private static List<StatEffect> E(params StatEffect[] effects) => new List<StatEffect>(effects);
        private static List<StateCondition> C(params StateCondition[] conditions) => new List<StateCondition>(conditions);

        private static PropDefinition Prop(string id, string displayName, string llmDescription, Color highlight)
        {
            var prop = CreateOrLoad<PropDefinition>($"{Root}/Props/{id}.asset");
            prop.id = id;
            prop.displayName = displayName;
            prop.llmDescription = llmDescription;
            prop.highlightColor = highlight;
            EditorUtility.SetDirty(prop);
            return prop;
        }

        private static ActionDefinition Action(
            string id, string llmDescription, string narration, string actor,
            string prop = "", string anim = "", string move = "", string expression = "",
            List<StatEffect> effects = null, List<StateCondition> conditions = null,
            bool ends = false, string outcomeId = "")
        {
            var action = CreateOrLoad<ActionDefinition>($"{Root}/Actions/{id}.asset");
            action.id = id;
            action.llmDescription = llmDescription;
            action.narrationText = narration;
            action.actorId = actor;
            action.targetPropId = prop;
            action.animationKey = anim;
            action.moveToLocationId = move;
            action.expressionOverride = expression;
            action.engineEffects = effects ?? new List<StatEffect>();
            action.availabilityConditions = conditions ?? new List<StateCondition>();
            action.endsScene = ends;
            action.outcomeId = outcomeId;
            EditorUtility.SetDirty(action);
            return action;
        }

        private static OutcomeRule Outcome(
            string id, string title, int priority, bool isWin, string resultText, List<StateCondition> conditions)
        {
            var outcome = CreateOrLoad<OutcomeRule>($"{Root}/Outcomes/{id}.asset");
            outcome.id = id;
            outcome.title = title;
            outcome.priority = priority;
            outcome.isWin = isWin;
            outcome.resultText = resultText;
            outcome.conditions = conditions ?? new List<StateCondition>();
            EditorUtility.SetDirty(outcome);
            return outcome;
        }

        private static FaceSet BuildFaceSet(string characterName)
        {
            var faceSet = CreateOrLoad<FaceSet>($"{Root}/Faces/{characterName}_FaceSet.asset");
            faceSet.faces = new List<FaceSet.FaceEntry>();
            foreach (var emotion in FaceTextureGenerator.Emotions)
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(
                    $"Assets/Art/FaceTextures/{characterName}/{emotion}.png");
                if (tex == null) continue;
                if (emotion == "neutral") faceSet.defaultFace = tex;
                faceSet.faces.Add(new FaceSet.FaceEntry { emotion = emotion, texture = tex });
            }
            EditorUtility.SetDirty(faceSet);
            return faceSet;
        }

        public static void BuildMaterials()
        {
            Mat("Skin_Officer", OfficerSkin);
            Mat("Skin_Passenger", PassengerSkin);
            Mat("Uniform_Navy", new Color(0.13f, 0.17f, 0.28f));
            Mat("Pants_Dark", new Color(0.10f, 0.11f, 0.14f));
            Mat("Shirt_Loud", new Color(0.75f, 0.35f, 0.15f));
            Mat("Car_Rust", new Color(0.45f, 0.20f, 0.12f));
            Mat("Car_Police", new Color(0.08f, 0.08f, 0.10f));
            Mat("Car_White", new Color(0.85f, 0.86f, 0.88f));
            Mat("Wheel_Black", new Color(0.06f, 0.06f, 0.06f));
            Mat("Asphalt", new Color(0.13f, 0.13f, 0.15f));
            Mat("Ground_Night", new Color(0.09f, 0.12f, 0.09f));
            Mat("Line_White", new Color(0.75f, 0.75f, 0.7f));
            Mat("Tree_Trunk", new Color(0.25f, 0.16f, 0.10f));
            Mat("Tree_Leaves", new Color(0.08f, 0.22f, 0.10f));
            Mat("Prop_Generic", new Color(0.55f, 0.55f, 0.6f));
            Mat("Prop_Coffee", new Color(0.85f, 0.82f, 0.78f));
            Mat("LightBar_Red", new Color(0.6f, 0.08f, 0.08f));
            Mat("LightBar_Blue", new Color(0.08f, 0.08f, 0.6f));

            var faceMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Face.mat");
            if (faceMat == null)
            {
                faceMat = new Material(Shader.Find("Unlit/Texture"));
                Directory.CreateDirectory("Assets/Art/Materials");
                AssetDatabase.CreateAsset(faceMat, "Assets/Art/Materials/Face.mat");
            }
        }

        public static Material Mat(string name, Color color)
        {
            string path = $"Assets/Art/Materials/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Standard"));
                Directory.CreateDirectory("Assets/Art/Materials");
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.color = color;
            mat.SetFloat("_Glossiness", 0.15f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void BuildPanelSettings()
        {
            string path = "Assets/UI/TalkOutPanelSettings.asset";
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(path);
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(panel, path);
            }
            panel.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>("Assets/UI/TalkOutTheme.tss");
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1920, 1080);
            EditorUtility.SetDirty(panel);
        }

        public static T CreateOrLoad<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
    }
}
