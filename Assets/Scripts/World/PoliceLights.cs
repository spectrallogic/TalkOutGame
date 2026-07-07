using UnityEngine;

namespace TalkOut.World
{
    /// Alternating red/blue flashers on the police car light bar.
    public class PoliceLights : MonoBehaviour
    {
        public Light redLight;
        public Light blueLight;
        public Renderer redCube;
        public Renderer blueCube;
        public float flashHz = 2f;

        private MaterialPropertyBlock block;
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private void Awake()
        {
            block = new MaterialPropertyBlock();
        }

        private void Update()
        {
            bool redOn = Mathf.Sin(Time.time * flashHz * Mathf.PI * 2f) > 0f;
            if (redLight != null) redLight.enabled = redOn;
            if (blueLight != null) blueLight.enabled = !redOn;

            SetCube(redCube, redOn ? Color.red : new Color(0.3f, 0.05f, 0.05f));
            SetCube(blueCube, !redOn ? Color.blue : new Color(0.05f, 0.05f, 0.3f));
        }

        private void SetCube(Renderer renderer, Color color)
        {
            if (renderer == null) return;
            renderer.GetPropertyBlock(block);
            block.SetColor(ColorId, color);
            renderer.SetPropertyBlock(block);
        }
    }
}
