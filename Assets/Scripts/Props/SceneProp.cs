using System.Threading.Tasks;
using UnityEngine;
using TalkOut.Data;

namespace TalkOut.Props
{
    /// A physical interactable the LLM director can target through actions:
    /// pulses bright and plays a micro-animation when used.
    public class SceneProp : MonoBehaviour
    {
        public PropDefinition definition;
        public Renderer targetRenderer;
        public AudioSource audioSource;

        private MaterialPropertyBlock block;
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private Color baseColor = Color.white;
        private bool baseColorCached;

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            if (targetRenderer == null) targetRenderer = GetComponentInChildren<Renderer>();
            if (targetRenderer != null && targetRenderer.sharedMaterial != null &&
                targetRenderer.sharedMaterial.HasProperty(ColorId))
            {
                baseColor = targetRenderer.sharedMaterial.color;
                baseColorCached = true;
            }
        }

        public async Task UseAsync()
        {
            if (audioSource != null && audioSource.clip != null) audioSource.Play();
            var highlight = definition != null ? definition.highlightColor : Color.yellow;

            float duration = 0.9f;
            float t = 0f;
            Vector3 baseScale = transform.localScale;
            while (t < duration && this != null)
            {
                t += Time.deltaTime;
                float pulse = Mathf.Sin(Mathf.Clamp01(t / duration) * Mathf.PI);
                if (targetRenderer != null && baseColorCached)
                {
                    targetRenderer.GetPropertyBlock(block);
                    block.SetColor(ColorId, Color.Lerp(baseColor, highlight, pulse));
                    targetRenderer.SetPropertyBlock(block);
                }
                transform.localScale = baseScale * (1f + pulse * 0.15f);
                await Task.Yield();
            }
            if (this == null) return;
            transform.localScale = baseScale;
            if (targetRenderer != null) targetRenderer.SetPropertyBlock(new MaterialPropertyBlock());
        }
    }
}
