using System;
using UnityEngine;
using NQuaternion = System.Numerics.Quaternion;
using NVector3 = System.Numerics.Vector3;

namespace GolfArcade.Swing
{
    /// One sample of how the phone is held and turning.
    public struct MotionSample
    {
        public double Time;
        public NQuaternion Attitude;
        public NVector3 RotationRate;
        /// Gravity in the device frame (unit length; zero when the source cannot tell). Device Y
        /// runs along the phone toward its top edge, so |Gravity.Y| near 1 means it hangs vertical.
        public NVector3 Gravity;
    }

    /// Where swing motion comes from: the phone's gyro on device, a phone on the network acting
    /// as the club, or something synthetic in the editor. `TryRead` returns the next unread
    /// sample (false when there is none yet this frame), so callers drain it in a loop.
    public interface IMotionSource
    {
        bool IsAvailable { get; }
        void Start();
        void Stop();
        bool TryRead(out MotionSample sample);
    }

    /// The iPhone's gyroscope through Unity's legacy Input.gyro, which Unity Remote also
    /// streams into the editor. Attitude is device orientation in a fixed frame; rotation
    /// rate is in the device frame, rad/s — exactly what the detector wants.
    public sealed class PhoneMotionSource : IMotionSource
    {
        int lastFrame = -1;
        public bool IsAvailable => SystemInfo.supportsGyroscope;

        public void Start()
        {
            if (!IsAvailable) return;
            Input.gyro.enabled = true;
            Input.gyro.updateInterval = 1f / 100f;
        }

        public void Stop()
        {
            if (IsAvailable) Input.gyro.enabled = false;
        }

        public bool TryRead(out MotionSample sample)
        {
            sample = default;
            if (!IsAvailable || !Input.gyro.enabled || lastFrame == Time.frameCount) return false;
            lastFrame = Time.frameCount;
            var q = Input.gyro.attitude;
            var r = Input.gyro.rotationRateUnbiased;
            var g = Input.gyro.gravity;
            var gravity = new NVector3(g.x, g.y, g.z);
            if (gravity.LengthSquared() > 0.01f) gravity = NVector3.Normalize(gravity);
            sample = new MotionSample
            {
                Time = Time.unscaledTimeAsDouble,
                Attitude = new NQuaternion(q.x, q.y, q.z, q.w),
                RotationRate = new NVector3(r.x, r.y, r.z),
                Gravity = gravity,
            };
            return true;
        }
    }

    /// Editor stand-in: hold the swing key to draw back (the meter fills), release to swing.
    /// Downswing speed follows how long you held — a short hold is a chip, a long one is full
    /// out. Hold Shift on release for an over-swing, A/D on release to open or close the face.
    /// Also drives on-screen buttons in play mode through the same Backswing/Release calls.
    public sealed class SyntheticMotionSource : IMotionSource
    {
        public KeyCode SwingKey = KeyCode.Space;
        public double BackswingRate = 2.0;     // rad/s of draw-back while held
        public double MaxBackswing = 2.6;      // rad, a full turn
        public double DownswingBase = 6.0;     // rad/s peak at the shortest hold
        public double DownswingPerRadian = 4.0; // extra peak rad/s per radian of backswing
        public double FaceRollDegrees = 0;     // set before release to shape the shot
        public double SpeedScale = 1;          // e.g. 1.4 for an over-swing

        public bool IsAvailable => true;

        enum Stage { Rest, Back, Down, Recover }
        Stage stage = Stage.Rest;
        int lastFrame = -1;
        double angle;          // current rotation from address about the swing axis, rad
        double peak;           // rad/s the downswing is aiming for
        double downswingTime;
        double face;           // current face roll, deg
        double faceAtImpact;
        bool holding;
        readonly NVector3 swingAxis = NVector3.Normalize(new NVector3(1, 0.2f, 0.1f));
        /// Held like a club: the phone's long axis (device Y) pointing straight down in the
        /// detector's Z-up frame, which is what arms the detector.
        static readonly NQuaternion address = NQuaternion.CreateFromAxisAngle(NVector3.UnitX, (float)(-Math.PI / 2));

        public void Start() { stage = Stage.Rest; angle = 0; face = 0; }
        public void Stop() { }

        public void Backswing(bool pressed) => holding = pressed;

        public bool TryRead(out MotionSample sample)
        {
            sample = default;
            if (lastFrame == Time.frameCount) return false;
            lastFrame = Time.frameCount;
            double dt = Time.unscaledDeltaTime;
            bool keyHeld = holding || (SwingKey != KeyCode.None && Input.GetKey(SwingKey));
            double rate = 0;
            switch (stage)
            {
                case Stage.Rest:
                    if (keyHeld) stage = Stage.Back;
                    break;
                case Stage.Back:
                    if (keyHeld)
                    {
                        rate = BackswingRate;
                        angle = Math.Min(MaxBackswing, angle + rate * dt);
                    }
                    else
                    {
                        stage = Stage.Down;
                        double scale = SpeedScale * (Input.GetKey(KeyCode.LeftShift) ? 1.4 : 1);
                        peak = (DownswingBase + DownswingPerRadian * angle) * scale;
                        faceAtImpact = FaceRollDegrees + (Input.GetKey(KeyCode.A) ? -25 : 0) + (Input.GetKey(KeyCode.D) ? 25 : 0);
                        downswingTime = 0;
                    }
                    break;
                case Stage.Down:
                    downswingTime += dt;
                    // A quick bell of speed through impact.
                    rate = -peak * Math.Sin(Math.Min(Math.PI, downswingTime / 0.25 * Math.PI));
                    angle += rate * dt;
                    face = faceAtImpact * Math.Min(1, downswingTime / 0.2);
                    if (angle <= -0.6 || downswingTime > 0.6) stage = Stage.Recover;
                    break;
                case Stage.Recover:
                    // Settle back at address so the detector re-arms.
                    angle = 0; face = 0; rate = 0;
                    if (!keyHeld) stage = Stage.Rest;
                    break;
            }
            var swing = NQuaternion.CreateFromAxisAngle(swingAxis, (float)angle);
            var shaft = NVector3.Transform(NVector3.UnitY, address);
            var roll = NQuaternion.CreateFromAxisAngle(shaft, (float)(face * Math.PI / 180));
            sample = new MotionSample
            {
                Time = Time.unscaledTimeAsDouble,
                Attitude = NQuaternion.Normalize(roll * swing * address),
                RotationRate = swingAxis * (float)rate,
                Gravity = NVector3.UnitY, // hanging like a club: gravity runs along the phone toward its top edge
            };
            return true;
        }
    }
}
