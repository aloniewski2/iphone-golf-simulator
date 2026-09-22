using UnityEngine;

namespace GolfArcade.Tennis
{
    /// Ball flight with spin. One integrator for the simulation AND every prediction (landing
    /// ring, auto-positioning, the opponent's read), so what the game predicts is exactly what
    /// the ball then does.
    ///
    /// Spin used to be cosmetic: topspin and slice changed the animation and nothing else.
    /// Now `spin` runs from -1 (heavy slice/backspin) to +1 (heavy topspin):
    ///   * in flight, the Magnus force pulls a topspin ball down and holds a slice up, so
    ///     topspin dips hard into the court and slice floats;
    ///   * at the bounce, topspin kicks up and forward, slice skids low and slows.
    public static class TennisBall
    {
        /// Downward acceleration per m/s of horizontal speed at full topspin. At a rally pace
        /// of 25 m/s that is about 4 m/s^2 -- roughly 0.4g, in line with measured lift
        /// coefficients for heavily spun tennis balls.
        public const float Magnus = .16f;
        public const float Step = 1f / 120;

        public static Vector3 Acceleration(Vector3 velocity, float spin)
        {
            float horizontal = Mathf.Sqrt(velocity.x * velocity.x + velocity.z * velocity.z);
            return new Vector3(0, -9.81f - Magnus * spin * horizontal, 0);
        }

        public static void Integrate(ref Vector3 position, ref Vector3 velocity, float spin, float dt)
        {
            Vector3 a = Acceleration(velocity, spin);
            position += velocity * dt + a * (.5f * dt * dt);
            velocity += a * dt;
        }

        /// Bounce off the court. `restitution` is the court's flat-ball value.
        public static void Bounce(ref Vector3 velocity, ref float spin, float restitution)
        {
            float s = Mathf.Clamp(spin, -1, 1);
            float vertical = Mathf.Clamp(restitution + (s > 0 ? .10f : .14f) * s, .3f, .95f);
            float along = .94f + (s > 0 ? .05f : .04f) * s;
            velocity = new Vector3(velocity.x * along, -velocity.y * vertical, velocity.z * along);
            spin *= .45f;
        }

        /// Where a ball first reaches the court, and when.
        public static bool Landing(Vector3 position, Vector3 velocity, float spin, out Vector3 landing, out float time)
        {
            landing = position; time = 0;
            for (int i = 0; i < 900; i++)
            {
                Vector3 p = position, v = velocity;
                Integrate(ref p, ref v, spin, Step);
                if (p.y <= TennisRules.BallRadius && v.y < 0)
                {
                    float span = position.y - p.y;
                    float t = Mathf.Abs(span) < .000001f ? 0 : (position.y - TennisRules.BallRadius) / span;
                    landing = Vector3.Lerp(position, p, Mathf.Clamp01(t)); landing.y = TennisRules.BallRadius;
                    time = (i + Mathf.Clamp01(t)) * Step;
                    return true;
                }
                position = p; velocity = v;
            }
            return false;
        }

        /// Launch velocity that lands a spun ball on `target` in the time a flat ball struck
        /// at `speed` would take. Exact, not iterated: the Magnus term here acts vertically
        /// and depends only on horizontal speed, which is constant in flight, so the vertical
        /// motion is plain ballistics under a stronger (topspin) or weaker (slice) gravity.
        public static Vector3 Solve(Vector3 start, Vector3 target, float speed, float spin)
        {
            Vector3 delta = target - start;
            float horizontal = new Vector2(delta.x, delta.z).magnitude;
            float flight = Mathf.Max(.25f, horizontal / Mathf.Max(12, speed));
            float gravity = 9.81f + Magnus * spin * (horizontal / flight);
            return delta / flight + Vector3.up * (.5f * gravity * flight);
        }
    }
}
