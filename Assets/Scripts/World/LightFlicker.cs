using UnityEngine;

namespace TalkOut.World
{
    /// Torch-style flicker: Perlin-wobbled intensity, unique per instance.
    [RequireComponent(typeof(Light))]
    public class LightFlicker : MonoBehaviour
    {
        public float baseIntensity = 1.6f;
        public float flickerAmount = 0.45f;
        public float speed = 7f;

        private Light torch;
        private float seed;

        private void Awake()
        {
            torch = GetComponent<Light>();
            seed = GetInstanceID() * 0.913f;
        }

        private void Update()
        {
            float noise = Mathf.PerlinNoise(Time.time * speed, seed);
            torch.intensity = baseIntensity + (noise - 0.5f) * 2f * flickerAmount;
        }
    }
}
