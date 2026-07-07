using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using LLMUnity;
using TalkOut.Actors;
using TalkOut.CameraRig;
using TalkOut.Core;
using TalkOut.Data;
using TalkOut.Debugging;
using TalkOut.Directing;
using TalkOut.Props;
using TalkOut.UI;
using TalkOut.World;

namespace TalkOut.EditorTools
{
    /// Builds the TrafficStop and MainMenu scenes from scratch (idempotent —
    /// re-running regenerates them). Run "Build Everything" on a fresh clone.
    public static class TalkOutSceneBuilder
    {
        [MenuItem("Tools/TalkOut/Build Everything (Textures + Assets + Scenes)")]
        public static void BuildEverything()
        {
            TalkOutAssetBuilder.GenerateFaces();
            TalkOutAssetBuilder.BuildAssets();
            BuildScenes();
            Debug.Log("[TalkOut] Build Everything finished. Open Assets/Scenes/TrafficStop.unity and press Play.");
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

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.10f, 0.11f, 0.17f);

            // --- lights ---
            var moon = new GameObject("Moonlight").AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.intensity = 0.35f;
            moon.color = new Color(0.62f, 0.70f, 0.95f);
            moon.shadows = LightShadows.Soft;
            moon.transform.rotation = Quaternion.Euler(48f, -32f, 0f);

            var fill = new GameObject("FillLight").AddComponent<Light>();
            fill.type = LightType.Point;
            fill.intensity = 0.8f;
            fill.range = 14f;
            fill.color = new Color(1.0f, 0.92f, 0.8f);
            fill.transform.position = new Vector3(-3.2f, 3.0f, 1.8f);

            // --- environment ---
            var env = new GameObject("Environment").transform;
            Prim(PrimitiveType.Plane, "Ground", env, new Vector3(0, -0.01f, 0), new Vector3(12, 1, 12), "Ground_Night");
            Prim(PrimitiveType.Cube, "Road", env, new Vector3(-1.2f, -0.005f, 0), new Vector3(6.5f, 0.02f, 90f), "Asphalt");
            for (int i = 0; i < 12; i++)
            {
                Prim(PrimitiveType.Cube, $"Dash_{i}", env,
                    new Vector3(-3.6f, 0.012f, -24f + i * 4.5f), new Vector3(0.14f, 0.02f, 1.4f), "Line_White");
            }
            Tree(env, new Vector3(-8f, 0, 7f));
            Tree(env, new Vector3(-9f, 0, -5f));
            Tree(env, new Vector3(7f, 0, 12f));
            Tree(env, new Vector3(8f, 0, -9f));

            // --- player car (with visible interior) ---
            var playerCar = new GameObject("PlayerCar").transform;
            playerCar.position = Vector3.zero;
            Prim(PrimitiveType.Cube, "Body", playerCar, new Vector3(0, 0.5f, 0), new Vector3(1.7f, 0.5f, 3.6f), "Car_Rust");
            Prim(PrimitiveType.Cube, "Roof", playerCar, new Vector3(0, 1.42f, -0.25f), new Vector3(1.55f, 0.08f, 1.9f), "Car_Rust");
            foreach (var (x, z) in new[] { (-0.7f, -1.1f), (0.7f, -1.1f), (-0.7f, 0.6f), (0.7f, 0.6f) })
            {
                Prim(PrimitiveType.Cube, "Pillar", playerCar, new Vector3(x, 1.1f, z), new Vector3(0.08f, 0.6f, 0.08f), "Car_Rust");
            }
            foreach (var (x, z) in new[] { (-0.95f, 1.25f), (0.95f, 1.25f), (-0.95f, -1.25f), (0.95f, -1.25f) })
            {
                Prim(PrimitiveType.Cube, "Wheel", playerCar, new Vector3(x, 0.24f, z), new Vector3(0.2f, 0.48f, 0.48f), "Wheel_Black");
            }
            Prim(PrimitiveType.Cube, "Dashboard", playerCar, new Vector3(0, 0.9f, 1.05f), new Vector3(1.5f, 0.12f, 0.35f), "Pants_Dark");
            Prim(PrimitiveType.Cube, "SeatL", playerCar, new Vector3(-0.42f, 0.62f, -0.15f), new Vector3(0.55f, 0.12f, 0.55f), "Pants_Dark");
            Prim(PrimitiveType.Cube, "SeatR", playerCar, new Vector3(0.42f, 0.62f, -0.15f), new Vector3(0.55f, 0.12f, 0.55f), "Pants_Dark");

            var wheelProp = Prim(PrimitiveType.Cube, "SteeringWheel", playerCar,
                new Vector3(-0.42f, 1.0f, 0.8f), new Vector3(0.32f, 0.26f, 0.07f), "Wheel_Black");
            AddProp(wheelProp, "horn");
            var licenseProp = Prim(PrimitiveType.Cube, "License", playerCar,
                new Vector3(-0.15f, 0.98f, 1.05f), new Vector3(0.18f, 0.02f, 0.26f), "Line_White");
            AddProp(licenseProp, "license");

            // player avatar (set dressing — the camera looks past them at the officer)
            var driver = new GameObject("PlayerAvatar").transform;
            driver.SetParent(playerCar, false);
            driver.localPosition = new Vector3(-0.42f, 0.68f, -0.15f);
            Prim(PrimitiveType.Cube, "Body", driver, new Vector3(0, 0.3f, 0), new Vector3(0.5f, 0.6f, 0.3f), "Car_White");
            Prim(PrimitiveType.Cube, "Head", driver, new Vector3(0, 0.82f, 0), new Vector3(0.4f, 0.4f, 0.4f), "Skin_Officer");

            // --- Benny, the passenger ---
            var passenger = BuildCharacter("Passenger_Benny", "passenger",
                "Skin_Passenger", "Shirt_Loud", "Passenger", seated: true);
            passenger.transform.SetParent(playerCar, false);
            passenger.transform.localPosition = new Vector3(0.42f, 0.68f, -0.15f);
            passenger.GetComponent<NPCActor>().canWalk = false;

            // --- police car ---
            var policeCar = new GameObject("PoliceCar").transform;
            policeCar.position = new Vector3(0.5f, 0, -5.8f);
            Prim(PrimitiveType.Cube, "Body", policeCar, new Vector3(0, 0.55f, 0), new Vector3(1.8f, 0.55f, 3.9f), "Car_Police");
            Prim(PrimitiveType.Cube, "Cabin", policeCar, new Vector3(0, 1.12f, -0.3f), new Vector3(1.6f, 0.6f, 1.8f), "Car_White");
            foreach (var (x, z) in new[] { (-1.0f, 1.35f), (1.0f, 1.35f), (-1.0f, -1.35f), (1.0f, -1.35f) })
            {
                Prim(PrimitiveType.Cube, "Wheel", policeCar, new Vector3(x, 0.26f, z), new Vector3(0.2f, 0.52f, 0.52f), "Wheel_Black");
            }
            Prim(PrimitiveType.Cube, "LightBarBase", policeCar, new Vector3(0, 1.48f, -0.3f), new Vector3(1.0f, 0.08f, 0.42f), "Wheel_Black");
            var redCube = Prim(PrimitiveType.Cube, "RedLamp", policeCar, new Vector3(-0.28f, 1.62f, -0.3f), new Vector3(0.36f, 0.2f, 0.36f), "LightBar_Red");
            var blueCube = Prim(PrimitiveType.Cube, "BlueLamp", policeCar, new Vector3(0.28f, 1.62f, -0.3f), new Vector3(0.36f, 0.2f, 0.36f), "LightBar_Blue");

            var redLight = new GameObject("RedLight").AddComponent<Light>();
            redLight.transform.SetParent(policeCar, false);
            redLight.transform.localPosition = new Vector3(-0.28f, 1.9f, -0.3f);
            redLight.type = LightType.Point; redLight.color = Color.red; redLight.intensity = 2.2f; redLight.range = 12f;
            var blueLight = new GameObject("BlueLight").AddComponent<Light>();
            blueLight.transform.SetParent(policeCar, false);
            blueLight.transform.localPosition = new Vector3(0.28f, 1.9f, -0.3f);
            blueLight.type = LightType.Point; blueLight.color = Color.blue; blueLight.intensity = 2.2f; blueLight.range = 12f;

            var lights = policeCar.gameObject.AddComponent<PoliceLights>();
            lights.redLight = redLight; lights.blueLight = blueLight;
            lights.redCube = redCube.GetComponent<Renderer>();
            lights.blueCube = blueCube.GetComponent<Renderer>();

            var coffeeProp = Prim(PrimitiveType.Cylinder, "CoffeeCup", policeCar,
                new Vector3(0.55f, 0.93f, 1.7f), new Vector3(0.12f, 0.09f, 0.12f), "Prop_Coffee");
            AddProp(coffeeProp, "coffee");

            // --- Officer Glazer ---
            var officer = BuildCharacter("Officer_Glazer", "officer",
                "Skin_Officer", "Uniform_Navy", "Officer", seated: false);
            officer.transform.position = new Vector3(-1.45f, 0, 0.3f);
            officer.transform.rotation = Quaternion.Euler(0, 90, 0); // faces the car

            var pad = Prim(PrimitiveType.Cube, "TicketPad", officer.transform,
                new Vector3(0.2f, 0.95f, 0.12f), new Vector3(0.16f, 0.03f, 0.22f), "Prop_Generic");
            AddProp(pad, "ticketPad");
            var radio = Prim(PrimitiveType.Cube, "Radio", officer.transform,
                new Vector3(-0.22f, 1.42f, 0.12f), new Vector3(0.08f, 0.12f, 0.06f), "Prop_Generic");
            AddProp(radio, "radio");
            var armR = officer.transform.Find("ArmR_Pivot");
            var flashlight = Prim(PrimitiveType.Cube, "Flashlight", armR,
                new Vector3(0, -0.52f, 0.08f), new Vector3(0.07f, 0.26f, 0.07f), "Prop_Generic");
            AddProp(flashlight, "flashlight");

            // --- location points ---
            var locations = new GameObject("Locations").transform;
            Location(locations, "DriverWindow", new Vector3(-1.45f, 0, 0.3f), 90f);
            Location(locations, "PassengerWindow", new Vector3(1.45f, 0, 0.3f), -90f);
            Location(locations, "PoliceCar", new Vector3(0.5f, 0, -4.2f), 0f);
            Location(locations, "PassengerSeat", new Vector3(0.42f, 0.68f, -0.15f), 0f);

            // --- camera ---
            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.015f, 0.02f, 0.05f);
            camera.fieldOfView = 50f;
            cameraGo.AddComponent<AudioListener>();
            cameraGo.transform.position = new Vector3(-3.6f, 1.8f, 2.8f);
            Vector3 aim = new Vector3(-1.45f, 1.5f, 0.3f); // officer's head
            cameraGo.transform.rotation = Quaternion.LookRotation(aim - cameraGo.transform.position);
            var cameraDirector = cameraGo.AddComponent<CameraDirector>();

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
            gameManager.useMockDirector = true; // flip off to use the local LLM

            performer.turnController = turnController;
            performer.cameraDirector = cameraDirector;
            performer.propRegistry = propRegistry;
            statsOverlay.turnController = turnController;
            harness.turnController = turnController;
            harness.corpus = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/GameData/TestCorpus.txt");

            // --- LLM stack (LLMUnity) ---
            var llmGo = new GameObject("LLM");
            var llm = llmGo.AddComponent<LLM>();
            llm.dontDestroyOnLoad = false; // scene reload = clean restart, no duplicate servers
            var so = new SerializedObject(llm);
            TrySet(so, new[] { "_model", "model", "m_Model" },
                p => p.stringValue = "Models/Phi-3.5-mini-instruct-Q4_K_M.gguf");
            TrySet(so, new[] { "_contextSize", "contextSize" }, p => p.intValue = 2048);
            TrySet(so, new[] { "_numGPULayers", "numGPULayers" }, p => p.intValue = 30);
            so.ApplyModifiedPropertiesWithoutUndo();

            var agent = llmGo.AddComponent<LLMAgent>();
            var agentSo = new SerializedObject(agent);
            TrySet(agentSo, new[] { "_llm", "llm", "m_LLM" },
                p => p.objectReferenceValue = llm);
            agentSo.ApplyModifiedPropertiesWithoutUndo();

            var llmDirector = llmGo.AddComponent<LlmDirector>();
            llmDirector.agent = agent;
            llmGo.transform.SetParent(systems.transform);

            // --- UI ---
            var uiGo = new GameObject("UI");
            var uiDoc = uiGo.AddComponent<UIDocument>();
            uiDoc.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/TalkOutPanelSettings.asset");
            uiDoc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Dialogue.uxml");
            var dialogue = uiGo.AddComponent<DialogueScreenController>();
            dialogue.turnController = turnController;

            EditorSceneManager.SaveScene(scene, "Assets/Scenes/TrafficStop.unity");
            Debug.Log("[TalkOut] TrafficStop scene built.");
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
            Debug.Log("[TalkOut] MainMenu scene built.");
        }

        // ====================================================================
        // helpers

        /// Blocky character rig: legs, body, head, face quad, arm pivots, components.
        private static GameObject BuildCharacter(
            string name, string actorId, string skinMat, string shirtMat, string faceSetName, bool seated)
        {
            var root = new GameObject(name);
            var t = root.transform;

            if (!seated)
            {
                Prim(PrimitiveType.Cube, "LegL", t, new Vector3(-0.14f, 0.25f, 0), new Vector3(0.18f, 0.5f, 0.18f), "Pants_Dark");
                Prim(PrimitiveType.Cube, "LegR", t, new Vector3(0.14f, 0.25f, 0), new Vector3(0.18f, 0.5f, 0.18f), "Pants_Dark");
            }

            float baseY = seated ? 0f : 0.5f;
            var body = Prim(PrimitiveType.Cube, "Body", t, new Vector3(0, baseY + 0.42f, 0), new Vector3(0.55f, 0.8f, 0.32f), shirtMat);
            var head = Prim(PrimitiveType.Cube, "Head", t, new Vector3(0, baseY + 1.06f, 0), new Vector3(0.45f, 0.45f, 0.45f), skinMat);

            var faceQuad = Prim(PrimitiveType.Quad, "FaceQuad", head.transform, new Vector3(0, 0, 0.51f), Vector3.one * 0.92f, null);
            faceQuad.transform.localRotation = Quaternion.Euler(0, 180f, 0);
            faceQuad.GetComponent<Renderer>().sharedMaterial =
                AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Face.mat");
            Object.DestroyImmediate(faceQuad.GetComponent<Collider>());

            var armLPivot = new GameObject("ArmL_Pivot").transform;
            armLPivot.SetParent(t, false);
            armLPivot.localPosition = new Vector3(-0.34f, baseY + 0.74f, 0);
            Prim(PrimitiveType.Cube, "ArmL", armLPivot, new Vector3(0, -0.24f, 0), new Vector3(0.15f, 0.5f, 0.15f), shirtMat);

            var armRPivot = new GameObject("ArmR_Pivot").transform;
            armRPivot.SetParent(t, false);
            armRPivot.localPosition = new Vector3(0.34f, baseY + 0.74f, 0);
            Prim(PrimitiveType.Cube, "ArmR", armRPivot, new Vector3(0, -0.24f, 0), new Vector3(0.15f, 0.5f, 0.15f), shirtMat);

            var face = root.AddComponent<FaceController>();
            face.faceRenderer = faceQuad.GetComponent<Renderer>();
            face.faceSet = AssetDatabase.LoadAssetAtPath<FaceSet>(
                $"Assets/GameData/Scenarios/TrafficStop/Faces/{faceSetName}_FaceSet.asset");

            var poser = root.AddComponent<SimplePoseAnimator>();
            poser.head = head.transform;
            poser.armL = armLPivot;
            poser.armR = armRPivot;
            poser.body = body.transform;

            var actor = root.AddComponent<NPCActor>();
            actor.actorId = actorId;
            actor.face = face;
            actor.poser = poser;
            actor.breathingTarget = body.transform;

            return root;
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
