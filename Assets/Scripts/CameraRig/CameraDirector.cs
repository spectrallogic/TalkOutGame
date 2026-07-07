using UnityEngine;

namespace TalkOut.CameraRig
{
    /// Fixed cinematic 3/4 camera that eases its aim (and a slight dolly)
    /// toward whoever or whatever currently has the scene's attention.
    public class CameraDirector : MonoBehaviour
    {
        [Tooltip("Seconds for a focus shift to settle")]
        public float focusEase = 0.6f;

        [Tooltip("How far the camera creeps toward the focus target (meters)")]
        public float dollyAmount = 0.5f;

        private Vector3 basePosition;
        private Transform focusTarget;
        private Quaternion desiredRotation;

        private void Start()
        {
            basePosition = transform.position;
            desiredRotation = transform.rotation;
        }

        public void FocusOn(Transform target)
        {
            focusTarget = target;
        }

        public void ClearFocus()
        {
            focusTarget = null;
        }

        private void LateUpdate()
        {
            Vector3 desiredPosition = basePosition;
            if (focusTarget != null)
            {
                Vector3 aim = focusTarget.position + Vector3.up * 0.4f;
                desiredRotation = Quaternion.LookRotation(aim - basePosition);
                desiredPosition = basePosition + (aim - basePosition).normalized * dollyAmount;
            }

            float k = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.01f, focusEase * 0.35f));
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, k);
            transform.position = Vector3.Lerp(transform.position, desiredPosition, k);
        }
    }
}
