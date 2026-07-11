using System;
using System.Collections.Generic;
using UnityEngine;
using TalkOut.Audio;

namespace TalkOut.Data
{
    public enum EnvironmentPreset { NightRoad, WarmInterior, StoneHall, ComedyClub }

    [Serializable]
    public class TemplateNpc
    {
        public NPCDefinition definition;
        [Tooltip("Uncheck for bodiless entities like a crowd")]
        public bool hasBody = true;
        public Vector3 position;
        public float yaw;
        public bool standing = true;
        public bool canWalk = true;
        public string skinMat = "Skin_Officer";
        public string shirtMat = "Uniform_Navy";
        public string hatMat = "";
        public string hairMat = "";
        [Tooltip("FaceTextures folder name, e.g. 'Officer'")]
        public string faceSetName = "";

        [Header("Voice")]
        public bool speaks = true;
        public string piperModel = "";
        public string sapiVoice = "David";
        [Range(-10, 10)] public int sapiRate;
        [Range(-10, 10)] public int sapiPitch;
    }

    [Serializable]
    public class TemplateInteractable
    {
        public string name = "Thing";
        public PrimitiveType shape = PrimitiveType.Cube;
        public Vector3 position;
        public Vector3 scale = new Vector3(0.2f, 0.2f, 0.2f);
        public string material = "Prop_Generic";
        public string hint = "Interact";
        [TextArea(2, 3)] public string eventText = "";
        [TextArea(2, 3)] public string eventTextClose = "";
        public bool isToggle;
        public bool copMayReact = true;
        public float cooldownSeconds = 5f;
        public List<StatEffect> effects = new List<StatEffect>();
        public string[] randomContents;
        [Tooltip("Also register as a SceneProp with this prop id (for judge actions)")]
        public string scenePropId = "";
    }

    [Serializable]
    public class TemplateLocation
    {
        public string id;
        public Vector3 position;
        public float yaw;
    }

    /// One asset = one level. The template scene builder turns this into a
    /// playable scene: environment preset, characters, voices, interactables,
    /// locations, systems wiring. New levels are data, not code.
    [CreateAssetMenu(menuName = "TalkOut/Level Template", fileName = "LevelTemplate")]
    public class LevelTemplate : ScriptableObject
    {
        public ScenarioDefinition scenario;
        [Tooltip("Scene file name (Assets/Scenes/<name>.unity) — also used by the menu")]
        public string sceneName;
        [Tooltip("One-liner on the level-select card")]
        public string menuDescription;

        [Header("Stage")]
        public EnvironmentPreset environment = EnvironmentPreset.WarmInterior;
        public bool includeMusic = true;
        public MusicStyle musicStyle = MusicStyle.Menu;
        [Tooltip("Procedural crowd reactions (comedy club)")]
        public bool crowdAudio;

        [Header("Player")]
        public Vector3 playerPosition;
        public float playerYaw;
        public float playerEyeHeight = 1.25f;
        public float maxYaw = 130f;
        public float minPitch = -50f;
        public float maxPitch = 60f;
        public float interactRange = 2.4f;
        public string micHint = "Hold V to talk  ·  Enter to type  ·  Click things around you";

        public List<TemplateNpc> npcs = new List<TemplateNpc>();
        public List<TemplateInteractable> interactables = new List<TemplateInteractable>();
        public List<TemplateLocation> locations = new List<TemplateLocation>();
    }
}
