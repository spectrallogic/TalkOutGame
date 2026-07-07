using System.Threading.Tasks;
using UnityEngine;

namespace TalkOut.Actors
{
    /// One blocky character in the scene: walking, gestures, face, idle life.
    public class NPCActor : MonoBehaviour
    {
        public string actorId;
        public FaceController face;
        public SimplePoseAnimator poser;

        [Tooltip("Meters per second when walking between locations")]
        public float walkSpeed = 1.6f;

        [Tooltip("Body transform used for the idle breathing scale")]
        public Transform breathingTarget;

        public bool canWalk = true; // the passenger stays seated

        private Vector3 baseScale = Vector3.one;
        private bool walking;

        private void Start()
        {
            if (breathingTarget != null) baseScale = breathingTarget.localScale;
        }

        private void Update()
        {
            if (breathingTarget != null && !walking)
            {
                float breathe = 1f + Mathf.Sin(Time.time * 1.7f + GetInstanceID()) * 0.012f;
                breathingTarget.localScale = new Vector3(baseScale.x, baseScale.y * breathe, baseScale.z);
            }
        }

        public async Task WalkToAsync(Transform target)
        {
            if (!canWalk || target == null) return;
            walking = true;

            Vector3 goal = new Vector3(target.position.x, transform.position.y, target.position.z);
            while (this != null && (transform.position - goal).sqrMagnitude > 0.01f)
            {
                Vector3 next = Vector3.MoveTowards(transform.position, goal, walkSpeed * Time.deltaTime);
                // walk bob
                float bob = Mathf.Abs(Mathf.Sin(Time.time * 9f)) * 0.05f;
                transform.position = new Vector3(next.x, goal.y + bob, next.z);

                Vector3 dir = goal - transform.position;
                dir.y = 0;
                if (dir.sqrMagnitude > 0.001f)
                {
                    transform.rotation = Quaternion.Slerp(
                        transform.rotation, Quaternion.LookRotation(dir), 10f * Time.deltaTime);
                }
                await Task.Yield();
            }

            if (this == null) return;
            transform.position = goal;
            if (target.childCount == 0) // face the same way the point faces
            {
                transform.rotation = Quaternion.Euler(0, target.eulerAngles.y, 0);
            }
            walking = false;
        }
    }
}
