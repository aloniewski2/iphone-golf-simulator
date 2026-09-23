using UnityEngine;

namespace GolfArcade.Tennis
{
    /// The server's ritual before the toss, as players actually do it: the ball in the non-
    /// racket hand at waist height, pushed down to bounce off the court and caught at the top
    /// of its rebound (twice), then a smooth wind-up that lifts the arm straight up in front
    /// of the body, letting go around shoulder height and carrying on to full extension,
    /// pointing at the ball, until the racket arm swings.
    ///
    /// Pure timing and geometry in the server's own frame; the game moves the ball and the
    /// actor reaches its tossing hand to the palm point this returns.
    public static class TennisServeRoutine
    {
        public const int Bounces = 2;
        const float Push = .06f, PushDepth = .08f, BounceSpeed = 3.5f, Restitution = .7f, Reset = .18f;
        public const float WindUp = .24f, Gather = .12f, Extend = .2f;
        const float Gravity = 9.81f;

        /// Tossing-hand side in the server's frame: a right-hander tosses with the left.
        static float Side(TennisActor a) => a.LeftHanded ? 1 : -1;
        public static Vector3 Hold(TennisActor a) => a.transform.TransformPoint(new Vector3(Side(a) * .26f, .92f, .34f));
        public static Vector3 Release(TennisActor a) => a.transform.TransformPoint(new Vector3(Side(a) * .22f, 1.38f, .26f));
        public static Vector3 Extended(TennisActor a) => a.transform.TransformPoint(new Vector3(Side(a) * .14f, 2.1f, .28f));

        /// Fall, rebound and cycle times for one bounce from the hand.
        static void Timing(float releaseHeight, out float fall, out float rebound, out float rise, out float cycle)
        {
            float drop = Mathf.Max(.1f, releaseHeight - TennisRules.BallRadius);
            fall = (-BounceSpeed + Mathf.Sqrt(BounceSpeed * BounceSpeed + 2 * Gravity * drop)) / Gravity;
            rebound = (BounceSpeed + Gravity * fall) * Restitution;
            rise = rebound / Gravity;
            cycle = Push + fall + rise + Reset;
        }

        /// Where the ball and the tossing palm are `t` seconds into the routine (which lasts
        /// ServeTossDelay; the toss is released at its end).
        public static void Pose(TennisActor a, float t, out Vector3 ball, out Vector3 palm)
        {
            Vector3 hold = Hold(a);
            float ground = a.transform.position.y;
            Timing(hold.y - PushDepth - ground, out float fall, out float rebound, out float rise, out float cycle);
            float windStart = TennisRules.ServeTossDelay - WindUp;
            float settle = Mathf.Max(0, windStart - Gather - Bounces * cycle);
            palm = hold; ball = hold;
            if (t >= windStart)
            {
                float k = Mathf.SmoothStep(0, 1, Mathf.Clamp01((t - windStart) / WindUp));
                palm = ball = Vector3.Lerp(hold, Release(a), k);
                return;
            }
            float b = t - settle;
            int n = Mathf.FloorToInt(b / cycle);
            if (b < 0 || n >= Bounces) return;
            float u = b - n * cycle;
            Vector3 pushed = hold + Vector3.down * PushDepth;
            if (u < Push) { palm = ball = Vector3.Lerp(hold, pushed, u / Push); return; }
            float r = u - Push;
            float height;
            if (r < fall) height = (pushed.y - ground) - BounceSpeed * r - .5f * Gravity * r * r;
            else if (r < fall + rise) { float s = r - fall; height = TennisRules.BallRadius + rebound * s - .5f * Gravity * s * s; }
            else
            {
                // Caught at the top of the rebound; hand and ball come back up to the hold.
                float peak = TennisRules.BallRadius + rebound * rise - .5f * Gravity * rise * rise;
                float k = Mathf.SmoothStep(0, 1, Mathf.Clamp01((r - fall - rise) / Reset));
                palm = ball = Vector3.Lerp(new Vector3(hold.x, ground + peak, hold.z), hold, k);
                return;
            }
            ball = new Vector3(pushed.x, ground + height, pushed.z);
            // The hand rises back after the push, then drops to meet the ball at its top.
            float meet = Mathf.Clamp01((r - (fall + rise - .14f)) / .14f);
            Vector3 waiting = Vector3.Lerp(pushed, hold, Mathf.Clamp01(r / .12f));
            palm = Vector3.Lerp(waiting, ball + Vector3.up * .03f, meet);
        }

        /// The tossing palm `t` seconds after release: rising with the ball to full extension,
        /// then held there pointing at it.
        public static Vector3 TossPalm(TennisActor a, float t) =>
            Vector3.Lerp(Release(a), Extended(a), Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / Extend)));
    }
}
