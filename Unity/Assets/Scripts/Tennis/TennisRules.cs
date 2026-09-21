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
        public const float StrokeDuration = .70f, SweetTime = .32f, TimingWindow = .17f;
        public const float StringHalfWidth = .123f, StringHalfHeight = .165f, BallRadius = .0335f;
        public static TennisHit Evaluate(float swingAge, Vector2 faceOffset, float balance, float reachQuality, float power, float stamina)
        {
            float radial = new Vector2(faceOffset.x / StringHalfWidth, faceOffset.y / StringHalfHeight).magnitude;
            float timing = Mathf.Clamp01(1 - Mathf.Abs(swingAge - SweetTime) / TimingWindow);
            bool contact = new Vector2(faceOffset.x / (StringHalfWidth + BallRadius), faceOffset.y / (StringHalfHeight + BallRadius)).magnitude <= 1 && timing > 0;
            float center = Mathf.Clamp01(1 - radial);
            float positioning = Mathf.Clamp01(balance) * Mathf.Clamp01(reachQuality);
            float quality = contact ? timing * .35f + center * .40f + positioning * .25f : 0;
            return new TennisHit {
                Contact = contact, Timing = timing, Center = center, Positioning = positioning, Quality = quality,
                Speed = contact ? Mathf.Lerp(9, 29, Mathf.Clamp01(power)) * Mathf.Lerp(.55f, 1, quality) * Mathf.Lerp(.8f, 1, Mathf.Clamp01(stamina)) : 0,
                ErrorDegrees = contact ? Mathf.Lerp(1.0f, 19f, 1 - quality) + (1 - Mathf.Clamp01(stamina)) * 5 : 0,
                Label = !contact ? "MISS" : quality > .86f ? "SWEET SPOT" : quality > .63f ? "CLEAN HIT" : radial > .72f ? "OFF CENTER" : timing < .45f ? "MISTIMED" : "OFF BALANCE"
            };
        }

        public static float StaminaStep(float value, float speed, float dt)
        {
            float exertion = Mathf.Pow(Mathf.Clamp01(Mathf.Abs(speed) / 7.2f), 2);
            return Mathf.Clamp01(value + (Mathf.Abs(speed) < .15f ? .13f : -.20f * exertion) * dt);
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
