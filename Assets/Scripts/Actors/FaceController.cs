using UnityEngine;
using TalkOut.Data;

namespace TalkOut.Actors
{
    /// Swaps the face texture on the head's face quad. Hard cuts on purpose —
    /// that's the blocky style. Also runs a random blink if the set has one.
    public class FaceController : MonoBehaviour
    {
        public Renderer faceRenderer;
        public FaceSet faceSet;

        [Tooltip("Seconds between blinks (randomized ±50%)")]
        public float blinkInterval = 4f;
        public float blinkDuration = 0.12f;

        private MaterialPropertyBlock block;
        private string currentEmotion = "";
        private float nextBlinkAt;
        private float blinkUntil = -1f;
        private static readonly int MainTex = Shader.PropertyToID("_MainTex");

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            ScheduleNextBlink();
        }

        public void SetFace(string emotion)
        {
            currentEmotion = emotion;
            Apply(faceSet != null ? faceSet.GetFace(emotion) : null);
        }

        private void Update()
        {
            if (faceSet == null) return;

            if (blinkUntil > 0f)
            {
                if (Time.time >= blinkUntil)
                {
                    blinkUntil = -1f;
                    SetFace(currentEmotion);
                    ScheduleNextBlink();
                }
                return;
            }

            if (Time.time >= nextBlinkAt)
            {
                var blink = faceSet.GetFace("blink");
                if (blink != null && blink != faceSet.defaultFace)
                {
                    Apply(blink);
                    blinkUntil = Time.time + blinkDuration;
                }
                else
                {
                    ScheduleNextBlink();
                }
            }
        }

        private void Apply(Texture2D texture)
        {
            if (faceRenderer == null || texture == null) return;
            faceRenderer.GetPropertyBlock(block);
            block.SetTexture(MainTex, texture);
            faceRenderer.SetPropertyBlock(block);
        }

        private void ScheduleNextBlink()
        {
            nextBlinkAt = Time.time + blinkInterval * Random.Range(0.5f, 1.5f);
        }
    }
}
