using System.Collections.Generic;
using UnityEngine;
using TalkOut.Core;
using TalkOut.Data;

namespace TalkOut.Player
{
    /// Something in the car the player can click: it animates, lands in the
    /// shared EventLog (so both LLMs remember it), and the cop may comment.
    public class Interactable : MonoBehaviour
    {
        [Tooltip("Shown in the crosshair hint, e.g. 'Open glove box'")]
        public string hintText = "Interact";

        [Tooltip("Event text for the LLM memory. {item} is replaced from randomContents once per scene.")]
        [TextArea(2, 3)]
        public string eventTextTemplate = "The player did something.";

        [Tooltip("Alternate event text when toggled back (empty = same text)")]
        [TextArea(2, 3)]
        public string eventTextClose = "";

        [Tooltip("One is picked at scene start and stays consistent")]
        public string[] randomContents;

        public bool isToggle;
        public bool copMayReact = true;
        public float cooldownSeconds = 2f;

        [Tooltip("Instant nudges to the officer's emotional meters (scaled up on repeats)")]
        public List<StatEffect> immediateEffects = new List<StatEffect>();

        [Header("Animation")]
        [Tooltip("Rotated by openEuler when used/opened (e.g. glove box lid)")]
        public Transform hinge;
        public Vector3 openEuler = new Vector3(-70, 0, 0);
        [Tooltip("Scale-pulse target when there is no hinge")]
        public Transform pulseTarget;

        public AudioSource audioSource;

        public bool IsOpen { get; private set; }

        private string chosenItem;
        private float lastUsedAt = -999f;
        private Quaternion hingeClosed;
        private TurnController turnController;

        private void Start()
        {
            if (hinge != null) hingeClosed = hinge.localRotation;
            if (randomContents != null && randomContents.Length > 0)
            {
                chosenItem = randomContents[Random.Range(0, randomContents.Length)];
            }
            turnController = FindObjectOfType<TurnController>();
        }

        public bool CanUse => Time.time - lastUsedAt >= cooldownSeconds;

        public string CurrentHint => isToggle && IsOpen ? hintText.Replace("Open", "Close") : hintText;

        public void Use()
        {
            if (!CanUse || turnController == null) return;
            lastUsedAt = Time.time;

            string text;
            if (isToggle && IsOpen)
            {
                IsOpen = false;
                text = string.IsNullOrEmpty(eventTextClose)
                    ? eventTextTemplate : eventTextClose;
            }
            else
            {
                IsOpen = true;
                text = eventTextTemplate;
            }
            text = text.Replace("{item}", chosenItem ?? "nothing");

            Animate();
            if (audioSource != null && audioSource.clip != null) audioSource.Play();
            turnController.ReportPlayerInteraction(text, copMayReact, immediateEffects);
        }

        private void Animate()
        {
            if (hinge != null)
            {
                hinge.localRotation = IsOpen || !isToggle
                    ? hingeClosed * Quaternion.Euler(openEuler)
                    : hingeClosed;
                if (!isToggle) Invoke(nameof(CloseHinge), 1.2f);
            }
            else if (pulseTarget != null)
            {
                StopAllCoroutines();
                StartCoroutine(Pulse());
            }
        }

        private void CloseHinge()
        {
            if (hinge != null) hinge.localRotation = hingeClosed;
            IsOpen = false;
        }

        private System.Collections.IEnumerator Pulse()
        {
            Vector3 baseScale = pulseTarget.localScale;
            float t = 0f;
            while (t < 0.4f)
            {
                t += Time.deltaTime;
                float k = Mathf.Sin(Mathf.Clamp01(t / 0.4f) * Mathf.PI);
                pulseTarget.localScale = baseScale * (1f + k * 0.2f);
                yield return null;
            }
            pulseTarget.localScale = baseScale;
        }
    }
}
