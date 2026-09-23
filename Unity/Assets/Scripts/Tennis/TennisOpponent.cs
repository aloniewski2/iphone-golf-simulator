using UnityEngine;

namespace GolfArcade.Tennis
{
    /// What the opponent decides to do with a ball, so the choice can be tested without a scene.
    public struct TennisReturn
    {
        public bool Reached;        // false = the ball went past them, point to the player
        public bool Error;          // reached it but put it out or in the net
        public Vector3 Landing;
        public float Speed;
        public string Label;
    }

    /// The rally opponent.
    ///
    /// The previous version was a wall with a coin-flip: it tracked the ball at a fixed speed
    /// and then missed on a flat 24% random roll regardless of what the player had done. That
    /// makes points feel arbitrary — you cannot earn them, and you cannot see why you lost one.
    ///
    /// This version borrows the thing Wii Sports Tennis got right: the interesting decision is
    /// placement and timing, not raw speed. So the opponent
    ///   * anticipates where the ball will land rather than chasing where it is,
    ///   * covers a generous but finite width, so genuinely wide balls win the point outright,
    ///   * errs in proportion to how far it was stretched, not at random, so moving it around
    ///     is the way a player creates openings, and
    ///   * hits into the open court, which forces the player to actually move.
    public static class TennisOpponent
    {
        /// How wide it can cover from where it is standing, and how fast it repositions.
        public const float Reach = 2.25f, Speed = 5.2f;
        /// It commits to the ball a beat late, like a person reading a shot.
        public const float Reaction = .16f;
        /// Error floor when comfortable, plus the part that grows with stretch.
        /// Tuned with the rally simulation (TennisRallyTests): at the default difficulty a
        /// player who mistimes about one ball in eight wins roughly half the points, in
        /// rallies averaging five to seven shots. The old .05 floor left a casual player
        /// winning under a fifth of points, almost all of them lost on their own errors.
        public const float BaseError = .10f, StretchError = .50f;
        /// Extra error per unit of pace on the incoming ball: hitting hard is rewarded.
        public const float PressureError = .06f;
        /// How far into its own half it recovers between shots.
        public const float RestX = 0f;
        /// Default strength, tuned by the rally simulation in the EditMode tests so a
        /// casual player gets long rallies and still wins points by moving the ball around.
        public const float DefaultDifficulty = .45f;

        public struct ServePlan { public Vector3 Landing; public float Speed, Spin; public bool Fault; public string Kind; }

        /// The opponent's serve. Real servers mix wide, body and down-the-T, go for more on
        /// a first serve and take a safer, spinning second serve. `roll`s are the caller's
        /// randomness so the plan stays testable.
        public static ServePlan PlanServe(bool deuceCourt, bool secondServe, float difficulty, float placementRoll, float faultRoll, float paceRoll)
        {
            var centre = TennisRules.ServeTargetCentre(false, deuceCourt);
            float boxHalf = TennisRules.CourtHalfWidth * .5f;
            // -1 = towards the centre line (T), 0 = body, +1 = wide.
            int lane = placementRoll < .38f ? -1 : placementRoll < .62f ? 0 : 1;
            float outward = Mathf.Sign(centre.x);
            float x = centre.x + outward * lane * boxHalf * .62f;
            float depth = -TennisRules.ServiceLine * Mathf.Lerp(.72f, .92f, paceRoll);
            var plan = new ServePlan
            {
                Landing = new Vector3(x, TennisRules.BallRadius, depth),
                Kind = lane < 0 ? "T" : lane > 0 ? "wide" : "body",
                Speed = secondServe ? Mathf.Lerp(20, 26, difficulty) * Mathf.Lerp(.95f, 1.05f, paceRoll)
                                    : Mathf.Lerp(28, 41, difficulty) * Mathf.Lerp(.9f, 1.05f, paceRoll),
                Spin = secondServe ? .8f : .12f,
            };
            float faultChance = secondServe ? Mathf.Lerp(.08f, .02f, difficulty) : Mathf.Lerp(.28f, .12f, difficulty);
            if (faultRoll < faultChance)
            {
                // Long, by a little: the most common real fault, and visibly out.
                plan.Fault = true;
                plan.Landing = new Vector3(x, TennisRules.BallRadius, -TennisRules.ServiceLine - Mathf.Lerp(.3f, 1.1f, faultRoll / Mathf.Max(.001f, faultChance)));
            }
            return plan;
        }

        /// 0 when the ball comes straight to it, 1 when it is at full stretch.
        public static float Stretch(float opponentX, float ballX) =>
            Mathf.Clamp01(Mathf.Abs(ballX - opponentX) / Reach);

        public static bool CanReach(float opponentX, float ballX) =>
            Mathf.Abs(ballX - opponentX) <= Reach;

        /// Chance of putting the return out. Rises with the square of stretch, so comfortable
        /// balls come back and balls that drag it to the edge often do not. `difficulty` runs
        /// 0 (club player) to 1 (very hard).
        public static float ErrorChance(float stretch, float difficulty, float pressure = 0) =>
            Mathf.Clamp01((BaseError + StretchError * stretch * stretch + PressureError * Mathf.Clamp01(pressure))
                * Mathf.Lerp(1.55f, .5f, Mathf.Clamp01(difficulty)));

        /// Where it aims: into the open court, away from the player, with real variety so the
        /// rally does not become a metronome.
        public static Vector3 OpenCourtTarget(float playerX, float widthRoll, float depthRoll)
        {
            float side = playerX >= 0 ? -1 : 1;
            float width = Mathf.Lerp(1.5f, 3.3f, Mathf.Clamp01(widthRoll));
            float depth = Mathf.Lerp(5.2f, 10.4f, Mathf.Clamp01(depthRoll));
            return new Vector3(side * width, TennisRules.BallRadius, -depth);
        }

        /// Full decision for one incoming ball. `roll` and the two variation values are the
        /// caller's randomness, kept as arguments so this stays deterministic under test.
        public static TennisReturn Decide(float opponentX, float ballX, float playerX,
            float difficulty, float roll, float widthRoll, float depthRoll, float pressure = 0)
        {
            var result = new TennisReturn();
            if (!CanReach(opponentX, ballX))
            { result.Label = "WINNER — past the opponent"; return result; }
            result.Reached = true;
            float stretch = Stretch(opponentX, ballX);
            if (roll < ErrorChance(stretch, difficulty, pressure))
            {
                result.Error = true;
                // Stretched wide, it nets or sprays wide; comfortable but wrong, it goes long.
                if (stretch > .55f)
                {
                    result.Landing = new Vector3(Mathf.Sign(ballX) * 5.4f, TennisRules.BallRadius, -6.5f);
                    result.Label = "Opponent sprays it wide";
                }
                else
                {
                    result.Landing = new Vector3(ballX * .4f, TennisRules.BallRadius, 1.0f);
                    result.Label = "Opponent nets it";
                }
                result.Speed = 18.5f;
                return result;
            }
            result.Landing = OpenCourtTarget(playerX, widthRoll, depthRoll);
            // A stretched opponent cannot hit as hard, which is what gives the player the
            // initiative after moving them.
            result.Speed = Mathf.Lerp(27, 16.5f, stretch);
            result.Label = stretch > .65f ? "Opponent scrambles it back" : "Opponent returns";
            return result;
        }

        /// Where it should be standing: on the predicted bounce once it has read the shot,
        /// drifting back toward the middle when nothing is coming.
        public static float Reposition(float current, float predictedX, bool reading, float dt)
        {
            float goal = reading ? Mathf.Clamp(predictedX, -TennisRules.CourtHalfWidth, TennisRules.CourtHalfWidth) : RestX;
            return Mathf.MoveTowards(current, goal, dt * Speed);
        }
    }
}
