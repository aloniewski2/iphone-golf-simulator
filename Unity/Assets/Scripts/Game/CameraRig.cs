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
            float back = putting ? 3.2f : 7.5f, up = putting ? 1.4f : 4f, ahead = putting ? 2.5f : 3.5f;
            targetPosition = ball - aimDirection * back + Vector3.up * up;
            targetLookAt = ball + aimDirection * ahead;
            positionLag = 0.35f; lookLag = 0.3f;
        }

        /// Reading a putt: low and a little behind the ball on the side away from the golfer,
        /// so the golfer frames the left edge and the line to the hole is clear — ball, break,
        /// cup — with the whole read in view, and it stays put while the putt rolls.
        public void FrameGreen(Vector3 ball, Vector3 aimDirection, float distanceToPin)
        {
            var right = Vector3.Cross(Vector3.up, aimDirection).normalized;
            float reach = Mathf.Clamp(distanceToPin, 4, 30);
            targetPosition = ball + right * 2.4f - aimDirection * (5.5f + reach * 0.12f) + Vector3.up * 4.2f;
            targetLookAt = ball + aimDirection * Mathf.Max(distanceToPin * 0.5f, 3f) + Vector3.up * 0.15f;
            positionLag = 0.35f; lookLag = 0.3f;
        }

        /// The hole cam: behind the cup, low, looking back along the line at the ball rolling in.
        public void HoleCam(Vector3 ball, Vector3 cup)
        {
            var away = cup - ball; away.y = 0;
            if (away.sqrMagnitude < 0.01f) away = transform.forward; away.Normalize();
            targetPosition = cup + away * 2.6f + Vector3.up * 1.1f;
            targetLookAt = Vector3.Lerp(cup, ball, 0.35f) + Vector3.up * 0.05f;
            positionLag = 0.3f; lookLag = 0.15f;
        }

        /// Chase from behind, like the Wii's ball cam: the camera rides a little above the ball
        /// and well behind it, so the ball is seen against the sky and the hole ahead rather than
        /// from overhead, and it looks a touch past the ball to keep the landing area in frame.
        public void Follow(Vector3 ball, Vector3 velocity, bool putting)
        {
            var dir = velocity; dir.y = 0;
            if (dir.sqrMagnitude < 0.01f) dir = transform.forward; dir.Normalize();
            // Higher and further back as the ball climbs, so a big drive is seen soaring against
            // the sky and the hole ahead, and the camera drops in with it as it comes down.
            float climb = putting ? 0 : Mathf.Clamp(velocity.y, -20f, 20f);
            float back = putting ? 3f : 13f + Mathf.Max(0, climb) * 0.12f, up = putting ? 1.6f : 3.2f + Mathf.Max(0, climb) * 0.14f;
            targetPosition = ball - dir * back + Vector3.up * up; // `ball` already carries its height
            targetLookAt = ball + dir * (putting ? 1f : 9f);
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

        /// The showcase on the way to the first tee: an aerial sweep round the whole hole
        /// that ends high behind the tee, then a low walkthrough up the fairway to the green.
        /// `t` runs 0→1 over the whole thing; the first `AerialShare` of it is the aerial.
        public const float AerialShare = 0.4f;
        public void Showcase(Course.Hole hole, float t)
        {
            var tee = Course.HoleView.ToWorld(hole.Tee); var pin = Course.HoleView.ToWorld(hole.Pin);
            var mid = (tee + pin) / 2;
            var along = pin - tee; along.y = 0; float length = along.magnitude; along.Normalize();
            var right = Vector3.Cross(Vector3.up, along);
            if (t < AerialShare)
            {
                // Round the hole from beyond the green on the left to high behind the tee.
                float u = Mathf.SmoothStep(0, 1, t / AerialShare);
                float angle = Mathf.Lerp(120f, -90f, u) * Mathf.Deg2Rad;
                float radius = length * 0.7f + 60, height = length * 0.45f + 40;
                targetPosition = mid + (right * Mathf.Cos(angle) + along * Mathf.Sin(angle)) * radius + Vector3.up * height;
                targetLookAt = mid + Vector3.up * 4;
                positionLag = 0.4f; lookLag = 0.4f;
            }
            else
            {
                // Down the hole: from well behind the tee, over every station, to the pin,
                // dropping from 24 to 10 yd above the ground, looking a little way ahead.
                float u = (t - AerialShare) / (1 - AerialShare);
                var path = new System.Collections.Generic.List<Vector3> { tee - along * 45 };
                foreach (var p in hole.Centerline) path.Add(Course.HoleView.ToWorld(p));
                var at = Spline(path, u); var ahead = Spline(path, Mathf.Min(1, u + 0.12f));
                float rise = Mathf.Lerp(24, 10, u);
                targetPosition = new Vector3(at.x, (float)Course.HoleView.GroundHeight(Course.HoleView.ToCourse(at)) + rise, at.z);
                var look = u > 0.85f ? pin : ahead;
                targetLookAt = look + Vector3.up * 1.5f;
                positionLag = 0.3f; lookLag = 0.25f;
            }
        }

        /// Catmull-Rom through `points`, `u` in 0..1 over the whole run, ends clamped.
        static Vector3 Spline(System.Collections.Generic.List<Vector3> points, float u)
        {
            int n = points.Count - 1;
            float s = Mathf.Clamp01(u) * n;
            int i = Mathf.Min((int)s, n - 1); float f = s - i;
            Vector3 P(int k) => points[Mathf.Clamp(k, 0, n)];
            Vector3 p0 = P(i - 1), p1 = P(i), p2 = P(i + 1), p3 = P(i + 2);
            return 0.5f * ((2 * p1) + (-p0 + p2) * f + (2 * p0 - 5 * p1 + 4 * p2 - p3) * f * f + (-p0 + 3 * p1 - 3 * p2 + p3) * f * f * f);
        }

        /// The golfer picker: in front of the figure, at chest height, drifting round it a
        /// little so the hair and the kit show from more than one side. The figure fills the
        /// top half of a portrait screen; the sheet takes the bottom.
        public void FramePortrait(Vector3 feet, Vector3 facing, float t)
        {
            var f = facing; f.y = 0; f.Normalize();
            var swing = Quaternion.AngleAxis(28f * Mathf.Sin(t * 0.45f), Vector3.up) * f;
            targetPosition = feet + swing * 3.1f + Vector3.up * 1.55f;
            targetLookAt = feet + Vector3.up * 1.05f - f * 0.1f;
            positionLag = 0.5f; lookLag = 0.4f;
        }

        public void SnapNext() => snap = true;

        /// Put the camera exactly here this frame, no damping — for a shot keyed elsewhere
        /// (the signature shot's Blender camera), called every frame it runs.
        public void Cue(Vector3 position, Vector3 lookAt) { targetPosition = position; targetLookAt = lookAt; snap = true; }

        float restFov; bool fovOverridden;
        /// Frame like a camera of this horizontal field of view: what a landscape render was
        /// composed with, kept across this screen's width whatever its shape (clamped, so a
        /// portrait phone does not turn into a fisheye).
        public void SetHorizontalFov(float degrees)
        {
            if (!fovOverridden) { restFov = Camera.fieldOfView; fovOverridden = true; }
            float vertical = 2f * Mathf.Atan(Mathf.Tan(degrees * Mathf.Deg2Rad / 2f) / Mathf.Max(0.2f, Camera.aspect)) * Mathf.Rad2Deg;
            Camera.fieldOfView = Mathf.Clamp(vertical, 30f, 85f);
        }
        public void RestoreFov() { if (fovOverridden) { Camera.fieldOfView = restFov; fovOverridden = false; } }

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
