using UnityEngine;

namespace GolfArcade.Tennis
{
    public struct TennisHit
    {
        public bool Contact;
        public float Timing, Center, Positioning, Quality, Speed, ErrorDegrees;
        public string Label;
    }

    /// How cleanly a ball was struck, reported back to the player as a hitmarker.
    public enum Timing { Missed, Ok, Good, Great, Excellent, Perfect }

    /// Deterministic, independently testable gameplay tuning. Distances are metres.
    public static class TennisRules
    {
        /// Consecutive well-struck balls needed to supercharge the next one.
        public const int SuperchargeStreak = 3;
        /// A hit must grade at least this well to extend the streak.
        public const Timing StreakFloor = Timing.Great;
        public const float SuperchargeSpeed = 1.35f;

        /// Grade a contact from its timing score (0 = mistimed, 1 = dead on).
        public static Timing Grade(float timing)
        {
            if (timing <= 0) return Timing.Missed;
            if (timing >= .96f) return Timing.Perfect;
            if (timing >= .88f) return Timing.Excellent;
            if (timing >= .75f) return Timing.Great;
            if (timing >= .55f) return Timing.Good;
            return Timing.Ok;
        }

        public static string GradeLabel(Timing grade) => grade switch
        {
            Timing.Perfect => "PERFECT!",
            Timing.Excellent => "EXCELLENT",
            Timing.Great => "GREAT",
            Timing.Good => "GOOD",
            Timing.Ok => "OK",
            _ => "MISS",
        };

        /// Does this grade keep a streak alive?
        public static bool Extends(Timing grade) => grade >= StreakFloor;

        /// Turn a hit into its supercharged version: markedly faster and dead accurate, as
        /// the reward for three well-timed balls in a row.
        public static TennisHit Supercharge(TennisHit hit)
        {
            hit.Speed *= SuperchargeSpeed;
            hit.ErrorDegrees *= .25f;
            hit.Quality = Mathf.Max(hit.Quality, .95f);
            hit.Label = "SUPERCHARGED";
            return hit;
        }

        /// Where the ball met the string bed, in string-bed coordinates (metres, x across and
        /// y up the face). Used for the impact map, so it must be a real measurement rather
        /// than an assumed centre hit: an assisted return still struck the racket somewhere.
        public static Vector2 FaceOffset(Vector3 ball, Vector3 sweetSpot, Vector3 right, Vector3 up)
        {
            Vector3 delta = ball - sweetSpot;
            return new Vector2(Vector3.Dot(delta, right), Vector3.Dot(delta, up));
        }

        /// How far off-centre a contact was, as a fraction of the string bed (0 = middle,
        /// 1 = the edge). Drives the impact map's colouring and the coaching hint.
        public static float FaceError(Vector2 offset) =>
            new Vector2(offset.x / StringHalfWidth, offset.y / StringHalfHeight).magnitude;
        public const float CourtHalfWidth = 4.115f, CourtHalfLength = 11.885f;
        /// Service line sits 6.40m from the net, and the centre line splits each side into
        /// two boxes 4.115m wide — full-size singles dimensions.
        public const float ServiceLine = 6.40f, NetHeight = .97f;
        // Reference animation time; actors stretch this timeline by strength.
        public const float StrokeDuration = .46f, SweetTime = .18f, TimingWindow = .19f;
        public const float RunSpeed = 6.2f, SprintSpeed = 9f, Acceleration = 28f;
        /// Braking the auto-positioning uses to arrive at the ball instead of stopping dead.
        public const float Deceleration = 22f;
        /// Opponent difficulty. It covers the court at a human pace with a limited reach and
        /// a real miss rate, so wide, deep and well-struck balls actually win points.
        public const float OpponentSpeed = 4.6f, OpponentReach = 1.75f, OpponentErrorRate = .24f;
        // Arcade racket: a genuinely bigger string bed than a real one, because the player is
        // aiming with a phone they cannot see while looking at a TV.
        public const float StringHalfWidth = .175f, StringHalfHeight = .24f, BallRadius = .10f;
        /// Movement assist. The character leans toward where the ball is actually going, but
        /// only while it is further away than `AssistDeadBand` — the last stretch is the
        /// player's own job, so positioning still matters.
        public const float AssistDeadBand = 1.0f, AssistAuthority = .55f;
        public static bool UseBackhand(float facing,bool fallbackLeft,bool leftHanded) =>
            (Mathf.Abs(facing)>.5f ? facing<0 : fallbackLeft) != leftHanded;
        public static float StrokePhase(float age)
        {
            if (age <= SweetTime) return .5f * Mathf.Pow(Mathf.Clamp01(age / SweetTime), 1.7f);
            float finish = Mathf.Clamp01((age - SweetTime) / (StrokeDuration - SweetTime));
            return .5f + .5f * (1 - (1 - finish) * (1 - finish));
        }
        public static TennisHit Evaluate(float swingAge, Vector2 faceOffset, float balance, float reachQuality, float power, float stamina)
        {
            float radial = new Vector2(faceOffset.x / StringHalfWidth, faceOffset.y / StringHalfHeight).magnitude;
            float timing = Mathf.Clamp01(1 - Mathf.Abs(swingAge - SweetTime) / Mathf.Lerp(TimingWindow,.10f,Mathf.Clamp01(power)));
            bool contact = new Vector2(faceOffset.x / (StringHalfWidth + BallRadius), faceOffset.y / (StringHalfHeight + BallRadius)).magnitude <= 1 && timing > 0;
            float center = Mathf.Clamp01(1 - radial);
            float positioning = Mathf.Clamp01(balance) * Mathf.Clamp01(reachQuality);
            float quality = contact ? timing * .35f + center * .40f + positioning * .25f : 0;
            return new TennisHit {
                Contact = contact, Timing = timing, Center = center, Positioning = positioning, Quality = quality,
                Speed = contact ? Mathf.Lerp(15, 27, Mathf.Clamp01(power)) * Mathf.Lerp(.85f, 1, quality) * Mathf.Lerp(.92f, 1, Mathf.Clamp01(stamina)) : 0,
                ErrorDegrees = contact ? Mathf.Lerp(1.0f, 19f, 1 - quality) + (1 - Mathf.Clamp01(stamina)) * 5 : 0,
                Label = !contact ? "MISS" : quality > .86f ? "SWEET SPOT" : quality > .63f ? "CLEAN HIT" : radial > .72f ? "OFF CENTER" : timing < .45f ? "MISTIMED" : "OFF BALANCE"
            };
        }

        public static float StaminaStep(float value, float speed, float dt)
        {
            float exertion = Mathf.Pow(Mathf.Clamp01(Mathf.Abs(speed) / SprintSpeed), 2);
            return Mathf.Clamp01(value + (Mathf.Abs(speed) < .15f ? .13f : -.23f * exertion) * dt);
        }

        public static bool AssistedContact(Vector3 oldBall, Vector3 ball, Vector3 player, float age, bool overhead, out float quality, float power=0.5f, bool dive=false)
        {
            quality=0;
            // A dive throws the racket much further sideways than a normal stroke can reach.
            float forgiveness=Mathf.Lerp(1.45f,.95f,Mathf.Clamp01(power))*(dive?1.8f:1f);
            if(Mathf.Abs(age-SweetTime)>Mathf.Lerp(.18f,.105f,Mathf.Clamp01(power))) return false;
            Vector3 center=player+new Vector3(0,overhead ? 2.1f : 1.1f,.65f);
            Vector3 segment=ball-oldBall;
            float t=segment.sqrMagnitude>.000001f ? Mathf.Clamp01(Vector3.Dot(center-oldBall,segment)/segment.sqrMagnitude) : 0;
            Vector3 offset=Vector3.Lerp(oldBall,ball,t)-center;
            float distance=new Vector3(offset.x/forgiveness,offset.y/(overhead ? 1.0f : .95f),offset.z/forgiveness).magnitude;
            if(distance>1 || ball.z<player.z-.25f) return false;
            quality=Mathf.Lerp(.3f,.65f,1-distance);
            return true;
        }

        /// Where an incoming ball will reach the player's baseline plane, by plain ballistics
        /// with bounces. Pure and deterministic so the movement assist can be tested without
        /// a scene.
        public static bool PredictInterceptX(Vector3 position, Vector3 velocity, float planeZ, float restitution, out float x, float spin = 0)
        {
            x = position.x;
            if (velocity.z >= 0 == position.z >= planeZ) return false;
            const float dt = TennisBall.Step;
            for (int i = 0; i < 480; i++)
            {
                Vector3 next = position, v = velocity;
                TennisBall.Integrate(ref next, ref v, spin, dt);
                // The same bounce the simulation applies, spin and all. The old prediction
                // skipped the bounce's loss of pace, so the auto-positioning aimed short.
                if (next.y < BallRadius && v.y < 0) { next.y = BallRadius; TennisBall.Bounce(ref v, ref spin, restitution); }
                if ((position.z - planeZ) * (next.z - planeZ) <= 0)
                {
                    float span = next.z - position.z;
                    float t = Mathf.Abs(span) < .000001f ? 0 : (planeZ - position.z) / span;
                    x = Mathf.Lerp(position.x, next.x, Mathf.Clamp01(t));
                    return true;
                }
                position = next; velocity = v;
            }
            return false;
        }

        /// How far to shift the player toward the predicted intercept, in court metres.
        /// Zero once the player is inside the dead band, so the final approach is always
        /// theirs: the assist closes the long gaps, never the last metre.
        public static float MovementAssist(float playerX, float interceptX)
        {
            float gap = interceptX - playerX;
            float beyond = Mathf.Abs(gap) - AssistDeadBand;
            if (beyond <= 0) return 0;
            return beyond * Mathf.Sign(gap) * AssistAuthority;
        }

        /// Serve timing. The ball is tossed, rises, and must be struck near the apex with a
        /// downward swing. Miss the window and it goes into the net — one fault is allowed.
        public const float ServeTossDelay = 1.75f, ServeApex = .62f, ServeIdealContact = .58f;
        /// Generous by design: almost any committed swing during the toss should go in. Only
        /// a wildly early or late one nets. The old +/-0.20s window was unplayable once swing
        /// detection latency was accounted for.
        public const float ServeCatch = 1.30f, ServePerfectWindow = .18f, ServeLegalWindow = .42f;
        /// Motion detection needs a moment of swing before it can confirm one, so the moment
        /// it reports is always later than the moment the player actually started. Without
        /// this correction every serve reads as late.
        public const float ServeLatency = .10f;
        /// The same correction when the swing is reported at onset rather than confirmation:
        /// only the short onset hold and the rise to threshold are left.
        public const float ServeOnsetLatency = .04f;
        /// A serve needs a real overhead action: the phone must be raised and swung.
        public const float ServeMinPower = .15f, ServeMinLift = .02f;
        /// Contact happens above the head, not wherever the racket happens to be resting.
        public const float ServeContactHeight = 2.45f;
        /// Clearance the ball must have over the net for a serve to count as safe.
        public const float NetMargin = .22f;
        /// How far off the centre mark the server stands, and how wide the receiver waits.
        public const float ServerStance = 1.35f, ReceiverStance = 1.95f;

        public struct ServeJudgement
        {
            public bool Struck, Legal;
            public Vector3 Landing;
            public float Speed, Accuracy;
            public string Label;
        }

        /// Judge a serve attempt. `offset` is how far the contact was from the ideal moment,
        /// in seconds (negative = early). A weak or flat-handed swing is not a serve at all.
        public static ServeJudgement JudgeServe(float offset, float power, float lift, Vector3 boxCentre, bool serverNearSide)
        {
            var judgement = new ServeJudgement { Struck = true };
            if (power < ServeMinPower || lift < ServeMinLift)
            { judgement.Struck = false; judgement.Label = "NOT A SERVE — swing down hard from above your head"; return judgement; }
            float error = Mathf.Abs(offset);
            judgement.Accuracy = Mathf.Clamp01(1 - error / ServeLegalWindow);
            if (error > ServeLegalWindow)
            {
                // Mistimed: struck too flat or too late to clear the net.
                judgement.Legal = false;
                judgement.Landing = new Vector3(boxCentre.x * .5f, BallRadius, serverNearSide ? .6f : -.6f);
                judgement.Speed = Mathf.Lerp(14, 20, Mathf.Clamp01(power));
                judgement.Label = offset < 0 ? "FAULT — too early, into the net" : "FAULT — too late, into the net";
                return judgement;
            }
            judgement.Legal = true;
            // Placement is automatic: a legal serve always goes to the correct box. Timing
            // decides only whether the ball clears the net, not where it lands.
            judgement.Landing = boxCentre;
            judgement.Speed = Mathf.Lerp(28, 44, Mathf.Clamp01(power));
            judgement.Label = error <= ServePerfectWindow
                ? $"ACE ATTEMPT · {judgement.Speed * 3.6f:0} km/h"
                : $"SERVE IN · {judgement.Speed * 3.6f:0} km/h";
            return judgement;
        }

        /// Which half of the court a serve must land in. The server starts each game on the
        /// deuce (right-hand) court and serves diagonally, so the target box is on the
        /// opposite side of the centre line from the server.
        public static bool ServeTargetIsPositiveX(bool serverNearSide, bool deuceCourt) =>
            serverNearSide ? !deuceCourt : deuceCourt;

        /// Centre of the box a serve must land in, for aiming and for the landing marker.
        public static Vector3 ServeTargetCentre(bool serverNearSide, bool deuceCourt)
        {
            float x = (CourtHalfWidth * .5f) * (ServeTargetIsPositiveX(serverNearSide, deuceCourt) ? 1 : -1);
            return new Vector3(x, BallRadius, (ServiceLine * .55f) * (serverNearSide ? 1 : -1));
        }

        /// Did a serve land in? Must clear the net, be on the receiver's side, inside the
        /// service line, and in the correct half.
        public static bool ServeIsIn(Vector3 landing, bool serverNearSide, bool deuceCourt)
        {
            float z = landing.z;
            if (serverNearSide ? (z <= 0 || z > ServiceLine) : (z >= 0 || z < -ServiceLine)) return false;
            if (Mathf.Abs(landing.x) > CourtHalfWidth) return false;
            return (landing.x >= 0) == ServeTargetIsPositiveX(serverNearSide, deuceCourt);
        }

        /// Velocity for a serve that is guaranteed to clear the net and still land on target.
        ///
        /// The plain ballistic solution takes the flattest arc that reaches the target, and
        /// from any realistic contact height that arc passes *under* the net — which made
        /// every serve a fault no matter how well it was timed. This lofts the arc until the
        /// ball is comfortably clear at the net crossing.
        public static Vector3 ServeVelocity(Vector3 start, Vector3 landing, float speed, float spin)
        {
            if (Mathf.Abs(spin) < .01f) return ServeVelocity(start, landing, speed);
            // Spun serves are solved numerically, then lofted until they clear the net by the
            // same margin the flat solution guarantees.
            Vector3 best = Vector3.zero;
            for (float s2 = speed; s2 > 8; s2 *= .92f)
            {
                best = TennisBall.Solve(start, landing, s2, spin);
                if (NetClearance(start, best, spin) > NetHeight + NetMargin) return best;
            }
            return best;
        }

        /// Height of a (possibly spun) ball as it crosses the net plane, by simulation.
        public static float NetClearance(Vector3 start, Vector3 velocity, float spin)
        {
            Vector3 p = start, v = velocity;
            for (int i = 0; i < 600; i++)
            {
                Vector3 before = p;
                TennisBall.Integrate(ref p, ref v, spin, TennisBall.Step);
                if (before.z * p.z <= 0)
                {
                    float t = Mathf.Abs(p.z - before.z) < 1e-6f ? 0 : -before.z / (p.z - before.z);
                    return Mathf.Lerp(before.y, p.y, Mathf.Clamp01(t));
                }
                if (p.y < BallRadius && v.y < 0) return 0;
            }
            return 0;
        }

        public static Vector3 ServeVelocity(Vector3 start, Vector3 landing, float speed)
        {
            float distance = new Vector2(landing.x - start.x, landing.z - start.z).magnitude;
            float flight = Mathf.Max(.28f, distance / Mathf.Max(12, speed));
            Vector3 best = Velocity(start, landing, flight);
            for (int i = 0; i < 60; i++)
            {
                Vector3 candidate = Velocity(start, landing, flight);
                if (NetCrossingHeight(start, landing, candidate, flight) > NetHeight + NetMargin) return candidate;
                best = candidate;
                flight += .03f;
            }
            return best;
        }

        static Vector3 Velocity(Vector3 start, Vector3 landing, float flight) =>
            (landing - start) / flight + Vector3.up * (4.905f * flight);

        /// How high the ball is as it passes over the net, for a given launch.
        public static float NetCrossingHeight(Vector3 start, Vector3 landing, Vector3 velocity, float flight)
        {
            float span = landing.z - start.z;
            if (Mathf.Abs(span) < .0001f) return start.y;
            float t = -start.z / span * flight;
            if (t <= 0 || t >= flight) return start.y;
            return start.y + velocity.y * t - 4.905f * t * t;
        }

        /// Where the meaningful part of a stroke clip lives. Playing from `StrokeEntry`
        /// rather than 0 skips a slow ready-in that a reactive swing has no time for, so the
        /// business end runs near authored speed instead of the whole clip being crushed.
        ///
        /// The spans are chosen so the clip genuinely accelerates INTO contact and relaxes
        /// afterwards. A first attempt used .32/.50/.88, which looks reasonable but covers
        /// 0.38 of the clip after contact against 0.18 before — making the follow-through
        /// 36% faster than the approach, i.e. decelerating into the ball. The window either
        /// side of contact has to be weighted against the real time available on each side.
        public const float StrokeEntry = .26f, StrokeContact = .50f, StrokeExit = .74f;

        /// Map elapsed swing time onto the stroke clip: accelerate into contact, then relax
        /// through the follow-through. Pure so the curve can be checked without a scene.
        public static float StrokePlayhead(float swingAge, float swingDuration) =>
            StrokePlayhead(swingAge, swingDuration, StrokeContact);

        /// Same curve, centred on a particular clip's own contact frame. Every clip used to be
        /// assumed to strike the ball exactly halfway through, whatever it actually animates;
        /// the measured frame (see ClipContacts) lands the racket on the ball on time.
        public static float StrokePlayhead(float swingAge, float swingDuration, float contact)
        {
            ContactWindow(contact, out float entry, out float exit);
            float toContact = SweetTime * swingDuration / StrokeDuration;
            if (swingAge <= toContact)
                return Mathf.Lerp(entry, contact, toContact > 0 ? Mathf.Clamp01(swingAge / toContact) : 1);
            float after = (swingAge - toContact) / Mathf.Max(.01f, swingDuration - toContact);
            return Mathf.Lerp(contact, exit, Mathf.Clamp01(after));
        }

        /// The slice of a clip a stroke plays, either side of its contact frame. Keeps the
        /// same spans as the default .26/.50/.74, shifted, and kept inside the clip.
        public static void ContactWindow(float contact, out float entry, out float exit)
        {
            contact = Mathf.Clamp(contact, .2f, .8f);
            entry = Mathf.Max(.02f, contact - (StrokeContact - StrokeEntry));
            exit = Mathf.Min(.98f, contact + (StrokeExit - StrokeContact));
        }

        /// Two-handed backhand shaping, layered on top of the clip.
        ///
        /// Measurement first: the authored Forehand and Backhand clips rotate the chest and
        /// hips by *identical* amounts, so the torso does the same thing in both strokes.
        /// They are not the same stroke, so the shared motion is right for the forehand by
        /// luck and wrong for the backhand.
        ///
        /// The reference is a TWO-handed backhand: both hands stay on the grip for the whole
        /// stroke, the shoulders coil deeply in preparation -- the player's back turns most
        /// of the way to the net -- the swing runs low to high, and the finish carries up
        /// over the leading shoulder with the hands still together. That is a different shape
        /// from a one-hander, where the off arm releases and sweeps back as a counterbalance;
        /// an earlier version of this modelled the one-hander and was simply the wrong stroke.
        ///
        /// Coil therefore peaks during the BACKSWING and releases through contact, rather
        /// than being held at contact the way a one-hander holds its chest side-on.
        /// Now authored into the clips; kept as the reference the Blender fix uses.
        public const float BackhandCoil = 42f;        // extra degrees of shoulder turn, peak backswing
        public const float BackhandSecondGrip = .085f; // metres down the handle for the off hand

        /// Coil strength across the stroke: builds into the backswing, releases through the
        /// ball, gone by the finish.
        public static float BackhandCoilAt(float playhead)
        {
            float span = Mathf.InverseLerp(StrokeEntry, StrokeExit, playhead);
            // Peak just before contact, then fall away faster than it built.
            return span < .35f ? Mathf.SmoothStep(0, 1, span / .35f)
                 : Mathf.SmoothStep(1, 0, Mathf.Clamp01((span - .35f) / .5f));
        }

        /// Locomotion styling, from running and tennis-movement biomechanics.
        ///
        /// Tennis coaching draws a hard line between the two ways of covering ground: a
        /// shuffle keeps the hips square to the net and is used for short distances, while a
        /// crossover step rotates the hips and is what you use when you have to reach a wide
        /// ball. Running the character square-on at every speed is what made the movement
        /// read as artificial.
        ///
        /// Running biomechanics adds the rest: the thorax and pelvis counter-rotate to cancel
        /// the angular momentum of the swinging legs, and that rotation grows with speed --
        /// trunk transverse-plane rotation is the component that changes most between walking
        /// and running. Arm swing is the dominant upper-body motion and scales with the legs.
        ///
        /// The thresholds below are sourced; the magnitudes are tuned for readability at the
        /// gameplay camera distance rather than measured.
        public const float ShuffleSpeed = 1.9f;      // below this, stay square to the net
        public const float CrossoverYaw = 70f;       // degrees of body turn at full sprint: turn and run
        public const float TrunkCounterRotation = 13f;   // degrees, anti-phase with the stride
        public const float ArmSwing = .24f;          // metres of fore/aft hand travel at sprint

        /// 0 while shuffling square to the net, 1 at a full crossover sprint.
        public static float CrossoverBlend(float speed) =>
            Mathf.Clamp01((Mathf.Abs(speed) - ShuffleSpeed) / Mathf.Max(.01f, RunSpeed - ShuffleSpeed));

        /// How far the body turns toward the direction of travel.
        public static float BodyYaw(float speed) =>
            Mathf.Sign(speed) * CrossoverYaw * CrossoverBlend(speed);

        /// One full run cycle covers roughly this far on the ground; root motion is stripped
        /// at import so it cannot be measured from the clip.
        public const float StrideMetres = 2.1f;

        /// Run-cycle advance for a ground speed, in cycles per second. No floor: standing
        /// still must not keep the feet turning over.
        public static float CycleRate(float speed) => Mathf.Abs(speed) / StrideMetres;

        /// Wii Sports put all the skill in the swing, because the character walks itself to
        /// the ball. Swinging early sends it cross-court, late sends it down the line, and
        /// only a wildly mistimed swing is punished. That is what keeps rallies alive while
        /// still rewarding timing.
        public const float AimWindow = .26f;

        /// Direction from swing timing, in the same -1..1 units the aim slider used.
        /// `offset` is seconds between the swing and the ball reaching the strike zone;
        /// negative is early.
        public static float AimFromTiming(float offset, bool backhand)
        {
            float bias = Mathf.Clamp(-offset / AimWindow, -1f, 1f);
            // A right-hander's early forehand pulls cross-court to their left; the backhand
            // mirrors it, so the same early swing opens the opposite corner.
            return backhand ? -bias : bias;
        }

        /// Which authored stroke suits the ball. Height and distance pick the animation, so
        /// the character visibly volleys at the net and reaches down for a low ball.
        public static string StrokeFor(float ballHeight, float ballZ, float playerZ, float lateralGap, bool highLift)
        {
            if (ballHeight > 1.75f) return "Smash";
            if (highLift) return "Lob";
            // A volley is about where the PLAYER is standing, not how near the ball has got:
            // testing ball-to-player distance made almost every rally ball a volley, because
            // the ball is always close to the player by the time they swing at it.
            if (Mathf.Abs(playerZ) < 4.5f && ballHeight > .8f) return "Volley";
            if (ballHeight < .55f) return "LowPickup";
            // Out of normal reach: throw the body at it. The dive clips exist in both wings.
            if (lateralGap > DiveGap) return "Dive";
            if (lateralGap > 1.5f) return "Running";
            return "Drive";
        }

        /// Lateral gap beyond which a stroke becomes a dive.
        public const float DiveGap = 2.6f;

        /// Where the server must stand: behind their own baseline, on the side of the centre
        /// mark matching the court they are serving from.
        public static float ServerStanceX(bool serverNearSide, bool deuceCourt) =>
            (serverNearSide ? 1 : -1) * (deuceCourt ? 1 : -1) * ServerStance;

        /// Where the receiver waits: on the side the serve is legally required to go, which
        /// is their own deuce side when the server is on theirs.
        public static float ReceiverStanceX(bool serverNearSide, bool deuceCourt) =>
            (ServeTargetIsPositiveX(serverNearSide, deuceCourt) ? 1 : -1) * ReceiverStance;

        /// Is a rally ball's bounce inside the singles court?
        public static bool BounceIsIn(Vector3 landing) =>
            Mathf.Abs(landing.x) <= CourtHalfWidth && Mathf.Abs(landing.z) <= CourtHalfLength;

        /// Where a ball will first touch the ground, by plain ballistics. Used for the
        /// landing marker and to judge in/out before the bounce actually happens.
        public static bool PredictLanding(Vector3 position, Vector3 velocity, out Vector3 landing, float spin = 0) =>
            TennisBall.Landing(position, velocity, spin, out landing, out _);

        public static Vector3 ShotTarget(float aim,float power) => new Vector3(Mathf.Clamp(aim,-1,1)*3.25f,BallRadius,Mathf.Lerp(4.5f,9.5f,Mathf.Clamp01(power)));

        public static Vector3 ShotVelocity(Vector3 start,Vector3 target,float speed)
        {
            Vector3 delta=target-start;
            float flight=Mathf.Max(.25f,new Vector2(delta.x,delta.z).magnitude/Mathf.Max(12,speed));
            return delta/flight+Vector3.up*(4.905f*flight);
        }

        // Relative sweep catches fast balls crossing the string bed between simulation steps.
        public static bool CrossStringBed(Vector3 oldBall, Vector3 ball, Vector3 oldCenter, Vector3 center,
            Vector3 normal, Vector3 right, Vector3 up, out Vector2 offset)
        {
            Vector3 a = oldBall - oldCenter, b = ball - center;
            float da = Vector3.Dot(a, normal), db = Vector3.Dot(b, normal);
            offset = default;
            if (da * db > 0 || Mathf.Abs(da - db) < .000001f) return false;
            Vector3 at = Vector3.Lerp(a, b, da / (da - db));
            offset = new Vector2(Vector3.Dot(at, right), Vector3.Dot(at, up));
            return new Vector2(offset.x / (StringHalfWidth + BallRadius), offset.y / (StringHalfHeight + BallRadius)).magnitude <= 1;
        }
    }
}
