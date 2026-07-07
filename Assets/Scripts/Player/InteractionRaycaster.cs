using System;
using UnityEngine;

namespace TalkOut.Player
{
    /// Crosshair raycast from the player camera: hovers show a hint,
    /// left click uses the Interactable.
    public class InteractionRaycaster : MonoBehaviour
    {
        public Camera playerCamera;
        public float range = 2.6f;

        /// UI subscribes: null = nothing hovered.
        public event Action<string> HintChanged;

        private Interactable hovered;

        private void Update()
        {
            if (playerCamera == null) return;
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                SetHovered(null);
                return;
            }

            var ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            Interactable hit = null;
            if (Physics.Raycast(ray, out var hitInfo, range))
            {
                hit = hitInfo.collider.GetComponentInParent<Interactable>();
            }
            SetHovered(hit);

            if (hovered != null && hovered.CanUse && Input.GetMouseButtonDown(0))
            {
                hovered.Use();
            }
        }

        private void SetHovered(Interactable interactable)
        {
            if (hovered == interactable) return;
            hovered = interactable;
            HintChanged?.Invoke(hovered != null ? hovered.CurrentHint : null);
        }
    }
}
