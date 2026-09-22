using System;
using System.Numerics;
using GolfArcade.Shot;

namespace GolfArcade.Swing
{
    public enum SwingPhase { Settling, Address, Backswing, Downswing, Finish }

    public enum SwingEventKind { Load, Cancel, Impact }

    /// What the phone reports to the game: the backswing filling (load, 0–1), a swing that
    /// fizzled, or an impact with everything the shot needs.
    public readonly struct SwingEvent
    {
        public readonly SwingEventKind Kind;
        public readonly double Load;
        public readonly SwingImpact Impact;

        SwingEvent(SwingEventKind kind, double load, SwingImpact impact) { Kind = kind; Load = load; Impact = impact; }
        public static SwingEvent Loaded(double load) => new(SwingEventKind.Load, load, default);
        public static SwingEvent Cancelled() => new(SwingEventKind.Cancel, 0, default);
        public static SwingEvent Struck(SwingImpact impact) => new(SwingEventKind.Impact, 0, impact);
    }

    /// Observed execution, separate from the target chosen by the player.
    public struct SwingImpact
    {
        /// 0–1 meter reading: peak downswing speed against the club's full speed.
        public double Power;
        /// Degrees right of the aim line the ball starts on (a pushed or pulled face).
        public double StartLineDegrees;
        /// Spin-axis tilt in degrees; positive curves right (slice for a right-hander).
        public double CurveDegrees;
        /// Wrist roll between address and impact, degrees; positive = face open.
        public double FaceDegrees;
        /// How far past the club's full speed the downswing went (0 = at or under). Wild when large.
        public double Overswing;
        /// Seconds from the start of the backswing to impact.
        public double TempoSeconds;
        /// Peak rotation speed of the downswing, rad/s.
        public double PeakSpeed;
        /// Backswing size, 0–1, the load the meter showed before the downswing.
        public double Backswing;
    }

    /// Phone-as-club swing recognizer. Feed it attitude and rotation-rate samples and it reports
    /// load, cancellation, and impact. No Unity types, so tests can drive it with synthetic swings.
    ///
    /// Address is the phone held still and hanging like a club — its long axis pointing at the
    /// ground, the way a shaft does when the hands are at the grip. Until it is, nothing arms
    /// and no power can be drawn. A backswing is rotation away from address — the meter fills
    /// as you draw back, like Wii Sports. The downswing starts when the phone turns back toward
    /// address quickly; impact is the moment it passes back through address (or clearly
    /// decelerates). Power is the peak rotation speed of that downswing — that is what the shot
    /// is made of — trimmed a little by how far back you took it: a short, low backswing gives up
    /// at most a third of the club, and a full turn swung hard is the full club. The wrist's
    /// roll at impact, relative to address, is the club face:
    /// open slices, closed hooks. Swinging much harder than the club's full speed makes the
    /// shot wild, which is the Wii's rule too.
    public sealed class MotionSwingDetector
    {
        /// Up in the attitude's reference frame. iOS Core Motion attitude (what Input.gyro gives)
        /// is in a Z-vertical frame.
        public Vector3 WorldUp = Vector3.UnitZ;
        /// How far, in degrees, the phone's long axis may lean from straight down and still count
        /// as a club at address. A driver shaft leans about 30° at address.
        public double PointedDownDegrees = 35;
        /// Radians from address that count as the start of a backswing.
        public double BackswingStart = 0.25;
        /// Radians of backswing shown as 100 % load: a full shoulder turn, phone up behind you.
        /// Hip-high (about 90°) reads around 60 %.
        public double FullBackswing = 2.6;
        /// Power at full downswing speed from a barely-there backswing; it climbs linearly to 1 at
        /// a full backswing. So power = speed ratio × (BackswingFloor + (1 − BackswingFloor) × load).
        /// High, so the downswing's speed is what decides the distance: a hip-high backswing swung
        /// at full speed is still nearly the whole club.
        public double BackswingFloor = 0.7;
        /// Rotation speed (rad/s) under which the phone counts as "not swinging" for arming. As
        /// long as the phone hangs like a club and is not mid-swing, it is ready — no dead-still
        /// hold needed.
        public double ArmSpeed = 1.2;
        /// Rotation speed (rad/s) that starts the downswing once the phone turns back toward address.
        public double DownswingSpeed = 2.5;
        /// Peak rotation speed (rad/s) that produces full power. Set per club.
        public double FullSpeed = 14.0;
        /// Slower peaks are a waggle, not a swing, and do not spend a shot.
        public double MinimumSpeed = 1.5;
        /// Radians from address at which the downswing counts as impact.
        public double ImpactAngle = 0.5;
        /// A phone rotating slower than this is at rest (used to notice the club being lifted
        /// out of position between swings).
        public double StillSpeed = 0.6;
        /// How long the phone has to hang like a club, not swinging, before it arms.
        public double StillDuration = 0.1;
        /// Degrees of ball curve per degree of face roll, and of start line per degree.
        public double CurvePerFaceDegree = 0.5;
        public double StartLinePerFaceDegree = 0.25;
        /// Face roll under this (degrees) is a square strike; keeps small wrist wobble straight.
        public double FaceDeadZoneDegrees = 6;
        public double MaxCurveDegrees = 15;
        public double MaxStartLineDegrees = 12;
        /// Speed past full (as a fraction of FullSpeed) that is forgiven before the shot goes wild.
        /// Full speed is a solid swing, not the hardest one; only half again past it is over the top.
        public double OverswingGrace = 0.5;
        /// Extra curve, degrees, per unit of overswing beyond the grace.
        public double OverswingCurve = 40;

        public SwingPhase Phase { get; private set; } = SwingPhase.Settling;
        /// Latest backswing load (0–1) for HUD polling between events.
        public double Load { get; private set; }
        /// Whether the last sample had the phone hanging like a club: long axis near vertical
        /// with the top edge down (when gravity is known — the attitude fallback cannot tell ends).
        public bool PointedDown { get; private set; }
        /// True when the phone is vertical enough but upside down for a club (top edge up).
        public bool WrongEndDown { get; private set; }
        /// Degrees the phone's long axis leans from vertical, from the last sample.
        public double LeanDegrees { get; private set; }

        Quaternion reference = Quaternion.Identity;
        double? stillSince;
        double peakAngle;
        double peakSpeed;
        double downswingStart;
        double swingStart;
        double backswingLoad;
        Vector3 swingAxis;

        public void Reset()
        {
            Phase = SwingPhase.Settling;
            stillSince = null;
            peakAngle = peakSpeed = downswingStart = swingStart = backswingLoad = 0;
            Load = 0;
        }

        /// Resets and re-tunes for a club. The putter needs a far gentler scale.
        public void Configure(Shot.GolfClub club)
        {
            var fresh = new MotionSwingDetector { FullSpeed = club.MotionFullSpeed() };
            if (club == Shot.GolfClub.Putter)
            {
                fresh.BackswingStart = 0.035;
                fresh.FullBackswing = 0.6;
                fresh.DownswingSpeed = 0.12;
                fresh.MinimumSpeed = 0.10;
                fresh.ImpactAngle = 0.025;
                fresh.StillSpeed = 0.04;
                fresh.ArmSpeed = 0.15;
                fresh.BackswingFloor = 0.6;
                fresh.CurvePerFaceDegree = 0;
                fresh.StartLinePerFaceDegree = 0.35;
                fresh.MaxStartLineDegrees = 6;
            }
            CopyTuning(fresh);
            Reset();
        }

        void CopyTuning(MotionSwingDetector o)
        {
            BackswingStart = o.BackswingStart; FullBackswing = o.FullBackswing; DownswingSpeed = o.DownswingSpeed;
            FullSpeed = o.FullSpeed; MinimumSpeed = o.MinimumSpeed; ImpactAngle = o.ImpactAngle; ArmSpeed = o.ArmSpeed; BackswingFloor = o.BackswingFloor;
            StillSpeed = o.StillSpeed; StillDuration = o.StillDuration; WorldUp = o.WorldUp; PointedDownDegrees = o.PointedDownDegrees;
            CurvePerFaceDegree = o.CurvePerFaceDegree; StartLinePerFaceDegree = o.StartLinePerFaceDegree;
            FaceDeadZoneDegrees = o.FaceDeadZoneDegrees; MaxCurveDegrees = o.MaxCurveDegrees;
            MaxStartLineDegrees = o.MaxStartLineDegrees; OverswingGrace = o.OverswingGrace; OverswingCurve = o.OverswingCurve;
        }

        /// `attitude` is the phone's orientation in a fixed world frame (any frame, as long as it
        /// is the same one every sample); `rotationRate` is angular velocity in the phone frame,
        /// rad/s. Returns an event when something happened.
        public SwingEvent? Ingest(double time, Quaternion attitude, Vector3 rotationRate) => Ingest(time, attitude, rotationRate, Vector3.Zero);

        /// `gravity` is the gravity direction in the phone's own frame when the source knows it
        /// (the device's accelerometer/gyro fusion); zero means "unknown", and the lean is then
        /// read off the attitude against `WorldUp` instead.
        public SwingEvent? Ingest(double time, Quaternion attitude, Vector3 rotationRate, Vector3 gravity)
        {
            double speed = rotationRate.Length();
            double angle = AngleBetween(reference, attitude);
            bool haveGravity = gravity.LengthSquared() > 0.25f;
            LeanDegrees = haveGravity ? LeanFromGravity(gravity) : LeanFromVertical(attitude, WorldUp);
            // Gravity points at the ground; with the top edge down it runs along device +Y.
            bool topDown = !haveGravity || gravity.Y > 0;
            PointedDown = LeanDegrees <= PointedDownDegrees && topDown;
            WrongEndDown = LeanDegrees <= PointedDownDegrees && !topDown;
            // "Ready" = hanging like a club and not mid-swing, for a moment.
            if (PointedDown && speed < ArmSpeed) stillSince ??= time; else stillSince = null;
            bool ready = stillSince is double since && time - since >= StillDuration;
            bool isStill = speed < StillSpeed;

            if ((Phase == SwingPhase.Backswing || Phase == SwingPhase.Downswing) && time - swingStart > 3)
            {
                Phase = SwingPhase.Settling;
                stillSince = null;
                Load = 0;
                return SwingEvent.Cancelled();
            }

            switch (Phase)
            {
                case SwingPhase.Settling:
                case SwingPhase.Finish:
                    // Arm as soon as the phone hangs like a club and is not swinging.
                    if (!ready) return null;
                    reference = attitude;
                    Phase = SwingPhase.Address;
                    Load = 0;
                    return null;

                case SwingPhase.Address:
                    // Lifted the phone out of the club position without swinging: back to settling.
                    if (!PointedDown && isStill)
                    {
                        Phase = SwingPhase.Settling;
                        stillSince = null;
                        return null;
                    }
                    // Still hanging and at rest: follow the hands, so address is wherever the
                    // club settles and a small waggle never becomes a backswing. (Not for the
                    // putter's tiny strokes: its StillSpeed is far below any real stroke.)
                    if (angle <= BackswingStart && isStill && ready) reference = attitude;
                    if (angle <= BackswingStart) return null;
                    Phase = SwingPhase.Backswing;
                    swingStart = time;
                    peakAngle = angle;
                    peakSpeed = 0;
                    swingAxis = RotationAxis(reference, attitude);
                    Load = LoadFor(angle);
                    return SwingEvent.Loaded(Load);

                case SwingPhase.Backswing:
                    if (angle >= peakAngle)
                    {
                        peakAngle = angle;
                        swingAxis = RotationAxis(reference, attitude);
                    }
                    if (angle < peakAngle - Math.Min(0.15, BackswingStart * 0.6) && speed >= DownswingSpeed)
                    {
                        Phase = SwingPhase.Downswing;
                        peakSpeed = speed;
                        downswingStart = time;
                        backswingLoad = LoadFor(peakAngle);
                        Load = backswingLoad;
                        return SwingEvent.Loaded(Load);
                    }
                    if (angle < BackswingStart && speed < DownswingSpeed)
                    {
                        Phase = SwingPhase.Address;
                        Load = 0;
                        return SwingEvent.Cancelled();
                    }
                    Load = LoadFor(angle);
                    return SwingEvent.Loaded(Load);

                case SwingPhase.Downswing:
                    peakSpeed = Math.Max(peakSpeed, speed);
                    bool decelerated = speed < peakSpeed * 0.4;
                    if (!(angle < ImpactAngle || decelerated || time - downswingStart > 1.2)) return null;
                    Phase = SwingPhase.Finish;
                    stillSince = null;
                    Load = 0;
                    if (peakSpeed < MinimumSpeed) return SwingEvent.Cancelled();
                    return SwingEvent.Struck(BuildImpact(time, attitude));
            }
            return null;
        }

        SwingImpact BuildImpact(double time, Quaternion attitude)
        {
            double face = FaceRollDegrees(reference, attitude, swingAxis);
            double signedFace = Math.Abs(face) <= FaceDeadZoneDegrees ? 0 : face - Math.Sign(face) * FaceDeadZoneDegrees;
            double ratio = peakSpeed / FullSpeed;
            double overswing = Math.Max(0, ratio - 1 - OverswingGrace);
            // How far back you went scales what the speed is worth: a low, short backswing chips.
            double power = Math.Min(1, ratio) * (BackswingFloor + (1 - BackswingFloor) * backswingLoad);
            // Over the top: the face error grows, and a square face still goes somewhere.
            double wildDirection = signedFace != 0 ? Math.Sign(signedFace) : (face >= 0 ? 1 : -1);
            double curve = signedFace * CurvePerFaceDegree + wildDirection * overswing * OverswingCurve;
            double startLine = signedFace * StartLinePerFaceDegree;
            return new SwingImpact
            {
                Power = Math.Min(1, power),
                CurveDegrees = Clamp(curve, -MaxCurveDegrees, MaxCurveDegrees),
                StartLineDegrees = Clamp(startLine, -MaxStartLineDegrees, MaxStartLineDegrees),
                FaceDegrees = face,
                Overswing = overswing,
                TempoSeconds = time - swingStart,
                PeakSpeed = peakSpeed,
                Backswing = backswingLoad,
            };
        }

        double LoadFor(double angle) => Math.Min(1, Math.Max(0, angle / FullBackswing));

        static double Clamp(double v, double lo, double hi) => Math.Max(lo, Math.Min(hi, v));

        /// Degrees between the phone's long axis (device Y) and gravity, either end down.
        public static double LeanFromGravity(Vector3 gravity)
        {
            var g = Vector3.Normalize(gravity);
            return Math.Acos(Math.Min(1, Math.Abs(g.Y))) * 180 / Math.PI;
        }

        /// Degrees between the phone's long axis (device Y) and vertical, either end down.
        public static double LeanFromVertical(Quaternion attitude, Vector3 worldUp)
        {
            var axis = Vector3.Normalize(Vector3.Transform(Vector3.UnitY, attitude));
            double c = Math.Abs(Vector3.Dot(axis, Vector3.Normalize(worldUp)));
            return Math.Acos(Math.Min(1, c)) * 180 / Math.PI;
        }

        /// Total rotation, in radians, between two orientations.
        public static double AngleBetween(Quaternion a, Quaternion b)
        {
            var relative = Quaternion.Normalize(b * Quaternion.Inverse(a));
            return 2 * Math.Acos(Math.Min(1, Math.Abs(relative.W)));
        }

        /// World-frame axis of the rotation from `a` to `b` (unit length; zero if no rotation).
        static Vector3 RotationAxis(Quaternion a, Quaternion b)
        {
            var relative = Quaternion.Normalize(b * Quaternion.Inverse(a));
            if (relative.W < 0) relative = Quaternion.Negate(relative);
            var axis = new Vector3(relative.X, relative.Y, relative.Z);
            float len = axis.Length();
            return len < 1e-6f ? Vector3.Zero : axis / len;
        }

        /// Wrist roll between address and now, degrees: the part of the relative rotation that
        /// is about the phone's own long axis rather than about the swing arc. Positive is a
        /// roll toward the swing axis's right-hand sense — which, with the phone held like a
        /// grip, is an opening face.
        public static double FaceRollDegrees(Quaternion address, Quaternion now, Vector3 swingAxis)
        {
            var relative = Quaternion.Normalize(now * Quaternion.Inverse(address));
            if (relative.W < 0) relative = Quaternion.Negate(relative);
            // The phone's long axis (device +Y) in the world at address is the shaft.
            var shaft = Vector3.Normalize(Vector3.Transform(Vector3.UnitY, address));
            // Swing-twist decomposition: twist is the projection of the rotation onto the shaft.
            var r = new Vector3(relative.X, relative.Y, relative.Z);
            float proj = Vector3.Dot(r, shaft);
            var twist = new Quaternion(shaft.X * proj, shaft.Y * proj, shaft.Z * proj, relative.W);
            float tl = twist.Length();
            if (tl < 1e-6f) return 0;
            twist = Quaternion.Normalize(twist);
            double angle = 2 * Math.Atan2(Math.Sqrt(twist.X * twist.X + twist.Y * twist.Y + twist.Z * twist.Z), twist.W);
            if (angle > Math.PI) angle -= 2 * Math.PI;
            double sign = Math.Sign(proj) == 0 ? 1 : Math.Sign(proj);
            // Reference the sign to the swing arc so left- and right-handed swings read alike.
            double hand = swingAxis == Vector3.Zero ? 1 : Math.Sign(Vector3.Dot(swingAxis, shaft)) switch { 0 => 1, var s => s };
            return angle * sign * hand * 180 / Math.PI;
        }
    }
}
