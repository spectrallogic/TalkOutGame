using UnityEngine;

namespace TalkOut.Player
{
    /// Seated first-person camera: mouse look with clamped yaw/pitch (you're
    /// belted into a car, not an owl). Cursor stays locked while looking;
    /// chat focus (Enter) or Escape frees it.
    public class FirstPersonRig : MonoBehaviour
    {
        [Tooltip("Degrees per mouse unit")]
        public float sensitivity = 2.2f;
        public float maxYaw = 150f;   // relative to seated forward
        public float minPitch = -55f;
        public float maxPitch = 55f;

        /// Set by the chat UI while the text field has focus.
        public bool LookEnabled { get; set; } = true;

        private float yaw;
        private float pitch;
        private Quaternion seatForward;

        private void Start()
        {
            seatForward = transform.localRotation;
            LockCursor(true);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) LockCursor(false);
            if (!LookEnabled) return;
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                // Click anywhere (outside UI) to re-capture the mouse.
                if (Input.GetMouseButtonDown(0)) LockCursor(true);
                return;
            }

            float effective = sensitivity * Core.GameSettings.MouseSensitivity;
            yaw += Input.GetAxis("Mouse X") * effective;
            pitch -= Input.GetAxis("Mouse Y") * effective;
            yaw = Mathf.Clamp(yaw, -maxYaw, maxYaw);
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

            transform.localRotation = seatForward * Quaternion.Euler(pitch, yaw, 0f);
        }

        public void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
