using System;
using System.Collections.Generic;
using UnityEngine;

namespace TalkOut.Data
{
    /// Emotion name -> face texture mapping for one character.
    [CreateAssetMenu(menuName = "TalkOut/Face Set", fileName = "FaceSet")]
    public class FaceSet : ScriptableObject
    {
        [Serializable]
        public struct FaceEntry
        {
            public string emotion;
            public Texture2D texture;
        }

        public Texture2D defaultFace;
        public List<FaceEntry> faces = new List<FaceEntry>();

        public Texture2D GetFace(string emotion)
        {
            if (!string.IsNullOrEmpty(emotion))
            {
                foreach (var entry in faces)
                {
                    if (entry.emotion == emotion && entry.texture != null) return entry.texture;
                }
            }
            return defaultFace;
        }
    }
}
