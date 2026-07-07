using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.UIElements;
using LLMUnity;
using Whisper;
using TalkOut.Actors;
using TalkOut.Audio;
using TalkOut.Core;
using TalkOut.Data;
using TalkOut.Debugging;
using TalkOut.Directing;
using TalkOut.Player;
using TalkOut.Props;
using TalkOut.UI;
using TalkOut.World;

namespace TalkOut.EditorTools
{
    /// Builds the first-person TrafficStop scene and MainMenu from scratch.
    /// Idempotent — re-running regenerates both scenes.
    public static class TalkOutSceneBuilder
    {
        private const int PostFxLayer = 8;

        [MenuItem("Tools/TalkOut/Build Everything (Graphics + Textures + Assets + Scenes)")]
        public static void BuildEverything()
        {
            ApplyGraphicsSettings();
            TalkOutAssetBuilder.GenerateFaces();
            TalkOutAssetBuilder.BuildAssets();
            BuildScenes();
            Debug.Log("[TalkOut] Build Everything finished. Open Assets/Scenes/TrafficStop.unity and press Play.");
        }

        [MenuItem("Tools/TalkOut/0. Apply Graphics Settings")]
        public static void ApplyGraphicsSettings()
        {
            PlayerSettings.colorSpace = ColorSpace.Linear;
            EnsureLayer(PostFxLayer, "PostFX");

            QualitySettings.antiAliasing = 4;
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
            QualitySettings.shadowDistance = 60f;
            QualitySettings.pixelLightCount = 8;
            Debug.Log("[TalkOut] Graphics settings applied (Linear color, 4x MSAA, soft shadows).");
        }

        [MenuItem("Tools/TalkOut/3. Build Scenes")]
        public static void BuildScenes()
        {
            BuildTrafficStopScene();
            BuildMainMenuScene();
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene("Assets/Scenes/MainMenu.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/TrafficStop.unity", true),
            };
            Debug.Log("[TalkOut] Scenes built and added to Build Settings.");
        }

        // ====================================================================
        private static void BuildTrafficStopScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var skybox = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/NightSky.mat");
            RenderSettings.skybox = skybox;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.13f, 0.15f, 0.24f);
            RenderSettings.ambientEquatorColor = new Color(0.09f, 0.10f, 0.15f);
            RenderSettings.ambientGroundColor = new Color(0.04f, 0.05f, 0.06f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.04f, 0.05f, 0.09f);
            RenderSettings.fogDensity = 0.012f;

            // --- lights ---
            var moon = new GameObject("Moonlight").AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.intensity = 0.4f;
            moon.color = new Color(0.6f, 0.68f, 0.95f);
            moon.shadows = LightShadows.Soft;
            moon.transform.rotation = Quaternion.Euler(46f, -34f, 0f);
            RenderSettings.sun = moon;

            // --- environment ---
            var env = new GameObject("Environment").transform;
            Prim(PrimitiveType.Plane, "Ground", env, new Vector3(0, -0.02f, 0), new Vector3(30, 1, 30), "Ground_Night");
            Prim(PrimitiveType.Cube, "Road", env, new Vector3(-1.2f, -0.005f, 0), new Vector3(7f, 0.02f, 240f), "Asphalt");
            for (int i = 0; i < 26; i++)
            {
                Prim(PrimitiveType.Cube, $"Dash_{i}", env,
                    new Vector3(-3.8f, 0.012f, -58f + i * 4.5f), new Vector3(0.14f, 0.02f, 1.4f), "Line_White");
            }
            // guardrail on the shoulder side
            for (int i = 0; i < 16; i++)
            {
                float z = -34f + i * 4.5f;
                Prim(PrimitiveType.Cube, $"RailPost_{i}", env, new Vector3(3.4f, 0.35f, z), new Vector3(0.12f, 0.7f, 0.12f), "Guardrail");
            }
            Prim(PrimitiveType.Cube, "Rail", env, new Vector3(3.4f, 0.62f, 0f), new Vector3(0.08f, 0.22f, 72f), "Guardrail");
            // distant mountain silhouettes
            Prim(PrimitiveType.Cube, "Mountain1", env, new Vector3(-60f, 8f, 60f), new Vector3(70f, 26f, 30f), "Mountain")
                .transform.rotation = Quaternion.Euler(0, 35f, 0);
            Prim(PrimitiveType.Cube, "Mountain2", env, new Vector3(40f, 6f, 90f), new Vector3(60f, 20f, 26f), "Mountain")
                .transform.rotation = Quaternion.Euler(0, -20f, 0);
            Prim(PrimitiveType.Cube, "Mountain3", env, new Vector3(-30f, 5f, -90f), new Vector3(80f, 16f, 24f), "Mountain");

            foreach (var pos in new[] {
                new Vector3(-9f, 0, 8f), new Vector3(-11f, 0, -6f), new Vector3(6.5f, 0, 14f),
                new Vector3(8f, 0, -12f), new Vector3(-8f, 0, 22f), new Vector3(7f, 0, 28f) })
            {
                Tree(env, pos);
            }

            // speed limit sign (the joke writes itself)
            var sign = new GameObject("SpeedSign").transform;
            sign.SetParent(env, false);
            sign.position = new Vector3(3.0f, 0, 6f);
            sign.rotation = Quaternion.Euler(0, 180f, 0);
            Prim(PrimitiveType.Cube, "Post", sign, new Vector3(0, 1.1f, 0), new Vector3(0.08f, 2.2f, 0.08f), "Guardrail");
            Prim(PrimitiveType.Cube, "Panel", sign, new Vector3(0, 2.1f, 0.05f), new Vector3(0.7f, 0.9f, 0.04f), "Car_White");
            var signText = new GameObject("Text").AddComponent<TextMesh>();
            signText.transform.SetParent(sign, false);
            signText.transform.localPosition = new Vector3(0, 2.35f, 0.08f);
            signText.transform.localScale = Vector3.one * 0.06f;
            signText.text = "SPEED\nLIMIT\n55";
            signText.fontSize = 60;
            signText.alignment = TextAlignment.Center;
            signText.anchor = TextAnchor.UpperCenter;
            signText.color = new Color(0.1f, 0.1f, 0.12f);
            signText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            signText.GetComponent<MeshRenderer>().sharedMaterial = signText.font.material;

            // --- player car (open cabin so we can sit in it) ---
            var playerCar = new GameObject("PlayerCar").transform;
            playerCar.position = Vector3.zero;
            Prim(PrimitiveType.Cube, "Body", playerCar, new Vector3(0, 0.5f, 0), new Vector3(1.7f, 0.5f, 3.6f), "Car_Rust");
            Prim(PrimitiveType.Cube, "Hood", playerCar, new Vector3(0, 0.78f, 1.35f), new Vector3(1.6f, 0.06f, 0.9f), "Car_Rust");
            Prim(PrimitiveType.Cube, "Trunk", playerCar, new Vector3(0, 0.78f, -1.4f), new Vector3(1.6f, 0.06f, 0.8f), "Car_Rust");
            Prim(PrimitiveType.Cube, "Roof", playerCar, new Vector3(0, 1.52f, -0.25f), new Vector3(1.6f, 0.07f, 1.9f), "Car_Rust");
            foreach (var (x, z) in new[] { (-0.75f, -1.15f), (0.75f, -1.15f), (-0.75f, 0.65f), (0.75f, 0.65f) })
            {
                Prim(PrimitiveType.Cube, "Pillar", playerCar, new Vector3(x, 1.15f, z), new Vector3(0.07f, 0.75f, 0.07f), "Car_Rust");
            }
            foreach (var (x, z) in new[] { (-0.95f, 1.25f), (0.95f, 1.25f), (-0.95f, -1.25f), (0.95f, -1.25f) })
            {
                Prim(PrimitiveType.Cube, "Wheel", playerCar, new Vector3(x, 0.26f, z), new Vector3(0.22f, 0.52f, 0.52f), "Wheel_Black");
            }
            Prim(PrimitiveType.Cube, "Dashboard", playerCar, new Vector3(0, 0.92f, 0.98f), new Vector3(1.55f, 0.16f, 0.4f), "Car_Interior");
            Prim(PrimitiveType.Cube, "SeatL", playerCar, new Vector3(-0.42f, 0.62f, -0.2f), new Vector3(0.55f, 0.14f, 0.55f), "Car_Interior");
            Prim(PrimitiveType.Cube, "SeatLBack", playerCar, new Vector3(-0.42f, 0.95f, -0.48f), new Vector3(0.55f, 0.6f, 0.12f), "Car_Interior");
            Prim(PrimitiveType.Cube, "SeatR", playerCar, new Vector3(0.42f, 0.62f, -0.2f), new Vector3(0.55f, 0.14f, 0.55f), "Car_Interior");
            Prim(PrimitiveType.Cube, "SeatRBack", playerCar, new Vector3(0.42f, 0.95f, -0.48f), new Vector3(0.55f, 0.6f, 0.12f), "Car_Interior");

            BuildInteractables(playerCar);

            // torso so looking down shows a body
            Prim(PrimitiveType.Cube, "PlayerTorso", playerCar, new Vector3(-0.42f, 0.85f, -0.18f), new Vector3(0.48f, 0.5f, 0.3f), "Car_White");

            // --- Benny ---
            var passenger = BuildCharacter("Passenger_Benny", "passenger",
                "Skin_Passenger", "Shirt_Loud", "Passenger", standing: false);
            passenger.transform.SetParent(playerCar, false);
            passenger.transform.localPosition = new Vector3(0.42f, 0.7f, -0.2f);
            passenger.GetComponent<NPCActor>().canWalk = false;

            // --- police car ---
            var policeCar = new GameObject("PoliceCar").transform;
            policeCar.position = new Vector3(0.6f, 0, -6.2f);
            Prim(PrimitiveType.Cube, "Body", policeCar, new Vector3(0, 0.55f, 0), new Vector3(1.8f, 0.55f, 3.9f), "Car_Police");
            Prim(PrimitiveType.Cube, "Cabin", policeCar, new Vector3(0, 1.12f, -0.3f), new Vector3(1.6f, 0.6f, 1.8f), "Car_White");
            foreach (var (x, z) in new[] { (-1.0f, 1.35f), (1.0f, 1.35f), (-1.0f, -1.35f), (1.0f, -1.35f) })
            {
                Prim(PrimitiveType.Cube, "Wheel", policeCar, new Vector3(x, 0.28f, z), new Vector3(0.22f, 0.56f, 0.56f), "Wheel_Black");
            }
            Prim(PrimitiveType.Cube, "LightBarBase", policeCar, new Vector3(0, 1.48f, -0.3f), new Vector3(1.0f, 0.08f, 0.42f), "Wheel_Black");
            var redCube = Prim(PrimitiveType.Cube, "RedLamp", policeCar, new Vector3(-0.28f, 1.62f, -0.3f), new Vector3(0.36f, 0.2f, 0.36f), "LightBar_Red");
            var blueCube = Prim(PrimitiveType.Cube, "BlueLamp", policeCar, new Vector3(0.28f, 1.62f, -0.3f), new Vector3(0.36f, 0.2f, 0.36f), "LightBar_Blue");

            var redLight = MakeLight(policeCar, "RedLight", new Vector3(-0.28f, 1.95f, -0.3f), Color.red, 3.5f, 16f);
            var blueLight = MakeLight(policeCar, "BlueLight", new Vector3(0.28f, 1.95f, -0.3f), Color.blue, 3.5f, 16f);

            var lights = policeCar.gameObject.AddComponent<PoliceLights>();
            lights.redLight = redLight;
            lights.blueLight = blueLight;
            lights.redCube = redCube.GetComponent<Renderer>();
            lights.blueCube = blueCube.GetComponent<Renderer>();

            var coffeeProp = Prim(PrimitiveType.Cylinder, "CoffeeCup", policeCar,
                new Vector3(0.55f, 0.95f, 1.7f), new Vector3(0.12f, 0.09f, 0.12f), "Prop_Coffee");
            AddProp(coffeeProp, "coffee");

            // headlights pointing at the player car for drama
            var headlight = MakeLight(policeCar, "Headlights", new Vector3(0, 0.7f, 2.0f), new Color(1f, 0.95f, 0.8f), 2.2f, 14f);
            headlight.type = LightType.Spot;
            headlight.spotAngle = 65f;
            headlight.transform.localRotation = Quaternion.Euler(2f, 0, 0);

            // --- Officer Glazer (starts at his car; walks up during the intro) ---
            // starts beside his cruiser on the driver's side so the intro walk
            // is a clean straight line up the lane (no clipping through the car)
            var officer = BuildCharacter("Officer_Glazer", "officer",
                "Skin_Officer", "Uniform_Navy", "Officer", standing: true);
            officer.transform.position = new Vector3(-1.6f, 0, -4.2f);

            var torso = officer.transform.Find("TorsoPivot");
            var pad = StripCollider(Prim(PrimitiveType.Cube, "TicketPad", torso,
                new Vector3(0.2f, 0.45f, 0.12f), new Vector3(0.16f, 0.03f, 0.22f), "Prop_Generic"));
            AddProp(pad, "ticketPad");
            var radio = StripCollider(Prim(PrimitiveType.Cube, "Radio", torso,
                new Vector3(-0.22f, 0.9f, 0.12f), new Vector3(0.08f, 0.12f, 0.06f), "Prop_Generic"));
            AddProp(radio, "radio");
            var armR = torso.Find("ArmR");
            var flashlight = StripCollider(Prim(PrimitiveType.Cube, "Flashlight", armR,
                new Vector3(0, -0.55f, 0.1f), new Vector3(0.45f, 0.5f, 0.45f), "Prop_Generic"));
            AddProp(flashlight, "flashlight");
            var beam = MakeLight(flashlight.transform, "Beam", new Vector3(0, -0.5f, 0.5f), new Color(1f, 0.98f, 0.85f), 2.5f, 9f);
            beam.type = LightType.Spot;
            beam.spotAngle = 42f;
            beam.transform.localRotation = Quaternion.Euler(10f, 0, 0);

            var speaker = officer.AddComponent<NpcSpeaker>();
            speaker.actorDisplayName = "Officer Glazer";
            speaker.voiceName = "David";
            speaker.rate = 0;
            speaker.pitch = -3;
            speaker.wobble = officer.GetComponent<WobbleAnimator>();

            var license = Prim(PrimitiveType.Cube, "LicenseCard", playerCar,
                new Vector3(-0.15f, 1.02f, 0.95f), new Vector3(0.16f, 0.02f, 0.22f), "Line_White");
            AddProp(license, "license");
            var handOver = license.AddComponent<Interactable>();
            handOver.hintText = "Hand over your license";
            handOver.pulseTarget = license.transform;
            handOver.eventTextTemplate = "The driver held their license out the window for the officer.";
            handOver.cooldownSeconds = 10f;

            // --- locations ---
            var locations = new GameObject("Locations").transform;
            Location(locations, "DriverWindow", new Vector3(-1.5f, 0, 0.2f), 90f);
            Location(locations, "PassengerWindow", new Vector3(1.5f, 0, 0.2f), -90f);
            Location(locations, "PoliceCar", new Vector3(-1.6f, 0, -4.2f), 0f);
            Location(locations, "PassengerSeat", new Vector3(0.42f, 0.7f, -0.2f), 0f);

            // --- first-person camera (driver's eyes) ---
            var cameraGo = new GameObject("PlayerCamera");
            cameraGo.tag = "MainCamera";
            cameraGo.transform.SetParent(playerCar, false);
            cameraGo.transform.localPosition = new Vector3(-0.42f, 1.3f, -0.1f);
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.fieldOfView = 62f;
            camera.nearClipPlane = 0.08f;
            cameraGo.AddComponent<AudioListener>();
            var rig = cameraGo.AddComponent<FirstPersonRig>();
            var raycaster = cameraGo.AddComponent<InteractionRaycaster>();
            raycaster.playerCamera = camera;

            SetupPostProcessing(cameraGo);

            // --- systems ---
            var systems = new GameObject("Systems");
            var turnController = systems.AddComponent<TurnController>();
            var gameManager = systems.AddComponent<GameManager>();
            var propRegistry = systems.AddComponent<PropRegistry>();
            var performer = systems.AddComponent<WorldPerformer>();
            var statsOverlay = systems.AddComponent<DebugStatsOverlay>();
            var harness = systems.AddComponent<DirectorTestHarness>();

            var scenario = AssetDatabase.LoadAssetAtPath<ScenarioDefinition>(
                "Assets/GameData/Scenarios/TrafficStop/TrafficStop_Scenario.asset");
            var llmConfig = AssetDatabase.LoadAssetAtPath<LlmConfig>("Assets/GameData/LlmConfig.asset");

            gameManager.scenario = scenario;
            gameManager.llmConfig = llmConfig;
            gameManager.turnController = turnController;
            gameManager.useMockBrains = false;

            performer.turnController = turnController;
            performer.propRegistry = propRegistry;
            statsOverlay.turnController = turnController;
            harness.turnController = turnController;
            harness.corpus = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/GameData/TestCorpus.txt");

            // --- LLM stack: one server, two agents (cop voice + judge) ---
            var llmGo = new GameObject("LLM");
            llmGo.transform.SetParent(systems.transform);
            var llm = llmGo.AddComponent<LLM>();
            llm.dontDestroyOnLoad = false;
            var llmSo = new SerializedObject(llm);
            TrySet(llmSo, new[] { "_model", "model", "m_Model" },
                p => p.stringValue = "Models/Phi-3.5-mini-instruct-Q4_K_M.gguf");
            TrySet(llmSo, new[] { "_contextSize", "contextSize" }, p => p.intValue = 4096);
            TrySet(llmSo, new[] { "_numGPULayers", "numGPULayers" }, p => p.intValue = 30);
            llmSo.ApplyModifiedPropertiesWithoutUndo();

            var copAgent = llmGo.AddComponent<LLMAgent>();
            var judgeAgent = llmGo.AddComponent<LLMAgent>();
            foreach (var agent in new[] { copAgent, judgeAgent })
            {
                var so = new SerializedObject(agent);
                TrySet(so, new[] { "_llm", "llm", "m_LLM" }, p => p.objectReferenceValue = llm);
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            var copBrain = llmGo.AddComponent<LlmCopBrain>();
            copBrain.agent = copAgent;
            var judge = llmGo.AddComponent<LlmJudge>();
            judge.agent = judgeAgent;
            gameManager.llmCopBrain = copBrain;
            gameManager.llmJudge = judge;

            // --- Whisper voice input ---
            var whisperGo = new GameObject("Whisper");
            whisperGo.transform.SetParent(systems.transform);
            var whisper = whisperGo.AddComponent<WhisperManager>();
            var whisperSo = new SerializedObject(whisper);
            TrySet(whisperSo, new[] { "modelPath", "_modelPath" },
                p => p.stringValue = "Models/ggml-base.en.bin");
            TrySet(whisperSo, new[] { "isModelPathInStreamingAssets" }, p => p.boolValue = true);
            TrySet(whisperSo, new[] { "language" }, p => p.stringValue = "en");
            whisperSo.ApplyModifiedPropertiesWithoutUndo();

            var voiceInput = systems.AddComponent<VoiceInput>();
            voiceInput.whisper = whisper;
            voiceInput.turnController = turnController;

            // --- UI ---
            var uiGo = new GameObject("UI");
            var uiDoc = uiGo.AddComponent<UIDocument>();
            uiDoc.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/TalkOutPanelSettings.asset");
            uiDoc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Dialogue.uxml");
            var dialogue = uiGo.AddComponent<DialogueScreenController>();
            dialogue.turnController = turnController;
            dialogue.firstPersonRig = rig;
            dialogue.raycaster = raycaster;
            dialogue.voiceInput = voiceInput;

            EditorSceneManager.SaveScene(scene, "Assets/Scenes/TrafficStop.unity");
            Debug.Log("[TalkOut] TrafficStop scene built (first-person).");
        }

        // ====================================================================
        private static void BuildInteractables(Transform playerCar)
        {
            // glove box with a hinged lid and a mystery item
            var gloveBox = new GameObject("GloveBox");
            gloveBox.transform.SetParent(playerCar, false);
            gloveBox.transform.localPosition = new Vector3(0.38f, 0.92f, 0.88f);
            Prim(PrimitiveType.Cube, "Box", gloveBox.transform, new Vector3(0, 0, 0.05f), new Vector3(0.42f, 0.18f, 0.16f), "Car_Interior");
            var lidPivot = new GameObject("LidPivot").transform;
            lidPivot.SetParent(gloveBox.transform, false);
            lidPivot.localPosition = new Vector3(0, -0.09f, -0.04f);
            Prim(PrimitiveType.Cube, "Lid", lidPivot, new Vector3(0, 0.09f, 0), new Vector3(0.42f, 0.17f, 0.03f), "Prop_Generic");
            var glove = gloveBox.AddComponent<Interactable>();
            glove.hintText = "Open glove box";
            glove.isToggle = true;
            glove.hinge = lidPivot;
            glove.openEuler = new Vector3(-95f, 0, 0);
            glove.eventTextTemplate = "The driver opened the glove compartment in front of the officer. Inside: {item}.";
            glove.eventTextClose = "The driver quietly closed the glove compartment.";
            glove.randomContents = new[]
            {
                "a single taco sauce packet labeled 'FIRE'",
                "roughly forty unpaid parking tickets",
                "a live hamster in a tiny sombrero",
                "an expired coupon for one free hug",
                "a harmonica and a note that says 'in case of emergency'",
                "a half-eaten birthday cake",
            };

            // horn
            var wheel = Prim(PrimitiveType.Cube, "SteeringWheel", playerCar,
                new Vector3(-0.42f, 1.02f, 0.72f), new Vector3(0.34f, 0.28f, 0.07f), "Wheel_Black");
            var hornAudio = wheel.AddComponent<AudioSource>();
            hornAudio.playOnAwake = false;
            hornAudio.spatialBlend = 1f;
            wheel.AddComponent<ToneGenerator>();
            var horn = wheel.AddComponent<Interactable>();
            horn.hintText = "Honk the horn";
            horn.pulseTarget = wheel.transform;
            horn.audioSource = hornAudio;
            horn.eventTextTemplate = "The driver honked the horn. At a police officer. During a traffic stop.";
            horn.cooldownSeconds = 3f;

            // car radio
            var carRadio = Prim(PrimitiveType.Cube, "CarRadio", playerCar,
                new Vector3(0f, 0.98f, 0.9f), new Vector3(0.28f, 0.1f, 0.06f), "Prop_Generic");
            var radioToggle = carRadio.AddComponent<Interactable>();
            radioToggle.hintText = "Turn on the radio";
            radioToggle.isToggle = true;
            radioToggle.pulseTarget = carRadio.transform;
            radioToggle.eventTextTemplate = "The driver turned on the car radio. Extremely loud polka music fills the night.";
            radioToggle.eventTextClose = "The driver sheepishly turned the radio back off.";

            // sunglasses
            var shades = Prim(PrimitiveType.Cube, "Sunglasses", playerCar,
                new Vector3(0.15f, 1.02f, 0.95f), new Vector3(0.18f, 0.03f, 0.07f), "Wheel_Black");
            var shadesUse = shades.AddComponent<Interactable>();
            shadesUse.hintText = "Put on sunglasses";
            shadesUse.pulseTarget = shades.transform;
            shadesUse.eventTextTemplate = "The driver slowly put on sunglasses. It is currently the middle of the night.";
            shadesUse.cooldownSeconds = 8f;
        }

        // ====================================================================
        private static void BuildMainMenuScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.04f, 0.05f, 0.08f);
            cameraGo.AddComponent<AudioListener>();

            var uiGo = new GameObject("UI");
            var uiDoc = uiGo.AddComponent<UIDocument>();
            uiDoc.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/TalkOutPanelSettings.asset");
            uiDoc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/MainMenu.uxml");
            var menu = uiGo.AddComponent<MainMenuController>();
            menu.llmConfig = AssetDatabase.LoadAssetAtPath<LlmConfig>("Assets/GameData/LlmConfig.asset");

            EditorSceneManager.SaveScene(scene, "Assets/Scenes/MainMenu.unity");
        }

        // ====================================================================
        // helpers

        /// Wobbly character: legs + hip TorsoPivot (kinematic RB) carrying body,
        /// head w/ face quad, and two physics-jointed floppy arms.
        private static GameObject BuildCharacter(
            string name, string actorId, string skinMat, string shirtMat, string faceSetName, bool standing)
        {
            var root = new GameObject(name);
            var t = root.transform;

            if (standing)
            {
                StripCollider(Prim(PrimitiveType.Cube, "LegL", t, new Vector3(-0.14f, 0.25f, 0), new Vector3(0.18f, 0.5f, 0.18f), "Pants_Dark"));
                StripCollider(Prim(PrimitiveType.Cube, "LegR", t, new Vector3(0.14f, 0.25f, 0), new Vector3(0.18f, 0.5f, 0.18f), "Pants_Dark"));
            }

            float hipY = standing ? 0.5f : 0f;
            var torsoPivot = new GameObject("TorsoPivot").transform;
            torsoPivot.SetParent(t, false);
            torsoPivot.localPosition = new Vector3(0, hipY, 0);
            var torsoRb = torsoPivot.gameObject.AddComponent<Rigidbody>();
            torsoRb.isKinematic = true;

            var body = StripCollider(Prim(PrimitiveType.Cube, "Body", torsoPivot, new Vector3(0, 0.42f, 0), new Vector3(0.55f, 0.8f, 0.32f), shirtMat));
            var head = StripCollider(Prim(PrimitiveType.Cube, "Head", torsoPivot, new Vector3(0, 1.06f, 0), new Vector3(0.45f, 0.45f, 0.45f), skinMat));

            var faceQuad = Prim(PrimitiveType.Quad, "FaceQuad", head.transform, new Vector3(0, 0, 0.51f), Vector3.one * 0.92f, null);
            faceQuad.transform.localRotation = Quaternion.Euler(0, 180f, 0);
            faceQuad.GetComponent<Renderer>().sharedMaterial =
                AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Face.mat");
            Object.DestroyImmediate(faceQuad.GetComponent<Collider>());

            MakeFloppyArm(torsoPivot, torsoRb, "ArmL", new Vector3(-0.36f, 0.74f, 0), shirtMat);
            MakeFloppyArm(torsoPivot, torsoRb, "ArmR", new Vector3(0.36f, 0.74f, 0), shirtMat);

            var face = root.AddComponent<FaceController>();
            face.faceRenderer = faceQuad.GetComponent<Renderer>();
            face.faceSet = AssetDatabase.LoadAssetAtPath<FaceSet>(
                $"Assets/GameData/Scenarios/TrafficStop/Faces/{faceSetName}_FaceSet.asset");

            var wobble = root.AddComponent<WobbleAnimator>();
            wobble.torsoPivot = torsoPivot;
            wobble.head = head.transform;

            var actor = root.AddComponent<NPCActor>();
            actor.actorId = actorId;
            actor.face = face;
            actor.wobble = wobble;

            return root;
        }

        /// Dangling arm: dynamic rigidbody + CharacterJoint anchored at the
        /// shoulder. It flops when the body wobbles/walks — free physical comedy.
        private static void MakeFloppyArm(Transform torsoPivot, Rigidbody torsoRb, string name, Vector3 shoulderLocal, string mat)
        {
            var arm = Prim(PrimitiveType.Cube, name, torsoPivot,
                shoulderLocal + new Vector3(0, -0.24f, 0), new Vector3(0.15f, 0.5f, 0.15f), mat);
            arm.layer = 2; // Ignore Raycast — arms must not block interactable clicks

            var rb = arm.AddComponent<Rigidbody>();
            rb.mass = 0.4f;
            rb.drag = 1.2f;
            rb.angularDrag = 1.5f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            var joint = arm.AddComponent<CharacterJoint>();
            joint.connectedBody = torsoRb;
            joint.anchor = new Vector3(0, 0.5f, 0); // top of the arm cube
            joint.enableProjection = true;
            var lowTwist = joint.lowTwistLimit; lowTwist.limit = -20f; joint.lowTwistLimit = lowTwist;
            var highTwist = joint.highTwistLimit; highTwist.limit = 20f; joint.highTwistLimit = highTwist;
            var swing1 = joint.swing1Limit; swing1.limit = 70f; joint.swing1Limit = swing1;
            var swing2 = joint.swing2Limit; swing2.limit = 70f; joint.swing2Limit = swing2;
        }

        private static void SetupPostProcessing(GameObject cameraGo)
        {
            string profilePath = "Assets/GameData/TalkOutPostFX.asset";
            AssetDatabase.DeleteAsset(profilePath);
            var profile = ScriptableObject.CreateInstance<PostProcessProfile>();
            AssetDatabase.CreateAsset(profile, profilePath);

            var bloom = profile.AddSettings<Bloom>();
            bloom.enabled.Override(true);
            bloom.intensity.Override(2.6f);
            bloom.threshold.Override(1.05f);
            bloom.softKnee.Override(0.6f);

            var vignette = profile.AddSettings<Vignette>();
            vignette.enabled.Override(true);
            vignette.intensity.Override(0.3f);
            vignette.smoothness.Override(0.5f);

            var grading = profile.AddSettings<ColorGrading>();
            grading.enabled.Override(true);
            grading.tonemapper.Override(Tonemapper.ACES);
            grading.saturation.Override(10f);
            grading.contrast.Override(12f);
            grading.temperature.Override(-6f); // cold night

            EditorUtility.SetDirty(profile);

            var volumeGo = new GameObject("PostFXVolume") { layer = PostFxLayer };
            var volume = volumeGo.AddComponent<PostProcessVolume>();
            volume.isGlobal = true;
            volume.profile = profile;

            var layer = cameraGo.AddComponent<PostProcessLayer>();
            layer.volumeLayer = 1 << PostFxLayer;
            layer.volumeTrigger = cameraGo.transform;
            layer.antialiasingMode = PostProcessLayer.Antialiasing.FastApproximateAntialiasing;

            // AddComponent from script skips the inspector's auto-init: wire the
            // package's PostProcessResources or effects won't render.
            string[] guids = AssetDatabase.FindAssets("t:PostProcessResources");
            if (guids.Length > 0)
            {
                var resources = AssetDatabase.LoadAssetAtPath<PostProcessResources>(
                    AssetDatabase.GUIDToAssetPath(guids[0]));
                layer.Init(resources);
                var layerSo = new SerializedObject(layer);
                var resourcesProp = layerSo.FindProperty("m_Resources");
                if (resourcesProp != null)
                {
                    resourcesProp.objectReferenceValue = resources;
                    layerSo.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            else
            {
                Debug.LogWarning("[TalkOut] PostProcessResources not found — post FX may not render.");
            }
        }

        private static Light MakeLight(Transform parent, string name, Vector3 localPos, Color color, float intensity, float range)
        {
            var light = new GameObject(name).AddComponent<Light>();
            light.transform.SetParent(parent, false);
            light.transform.localPosition = localPos;
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            return light;
        }

        private static GameObject Prim(
            PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 localScale, string matName)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            if (matName != null)
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>($"Assets/Art/Materials/{matName}.mat");
                if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
            }
            return go;
        }

        private static GameObject StripCollider(GameObject go)
        {
            var collider = go.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);
            return go;
        }

        private static void AddProp(GameObject go, string propId)
        {
            var sceneProp = go.AddComponent<SceneProp>();
            sceneProp.definition = AssetDatabase.LoadAssetAtPath<PropDefinition>(
                $"Assets/GameData/Scenarios/TrafficStop/Props/{propId}.asset");
            sceneProp.targetRenderer = go.GetComponent<Renderer>();
        }

        private static void Location(Transform parent, string id, Vector3 position, float yaw)
        {
            var go = new GameObject($"Loc_{id}");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0, yaw, 0);
            go.AddComponent<LocationPoint>().id = id;
        }

        private static void Tree(Transform parent, Vector3 position)
        {
            var tree = new GameObject("Tree").transform;
            tree.SetParent(parent, false);
            tree.position = position;
            Prim(PrimitiveType.Cube, "Trunk", tree, new Vector3(0, 0.6f, 0), new Vector3(0.35f, 1.2f, 0.35f), "Tree_Trunk");
            Prim(PrimitiveType.Cube, "Leaves1", tree, new Vector3(0, 1.7f, 0), new Vector3(1.3f, 1.0f, 1.3f), "Tree_Leaves");
            Prim(PrimitiveType.Cube, "Leaves2", tree, new Vector3(0, 2.5f, 0), new Vector3(0.85f, 0.8f, 0.85f), "Tree_Leaves");
        }

        private static void EnsureLayer(int index, string layerName)
        {
            var tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            var element = layers.GetArrayElementAtIndex(index);
            if (element.stringValue != layerName)
            {
                element.stringValue = layerName;
                tagManager.ApplyModifiedProperties();
            }
        }

        private static void TrySet(
            SerializedObject so, string[] candidateNames, System.Action<SerializedProperty> setter)
        {
            foreach (var name in candidateNames)
            {
                var prop = so.FindProperty(name);
                if (prop != null)
                {
                    setter(prop);
                    return;
                }
            }
            Debug.LogWarning($"[TalkOut] Could not find serialized property among [{string.Join(", ", candidateNames)}] " +
                             $"on {so.targetObject.GetType().Name} — set it manually in the Inspector.");
        }
    }
}
