using System.Threading.Tasks;
using UnityEngine;

namespace TalkOut.Actors
{
    /// One wobbling character in the scene: walking, face, body language.
    public class NPCActor : MonoBehaviour
    {
        public string actorId;
        public FaceController face;
        public WobbleAnimator wobble;

        [Tooltip("Meters per second when walking between locations")]
        public float walkSpeed = 1.6f;

        public bool canWalk = true; // the passenger stays seated

        private bool walking;

        public async Task WalkToAsync(Transform target)
        {
            if (!canWalk || target == null) return;
            walking = true;
            if (wobble != null) wobble.Impulse(0.5f); // getting moving is an event

            Vector3 goal = new Vector3(target.position.x, transform.position.y, target.position.z);
            while (this != null && (transform.position - goal).sqrMagnitude > 0.01f)
            {
                Vector3 next = Vector3.MoveTowards(transform.position, goal, walkSpeed * Time.deltaTime);
                float bob = Mathf.Abs(Mathf.Sin(Time.time * 9f)) * 0.05f;
                transform.position = new Vector3(next.x, goal.y + bob, next.z);

                Vector3 direction = goal - transform.position;
                direction.y = 0;
                if (direction.sqrMagnitude > 0.001f)
                {
                    transform.rotation = Quaternion.Slerp(
                        transform.rotation, Quaternion.LookRotation(direction), 10f * Time.deltaTime);
                }
                await Task.Yield();
            }

            if (this == null) return;
            transform.position = goal;
            transform.rotation = Quaternion.Euler(0, target.eulerAngles.y, 0);
            walking = false;
        }
    }
}
