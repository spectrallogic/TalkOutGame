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
    /// Builds all scenes from scratch (idempotent): TrafficStop, Date, MainMenu.
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
            Debug.Log("[TalkOut] Build Everything finished. Open a scene in Assets/Scenes and press Play.");
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
            QualitySettings.pixelLightCount = 10;
            Debug.Log("[TalkOut] Graphics settings applied (Linear color, 4x MSAA, soft shadows).");
        }

        [MenuItem("Tools/TalkOut/3. Build Scenes")]
        public static void BuildScenes()
        {
            BuildTrafficStopScene();
            BuildDateScene();
            BuildKingScene();
            BuildMainMenuScene();
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene("Assets/Scenes/MainMenu.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/TrafficStop.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/Date.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/King.unity", true),
            };
            Debug.Log("[TalkOut] Scenes built and added to Build Settings.");
        }

        // ====================================================================
        // LEVEL 1 — TRAFFIC STOP
        // ====================================================================
        private static void BuildTrafficStopScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var skybox = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/NightSky.mat");
            RenderSettings.skybox = skybox;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.17f, 0.19f, 0.30f);
            RenderSettings.ambientEquatorColor = new Color(0.12f, 0.13f, 0.19f);
            RenderSettings.ambientGroundColor = new Color(0.05f, 0.06f, 0.08f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.05f, 0.06f, 0.10f);
            RenderSettings.fogDensity = 0.010f;

            var moon = new GameObject("Moonlight").AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.intensity = 0.55f;
            moon.color = new Color(0.62f, 0.70f, 0.98f);
            moon.shadows = LightShadows.Soft;
            moon.transform.rotation = Quaternion.Euler(46f, -34f, 0f);
            RenderSettings.sun = moon;

            var fill = MakeLight(null, "FillLight", new Vector3(-3.4f, 3.2f, 1.6f), new Color(1.0f, 0.93f, 0.82f), 1.1f, 16f);

            // street lamp: a warm pool of light over the stop
            var lamp = new GameObject("StreetLamp").transform;
            lamp.position = new Vector3(2.9f, 0, 2.2f);
            Prim(PrimitiveType.Cube, "Pole", lamp, new Vector3(0, 2.6f, 0), new Vector3(0.14f, 5.2f, 0.14f), "Guardrail");
            Prim(PrimitiveType.Cube, "Arm", lamp, new Vector3(-1.1f, 5.1f, 0), new Vector3(2.2f, 0.12f, 0.12f), "Guardrail");
            Prim(PrimitiveType.Cube, "Head", lamp, new Vector3(-2.1f, 5.0f, 0), new Vector3(0.5f, 0.14f, 0.3f), "Lamp_Warm");
            var lampLight = MakeLight(lamp, "LampLight", new Vector3(-2.1f, 4.9f, 0), new Color(1f, 0.85f, 0.6f), 2.6f, 13f);
            lampLight.type = LightType.Spot;
            lampLight.spotAngle = 95f;
            lampLight.shadows = LightShadows.Soft;
            lampLight.transform.localRotation = Quaternion.Euler(90f, 0, 0);

            // --- environment ---
            var env = new GameObject("Environment").transform;
            Prim(PrimitiveType.Plane, "Ground", env, new Vector3(0, -0.02f, 0), new Vector3(30, 1, 30), "Ground_Night");
            Prim(PrimitiveType.Cube, "Road", env, new Vector3(-1.2f, -0.005f, 0), new Vector3(7f, 0.02f, 240f), "Asphalt");
            for (int i = 0; i < 26; i++)
            {
                Prim(PrimitiveType.Cube, $"Dash_{i}", env,
                    new Vector3(-3.8f, 0.012f, -58f + i * 4.5f), new Vector3(0.14f, 0.02f, 1.4f), "Line_White");
            }
            for (int i = 0; i < 16; i++)
            {
                Prim(PrimitiveType.Cube, $"RailPost_{i}", env, new Vector3(3.4f, 0.35f, -34f + i * 4.5f), new Vector3(0.12f, 0.7f, 0.12f), "Guardrail");
            }
            Prim(PrimitiveType.Cube, "Rail", env, new Vector3(3.4f, 0.62f, 0f), new Vector3(0.08f, 0.22f, 72f), "Guardrail");
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

            // --- player car ---
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
            Prim(PrimitiveType.Cube, "PlayerTorso", playerCar, new Vector3(-0.42f, 0.85f, -0.18f), new Vector3(0.48f, 0.5f, 0.3f), "Car_White");

            // dome light so the cabin isn't a black pit
            MakeLight(playerCar, "DomeLight", new Vector3(0, 1.45f, 0.1f), new Color(1f, 0.9f, 0.75f), 0.9f, 2.5f);

            BuildCarInteractables(playerCar);

            var passenger = BuildCharacter("Passenger_Benny", "passenger",
                "Skin_Passenger", "Shirt_Loud", "Passenger", standing: false, hairMat: "Hair_Dark");
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
            AddProp(coffeeProp, "TrafficStop", "coffee");

            var headlight = MakeLight(policeCar, "Headlights", new Vector3(0, 0.7f, 2.0f), new Color(1f, 0.95f, 0.8f), 2.4f, 15f);
            headlight.type = LightType.Spot;
            headlight.spotAngle = 68f;
            headlight.transform.localRotation = Quaternion.Euler(2f, 0, 0);

            // --- Officer Glazer (with cap) ---
            var officer = BuildCharacter("Officer_Glazer", "officer",
                "Skin_Officer", "Uniform_Navy", "Officer", standing: true, hatMat: "Uniform_Navy");
            officer.transform.position = new Vector3(-1.6f, 0, -4.2f);

            var torso = officer.transform.Find("TorsoPivot");
            var pad = StripCollider(Prim(PrimitiveType.Cube, "TicketPad", torso,
                new Vector3(0.2f, 0.45f, 0.12f), new Vector3(0.16f, 0.03f, 0.22f), "Prop_Generic"));
            AddProp(pad, "TrafficStop", "ticketPad");
            var radio = StripCollider(Prim(PrimitiveType.Cube, "Radio", torso,
                new Vector3(-0.22f, 0.9f, 0.12f), new Vector3(0.08f, 0.12f, 0.06f), "Prop_Generic"));
            AddProp(radio, "TrafficStop", "radio");
            var armR = torso.Find("ArmR");
            var flashlight = StripCollider(Prim(PrimitiveType.Cube, "Flashlight", armR,
                new Vector3(0, -0.55f, 0.1f), new Vector3(0.45f, 0.5f, 0.45f), "Prop_Generic"));
            AddProp(flashlight, "TrafficStop", "flashlight");
            var beam = MakeLight(flashlight.transform, "Beam", new Vector3(0, -0.5f, 0.5f), new Color(1f, 0.98f, 0.85f), 2.5f, 9f);
            beam.type = LightType.Spot;
            beam.spotAngle = 42f;
            beam.transform.localRotation = Quaternion.Euler(10f, 0, 0);

            var license = Prim(PrimitiveType.Cube, "LicenseCard", playerCar,
                new Vector3(-0.15f, 1.02f, 0.95f), new Vector3(0.16f, 0.02f, 0.22f), "Line_White");
            AddProp(license, "TrafficStop", "license");
            var handOver = license.AddComponent<Interactable>();
            handOver.hintText = "Hand over your license";
            handOver.pulseTarget = license.transform;
            handOver.eventTextTemplate = "The driver held their license out the window for the officer.";
            handOver.cooldownSeconds = 10f;

            var locations = new GameObject("Locations").transform;
            Location(locations, "DriverWindow", new Vector3(-1.5f, 0, 0.2f), 90f);
            Location(locations, "PassengerWindow", new Vector3(1.5f, 0, 0.2f), -90f);
            Location(locations, "PoliceCar", new Vector3(-1.6f, 0, -4.2f), 0f);
            Location(locations, "PassengerSeat", new Vector3(0.42f, 0.7f, -0.2f), 0f);

            // --- first-person camera ---
            var (cameraGo, rig, raycaster) = BuildPlayerCamera(playerCar, new Vector3(-0.42f, 1.3f, -0.1f), 150f);

            // --- systems + wiring (shared) ---
            var wiring = WireGameSystems(
                "Assets/GameData/Scenarios/TrafficStop/TrafficStop_Scenario.asset",
                cameraGo, rig, raycaster,
                "Hold V to talk  ·  Enter to type  ·  Click things in the car",
                includeHarness: true, musicStyle: MusicStyle.Night);

            var speaker = officer.AddComponent<NpcSpeaker>();
            speaker.actorDisplayName = "Officer Glazer";
            speaker.voiceName = "David";
            speaker.rate = 2;
            speaker.pitch = -2;
            speaker.wobble = officer.GetComponent<WobbleAnimator>();

            EditorSceneManager.SaveScene(scene, "Assets/Scenes/TrafficStop.unity");
            Debug.Log("[TalkOut] TrafficStop scene built.");
        }

        private static void BuildCarInteractables(Transform playerCar)
        {
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
            glove.immediateEffects = new System.Collections.Generic.List<StatEffect>
            {
                StatEffect.Delta("suspicion", 7),
            };

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
            horn.immediateEffects = new System.Collections.Generic.List<StatEffect>
            {
                StatEffect.Delta("annoyance", 8),
                StatEffect.Delta("suspicion", 3),
            };

            var carRadio = Prim(PrimitiveType.Cube, "CarRadio", playerCar,
                new Vector3(0f, 0.98f, 0.9f), new Vector3(0.28f, 0.1f, 0.06f), "Prop_Generic");
            var radioToggle = carRadio.AddComponent<Interactable>();
            radioToggle.hintText = "Turn on the radio";
            radioToggle.isToggle = true;
            radioToggle.pulseTarget = carRadio.transform;
            radioToggle.eventTextTemplate = "The driver turned on the car radio. Extremely loud polka music fills the night.";
            radioToggle.eventTextClose = "The driver sheepishly turned the radio back off.";
            radioToggle.immediateEffects = new System.Collections.Generic.List<StatEffect>
            {
                StatEffect.Delta("annoyance", 6),
            };

            var shades = Prim(PrimitiveType.Cube, "Sunglasses", playerCar,
                new Vector3(0.15f, 1.02f, 0.95f), new Vector3(0.18f, 0.03f, 0.07f), "Wheel_Black");
            var shadesUse = shades.AddComponent<Interactable>();
            shadesUse.hintText = "Put on sunglasses";
            shadesUse.pulseTarget = shades.transform;
            shadesUse.eventTextTemplate = "The driver slowly put on sunglasses. It is currently the middle of the night.";
            shadesUse.cooldownSeconds = 8f;
            shadesUse.immediateEffects = new System.Collections.Generic.List<StatEffect>
            {
                StatEffect.Delta("amusement", 5),
                StatEffect.Delta("annoyance", 3),
            };
        }

        // ====================================================================
        // LEVEL 2 — THE DATE
        // ====================================================================
        private static void BuildDateScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            RenderSettings.skybox = null;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.30f, 0.24f, 0.18f);
            RenderSettings.ambientEquatorColor = new Color(0.22f, 0.17f, 0.13f);
            RenderSettings.ambientGroundColor = new Color(0.10f, 0.08f, 0.06f);
            RenderSettings.fog = false;

            var keyLight = new GameObject("WarmKey").AddComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.intensity = 0.35f;
            keyLight.color = new Color(1f, 0.85f, 0.68f);
            keyLight.shadows = LightShadows.Soft;
            keyLight.transform.rotation = Quaternion.Euler(55f, 25f, 0f);

            // --- room ---
            var room = new GameObject("Restaurant").transform;
            Prim(PrimitiveType.Plane, "Floor", room, Vector3.zero, new Vector3(2.4f, 1, 2.4f), "Wood_Floor");
            Prim(PrimitiveType.Cube, "WallBack", room, new Vector3(0, 1.6f, 6f), new Vector3(24f, 3.2f, 0.2f), "Wall_Warm");
            Prim(PrimitiveType.Cube, "WallLeft", room, new Vector3(-6f, 1.6f, 0), new Vector3(0.2f, 3.2f, 24f), "Wall_Warm");
            Prim(PrimitiveType.Cube, "WallRight", room, new Vector3(6f, 1.6f, 0), new Vector3(0.2f, 3.2f, 24f), "Wall_Warm");
            Prim(PrimitiveType.Cube, "WallFront", room, new Vector3(0, 1.6f, -6f), new Vector3(24f, 3.2f, 0.2f), "Wall_Warm");
            Prim(PrimitiveType.Cube, "Ceiling", room, new Vector3(0, 3.2f, 0), new Vector3(24f, 0.1f, 24f), "Pants_Dark");
            // wall art
            Prim(PrimitiveType.Cube, "Art1", room, new Vector3(-2.5f, 1.9f, 5.88f), new Vector3(1.2f, 0.9f, 0.05f), "Tree_Leaves");
            Prim(PrimitiveType.Cube, "Art2", room, new Vector3(2.5f, 1.9f, 5.88f), new Vector3(1.2f, 0.9f, 0.05f), "Car_Rust");
            // door (Chloe's escape route)
            Prim(PrimitiveType.Cube, "Door", room, new Vector3(4.0f, 1.1f, -5.88f), new Vector3(1.1f, 2.2f, 0.1f), "Tree_Trunk");

            // ceiling lamps
            foreach (var pos in new[] { new Vector3(0, 3.0f, 0), new Vector3(-3.5f, 3.0f, 3f), new Vector3(3.5f, 3.0f, 3f), new Vector3(0, 3.0f, -4f) })
            {
                Prim(PrimitiveType.Cube, "LampShade", room, pos + new Vector3(0, 0.1f, 0), new Vector3(0.4f, 0.12f, 0.4f), "Lamp_Warm");
                var lamp = MakeLight(room, "Lamp", pos, new Color(1f, 0.82f, 0.6f), 1.4f, 7f);
                lamp.shadows = LightShadows.Soft;
            }

            // --- your table ---
            var table = new GameObject("Table").transform;
            table.SetParent(room, false);
            table.position = Vector3.zero;
            Prim(PrimitiveType.Cube, "Top", table, new Vector3(0, 0.78f, 0), new Vector3(1.3f, 0.06f, 0.9f), "Table_Cloth");
            Prim(PrimitiveType.Cylinder, "Leg", table, new Vector3(0, 0.4f, 0), new Vector3(0.15f, 0.4f, 0.15f), "Tree_Trunk");
            Prim(PrimitiveType.Cube, "YourChair", table, new Vector3(0, 0.45f, -0.85f), new Vector3(0.5f, 0.1f, 0.5f), "Tree_Trunk");
            Prim(PrimitiveType.Cube, "HerChair", table, new Vector3(0, 0.45f, 0.85f), new Vector3(0.5f, 0.1f, 0.5f), "Tree_Trunk");
            Prim(PrimitiveType.Cube, "PlayerTorso", table, new Vector3(0, 0.95f, -0.85f), new Vector3(0.48f, 0.5f, 0.3f), "Car_White");

            // candle (emissive + light)
            var candle = Prim(PrimitiveType.Cylinder, "Candle", table, new Vector3(-0.25f, 0.88f, 0), new Vector3(0.05f, 0.07f, 0.05f), "Candle_Wax");
            Prim(PrimitiveType.Cube, "Flame", candle.transform, new Vector3(0, 1.2f, 0), new Vector3(0.5f, 0.4f, 0.5f), "Candle_Flame");
            var candleLight = MakeLight(candle.transform, "CandleLight", new Vector3(0, 2f, 0), new Color(1f, 0.75f, 0.45f), 1.6f, 3.5f);
            AddProp(candle, "Date", "candle");
            var candleUse = candle.AddComponent<Interactable>();
            candleUse.hintText = "Slide the candle closer to her";
            candleUse.pulseTarget = candle.transform;
            candleUse.eventTextTemplate = "The player slowly slid the candle toward Chloe. For romance. Allegedly.";
            candleUse.cooldownSeconds = 8f;
            candleUse.immediateEffects = new System.Collections.Generic.List<StatEffect>
            {
                StatEffect.Delta("awkwardness", 6),
                StatEffect.Delta("amusement", 4),
            };

            // wine glass (hers)
            var wine = Prim(PrimitiveType.Cylinder, "WineGlass", table, new Vector3(0.3f, 0.9f, 0.25f), new Vector3(0.06f, 0.09f, 0.06f), "Wine_Red");
            AddProp(wine, "Date", "wineGlass");

            // her phone
            var phone = Prim(PrimitiveType.Cube, "HerPhone", table, new Vector3(0.42f, 0.83f, 0.35f), new Vector3(0.1f, 0.015f, 0.18f), "Wheel_Black");
            AddProp(phone, "Date", "herPhone");

            // breadsticks
            var bread = Prim(PrimitiveType.Cube, "Breadsticks", table, new Vector3(0.25f, 0.86f, -0.15f), new Vector3(0.3f, 0.08f, 0.16f), "Bread_Tan");
            AddProp(bread, "Date", "breadsticks");
            var breadUse = bread.AddComponent<Interactable>();
            breadUse.hintText = "Stress-eat a breadstick";
            breadUse.pulseTarget = bread.transform;
            breadUse.eventTextTemplate = "The player grabbed a breadstick and ate it. Aggressively. Maintaining eye contact.";
            breadUse.cooldownSeconds = 5f;
            breadUse.immediateEffects = new System.Collections.Generic.List<StatEffect>
            {
                StatEffect.Delta("amusement", 4),
                StatEffect.Delta("awkwardness", 3),
            };

            // the bill
            var bill = Prim(PrimitiveType.Cube, "Bill", table, new Vector3(-0.35f, 0.83f, -0.25f), new Vector3(0.14f, 0.015f, 0.2f), "Napkin_White");
            AddProp(bill, "Date", "bill");
            var payBill = bill.AddComponent<Interactable>();
            payBill.hintText = "Grab the bill — you've got this";
            payBill.pulseTarget = bill.transform;
            payBill.eventTextTemplate = "The player grabbed the bill and declared they were paying, with unearned confidence.";
            payBill.cooldownSeconds = 15f;
            payBill.immediateEffects = new System.Collections.Generic.List<StatEffect>
            {
                StatEffect.Delta("interest", 6),
            };

            // your phone — a trap, obviously
            var yourPhone = Prim(PrimitiveType.Cube, "YourPhone", table, new Vector3(-0.15f, 0.83f, -0.3f), new Vector3(0.1f, 0.015f, 0.18f), "Wheel_Black");
            var checkPhone = yourPhone.AddComponent<Interactable>();
            checkPhone.hintText = "Check your phone";
            checkPhone.pulseTarget = yourPhone.transform;
            checkPhone.eventTextTemplate = "The player pulled out their phone and checked it. Mid-date. In front of her.";
            checkPhone.cooldownSeconds = 6f;
            checkPhone.immediateEffects = new System.Collections.Generic.List<StatEffect>
            {
                StatEffect.Delta("annoyance", 9),
                StatEffect.Delta("interest", -5),
            };

            // background couples (set dressing)
            BuildBackgroundCouple(room, new Vector3(-3.8f, 0, 3.4f), 30f);
            BuildBackgroundCouple(room, new Vector3(3.8f, 0, 2.2f), -20f);

            // --- Chloe ---
            var chloe = BuildCharacter("Chloe", "date",
                "Skin_Chloe", "Dress_Teal", "Chloe", standing: false, hairMat: "Hair_Brown");
            chloe.transform.position = new Vector3(0, 0.55f, 0.85f);
            chloe.transform.rotation = Quaternion.Euler(0, 180f, 0); // facing you
            chloe.GetComponent<NPCActor>().canWalk = true; // she CAN leave

            var chloeSpeaker = chloe.AddComponent<NpcSpeaker>();
            chloeSpeaker.actorDisplayName = "Chloe";
            chloeSpeaker.voiceName = "Zira";
            chloeSpeaker.rate = 2;
            chloeSpeaker.pitch = 2;
            chloeSpeaker.wobble = chloe.GetComponent<WobbleAnimator>();

            var locations = new GameObject("Locations").transform;
            Location(locations, "TableSeat", new Vector3(0, 0.55f, 0.85f), 180f);
            Location(locations, "Door", new Vector3(4.0f, 0, -5.2f), 180f);

            // --- first-person camera (your seat) ---
            var cameraHost = new GameObject("PlayerSeat");
            cameraHost.transform.position = new Vector3(0, 0, -0.85f);
            var (cameraGo, rig, raycaster) = BuildPlayerCamera(cameraHost.transform, new Vector3(0, 1.35f, 0), 120f);
            raycaster.range = 1.8f; // table reach

            WireGameSystems(
                "Assets/GameData/Scenarios/Date/Date_Scenario.asset",
                cameraGo, rig, raycaster,
                "Hold V to talk  ·  Enter to type  ·  Click things on the table",
                includeHarness: false, musicStyle: MusicStyle.Romantic);

            EditorSceneManager.SaveScene(scene, "Assets/Scenes/Date.unity");
            Debug.Log("[TalkOut] Date scene built.");
        }

        private static void BuildBackgroundCouple(Transform room, Vector3 position, float yaw)
        {
            var spot = new GameObject("BgTable").transform;
            spot.SetParent(room, false);
            spot.position = position;
            spot.rotation = Quaternion.Euler(0, yaw, 0);
            Prim(PrimitiveType.Cube, "Top", spot, new Vector3(0, 0.78f, 0), new Vector3(1.1f, 0.06f, 0.8f), "Table_Cloth");
            Prim(PrimitiveType.Cylinder, "Leg", spot, new Vector3(0, 0.4f, 0), new Vector3(0.14f, 0.4f, 0.14f), "Tree_Trunk");
            foreach (float z in new[] { 0.7f, -0.7f })
            {
                var person = new GameObject("Patron").transform;
                person.SetParent(spot, false);
                person.localPosition = new Vector3(0, 0.55f, z);
                person.localRotation = Quaternion.Euler(0, z > 0 ? 180f : 0f, 0);
                StripCollider(Prim(PrimitiveType.Cube, "Body", person, new Vector3(0, 0.42f, 0), new Vector3(0.5f, 0.75f, 0.3f), z > 0 ? "Shirt_Loud" : "Uniform_Navy"));
                StripCollider(Prim(PrimitiveType.Cube, "Head", person, new Vector3(0, 1.0f, 0), new Vector3(0.4f, 0.4f, 0.4f), "Skin_Passenger"));
            }
        }

        // ====================================================================
        // LEVEL 3 — THE EXECUTION (King)
        // ====================================================================
        private static void BuildKingScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            RenderSettings.skybox = null;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.22f, 0.18f, 0.14f);
            RenderSettings.ambientEquatorColor = new Color(0.15f, 0.12f, 0.10f);
            RenderSettings.ambientGroundColor = new Color(0.07f, 0.06f, 0.05f);
            RenderSettings.fog = false;

            var shaft = new GameObject("LightShaft").AddComponent<Light>();
            shaft.type = LightType.Directional;
            shaft.intensity = 0.3f;
            shaft.color = new Color(0.8f, 0.85f, 1f);
            shaft.shadows = LightShadows.Soft;
            shaft.transform.rotation = Quaternion.Euler(60f, 15f, 0f);

            // --- hall ---
            var hall = new GameObject("ThroneRoom").transform;
            Prim(PrimitiveType.Plane, "Floor", hall, Vector3.zero, new Vector3(2.4f, 1, 3f), "Stone_Dark");
            Prim(PrimitiveType.Cube, "Carpet", hall, new Vector3(0, 0.012f, 0.6f), new Vector3(1.6f, 0.02f, 9f), "Carpet_Red");
            Prim(PrimitiveType.Cube, "WallBack", hall, new Vector3(0, 2.5f, 6.5f), new Vector3(12f, 5f, 0.3f), "Stone_Grey");
            Prim(PrimitiveType.Cube, "WallFront", hall, new Vector3(0, 2.5f, -7f), new Vector3(12f, 5f, 0.3f), "Stone_Grey");
            Prim(PrimitiveType.Cube, "WallLeft", hall, new Vector3(-6f, 2.5f, 0), new Vector3(0.3f, 5f, 14f), "Stone_Grey");
            Prim(PrimitiveType.Cube, "WallRight", hall, new Vector3(6f, 2.5f, 0), new Vector3(0.3f, 5f, 14f), "Stone_Grey");
            Prim(PrimitiveType.Cube, "Ceiling", hall, new Vector3(0, 5f, 0), new Vector3(12f, 0.2f, 14f), "Stone_Dark");

            // banners behind the throne
            foreach (float x in new[] { -2.2f, 0f, 2.2f })
            {
                Prim(PrimitiveType.Cube, "Banner", hall, new Vector3(x, 3.2f, 6.3f), new Vector3(1.1f, 2.6f, 0.06f), "Banner_Red");
                Prim(PrimitiveType.Cube, "BannerTrim", hall, new Vector3(x, 4.45f, 6.28f), new Vector3(1.2f, 0.12f, 0.1f), "Gold");
            }

            // torch pillars with flicker
            foreach (var (x, z) in new[] { (-3.4f, 2.5f), (3.4f, 2.5f), (-3.4f, -2.5f), (3.4f, -2.5f) })
            {
                var pillar = Prim(PrimitiveType.Cube, "Pillar", hall, new Vector3(x, 1.75f, z), new Vector3(0.6f, 3.5f, 0.6f), "Stone_Grey");
                Prim(PrimitiveType.Cube, "TorchFlame", pillar.transform, new Vector3(0, 0.58f, 0), new Vector3(0.35f, 0.18f, 0.35f), "Candle_Flame");
                var torch = MakeLight(pillar.transform, "TorchLight", new Vector3(0, 0.72f, 0), new Color(1f, 0.65f, 0.3f), 1.7f, 8f);
                torch.shadows = LightShadows.Soft;
                var flicker = torch.gameObject.AddComponent<LightFlicker>();
                flicker.baseIntensity = 1.7f;
            }

            // --- dais + throne ---
            Prim(PrimitiveType.Cube, "DaisStep1", hall, new Vector3(0, 0.15f, 3.4f), new Vector3(4.2f, 0.3f, 3f), "Stone_Grey");
            Prim(PrimitiveType.Cube, "DaisStep2", hall, new Vector3(0, 0.45f, 3.7f), new Vector3(3.2f, 0.3f, 2.4f), "Stone_Grey");
            Prim(PrimitiveType.Cube, "ThroneSeat", hall, new Vector3(0, 0.92f, 3.9f), new Vector3(1.0f, 0.25f, 0.9f), "Gold");
            Prim(PrimitiveType.Cube, "ThroneBack", hall, new Vector3(0, 1.9f, 4.3f), new Vector3(1.0f, 1.9f, 0.18f), "Gold");
            Prim(PrimitiveType.Cube, "ThroneArmL", hall, new Vector3(-0.55f, 1.2f, 3.9f), new Vector3(0.14f, 0.35f, 0.85f), "Gold");
            Prim(PrimitiveType.Cube, "ThroneArmR", hall, new Vector3(0.55f, 1.2f, 3.9f), new Vector3(0.14f, 0.35f, 0.85f), "Gold");

            // --- the King (seated, crowned) ---
            var king = BuildCharacter("King_Aldric", "king",
                "Skin_King", "Velvet_Purple", "King", standing: false, hatMat: "Gold");
            king.transform.position = new Vector3(0, 1.05f, 3.85f);
            king.transform.rotation = Quaternion.Euler(0, 180f, 0);
            king.GetComponent<NPCActor>().canWalk = true; // he can rise and descend

            var kingTorso = king.transform.Find("TorsoPivot");
            var scepter = StripCollider(Prim(PrimitiveType.Cube, "Scepter", kingTorso.Find("ArmR"),
                new Vector3(0, -0.55f, 0.1f), new Vector3(0.35f, 1.1f, 0.35f), "Gold"));
            AddProp(scepter, "King", "scepter");

            var goblet = Prim(PrimitiveType.Cylinder, "Goblet", hall,
                new Vector3(0.55f, 1.45f, 3.85f), new Vector3(0.09f, 0.08f, 0.09f), "Gold");
            AddProp(goblet, "King", "goblet");

            var kingSpeaker = king.AddComponent<NpcSpeaker>();
            kingSpeaker.actorDisplayName = "King Aldric IV";
            kingSpeaker.voiceName = "David";
            kingSpeaker.rate = 1;
            kingSpeaker.pitch = 3; // pompous
            kingSpeaker.wobble = king.GetComponent<WobbleAnimator>();

            // --- Dennis the executioner (behind the prisoner) ---
            var dennis = BuildCharacter("Dennis_Executioner", "passenger",
                "Skin_Dennis", "Pants_Dark", "Dennis", standing: true, hatMat: "Pants_Dark");
            dennis.transform.position = new Vector3(1.7f, 0, -1.6f);
            dennis.transform.rotation = Quaternion.Euler(0, -120f, 0);
            dennis.GetComponent<NPCActor>().canWalk = false;

            var dennisTorso = dennis.transform.Find("TorsoPivot");
            var axe = new GameObject("Axe").transform;
            axe.SetParent(dennisTorso, false);
            axe.localPosition = new Vector3(0.3f, 0.3f, 0.15f);
            axe.localRotation = Quaternion.Euler(0, 0, 15f);
            StripCollider(Prim(PrimitiveType.Cube, "Handle", axe, new Vector3(0, 0.4f, 0), new Vector3(0.06f, 1.3f, 0.06f), "Tree_Trunk"));
            StripCollider(Prim(PrimitiveType.Cube, "Blade", axe, new Vector3(0.18f, 0.95f, 0), new Vector3(0.35f, 0.4f, 0.04f), "Axe_Steel"));
            AddProp(axe.gameObject, "King", "axe");

            // --- interactables around the kneeling prisoner ---
            var cushionStand = Prim(PrimitiveType.Cube, "EvidencePedestal", hall,
                new Vector3(-1.5f, 0.5f, 0.9f), new Vector3(0.4f, 1.0f, 0.4f), "Stone_Grey");
            var cushion = Prim(PrimitiveType.Cube, "Cushion", hall,
                new Vector3(-1.5f, 1.06f, 0.9f), new Vector3(0.42f, 0.12f, 0.42f), "Velvet_Purple");
            var fork = Prim(PrimitiveType.Cube, "FishFork", hall,
                new Vector3(-1.5f, 1.15f, 0.9f), new Vector3(0.05f, 0.02f, 0.28f), "Axe_Steel");
            AddProp(fork, "King", "fishFork");
            var forkUse = fork.AddComponent<Interactable>();
            forkUse.hintText = "Gesture at the fish fork";
            forkUse.pulseTarget = fork.transform;
            forkUse.eventTextTemplate = "The prisoner gestures dramatically at the fish fork — the entire case against them, resting on velvet.";
            forkUse.cooldownSeconds = 8f;
            forkUse.immediateEffects = new System.Collections.Generic.List<StatEffect>
            {
                StatEffect.Delta("amusement", 3),
            };

            // Reginald, the royal corgi
            var corgi = new GameObject("RoyalCorgi");
            corgi.transform.position = new Vector3(1.3f, 0, 1.7f);
            corgi.transform.rotation = Quaternion.Euler(0, -140f, 0);
            var corgiTorso = new GameObject("TorsoPivot").transform;
            corgiTorso.SetParent(corgi.transform, false);
            corgiTorso.localPosition = new Vector3(0, 0.18f, 0);
            Prim(PrimitiveType.Cube, "Body", corgiTorso, new Vector3(0, 0.14f, 0), new Vector3(0.32f, 0.26f, 0.62f), "Corgi_Tan");
            var corgiHead = Prim(PrimitiveType.Cube, "Head", corgiTorso, new Vector3(0, 0.34f, 0.34f), new Vector3(0.26f, 0.24f, 0.24f), "Corgi_Tan");
            StripCollider(Prim(PrimitiveType.Cube, "EarL", corgiHead.transform, new Vector3(-0.3f, 0.6f, 0), new Vector3(0.25f, 0.4f, 0.15f), "Corgi_Tan"));
            StripCollider(Prim(PrimitiveType.Cube, "EarR", corgiHead.transform, new Vector3(0.3f, 0.6f, 0), new Vector3(0.25f, 0.4f, 0.15f), "Corgi_Tan"));
            StripCollider(Prim(PrimitiveType.Cube, "Tail", corgiTorso, new Vector3(0, 0.28f, -0.36f), new Vector3(0.1f, 0.1f, 0.18f), "Napkin_White"));
            foreach (var (x, z) in new[] { (-0.1f, 0.2f), (0.1f, 0.2f), (-0.1f, -0.2f), (0.1f, -0.2f) })
            {
                StripCollider(Prim(PrimitiveType.Cube, "Leg", corgiTorso, new Vector3(x, -0.08f, z), new Vector3(0.09f, 0.2f, 0.09f), "Corgi_Tan"));
            }
            var corgiWobble = corgi.AddComponent<WobbleAnimator>();
            corgiWobble.torsoPivot = corgiTorso;
            corgiWobble.head = corgiHead.transform;
            corgiWobble.idleDegrees = 5f;
            AddProp(corgi, "King", "corgi");
            var petCorgi = corgi.AddComponent<Interactable>();
            petCorgi.hintText = "Pet the royal corgi";
            petCorgi.pulseTarget = corgiTorso;
            petCorgi.eventTextTemplate = "The prisoner pets Reginald, the royal corgi. Reginald allows it. The king watches very closely.";
            petCorgi.cooldownSeconds = 6f;
            petCorgi.immediateEffects = new System.Collections.Generic.List<StatEffect>
            {
                StatEffect.Delta("amusement", 5),
            };

            // chains, lute, groveling cushion
            var chains = Prim(PrimitiveType.Cube, "Chains", hall,
                new Vector3(0.35f, 0.55f, -1.15f), new Vector3(0.22f, 0.1f, 0.22f), "Guardrail");
            var rattle = chains.AddComponent<Interactable>();
            rattle.hintText = "Rattle your chains";
            rattle.pulseTarget = chains.transform;
            rattle.eventTextTemplate = "The prisoner rattles their chains. For emphasis.";
            rattle.cooldownSeconds = 5f;
            rattle.immediateEffects = new System.Collections.Generic.List<StatEffect>
            {
                StatEffect.Delta("annoyance", 3),
                StatEffect.Delta("amusement", 2),
            };

            var lute = Prim(PrimitiveType.Cube, "SadLute", hall,
                new Vector3(-1.1f, 0.08f, -0.9f), new Vector3(0.3f, 0.12f, 0.7f), "Tree_Trunk");
            var kickLute = lute.AddComponent<Interactable>();
            kickLute.hintText = "Kick the sad lute";
            kickLute.pulseTarget = lute.transform;
            kickLute.eventTextTemplate = "The prisoner kicks the abandoned lute. A single, deeply sad twang echoes through the hall.";
            kickLute.cooldownSeconds = 7f;
            kickLute.immediateEffects = new System.Collections.Generic.List<StatEffect>
            {
                StatEffect.Delta("amusement", 4),
            };

            var grovelSpot = Prim(PrimitiveType.Cube, "GrovelCushion", hall,
                new Vector3(0, 0.05f, -0.5f), new Vector3(0.6f, 0.08f, 0.4f), "Velvet_Purple");
            var grovel = grovelSpot.AddComponent<Interactable>();
            grovel.hintText = "Grovel dramatically";
            grovel.pulseTarget = grovelSpot.transform;
            grovel.eventTextTemplate = "The prisoner presses their forehead to the stone floor in exquisite, theatrical groveling.";
            grovel.cooldownSeconds = 10f;
            grovel.immediateEffects = new System.Collections.Generic.List<StatEffect>
            {
                StatEffect.Delta("flattery", 6),
            };

            // --- locations ---
            var locations = new GameObject("Locations").transform;
            Location(locations, "Throne", new Vector3(0, 1.05f, 3.85f), 180f);
            Location(locations, "ThroneSteps", new Vector3(0, 1.05f, 2.6f), 180f);
            Location(locations, "AxeSpot", new Vector3(1.7f, 0, -1.6f), -120f);

            // --- kneeling first-person camera ---
            var cameraHost = new GameObject("PrisonerSpot");
            cameraHost.transform.position = new Vector3(0, 0, -1.3f);
            Prim(PrimitiveType.Cube, "PlayerTorso", cameraHost.transform, new Vector3(0, 0.45f, 0), new Vector3(0.45f, 0.6f, 0.3f), "Car_White");
            var (cameraGo, rig, raycaster) = BuildPlayerCamera(cameraHost.transform, new Vector3(0, 0.95f, 0), 130f);
            raycaster.range = 2.4f;
            rig.minPitch = -40f;
            rig.maxPitch = 65f; // you WILL be looking up at this man

            WireGameSystems(
                "Assets/GameData/Scenarios/King/King_Scenario.asset",
                cameraGo, rig, raycaster,
                "Hold V to talk  ·  Enter to type  ·  Click things around you",
                includeHarness: false, musicStyle: MusicStyle.Royal);

            EditorSceneManager.SaveScene(scene, "Assets/Scenes/King.unity");
            Debug.Log("[TalkOut] King scene built.");
        }

        // ====================================================================
        // MAIN MENU
        // ====================================================================
        private static void BuildMainMenuScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // live diorama behind the menu: the traffic stop, lights flashing
            RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/NightSky.mat");
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.15f, 0.17f, 0.27f);
            RenderSettings.ambientEquatorColor = new Color(0.10f, 0.11f, 0.16f);
            RenderSettings.ambientGroundColor = new Color(0.05f, 0.06f, 0.08f);

            var moon = new GameObject("Moonlight").AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.intensity = 0.45f;
            moon.color = new Color(0.62f, 0.70f, 0.98f);
            moon.shadows = LightShadows.Soft;
            moon.transform.rotation = Quaternion.Euler(46f, -34f, 0f);
            RenderSettings.sun = moon;

            var diorama = new GameObject("Diorama").transform;
            Prim(PrimitiveType.Plane, "Ground", diorama, new Vector3(0, -0.02f, 0), new Vector3(10, 1, 10), "Ground_Night");
            Prim(PrimitiveType.Cube, "Road", diorama, new Vector3(0, -0.005f, 0), new Vector3(7f, 0.02f, 60f), "Asphalt");

            var menuCar = new GameObject("MenuCar").transform;
            menuCar.SetParent(diorama, false);
            menuCar.position = new Vector3(0.8f, 0, 1.5f);
            Prim(PrimitiveType.Cube, "Body", menuCar, new Vector3(0, 0.5f, 0), new Vector3(1.7f, 0.5f, 3.6f), "Car_Rust");
            Prim(PrimitiveType.Cube, "Roof", menuCar, new Vector3(0, 1.3f, -0.25f), new Vector3(1.5f, 0.55f, 1.9f), "Car_Rust");
            foreach (var (x, z) in new[] { (-0.95f, 1.25f), (0.95f, 1.25f), (-0.95f, -1.25f), (0.95f, -1.25f) })
            {
                Prim(PrimitiveType.Cube, "Wheel", menuCar, new Vector3(x, 0.26f, z), new Vector3(0.22f, 0.52f, 0.52f), "Wheel_Black");
            }

            var menuPolice = new GameObject("MenuPolice").transform;
            menuPolice.SetParent(diorama, false);
            menuPolice.position = new Vector3(1.2f, 0, -4.5f);
            Prim(PrimitiveType.Cube, "Body", menuPolice, new Vector3(0, 0.55f, 0), new Vector3(1.8f, 0.55f, 3.9f), "Car_Police");
            Prim(PrimitiveType.Cube, "Cabin", menuPolice, new Vector3(0, 1.12f, -0.3f), new Vector3(1.6f, 0.6f, 1.8f), "Car_White");
            var redCube = Prim(PrimitiveType.Cube, "RedLamp", menuPolice, new Vector3(-0.28f, 1.55f, -0.3f), new Vector3(0.36f, 0.2f, 0.36f), "LightBar_Red");
            var blueCube = Prim(PrimitiveType.Cube, "BlueLamp", menuPolice, new Vector3(0.28f, 1.55f, -0.3f), new Vector3(0.36f, 0.2f, 0.36f), "LightBar_Blue");
            var redLight = MakeLight(menuPolice, "RedLight", new Vector3(-0.28f, 1.9f, -0.3f), Color.red, 3f, 14f);
            var blueLight = MakeLight(menuPolice, "BlueLight", new Vector3(0.28f, 1.9f, -0.3f), Color.blue, 3f, 14f);
            var menuLights = menuPolice.gameObject.AddComponent<PoliceLights>();
            menuLights.redLight = redLight;
            menuLights.blueLight = blueLight;
            menuLights.redCube = redCube.GetComponent<Renderer>();
            menuLights.blueCube = blueCube.GetComponent<Renderer>();

            // the officer, wobbling patiently in the background
            var menuOfficer = BuildCharacter("MenuOfficer", "menu_officer",
                "Skin_Officer", "Uniform_Navy", "Officer", standing: true, hatMat: "Uniform_Navy");
            menuOfficer.transform.position = new Vector3(-0.9f, 0, 0.2f);
            menuOfficer.transform.rotation = Quaternion.Euler(0, 120f, 0);

            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.fieldOfView = 46f;
            cameraGo.transform.position = new Vector3(-4.6f, 2.0f, 4.4f);
            cameraGo.transform.rotation = Quaternion.LookRotation(
                new Vector3(0.4f, 0.9f, -1.2f) - cameraGo.transform.position);
            cameraGo.AddComponent<AudioListener>();
            SetupPostProcessing(cameraGo);

            var musicGo = new GameObject("Music");
            musicGo.AddComponent<AudioSource>();
            musicGo.AddComponent<MusicPlayer>().style = MusicStyle.Menu;

            var uiGo = new GameObject("UI");
            var uiDoc = uiGo.AddComponent<UIDocument>();
            uiDoc.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/TalkOutPanelSettings.asset");
            uiDoc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/MainMenu.uxml");
            var menu = uiGo.AddComponent<MainMenuController>();
            menu.llmConfig = AssetDatabase.LoadAssetAtPath<LlmConfig>("Assets/GameData/LlmConfig.asset");
            menu.levels = new System.Collections.Generic.List<MainMenuController.LevelEntry>
            {
                new MainMenuController.LevelEntry
                {
                    sceneName = "TrafficStop",
                    scenarioId = "traffic_stop",
                    title = "TRAFFIC STOP",
                    description = "Talk your way out of the ticket. The hamster is not helping."
                },
                new MainMenuController.LevelEntry
                {
                    sceneName = "Date",
                    scenarioId = "the_date",
                    title = "THE DATE",
                    description = "Convince Chloe to give you a second date. Do not mention the car."
                },
                new MainMenuController.LevelEntry
                {
                    sceneName = "King",
                    scenarioId = "the_king",
                    title = "THE EXECUTION",
                    description = "Convince the king your beheading is, legally speaking, a scheduling error."
                },
            };

            EditorSceneManager.SaveScene(scene, "Assets/Scenes/MainMenu.unity");
        }

        // ====================================================================
        // shared helpers
        // ====================================================================

        private static (GameObject cameraGo, FirstPersonRig rig, InteractionRaycaster raycaster)
            BuildPlayerCamera(Transform parent, Vector3 localPos, float maxYaw)
        {
            var cameraGo = new GameObject("PlayerCamera");
            cameraGo.tag = "MainCamera";
            cameraGo.transform.SetParent(parent, false);
            cameraGo.transform.localPosition = localPos;
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.fieldOfView = 62f;
            camera.nearClipPlane = 0.08f;
            cameraGo.AddComponent<AudioListener>();
            var rig = cameraGo.AddComponent<FirstPersonRig>();
            rig.maxYaw = maxYaw;
            var raycaster = cameraGo.AddComponent<InteractionRaycaster>();
            raycaster.playerCamera = camera;
            SetupPostProcessing(cameraGo);
            return (cameraGo, rig, raycaster);
        }

        private static TurnController WireGameSystems(
            string scenarioPath, GameObject cameraGo, FirstPersonRig rig,
            InteractionRaycaster raycaster, string micHint, bool includeHarness,
            MusicStyle musicStyle = MusicStyle.Night)
        {
            var systems = new GameObject("Systems");

            var musicGo = new GameObject("Music");
            musicGo.transform.SetParent(systems.transform);
            musicGo.AddComponent<AudioSource>();
            musicGo.AddComponent<MusicPlayer>().style = musicStyle;
            var turnController = systems.AddComponent<TurnController>();
            var gameManager = systems.AddComponent<GameManager>();
            var propRegistry = systems.AddComponent<PropRegistry>();
            var performer = systems.AddComponent<WorldPerformer>();
            var statsOverlay = systems.AddComponent<DebugStatsOverlay>();

            var scenario = AssetDatabase.LoadAssetAtPath<ScenarioDefinition>(scenarioPath);
            var llmConfig = AssetDatabase.LoadAssetAtPath<LlmConfig>("Assets/GameData/LlmConfig.asset");

            gameManager.scenario = scenario;
            gameManager.llmConfig = llmConfig;
            gameManager.turnController = turnController;
            gameManager.useMockBrains = false;

            performer.turnController = turnController;
            performer.propRegistry = propRegistry;
            statsOverlay.turnController = turnController;

            if (includeHarness)
            {
                var harness = systems.AddComponent<DirectorTestHarness>();
                harness.turnController = turnController;
                harness.corpus = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/GameData/TestCorpus.txt");
            }

            // LLM stack: one server, two agents (voice + judge)
            var llmGo = new GameObject("LLM");
            llmGo.transform.SetParent(systems.transform);
            var llm = llmGo.AddComponent<LLM>();
            llm.dontDestroyOnLoad = false;
            var llmSo = new SerializedObject(llm);
            TrySet(llmSo, new[] { "_model", "model", "m_Model" },
                p => p.stringValue = "Models/Dolphin3.0-Llama3.1-8B-Q4_K_M.gguf");
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

            // Whisper voice input
            var whisperGo = new GameObject("Whisper");
            whisperGo.transform.SetParent(systems.transform);
            var whisper = whisperGo.AddComponent<WhisperManager>();
            var whisperSo = new SerializedObject(whisper);
            TrySet(whisperSo, new[] { "modelPath", "_modelPath" }, p => p.stringValue = "Models/ggml-base.en.bin");
            TrySet(whisperSo, new[] { "isModelPathInStreamingAssets" }, p => p.boolValue = true);
            TrySet(whisperSo, new[] { "language" }, p => p.stringValue = "en");
            whisperSo.ApplyModifiedPropertiesWithoutUndo();

            var voiceInput = systems.AddComponent<VoiceInput>();
            voiceInput.whisper = whisper;
            voiceInput.turnController = turnController;

            // UI
            var uiGo = new GameObject("UI");
            var uiDoc = uiGo.AddComponent<UIDocument>();
            uiDoc.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/TalkOutPanelSettings.asset");
            uiDoc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Dialogue.uxml");
            var dialogue = uiGo.AddComponent<DialogueScreenController>();
            dialogue.turnController = turnController;
            dialogue.firstPersonRig = rig;
            dialogue.raycaster = raycaster;
            dialogue.voiceInput = voiceInput;
            dialogue.micHintText = micHint;

            return turnController;
        }

        /// Wobbly character: legs + hip TorsoPivot (kinematic RB) carrying body,
        /// head w/ face quad, physics floppy arms, optional hat or hair.
        private static GameObject BuildCharacter(
            string name, string actorId, string skinMat, string shirtMat, string faceSetName,
            bool standing, string hatMat = null, string hairMat = null)
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

            if (hatMat != null)
            {
                StripCollider(Prim(PrimitiveType.Cube, "HatBrim", head.transform, new Vector3(0, 0.5f, 0.15f), new Vector3(1.15f, 0.08f, 1.25f), hatMat));
                StripCollider(Prim(PrimitiveType.Cube, "HatTop", head.transform, new Vector3(0, 0.62f, -0.05f), new Vector3(0.95f, 0.3f, 0.95f), hatMat));
            }
            if (hairMat != null)
            {
                StripCollider(Prim(PrimitiveType.Cube, "HairTop", head.transform, new Vector3(0, 0.52f, -0.05f), new Vector3(1.1f, 0.25f, 1.05f), hairMat));
                StripCollider(Prim(PrimitiveType.Cube, "HairBack", head.transform, new Vector3(0, 0.05f, -0.52f), new Vector3(1.05f, 1.0f, 0.18f), hairMat));
            }

            var faceQuad = Prim(PrimitiveType.Quad, "FaceQuad", head.transform, new Vector3(0, 0, 0.51f), Vector3.one * 0.92f, null);
            faceQuad.transform.localRotation = Quaternion.Euler(0, 180f, 0);
            faceQuad.GetComponent<Renderer>().sharedMaterial =
                AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Face.mat");
            Object.DestroyImmediate(faceQuad.GetComponent<Collider>());

            MakeFloppyArm(torsoPivot, torsoRb, "ArmL", new Vector3(-0.36f, 0.74f, 0), shirtMat);
            MakeFloppyArm(torsoPivot, torsoRb, "ArmR", new Vector3(0.36f, 0.74f, 0), shirtMat);

            var face = root.AddComponent<FaceController>();
            face.faceRenderer = faceQuad.GetComponent<Renderer>();
            face.faceSet = FindFaceSet(faceSetName);

            var wobble = root.AddComponent<WobbleAnimator>();
            wobble.torsoPivot = torsoPivot;
            wobble.head = head.transform;

            var actor = root.AddComponent<NPCActor>();
            actor.actorId = actorId;
            actor.face = face;
            actor.wobble = wobble;

            return root;
        }

        private static FaceSet FindFaceSet(string faceSetName)
        {
            foreach (var guid in AssetDatabase.FindAssets($"{faceSetName}_FaceSet t:FaceSet"))
            {
                var faceSet = AssetDatabase.LoadAssetAtPath<FaceSet>(AssetDatabase.GUIDToAssetPath(guid));
                if (faceSet != null) return faceSet;
            }
            Debug.LogWarning($"[TalkOut] FaceSet '{faceSetName}_FaceSet' not found.");
            return null;
        }

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
            joint.anchor = new Vector3(0, 0.5f, 0);
            joint.enableProjection = true;
            var lowTwist = joint.lowTwistLimit; lowTwist.limit = -20f; joint.lowTwistLimit = lowTwist;
            var highTwist = joint.highTwistLimit; highTwist.limit = 20f; joint.highTwistLimit = highTwist;
            var swing1 = joint.swing1Limit; swing1.limit = 70f; joint.swing1Limit = swing1;
            var swing2 = joint.swing2Limit; swing2.limit = 70f; joint.swing2Limit = swing2;
        }

        private static void SetupPostProcessing(GameObject cameraGo)
        {
            string profilePath = "Assets/GameData/TalkOutPostFX.asset";
            var profile = AssetDatabase.LoadAssetAtPath<PostProcessProfile>(profilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<PostProcessProfile>();
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

                EditorUtility.SetDirty(profile);
            }

            var volumeGo = new GameObject("PostFXVolume") { layer = PostFxLayer };
            var volume = volumeGo.AddComponent<PostProcessVolume>();
            volume.isGlobal = true;
            volume.profile = profile;

            var layer = cameraGo.AddComponent<PostProcessLayer>();
            layer.volumeLayer = 1 << PostFxLayer;
            layer.volumeTrigger = cameraGo.transform;
            layer.antialiasingMode = PostProcessLayer.Antialiasing.FastApproximateAntialiasing;

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
        }

        private static Light MakeLight(Transform parent, string name, Vector3 localPos, Color color, float intensity, float range)
        {
            var light = new GameObject(name).AddComponent<Light>();
            if (parent != null) light.transform.SetParent(parent, false);
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

        private static void AddProp(GameObject go, string scenarioFolder, string propId)
        {
            var sceneProp = go.AddComponent<SceneProp>();
            sceneProp.definition = AssetDatabase.LoadAssetAtPath<PropDefinition>(
                $"Assets/GameData/Scenarios/{scenarioFolder}/Props/{propId}.asset");
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
