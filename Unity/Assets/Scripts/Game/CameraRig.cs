using UnityEngine;
using UnityEngine.Rendering.Universal;

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
            rig.Camera.farClipPlane = GolfArcade.Course.HoleAtmosphere.FarClip;
            rig.Camera.clearFlags = CameraClearFlags.Skybox;
            rig.Camera.backgroundColor = new Color(0.55f, 0.78f, 0.95f);
            rig.Camera.depthTextureMode |= DepthTextureMode.Depth;
            rig.Camera.GetUniversalAdditionalCameraData().requiresDepthTexture = true;
            GolfArcade.Tennis.TennisLook.SetupPost(rig.Camera);
            go.AddComponent<AudioListener>();
            return rig;
        }

        /// Close third-person address: the whole golfer sits left of the ball, with the
        /// target line and course ahead. The camera stays near shoulder height.
        public void FrameAddress(Vector3 ball, Vector3 aimDirection, bool putting, float look = 0, Vector3? pin = null)
        {
            aimDirection = GroundDirection(aimDirection);
            var right = Vector3.Cross(Vector3.up, aimDirection);
            bool portrait = Camera.aspect < 1.2f;
            float back = portrait ? (putting ? 4.4f : 4.65f) : (putting ? 3.3f : 3.85f);
            targetPosition = ball - aimDirection * back + right * (portrait ? -.2f : .55f) + Vector3.up * (portrait ? 2.45f : 2.05f);
            targetLookAt = ball + aimDirection * (portrait ? 2.1f : 3f) + Vector3.up * (portrait ? .8f : .95f);
            var course = Course.HoleView.Current;
            if (portrait && !putting && Course.GolfCoastalComposition.Enabled && course && course.Hole.Number is 9 or 12)
            {
                var fromTee = ball - Course.HoleView.ToWorld(course.Hole.Tee); fromTee.y = 0;
                if (fromTee.sqrMagnitude < 24f * 24f)
                {
                    // Rise above the near turf lip so the coastal opening reads
                    // during ordinary play. Preserve the lens and downward aim angle.
                    targetPosition += Vector3.up * (course.Hole.Number==9 ? .8f : .9f);
                    if(course.Hole.Number==9) targetLookAt-=right*1.15f;
                }
            }
            Look(ball, aimDirection, look, pin, 14f, 5f);
            ClearGround();
            cueUp = Vector3.up; targetRoll = 0;
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
            aimDirection = GroundDirection(aimDirection);
            var right = Vector3.Cross(Vector3.up, aimDirection);
            targetPosition = ball + right * .65f - aimDirection * 3.8f + Vector3.up * 2.5f;
            targetLookAt = ball + aimDirection * Mathf.Clamp(distanceToPin * .4f, 2f, 4f) + Vector3.up * .35f;
            Look(ball, aimDirection, look, pin, 7f, 3f);
            ClearGround();
            cueUp = Vector3.up; targetRoll = 0;
            positionLag = 0.35f; lookLag = 0.3f;
        }

        /// Return to the exact address composition, so the result feels like part of the shot.
        public void FrameGolfParty(Vector3 origin,Vector3 direction,bool result) {
            RestoreFov();ResetZoom();direction=GroundDirection(direction);
            float back=Camera.aspect<1.2f?10.5f:7.5f;
            var center=origin-direction*1.3f;
            targetPosition=center+direction*(result?back:-back)+Vector3.up*3.4f;
            targetLookAt=center+Vector3.up*.85f;
            cueUp=Vector3.up;targetRoll=0;shotFrame=povThisFrame=false;
            positionLag=.22f;lookLag=.22f;ClearGround();
        }

        public void FrameShotResult()
        {
            RestoreFov(); ResetZoom();
            targetPosition = launchCamera;
            targetLookAt = launchLook;
            cueUp = Vector3.up; targetRoll = 0;
            shotFrame = povThisFrame = false;
        }

        /// Face the player after rest; leave the right-hand result card and phone footer clear.
        float resultAspect;
        public void FrameCharacterResult(Transform golfer)
        {
            if (Mathf.Abs(resultAspect-Camera.aspect)>.01f) SnapNext();
            resultAspect=Camera.aspect;
            bool portrait=Camera.aspect<1.2f;
            var facing=GroundDirection(golfer.forward);
            FrameStage(golfer.position,facing,0,portrait?.29f:.16f,portrait?.76f:.82f,4f);
            var right=Vector3.Cross(Vector3.up,-facing);
            float distance=Vector3.Distance(targetPosition,targetLookAt);
            float halfWidth=distance*Mathf.Tan(Camera.fieldOfView*Mathf.Deg2Rad/2)*Camera.aspect;
            var shift=right*halfWidth*(portrait?.14f:.34f);
            targetPosition+=shift;targetLookAt+=shift;
            shotFrame=povThisFrame=false;cueUp=Vector3.up;targetRoll=0;
        }

        public bool LandingView { get; private set; }
        Vector3 landingCamera;
        public const float LaunchHoldSeconds = 1.5f;
        public const float FollowTransitionSeconds = .28f;
        Vector3 launchCamera, launchLook, flightDirection;
        Vector3 flightBall;
        bool shotFrame;

        static Vector3 GroundDirection(Vector3 direction)
        {
            direction.y = 0;
            return direction.sqrMagnitude > .0001f ? direction.normalized : Vector3.forward;
        }

        void ClearGround()
        {
            float floor = (float)Course.HoleView.GroundHeight(Course.HoleView.ToCourse(targetPosition)) + .65f;
            targetPosition.y = Mathf.Max(targetPosition.y, floor);
        }

        /// Keep the strike's heading for the whole shot, including bounce, cup and runout.
        /// Capturing the current pose lets the launch view lead into the chase without a cut.
        public void BeginShot(Vector3 direction)
        {
            LandingView=false;
            launchCamera = transform.position;
            launchLook = lookAt;
            flightDirection = GroundDirection(direction);
            positionVelocity = lookVelocity = Vector3.zero;
            cueUp = Vector3.up;
            targetRoll = 0;
            povThisFrame = false;
        }

        public void FollowShot(Vector3 ball, float sinceLaunch, bool down, bool putting)
        {
            float hold = putting ? .6f : LaunchHoldSeconds;
            float follow = Mathf.SmoothStep(0, 1, Mathf.Clamp01((sinceLaunch - hold) / FollowTransitionSeconds));
            var right = Vector3.Cross(Vector3.up, flightDirection);
            if(down && !putting && !LandingView){
                LandingView=true;
                landingCamera=ball-flightDirection*11f+right*1.2f+Vector3.up*4.2f;
                SnapNext();
            }
            float back = putting ? 5f : 30f;
            float height = putting ? 2.8f : 6f;
            var chase = ball - flightDirection * back + right * (putting ? .65f : .9f) + Vector3.up * height;
            targetPosition = LandingView ? landingCamera : Vector3.Lerp(launchCamera, chase, follow);
            // Even a ricochet cannot put the camera ahead of its subject and reverse the view.
            float ahead = Vector3.Dot(targetPosition - ball, flightDirection);
            if (ahead > -2f) targetPosition -= flightDirection * (ahead + 2f);
            ClearGround();
            flightBall = ball;
            targetLookAt = LandingView ? ball+flightDirection*.8f : Vector3.Lerp(launchLook, ball + flightDirection * .8f, follow);
            positionLag = .065f; lookLag = .055f;
            cueUp = Vector3.up; targetRoll = 0; targetZoom = 1;
            shotFrame = true;
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
        Course.Hole showcaseHole;
        readonly System.Collections.Generic.List<Vector3> showcasePath = new();
        public void Showcase(Course.Hole hole, float t)
        {
            RestoreFov(); ResetZoom();
            cueUp = Vector3.up; targetRoll = 0; shotFrame = povThisFrame = false;
            var tee = Course.HoleView.ToWorld(hole.Tee); var pin = Course.HoleView.ToWorld(hole.Pin);
            var mid = (tee + pin) / 2;
            var along = pin - tee; along.y = 0; float length = along.magnitude; along.Normalize();
            var right = Vector3.Cross(Vector3.up, along);
            if(showcaseHole != hole)
            {
                showcaseHole=hole;showcasePath.Clear();showcasePath.Add(tee-along*16);
                foreach(var point in hole.Centerline)showcasePath.Add(Course.HoleView.ToWorld(point));
            }
            Vector3 Establishing(float u)
            {
                float angle=Mathf.Lerp(55f,-105f,Mathf.SmoothStep(0,1,u))*Mathf.Deg2Rad;
                var focus=mid+Vector3.up*3;
                var offset=(right*Mathf.Cos(angle)+along*Mathf.Sin(angle))*(length*.48f+32)+Vector3.up*(length*.22f+25);
                // Both tee and green stay inside the portrait lens during the reveal.
                float vertical=Mathf.Tan(Camera.fieldOfView*.5f*Mathf.Deg2Rad)*.84f;
                float horizontal=vertical*Mathf.Max(.3f,Camera.aspect);
                for(int attempt=0;attempt<16;attempt++)
                {
                    var inverse=Quaternion.Inverse(Quaternion.LookRotation(-offset));bool fits=true;
                    foreach(var point in new[]{tee,pin})
                    {
                        var local=inverse*(point-focus-offset);
                        if(local.z<=0 || Mathf.Abs(local.x)>local.z*horizontal || Mathf.Abs(local.y)>local.z*vertical){fits=false;break;}
                    }
                    if(fits)break;offset*=1.1f;
                }
                return focus+offset;
            }
            if (t < AerialShare)
            {
                targetPosition = Establishing(t/AerialShare);
                targetLookAt = mid + Vector3.up * 3;
                positionLag = .25f; lookLag = .25f;
            }
            else
            {
                float progress=Mathf.Clamp01((t-AerialShare)/(1-AerialShare));
                float u=Mathf.SmoothStep(0,1,progress);
                var at=Spline(showcasePath,u);var ahead=Spline(showcasePath,Mathf.Min(1,u+.10f));
                var forward=ahead-at;forward.y=0;
                forward=forward.sqrMagnitude>.001f?forward.normalized:along;
                var position=at-forward*18+right*(7*Mathf.Sin(u*Mathf.PI));
                position.y=(float)Course.HoleView.GroundHeight(Course.HoleView.ToCourse(position))+Mathf.Lerp(18,9,u);
                var look=Vector3.Lerp(ahead+Vector3.up*2,pin+Vector3.up,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.85f,1,u)));
                float descend=Mathf.SmoothStep(0,1,Mathf.Clamp01(progress/.2f));
                targetPosition=Vector3.Lerp(Establishing(1),position,descend);
                targetLookAt=Vector3.Lerp(mid+Vector3.up*3,look,descend);
                positionLag=.22f;lookLag=.20f;
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

        /// The locker: the golfer in the top of the screen over the panel, level and square in front of
        /// them. `head` 0 frames the whole golfer, 1 the head and shoulders (the HAIR and HEADWEAR tabs).
        public void FrameLocker(Vector3 feet, Vector3 facing, float head) => FrameStage(feet, facing, head, 0.53f, 0.93f);

        /// The golfer standing square to the camera between `screenBottom` and `screenTop` (fractions of the
        /// screen's height from its foot): the locker, and the clubhouse on the home screen. `head` 0 frames
        /// the whole golfer, 1 the head and shoulders.
        public void FrameStage(Vector3 feet, Vector3 facing, float head, float screenBottom, float screenTop, float tilt = 0)
        {
            var f = facing; f.y = 0; f.Normalize();
            RestoreFov(); ResetZoom();
            float ScreenBottom = screenBottom, ScreenTop = screenTop;   // the part of the screen the golfer has (from the bottom)
            float lo = Mathf.Lerp(0f, 1.32f, head), hi = Mathf.Lerp(2.16f, 2.14f, head);   // yards above the feet at those two lines
            float span = (hi - lo) / (ScreenTop - ScreenBottom);    // the screen's height, at the golfer
            float distance = span / (2f * Mathf.Tan(Camera.fieldOfView * 0.5f * Mathf.Deg2Rad));
            float centre = lo - ScreenBottom * span + 0.5f * span;
            // looking down a little (`tilt` degrees) onto the floor they stand on, from the same distance
            var back = Quaternion.AngleAxis(-tilt, Vector3.Cross(Vector3.up, f)) * f;
            targetLookAt = feet + Vector3.up * centre;
            targetPosition = targetLookAt + back * distance;
            positionLag = 0.35f; lookLag = 0.3f;
        }

        /// The home screen: behind the golfer on the first tee, `back` yards back and `up` yards
        /// up and a touch to their left, the hole opening out ahead over their shoulder,
        /// drifting slowly from side to side.
        public void FrameHome(Vector3 ball, Vector3 aimDirection, float back, float up, float t)
        {
            var right = Vector3.Cross(Vector3.up, aimDirection);
            RestoreFov(); ResetZoom();
            Camera.farClipPlane = FarClip;
            float sway = 0.35f * Mathf.Sin(t * 0.21f);
            targetPosition = ball - aimDirection * back + right * (sway - 0.3f) + Vector3.up * up;
            // tipped down just enough that the golfer's feet stand a quarter of the way up the
            // picture, clear of the dock, however high the camera had to go
            float feet = Mathf.Atan2(up, back) * Mathf.Rad2Deg;
            float pitch = feet - Mathf.Atan(0.5f * Mathf.Tan(Camera.fieldOfView * 0.5f * Mathf.Deg2Rad)) * Mathf.Rad2Deg;
            targetLookAt = targetPosition + Quaternion.AngleAxis(pitch, right) * aimDirection * 20f;
            positionLag = 0.8f; lookLag = 0.8f;
        }

        /// The course screen: high over the hole and circling it, a turn a minute, near enough
        /// that the whole of it — a circle of `radius` round `centre` — fills the picture's width
        /// whichever way round it is. Returns how far off it is.
        public float Orbit(Vector3 centre, float radius, float t)
        {
            RestoreFov(); ResetZoom();
            float half = Mathf.Atan(Mathf.Tan(Camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * Camera.aspect);
            float distance = radius / Mathf.Sin(half) * 0.96f;   // (the circle is the worst case)
            float pitch = 42f * Mathf.Deg2Rad, angle = (200f + t * 6f) * Mathf.Deg2Rad;
            var round = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            targetPosition = centre + round * (distance * Mathf.Cos(pitch)) + Vector3.up * (distance * Mathf.Sin(pitch));
            targetLookAt = centre;
            positionLag = 0.25f; lookLag = 0.25f;
            Camera.farClipPlane = Mathf.Max(FarClip, distance * 3f);
            return distance;
        }

        static float FarClip => GolfArcade.Course.HoleAtmosphere.FarClip;

        public void SnapNext() => snap = true;
        public void ApplyFrame() => LateUpdate();

        /// Chase these with the given lags (seconds) — for a shot keyed elsewhere that should
        /// still move like a camera operator, not jump (the instant replay).
        public void Follow(Vector3 ball, Vector3 velocity, bool putting)
        {
            var dir = velocity; dir.y = 0;
            if (dir.sqrMagnitude < .01f) dir = transform.forward;
            dir.Normalize();
            float back = putting ? 3f : 14f, up = putting ? 1.6f : 3.5f;
            targetPosition = ball - dir * back + Vector3.up * up;
            targetLookAt = ball + dir * (putting ? 1f : 8f);
            positionLag = .25f; lookLag = .08f;
        }

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
            if (shotFrame)
            {
                // Position and look smoothing must never let the ball pass behind the lens.
                float ahead = Vector3.Dot(transform.position - flightBall, flightDirection);
                if (ahead > -1f) transform.position -= flightDirection * (ahead + 1f);
                float lookAhead = Vector3.Dot(lookAt - transform.position, flightDirection);
                if (lookAhead < 1f) lookAt += flightDirection * (1f - lookAhead);
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
            {
                var wanted = Quaternion.LookRotation(forward, cueUp) * Quaternion.Euler(0, 0, roll);
                transform.rotation = shotFrame && !cut
                    ? Quaternion.Slerp(transform.rotation, wanted, 1f - Mathf.Exp(-Time.deltaTime / .10f)) : wanted;
            }
            shotFrame = false;
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
