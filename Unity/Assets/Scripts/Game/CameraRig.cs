using UnityEngine;

namespace GolfArcade.Game
{
    /// Wii Sports framing: over the golfer's shoulder looking down the aim line while you set
    /// up, then chasing the ball from behind through the air, then a hold on where it stopped.
    public sealed class CameraRig : MonoBehaviour
    {
        public Camera Camera { get; private set; }

        Vector3 targetPosition, targetLookAt;
        float positionLag = 0.12f, lookLag = 0.08f;
        Vector3 positionVelocity, lookVelocity;
        Vector3 lookAt;
        bool snap = true;

        public static CameraRig Create()
        {
            var go = new GameObject("Camera Rig");
            var rig = go.AddComponent<CameraRig>();
            rig.Camera = go.AddComponent<Camera>();
            go.tag = "MainCamera";
            rig.Camera.fieldOfView = 60; // portrait phone: tall and narrow, so open it up
            rig.Camera.nearClipPlane = 0.05f;
            rig.Camera.farClipPlane = 900;
            rig.Camera.clearFlags = CameraClearFlags.Skybox;
            rig.Camera.backgroundColor = new Color(0.55f, 0.78f, 0.95f);
            go.AddComponent<AudioListener>();
            return rig;
        }

        /// Behind the ball on the aim line, pitched down about 20° so on a portrait screen the
        /// horizon sits in the top fifth, the landing area just under it, and the ball and golfer
        /// in the lower third above the buttons. Putts sit lower and closer.
        public void FrameAddress(Vector3 ball, Vector3 aimDirection, bool putting)
        {
            float back = putting ? 3.2f : 4.5f, up = putting ? 1.4f : 2.4f, ahead = putting ? 2.5f : 1.5f;
            targetPosition = ball - aimDirection * back + Vector3.up * up;
            targetLookAt = ball + aimDirection * ahead;
            positionLag = 0.35f; lookLag = 0.3f;
        }

        /// Chase from behind, like the Wii's ball cam: the camera rides a little above the ball
        /// and well behind it, so the ball is seen against the sky and the hole ahead rather than
        /// from overhead, and it looks a touch past the ball to keep the landing area in frame.
        public void Follow(Vector3 ball, Vector3 velocity, bool putting)
        {
            var dir = velocity; dir.y = 0;
            if (dir.sqrMagnitude < 0.01f) dir = transform.forward; dir.Normalize();
            float back = putting ? 3f : 14f, up = putting ? 1.6f : 3.5f;
            targetPosition = ball - dir * back + Vector3.up * up; // `ball` already carries its height
            targetLookAt = ball + dir * (putting ? 1f : 8f);
            positionLag = 0.25f; lookLag = 0.08f;
        }

        /// A slow settle on the resting ball, with the pin in shot when it is near.
        public void HoldOn(Vector3 ball, Vector3 towardPin, bool putting)
        {
            var dir = towardPin; dir.y = 0; dir.Normalize();
            targetPosition = ball - dir * (putting ? 2.5f : 6f) + Vector3.up * (putting ? 1.2f : 2.5f);
            targetLookAt = ball + dir * (putting ? 3f : 12f);
            positionLag = 0.6f; lookLag = 0.5f;
        }

        /// Flyover: from above the green looking back down the hole toward the tee.
        public void Flyover(Vector3 pin, Vector3 tee, float t)
        {
            var dir = (tee - pin).normalized;
            targetPosition = Vector3.Lerp(pin + Vector3.up * 40 - dir * 30, pin + dir * 60 + Vector3.up * 25, t);
            targetLookAt = Vector3.Lerp(pin, Vector3.Lerp(pin, tee, 0.4f), t);
            positionLag = 0.3f; lookLag = 0.3f;
        }

        public void SnapNext() => snap = true;

        void LateUpdate()
        {
            if (snap)
            {
                transform.position = targetPosition; lookAt = targetLookAt; snap = false;
                positionVelocity = lookVelocity = Vector3.zero;
            }
            else
            {
                transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref positionVelocity, positionLag);
                lookAt = Vector3.SmoothDamp(lookAt, targetLookAt, ref lookVelocity, lookLag);
            }
            var forward = lookAt - transform.position;
            if (forward.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }
    }
}
