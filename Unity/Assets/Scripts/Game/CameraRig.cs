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
        // The lens: a zoom on top of whatever field of view the camera was given (the phone's
        // 60°, the big screen's 42°), in tan space so 2× halves the window either way. The
        // base is re-read whenever the zoom is at rest, so a change of screen is picked up.
        float zoom = 1f, targetZoom = 1f, zoomVelocity, baseFov = 60f;
        bool zooming;
        // the bank: a few degrees of roll the cinematic chase leans into its turns with
        float roll, targetRoll, rollVelocity;
        const float ZoomLag = 0.35f;

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

        /// Behind the ball on the aim line and up high, pitched down about 27°, so on a portrait
        /// screen the hole ahead opens out down the middle — fairway, hazards, the landing area —
        /// with the horizon near the top and the ball and golfer seen from above in the lower
        /// third, over the buttons. Putts sit lower and closer.
        public void FrameAddress(Vector3 ball, Vector3 aimDirection, bool putting, float look = 0, Vector3? pin = null)
        {
            float back = putting ? 3.2f : 8.5f, up = putting ? 1.4f : 6.8f, ahead = putting ? 2.5f : 5f;
            targetPosition = ball - aimDirection * back + Vector3.up * up;
            targetLookAt = ball + aimDirection * ahead;
            Look(ball, aimDirection, look, pin, 14f, 5f);
            positionLag = 0.35f; lookLag = 0.3f;
        }

        /// The joystick pushed up or down while aiming (`look` -1..1): up, the camera climbs and
        /// backs off and the view swings out to the pin, so it can be found over a rise or down
        /// a long hole; down, it comes in low over the ball. `climb` and `backOff` are how far up
        /// and back a full push goes (more climb for a pin higher than the ball).
        void Look(Vector3 ball, Vector3 aimDirection, float look, Vector3? pin, float climb, float backOff)
        {
            if (look > 0)
            {
                // a pin up on a summit or a terrace needs the camera up level with it to be seen
                float above = pin.HasValue ? Mathf.Max(0f, pin.Value.y - ball.y) : 0f;
                targetPosition += Vector3.up * ((climb + above) * look) - aimDirection * (backOff * look);
                var towards = pin ?? (ball + aimDirection * 150f);
                targetLookAt = Vector3.Lerp(targetLookAt, towards + Vector3.up * 1.5f, 0.85f * look);
            }
            else if (look < 0)
            {
                float down = -look;
                targetPosition = Vector3.Lerp(targetPosition, ball - aimDirection * 3.5f + Vector3.up * 2.2f, 0.6f * down);
                targetLookAt = Vector3.Lerp(targetLookAt, ball + aimDirection * 1.5f, 0.6f * down);
            }
        }

        /// Reading a putt: low and a little behind the ball on the side away from the golfer,
        /// so the golfer frames the left edge and the line to the hole is clear — ball, break,
        /// cup — with the whole read in view, and it stays put while the putt rolls.
        public void FrameGreen(Vector3 ball, Vector3 aimDirection, float distanceToPin, float look = 0, Vector3? pin = null)
        {
            var right = Vector3.Cross(Vector3.up, aimDirection).normalized;
            float reach = Mathf.Clamp(distanceToPin, 4, 30);
            // back and up far enough that the golfer is a figure at the edge, not half the screen
            targetPosition = ball + right * 3.2f - aimDirection * (8.5f + reach * 0.2f) + Vector3.up * (6.2f + reach * 0.08f);
            targetLookAt = ball + aimDirection * Mathf.Max(distanceToPin * 0.5f, 3f) + Vector3.up * 0.15f;
            Look(ball, aimDirection, look, pin, 7f, 3f);
            positionLag = 0.35f; lookLag = 0.3f;
        }

        /// The hole cam: behind the cup, low, looking back along the line at the ball rolling in.
        public void HoleCam(Vector3 ball, Vector3 cup)
        {
            var away = cup - ball; away.y = 0;
            if (away.sqrMagnitude < 0.01f) away = transform.forward; away.Normalize();
            // far enough back and up that the cup and the flag are part of the picture, not all of it
            targetPosition = cup + away * 7f + Vector3.up * 2.8f;
            targetLookAt = Vector3.Lerp(cup, ball, 0.4f) + Vector3.up * 0.05f;
            positionLag = 0.3f; lookLag = 0.15f;
        }

        /// Zoom so a window `window` yards tall around something `distance` away fills the
        /// frame — the long lens a broadcast follows a ball with — never tighter than `minFov`.
        void FrameWindow(float distance, float window, float minFov)
        {
            float wanted = Mathf.Clamp(2f * Mathf.Atan(window / (2f * Mathf.Max(distance, 1f))) * Mathf.Rad2Deg, minFov, baseFov);
            targetZoom = Mathf.Tan(baseFov * Mathf.Deg2Rad / 2f) / Mathf.Tan(wanted * Mathf.Deg2Rad / 2f);
        }

        // ---- The shot camera, from the air: at the strike the camera cranes up and back from
        // the address view into the sky behind the tee, a little off the line so the ball's arc
        // reads across the hole rather than straight away, looking down the fairway to where it
        // will come down; then it follows down the line, staying well behind and above the ball,
        // closing in and coming lower (but never low) as the ball does, so the landing is seen
        // from above; once the ball is down it looks down on it as it runs out. Nothing is keyed
        // to the ball's frame-to-frame motion: position comes off how far along the line the ball
        // is, smoothed long.

        /// One frame of it: `ball` where it is, `origin` where it was struck, `landing` where it
        /// first comes down, `line` the shot's direction over the ground; `along` how far down
        /// the line the ball is and `carry` how far the landing is; `down` once it has landed;
        /// `since` seconds since this camera took over (the crane up is slower than the follow).
        public void Drone(Vector3 ball, Vector3 origin, Vector3 landing, Vector3 line, float along, float carry, bool down, float since = 10f)
        {
            line.y = 0;
            if (line.sqrMagnitude < 1e-4f) line = transform.forward; line.y = 0; line.Normalize();
            carry = Mathf.Max(carry, 1f);
            float s = Mathf.SmoothStep(0, 1, Mathf.Clamp01(along / carry));
            float farBack = Mathf.Clamp(carry * 0.2f, 14f, 46f), farUp = Mathf.Clamp(carry * 0.2f, 16f, 46f);
            float nearBack = Mathf.Clamp(carry * 0.08f, 11f, 22f), nearUp = Mathf.Clamp(carry * 0.075f, 11f, 20f);
            float back = Mathf.Lerp(farBack, nearBack, s), up = Mathf.Lerp(farUp, nearUp, s);
            var right = Vector3.Cross(Vector3.up, line);
            float side = Mathf.Clamp(carry * 0.05f, 3f, 11f);
            float camAlong = Mathf.Max(along - back, -farBack);
            var flat = origin + line * camAlong + right * side;
            float ground = (float)Course.HoleView.GroundHeight(Course.HoleView.ToCourse(flat));
            targetPosition = new Vector3(flat.x, Mathf.Max(ground, origin.y - 2f) + up, flat.z);
            // in the air: down the fairway ahead of the ball toward the landing, drawn onto the
            // ball as it comes down; down: just past the ball
            var ahead = origin + line * Mathf.Lerp(along, carry, 0.6f); ahead.y = landing.y;
            targetLookAt = down ? ball + line * 4f : Vector3.Lerp(ahead, ball, 0.3f + 0.35f * s);
            // the crane up and back from the address view is long and soft; the follow is quicker
            positionLag = Mathf.Lerp(0.9f, 0.5f, Mathf.Clamp01(since / 1.4f)); lookLag = 0.35f;
            float floor = (float)Course.HoleView.GroundHeight(Course.HoleView.ToCourse(targetPosition)) + 1.8f;
            if (targetPosition.y < floor) targetPosition.y = floor;
            targetZoom = 1f; targetRoll = 0f;
        }

        // ---- Codex's ball POV (Hole_12_Cinematic.blend, CAM_Ball_POV), for an approach that comes
        // down by the pin. His camera rides a fixed offset from the ball on the tee→cup line —
        // 4 m back, 2.5 m to the right so the trail never crosses the lens, 7 m up — on a 24 mm
        // lens, looking 44.7° down, with the ball big and low on the left (35 % across the frame)
        // and the course, the green and the flag ahead of it. From half a second before touchdown
        // it eases back to 8 m and lifts its eyes to 24° down, the ball a little nearer the middle
        // (40 %), and rides the bounces and the roll to the cup like that. Numbers read off the
        // .blend. His pitch is kept as it is; across, the ball keeps its place in the frame, since
        // a portrait phone is far narrower than his 16:9.
        const float PovMetres = 1.094f;          // his scene metres in course yards (181 m ↔ 198 yd)
        const float PovPitchInFlight = 44.7f, PovPitchDown = 24.1f, PovAcrossInFlight = 0.35f, PovAcrossDown = 0.40f;
        public const float PovLensHorizontal = 73.7f;   // 24 mm on a 36 mm sensor
        Vector3 povBall;
        float povAcross, povPitch;
        bool povThisFrame;
        float povTurnLag = 0.25f;

        /// One frame of the POV: `toLanding` seconds until the ball first comes down (negative
        /// after), `sinceLaunch` seconds since it left the club.
        public void BallPov(Vector3 ball, Vector3 line, float toLanding, float sinceLaunch)
        {
            line.y = 0;
            if (line.sqrMagnitude < 1e-4f) line = transform.forward; line.y = 0; line.Normalize();
            var right = Vector3.Cross(Vector3.up, line);
            float s = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.53f, -0.2f, toLanding));
            targetPosition = ball + (-line * Mathf.Lerp(4f, 8f, s) + right * 2.5f + Vector3.up * 7f) * PovMetres;
            float floor = (float)Course.HoleView.GroundHeight(Course.HoleView.ToCourse(targetPosition)) + 1.8f;
            if (targetPosition.y < floor) targetPosition.y = floor;
            povBall = ball;
            povAcross = Mathf.Lerp(PovAcrossInFlight, PovAcrossDown, s);
            povPitch = Mathf.Lerp(PovPitchInFlight, PovPitchDown, s);
            povThisFrame = true;
            // off the address view and onto the ball as it leaves, then locked to it like his rig
            float onto = Mathf.Clamp01(sinceLaunch / 0.7f);
            positionLag = Mathf.Lerp(0.3f, 0.03f, onto);
            povTurnLag = Mathf.Lerp(0.22f, 0.04f, onto);
            targetZoom = 1f; targetRoll = 0f;
        }

        /// Looking `pitch` degrees down, the heading that puts `ball` at `across` (0–1) of the
        /// frame's width from `from`.
        Quaternion PovRotation(Vector3 from, Vector3 ball, float across, float pitch)
        {
            float tanH = Mathf.Tan(Camera.fieldOfView * Mathf.Deg2Rad / 2f) * Camera.aspect;
            float want = (across * 2f - 1f) * tanH;
            var d = ball - from;
            var flat = new Vector3(d.x, 0, d.z);
            if (flat.sqrMagnitude < 1e-6f) return transform.rotation;
            float yaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
            var tilt = Quaternion.AngleAxis(pitch, Vector3.right);
            for (int i = 0; i < 6; i++)
            {
                var local = Quaternion.Inverse(Quaternion.AngleAxis(yaw, Vector3.up) * tilt) * d;
                if (local.z < 1e-3f) break;
                yaw += Mathf.Atan(local.x / local.z) * Mathf.Rad2Deg - Mathf.Atan(want) * Mathf.Rad2Deg;
            }
            return Quaternion.AngleAxis(yaw, Vector3.up) * tilt;
        }

        /// A slow settle on the resting ball, with the pin in shot when it is near.
        public void HoldOn(Vector3 ball, Vector3 towardPin, bool putting)
        {
            var dir = towardPin; dir.y = 0; dir.Normalize();
            targetPosition = ball - dir * (putting ? 5.5f : 6f) + Vector3.up * (putting ? 2.8f : 2.5f);
            targetLookAt = ball + dir * (putting ? 3f : 12f);
            // from the tee view this is the trip up the hole to the ball: longer the further it is
            positionLag = Mathf.Clamp(Vector3.Distance(transform.position, targetPosition) / 150f, 0.6f, 1.6f); lookLag = 0.5f;
            targetZoom = 1f; targetRoll = 0f;
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

        /// The golfer select screen: level and square in front of the figure, framed head to toe
        /// in the space between the title and the name plate, the shoes standing just above
        /// the plate (the screen's fractions from the bottom).
        public void FramePortrait(Vector3 feet, Vector3 facing, float t)
        {
            var f = facing; f.y = 0; f.Normalize();
            const float Top = 2.05f;                       // yards above the feet
            const float TopAt = 0.875f, FeetAt = 0.365f;
            RestoreFov(); ResetZoom();
            float span = Top / (TopAt - FeetAt);           // the screen's height, at the golfer
            float distance = span / (2f * Mathf.Tan(Camera.fieldOfView * 0.5f * Mathf.Deg2Rad));
            float centre = (0.5f - FeetAt) * span;
            targetPosition = feet + f * distance + Vector3.up * centre;
            targetLookAt = feet + Vector3.up * centre;
            positionLag = 0.35f; lookLag = 0.3f;
        }

        public void SnapNext() => snap = true;

        /// Chase these with the given lags (seconds) — for a shot keyed elsewhere that should
        /// still move like a camera operator, not jump (the instant replay).
        public void Follow(Vector3 position, Vector3 look, float lag, float lookLagSeconds)
        {
            targetPosition = position; targetLookAt = look; positionLag = lag; lookLag = lookLagSeconds;
        }

        /// Push the lens in (2 = twice as tight), eased like the flight camera's.
        public void Zoom(float z) => targetZoom = Mathf.Max(0.5f, z);
        /// Any framing that is not a flight shot wants the plain lens back.
        public void ResetZoom() { targetZoom = 1f; targetRoll = 0f; }

        /// Put the camera exactly here this frame, no damping — for a shot keyed elsewhere
        /// (the signature shot's Blender camera), called every frame it runs.
        public void Cue(Vector3 position, Vector3 lookAt) { targetPosition = position; targetLookAt = lookAt; snap = true; cueUp = Vector3.up; }
        public void Cue(Vector3 position, Vector3 lookAt, Vector3 up) { targetPosition = position; targetLookAt = lookAt; snap = true; cueUp = up; }
        Vector3 cueUp = Vector3.up;

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

        /// Ease toward a lens of this horizontal field of view (never taller than `maxVertical`
        /// on a portrait screen), a few degrees a frame rather than a jump.
        public void EaseHorizontalFov(float degrees, float maxVertical, float seconds)
        {
            if (!fovOverridden) { restFov = Camera.fieldOfView; fovOverridden = true; }
            float vertical = 2f * Mathf.Atan(Mathf.Tan(degrees * Mathf.Deg2Rad / 2f) / Mathf.Max(0.2f, Camera.aspect)) * Mathf.Rad2Deg;
            vertical = Mathf.Clamp(vertical, 30f, maxVertical);
            float rate = Mathf.Abs(vertical - restFov) / Mathf.Max(0.05f, seconds);
            Camera.fieldOfView = Mathf.MoveTowards(Camera.fieldOfView, vertical, Mathf.Max(rate, 1f) * Time.deltaTime);
        }

        void LateUpdate()
        {
            bool cut = snap;
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
            roll = cut ? targetRoll : Mathf.SmoothDamp(roll, targetRoll, ref rollVelocity, 0.4f);
            if (povThisFrame)
            {
                // the POV turns to hold the ball where Codex framed it; the look point follows so
                // the view holds still when the POV lets go
                var wanted = PovRotation(transform.position, povBall, povAcross, povPitch);
                transform.rotation = cut ? wanted : Quaternion.Slerp(transform.rotation, wanted, 1f - Mathf.Exp(-Time.deltaTime / povTurnLag));
                lookAt = targetLookAt = transform.position + transform.forward * 20f;
                lookVelocity = Vector3.zero;
                povThisFrame = false;
            }
            else if (forward.sqrMagnitude > 1e-4f)
                transform.rotation = Quaternion.LookRotation(forward, cueUp) * Quaternion.Euler(0, 0, roll);
            if (cut) cueUp = Vector3.up;
            // the lens
            if (cut) { zoom = targetZoom; zoomVelocity = 0; }
            else zoom = Mathf.SmoothDamp(zoom, targetZoom, ref zoomVelocity, ZoomLag);
            if (Mathf.Abs(zoom - 1f) < 1e-3f && Mathf.Abs(targetZoom - 1f) < 1e-3f) zoom = 1f;
            if (fovOverridden) return;
            if (zoom == 1f)
            {
                if (zooming) { Camera.fieldOfView = baseFov; zooming = false; }   // hand the lens back exactly
                baseFov = Camera.fieldOfView;                                    // and follow whoever sets it
            }
            else
            {
                zooming = true;
                Camera.fieldOfView = 2f * Mathf.Atan(Mathf.Tan(baseFov * Mathf.Deg2Rad / 2f) / zoom) * Mathf.Rad2Deg;
            }
        }
    }
}
