using UnityEngine;

namespace GolfArcade.Tennis
{
    public struct TennisHit
    {
        public bool Contact;
        public float Timing, Center, Positioning, Quality, Speed, ErrorDegrees;
        public string Label;
    }

    /// Deterministic, independently testable gameplay tuning. Distances are metres.
    public static class TennisRules
    {
        public const float CourtHalfWidth = 4.115f, CourtHalfLength = 11.885f;
        // Reference animation time; actors stretch this timeline by strength.
        public const float StrokeDuration = .46f, SweetTime = .18f, TimingWindow = .19f;
        public const float RunSpeed = 6.2f, SprintSpeed = 9f, Acceleration = 28f;
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

        public static bool AssistedContact(Vector3 oldBall, Vector3 ball, Vector3 player, float age, bool overhead, out float quality, float power=0.5f)
        {
            quality=0;
            float forgiveness=Mathf.Lerp(1.45f,.95f,Mathf.Clamp01(power));
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
        public static bool PredictInterceptX(Vector3 position, Vector3 velocity, float planeZ, float restitution, out float x)
        {
            x = position.x;
            if (velocity.z >= 0 == position.z >= planeZ) return false;
            const float dt = 1f/120;
            for (int i = 0; i < 480; i++)
            {
                Vector3 next = position + velocity * dt + Vector3.down * (4.905f * dt * dt);
                velocity += Vector3.down * (9.81f * dt);
                if (next.y < BallRadius && velocity.y < 0)
                {
                    next.y = BallRadius; velocity.y = -velocity.y * Mathf.Clamp01(restitution);
                }
                if ((position.z - planeZ) * (next.z - planeZ) <= 0)
                {
                    float span = next.z - position.z;
                    float t = Mathf.Abs(span) < .000001f ? 0 : (planeZ - position.z) / span;
                    x = Mathf.Lerp(position.x, next.x, Mathf.Clamp01(t));
                    return true;
                }
                position = next;
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
