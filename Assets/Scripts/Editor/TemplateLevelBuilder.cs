using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TalkOut.Actors;
using TalkOut.Audio;
using TalkOut.Core;
using TalkOut.Data;
using TalkOut.Player;
using TalkOut.World;

namespace TalkOut.EditorTools
{
    /// Turns LevelTemplate assets into playable scenes. From here on, a new
    /// level = one scenario asset + one template asset; no new builder code.
    public static class TemplateLevelBuilder
    {
        [MenuItem("Tools/TalkOut/4. Build Template Levels")]
        public static void BuildAllTemplates()
        {
            foreach (var template in FindAllTemplates())
            {
                BuildFromTemplate(template);
            }
        }

        public static List<LevelTemplate> FindAllTemplates()
        {
            var templates = new List<LevelTemplate>();
            foreach (var guid in AssetDatabase.FindAssets("t:LevelTemplate"))
            {
                var template = AssetDatabase.LoadAssetAtPath<LevelTemplate>(AssetDatabase.GUIDToAssetPath(guid));
                if (template != null && template.scenario != null && !string.IsNullOrEmpty(template.sceneName))
                {
                    templates.Add(template);
                }
            }
            return templates;
        }

        public static void BuildFromTemplate(LevelTemplate template)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildEnvironment(template.environment);

            // characters
            foreach (var npc in template.npcs)
            {
                if (npc.definition == null || !npc.hasBody) continue;

                var character = TalkOutSceneBuilder.BuildCharacter(
                    npc.definition.displayName.Replace(" ", "_"), npc.definition.id,
                    npc.skinMat, npc.shirtMat,
                    string.IsNullOrEmpty(npc.faceSetName) ? npc.definition.displayName : npc.faceSetName,
                    npc.standing,
                    string.IsNullOrEmpty(npc.hatMat) ? null : npc.hatMat,
                    string.IsNullOrEmpty(npc.hairMat) ? null : npc.hairMat);
                character.transform.position = npc.position;
                character.transform.rotation = Quaternion.Euler(0, npc.yaw, 0);
                character.GetComponent<NPCActor>().canWalk = npc.canWalk;

                if (npc.speaks)
                {
                    var speaker = character.AddComponent<NpcSpeaker>();
                    speaker.actorDisplayName = npc.definition.displayName;
                    speaker.piperModel = npc.piperModel;
                    speaker.voiceName = npc.sapiVoice;
                    speaker.rate = npc.sapiRate;
                    speaker.pitch = npc.sapiPitch;
                    speaker.wobble = character.GetComponent<WobbleAnimator>();
                }
            }

            // interactables
            foreach (var item in template.interactables)
            {
                var go = TalkOutSceneBuilder.Prim(item.shape, item.name, null, item.position, item.scale, item.material);
                var use = go.AddComponent<Interactable>();
                use.hintText = item.hint;
                use.eventTextTemplate = item.eventText;
                use.eventTextClose = item.eventTextClose;
                use.isToggle = item.isToggle;
                use.copMayReact = item.copMayReact;
                use.cooldownSeconds = item.cooldownSeconds;
                use.immediateEffects = new List<StatEffect>(item.effects);
                use.randomContents = item.randomContents;
                use.pulseTarget = go.transform;

                if (!string.IsNullOrEmpty(item.scenePropId))
                {
                    var propDef = template.scenario.props.Find(p => p != null && p.id == item.scenePropId);
                    if (propDef != null)
                    {
                        var sceneProp = go.AddComponent<Props.SceneProp>();
                        sceneProp.definition = propDef;
                        sceneProp.targetRenderer = go.GetComponent<Renderer>();
                    }
                }
            }

            // locations
            var locations = new GameObject("Locations").transform;
            foreach (var location in template.locations)
            {
                TalkOutSceneBuilder.Location(locations, location.id, location.position, location.yaw);
            }

            // player + systems
            var cameraHost = new GameObject("PlayerSpot");
            cameraHost.transform.position = template.playerPosition;
            cameraHost.transform.rotation = Quaternion.Euler(0, template.playerYaw, 0);
            TalkOutSceneBuilder.Prim(PrimitiveType.Cube, "PlayerTorso", cameraHost.transform,
                new Vector3(0, template.playerEyeHeight - 0.55f, 0), new Vector3(0.45f, 0.6f, 0.3f), "Car_White");
            var (cameraGo, rig, raycaster) = TalkOutSceneBuilder.BuildPlayerCamera(
                cameraHost.transform, new Vector3(0, template.playerEyeHeight, 0), template.maxYaw);
            rig.minPitch = template.minPitch;
            rig.maxPitch = template.maxPitch;
            raycaster.range = template.interactRange;

            var turnController = TalkOutSceneBuilder.WireGameSystems(
                AssetDatabase.GetAssetPath(template.scenario),
                cameraGo, rig, raycaster, template.micHint,
                includeHarness: false,
                musicStyle: template.musicStyle,
                includeMusic: template.includeMusic);

            if (template.crowdAudio)
            {
                var crowdGo = new GameObject("CrowdAudio");
                crowdGo.AddComponent<AudioSource>();
                var reactor = crowdGo.AddComponent<CrowdAudioReactor>();
                reactor.turnController = turnController;
                // real recordings drop in by convention; synth covers the gaps
                reactor.coughClip = LoadClip("cough");
                reactor.murmurClip = LoadClip("murmur");
                reactor.chuckleClip = LoadClip("laugh_small");
                reactor.laughClip = LoadClip("laugh_big");
                reactor.booClip = LoadClip("boo");
            }

            EditorSceneManager.SaveScene(scene, $"Assets/Scenes/{template.sceneName}.unity");
            Debug.Log($"[TalkOut] Template level built: {template.sceneName}");
        }

        private static AudioClip LoadClip(string baseName)
        {
            foreach (var ext in new[] { "wav", "ogg", "mp3" })
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Audio/Crowd/{baseName}.{ext}");
                if (clip != null) return clip;
            }
            return null;
        }

        // ------------------------------------------------------------------
        private static void BuildEnvironment(EnvironmentPreset preset)
        {
            switch (preset)
            {
                case EnvironmentPreset.NightRoad: BuildNightRoad(); break;
                case EnvironmentPreset.WarmInterior: BuildWarmInterior(); break;
                case EnvironmentPreset.StoneHall: BuildStoneHall(); break;
                case EnvironmentPreset.ComedyClub: BuildComedyClub(); break;
            }
        }

        private static void BuildNightRoad()
        {
            RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/NightSky.mat");
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.17f, 0.19f, 0.30f);
            RenderSettings.ambientEquatorColor = new Color(0.12f, 0.13f, 0.19f);
            RenderSettings.ambientGroundColor = new Color(0.05f, 0.06f, 0.08f);
            var moon = new GameObject("Moonlight").AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.intensity = 0.55f;
            moon.color = new Color(0.62f, 0.70f, 0.98f);
            moon.shadows = LightShadows.Soft;
            moon.transform.rotation = Quaternion.Euler(46f, -34f, 0f);
            RenderSettings.sun = moon;
            var env = new GameObject("Environment").transform;
            TalkOutSceneBuilder.Prim(PrimitiveType.Plane, "Ground", env, new Vector3(0, -0.02f, 0), new Vector3(20, 1, 20), "Ground_Night");
            TalkOutSceneBuilder.Prim(PrimitiveType.Cube, "Road", env, new Vector3(0, -0.005f, 0), new Vector3(7f, 0.02f, 120f), "Asphalt");
        }

        private static void BuildWarmInterior()
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.30f, 0.24f, 0.18f);
            RenderSettings.ambientEquatorColor = new Color(0.22f, 0.17f, 0.13f);
            RenderSettings.ambientGroundColor = new Color(0.10f, 0.08f, 0.06f);
            var room = new GameObject("Room").transform;
            TalkOutSceneBuilder.Prim(PrimitiveType.Plane, "Floor", room, Vector3.zero, new Vector3(2.4f, 1, 2.4f), "Wood_Floor");
            TalkOutSceneBuilder.Prim(PrimitiveType.Cube, "WallBack", room, new Vector3(0, 1.6f, 6f), new Vector3(24f, 3.2f, 0.2f), "Wall_Warm");
            TalkOutSceneBuilder.Prim(PrimitiveType.Cube, "WallLeft", room, new Vector3(-6f, 1.6f, 0), new Vector3(0.2f, 3.2f, 24f), "Wall_Warm");
            TalkOutSceneBuilder.Prim(PrimitiveType.Cube, "WallRight", room, new Vector3(6f, 1.6f, 0), new Vector3(0.2f, 3.2f, 24f), "Wall_Warm");
            TalkOutSceneBuilder.Prim(PrimitiveType.Cube, "WallFront", room, new Vector3(0, 1.6f, -6f), new Vector3(24f, 3.2f, 0.2f), "Wall_Warm");
            TalkOutSceneBuilder.Prim(PrimitiveType.Cube, "Ceiling", room, new Vector3(0, 3.2f, 0), new Vector3(24f, 0.1f, 24f), "Pants_Dark");
            foreach (var pos in new[] { new Vector3(0, 3.0f, 0), new Vector3(-3.5f, 3.0f, 3f), new Vector3(3.5f, 3.0f, 3f) })
            {
                var lamp = TalkOutSceneBuilder.MakeLight(room, "Lamp", pos, new Color(1f, 0.82f, 0.6f), 1.4f, 7f);
                lamp.shadows = LightShadows.Soft;
            }
        }

        private static void BuildStoneHall()
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.22f, 0.18f, 0.14f);
            RenderSettings.ambientEquatorColor = new Color(0.15f, 0.12f, 0.10f);
            RenderSettings.ambientGroundColor = new Color(0.07f, 0.06f, 0.05f);
            var hall = new GameObject("Hall").transform;
            TalkOutSceneBuilder.Prim(PrimitiveType.Plane, "Floor", hall, Vector3.zero, new Vector3(2.4f, 1, 3f), "Stone_Dark");
            TalkOutSceneBuilder.Prim(PrimitiveType.Cube, "WallBack", hall, new Vector3(0, 2.5f, 6.5f), new Vector3(12f, 5f, 0.3f), "Stone_Grey");
            TalkOutSceneBuilder.Prim(PrimitiveType.Cube, "WallFront", hall, new Vector3(0, 2.5f, -7f), new Vector3(12f, 5f, 0.3f), "Stone_Grey");
            TalkOutSceneBuilder.Prim(PrimitiveType.Cube, "WallLeft", hall, new Vector3(-6f, 2.5f, 0), new Vector3(0.3f, 5f, 14f), "Stone_Grey");
            TalkOutSceneBuilder.Prim(PrimitiveType.Cube, "WallRight", hall, new Vector3(6f, 2.5f, 0), new Vector3(0.3f, 5f, 14f), "Stone_Grey");
            TalkOutSceneBuilder.Prim(PrimitiveType.Cube, "Ceiling", hall, new Vector3(0, 5f, 0), new Vector3(12f, 0.2f, 14f), "Stone_Dark");
            foreach (var (x, z) in new[] { (-3.4f, 2.5f), (3.4f, 2.5f), (-3.4f, -2.5f), (3.4f, -2.5f) })
            {
                var pillar = TalkOutSceneBuilder.Prim(PrimitiveType.Cube, "Pillar", hall, new Vector3(x, 1.75f, z), new Vector3(0.6f, 3.5f, 0.6f), "Stone_Grey");
                TalkOutSceneBuilder.Prim(PrimitiveType.Cube, "TorchFlame", pillar.transform, new Vector3(0, 0.58f, 0), new Vector3(0.35f, 0.18f, 0.35f), "Candle_Flame");
                var torch = TalkOutSceneBuilder.MakeLight(pillar.transform, "TorchLight", new Vector3(0, 0.72f, 0), new Color(1f, 0.65f, 0.3f), 1.7f, 8f);
                torch.gameObject.AddComponent<LightFlicker>();
            }
        }

        private static void BuildComedyClub()
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.10f, 0.07f, 0.09f);
            RenderSettings.ambientEquatorColor = new Color(0.07f, 0.05f, 0.06f);
            RenderSettings.ambientGroundColor = new Color(0.03f, 0.02f, 0.03f);

            var club = new GameObject("Club").transform;
            TalkOutSceneBuilder.Prim(PrimitiveType.Plane, "Floor", club, Vector3.zero, new Vector3(2.4f, 1, 2.4f), "Stone_Dark");
            TalkOutSceneBuilder.Prim(PrimitiveType.Cube, "BrickWall", club, new Vector3(0, 1.8f, -4.2f), new Vector3(14f, 3.6f, 0.2f), "Brick_Red");
            TalkOutSceneBuilder.Prim(PrimitiveType.Cube, "WallLeft", club, new Vector3(-7f, 1.8f, 2f), new Vector3(0.2f, 3.6f, 14f), "Pants_Dark");
            TalkOutSceneBuilder.Prim(PrimitiveType.Cube, "WallRight", club, new Vector3(7f, 1.8f, 2f), new Vector3(0.2f, 3.6f, 14f), "Pants_Dark");
            TalkOutSceneBuilder.Prim(PrimitiveType.Cube, "WallBack", club, new Vector3(0, 1.8f, 9f), new Vector3(14f, 3.6f, 0.2f), "Pants_Dark");
            TalkOutSceneBuilder.Prim(PrimitiveType.Cube, "Ceiling", club, new Vector3(0, 3.6f, 2f), new Vector3(14f, 0.1f, 14f), "Pants_Dark");

            // stage (player stands here, in front of the brick wall)
            TalkOutSceneBuilder.Prim(PrimitiveType.Cube, "Stage", club, new Vector3(0, 0.2f, -3f), new Vector3(4.5f, 0.4f, 2.2f), "Tree_Trunk");

            // the spotlight: you, illuminated; them, in the dark
            var spot = TalkOutSceneBuilder.MakeLight(club, "Spotlight", new Vector3(0, 3.4f, -0.5f), new Color(1f, 0.95f, 0.85f), 3.2f, 10f);
            spot.type = LightType.Spot;
            spot.spotAngle = 42f;
            spot.shadows = LightShadows.Soft;
            spot.transform.rotation = Quaternion.Euler(64f, 0, 0);

            // dim red table lamps + audience silhouettes at little round tables
            var rng = new System.Random(7);
            foreach (var (x, z) in new[] { (-3.2f, 0.8f), (0.2f, 1.4f), (3.1f, 0.9f), (-1.8f, 3.2f), (1.9f, 3.4f), (-3.6f, 5.4f), (0.4f, 5.8f), (3.4f, 5.5f) })
            {
                var table = new GameObject("Table").transform;
                table.SetParent(club, false);
                table.position = new Vector3(x, 0, z);
                TalkOutSceneBuilder.Prim(PrimitiveType.Cylinder, "Top", table, new Vector3(0, 0.75f, 0), new Vector3(0.7f, 0.03f, 0.7f), "Wheel_Black");
                TalkOutSceneBuilder.Prim(PrimitiveType.Cylinder, "Leg", table, new Vector3(0, 0.38f, 0), new Vector3(0.08f, 0.38f, 0.08f), "Wheel_Black");
                TalkOutSceneBuilder.MakeLight(table, "TableLamp", new Vector3(0, 0.95f, 0), new Color(1f, 0.25f, 0.2f), 0.5f, 2.2f);

                // 1-2 dark silhouettes per table, facing the stage
                int patrons = 1 + rng.Next(2);
                for (int i = 0; i < patrons; i++)
                {
                    float ang = (float)(rng.NextDouble() * Mathf.PI * 2f);
                    var seat = new Vector3(Mathf.Cos(ang) * 0.65f, 0, Mathf.Sin(ang) * 0.65f);
                    var person = new GameObject("Silhouette").transform;
                    person.SetParent(table, false);
                    person.localPosition = seat + new Vector3(0, 0.55f, 0);
                    person.LookAt(new Vector3(0, 0.55f, -3f));
                    TalkOutSceneBuilder.StripCollider(TalkOutSceneBuilder.Prim(PrimitiveType.Cube, "Body", person, new Vector3(0, 0.35f, 0), new Vector3(0.45f, 0.7f, 0.28f), "Wheel_Black"));
                    TalkOutSceneBuilder.StripCollider(TalkOutSceneBuilder.Prim(PrimitiveType.Cube, "Head", person, new Vector3(0, 0.9f, 0), new Vector3(0.35f, 0.35f, 0.35f), "Wheel_Black"));
                }
            }

            // neon-ish sign behind the stage
            TalkOutSceneBuilder.Prim(PrimitiveType.Cube, "SignGlow", club, new Vector3(0, 2.6f, -4.08f), new Vector3(3.2f, 0.5f, 0.05f), "Lamp_Warm");
        }
    }
}
