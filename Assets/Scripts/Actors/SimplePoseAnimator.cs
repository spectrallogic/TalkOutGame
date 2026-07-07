using System.Threading.Tasks;
using UnityEngine;

namespace TalkOut.Actors
{
    /// Micro-tween gesture player for blocky characters — no Mecanim.
    /// Each pose is a short parametric wiggle on head/arm transforms.
    public class SimplePoseAnimator : MonoBehaviour
    {
        public Transform head;
        public Transform armL;
        public Transform armR;
        public Transform body;

        private int playToken; // invalidates a running pose if a new one starts

        public async Task PlayPoseAsync(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            int token = ++playToken;

            switch (key)
            {
                case "headBob": await Wiggle(head, Vector3.right, 18f, 3f, 1.0f, token); break;
                case "headShake": await Wiggle(head, Vector3.up, 22f, 4f, 1.0f, token); break;
                case "headTilt": await HoldRotation(head, new Vector3(0, 0, 18f), 1.2f, token); break;
                case "leanIn": await HoldRotation(body, new Vector3(14f, 0, 0), 1.4f, token); break;
                case "armRaise": await HoldRotation(armR, new Vector3(0, 0, -95f), 1.2f, token); break;
                case "armRaiseL": await HoldRotation(armL, new Vector3(0, 0, 95f), 1.2f, token); break;
                case "scribble": await Wiggle(armR, Vector3.forward, 10f, 9f, 1.6f, token); break;
                case "panicShake": await Wiggle(body, Vector3.forward, 6f, 12f, 1.2f, token); break;
                case "shrug": await Shrug(token); break;
                case "laugh": await Wiggle(head, Vector3.right, 12f, 6f, 1.4f, token); break;
                case "sipCoffee": await SipCoffee(token); break;
                case "point": await HoldRotation(armR, new Vector3(-80f, 0, 0), 1.3f, token); break;
                default:
                    Debug.LogWarning($"[Pose] Unknown pose key '{key}'");
                    break;
            }
        }

        private async Task Wiggle(Transform target, Vector3 axis, float degrees, float hz, float seconds, int token)
        {
            if (target == null) return;
            Quaternion start = target.localRotation;
            float t = 0f;
            while (t < seconds && token == playToken && this != null)
            {
                t += Time.deltaTime;
                float envelope = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / seconds)); // ease in/out
                float angle = Mathf.Sin(t * hz * Mathf.PI * 2f) * degrees * envelope;
                target.localRotation = start * Quaternion.AngleAxis(angle, axis);
                await Task.Yield();
            }
            if (this != null && token == playToken) target.localRotation = start;
        }

        private async Task HoldRotation(Transform target, Vector3 euler, float seconds, int token)
        {
            if (target == null) return;
            Quaternion start = target.localRotation;
            Quaternion goal = start * Quaternion.Euler(euler);
            float t = 0f;
            while (t < seconds && token == playToken && this != null)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / seconds);
                // out and back: 0 -> 1 -> 0
                float blend = Mathf.Sin(k * Mathf.PI);
                target.localRotation = Quaternion.Slerp(start, goal, blend);
                await Task.Yield();
            }
            if (this != null && token == playToken) target.localRotation = start;
        }

        private async Task Shrug(int token)
        {
            var left = HoldRotation(armL, new Vector3(0, 0, 55f), 1.1f, token);
            var right = HoldRotation(armR, new Vector3(0, 0, -55f), 1.1f, token);
            await Task.WhenAll(left, right);
        }

        private async Task SipCoffee(int token)
        {
            await HoldRotation(armR, new Vector3(-120f, 0, 0), 1.0f, token);
            await HoldRotation(head, new Vector3(-12f, 0, 0), 0.6f, token);
        }
    }
}
