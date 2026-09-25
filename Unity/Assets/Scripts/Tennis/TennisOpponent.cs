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
        /// Topspin (+) or slice (-) the return is struck with.
        public float Spin;
        public string Label;
        /// Why it missed, for the call on the TV ("STRETCHED WIDE"); null when it did not.
        public string Reason;
        /// How hard the ball was to handle, 0 (sitting up) .. 1 (barely playable).
        public float Difficulty;
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
        public const float Reach = 2.25f, Speed = 4.8f;
        /// It commits to each ball a beat late, like a person reading a shot off the racket.
        public const float Reaction = .28f;
        /// It never gifts a point. It errs only when the ball is genuinely hard to play: at
        /// full stretch, rushed by pace, dug out low or above the shoulder, or struck so well
        /// it cannot be controlled. Below a threshold (higher for a stronger opponent) every
        /// ball comes back; above it, the chance climbs smoothly. Tuned with the rally
        /// simulation (TennisPolishTests): at the default difficulty a casual player gets
        /// rallies of eight or so shots and wins points by moving the ball around.
        public const float EasyThreshold = .25f, HardThreshold = .46f;
        /// How far into its own half it recovers between shots.
        public const float RestX = 0f;
        /// Default strength, tuned by the rally simulation in the EditMode tests so a
        /// casual player gets long rallies and still wins points by moving the ball around.
        public const float DefaultDifficulty = .45f;

        public struct ServePlan { public Vector3 Landing; public float Speed, Spin; public bool Fault; public string Kind; }

        /// The opponent's serve. Real servers mix wide, body and down-the-T, go for more on
        /// a first serve and take a safer, spinning second serve. `roll`s are the caller's
        /// randomness so the plan stays testable.
        public static ServePlan PlanServe(bool deuceCourt, bool secondServe, float difficulty, float placementRoll, float faultRoll, float paceRoll) =>
            PlanServe(deuceCourt, secondServe, OpponentProfile.FromDifficulty(difficulty), placementRoll, faultRoll, paceRoll);

        public static ServePlan PlanServe(bool deuceCourt, bool secondServe, OpponentProfile p, float placementRoll, float faultRoll, float paceRoll)
        {
            var centre = TennisRules.ServeTargetCentre(false, deuceCourt);
            float boxHalf = TennisRules.CourtHalfWidth * .5f;
            // -1 = towards the centre line (T), 0 = body, +1 = wide. A big server goes wide
            // more; a second serve goes to the body more.
            float wide = secondServe ? p.ServeWide * .6f : p.ServeWide;
            float t = (1 - wide) * (secondServe ? .45f : .62f);
            int lane = placementRoll < t ? -1 : placementRoll < 1 - wide ? 0 : 1;
            float outward = Mathf.Sign(centre.x);
            // Never the same spot twice: a spread within each lane, tighter for an accurate server.
            float within = Mathf.Repeat(placementRoll * 7.31f, 1f) * 2 - 1;
            float spread = Mathf.Lerp(.32f, .1f, p.ServeAccuracy);
            float x = centre.x + outward * (lane * Mathf.Lerp(.5f, .78f, p.ServeAccuracy) + within * spread) * boxHalf;
            float depth = -TennisRules.ServiceLine * Mathf.Lerp(Mathf.Lerp(.62f, .8f, p.ServeAccuracy), .93f, paceRoll);
            var plan = new ServePlan
            {
                Landing = new Vector3(x, TennisRules.BallRadius, depth),
                Kind = lane < 0 ? "T" : lane > 0 ? "wide" : "body",
                Speed = (secondServe ? p.SecondServeSpeed : p.ServeSpeed) * Mathf.Lerp(.93f, 1.04f, paceRoll),
                Spin = secondServe ? .8f : .12f,
            };
            float faultChance = secondServe ? p.SecondFault : p.FirstFault;
            if (faultRoll < faultChance)
            {
                // Long, by a little: the most common real fault, and visibly out.
                plan.Fault = true;
                plan.Landing = new Vector3(x, TennisRules.BallRadius, -TennisRules.ServiceLine - Mathf.Lerp(.3f, 1.1f, faultRoll / Mathf.Max(.001f, faultChance)));
            }
            return plan;
        }

        /// 0 when the ball comes straight to it, 1 when it is at full stretch.
        public static float Stretch(float opponentX, float ballX, float reach = Reach) =>
            Mathf.Clamp01(Mathf.Abs(ballX - opponentX) / reach);

        public static bool CanReach(float opponentX, float ballX, float reach = Reach) =>
            Mathf.Abs(ballX - opponentX) <= reach;

        /// How hard a ball is to return, 0..1, from what makes a real ball hard: `stretch`
        /// (0 at the body .. 1 at full reach), `pace` (0 slow .. 1 very fast), `height`
        /// (0 comfortable waist height .. 1 at the shoe-tops or over the head) and `quality`
        /// (how cleanly the player struck it). Each takes away a share of the ease.
        public static float ShotDifficulty(float stretch, float pace, float height = 0, float quality = 0)
        {
            float s = Mathf.Clamp01(stretch), q = Mathf.Clamp01(quality);
            float ease = (1 - .92f * s * s) * (1 - .38f * Mathf.Clamp01(pace)) * (1 - .35f * Mathf.Clamp01(height)) * (1 - .3f * q * q);
            return Mathf.Clamp01(1 - ease);
        }

        /// Chance of missing a ball this hard. Zero below the threshold, so comfortable balls
        /// always come back, then a smooth climb. `difficulty` runs 0 (club player) to 1.
        public static float MissChance(float shotDifficulty, float difficulty) =>
            MissChance(shotDifficulty, OpponentProfile.FromDifficulty(difficulty));

        public static float MissChance(float shotDifficulty, OpponentProfile p)
        {
            float x = Mathf.Clamp01((shotDifficulty - p.Threshold) / (1 - p.Threshold));
            float s = x * x * (3 - 2 * x);
            return s * Mathf.Sqrt(s) * p.MissScale;
        }

        /// Chance of putting the return out, from stretch and the rest of the ball's threat.
        public static float ErrorChance(float stretch, float difficulty, float pace = 0, float height = 0, float quality = 0) =>
            MissChance(ShotDifficulty(stretch, pace, height, quality), difficulty);

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
        /// `pace` is the incoming ball's speed (0..1), `height` how far from a comfortable
        /// contact height it will be met (-1 at the shoe-tops .. +1 over the head) and
        /// `quality` how well the player hit it (or served it).
        public static TennisReturn Decide(float opponentX, float ballX, float playerX,
            float difficulty, float roll, float widthRoll, float depthRoll, float pace = 0, float reach = Reach,
            float height = 0, float quality = 0) =>
            Decide(opponentX, ballX, playerX, 0, 0, OpponentProfile.FromDifficulty(difficulty), roll, widthRoll, depthRoll, pace, reach, height, quality);

        /// `playerVX` is how fast the player is moving across the court (for wrong-footing),
        /// `weakSide` the side of the court (-1/+1, 0 unknown) where the player has been missing.
        public static TennisReturn Decide(float opponentX, float ballX, float playerX, float playerVX, float weakSide,
            OpponentProfile p, float roll, float widthRoll, float depthRoll, float pace, float reach,
            float height = 0, float quality = 0)
        {
            var result = new TennisReturn();
            if (!CanReach(opponentX, ballX, reach))
            { result.Label = "WINNER — past the opponent"; result.Difficulty = 1; return result; }
            result.Reached = true;
            float stretch = Stretch(opponentX, ballX, reach);
            float hard = ShotDifficulty(stretch, pace, Mathf.Abs(height), quality);
            result.Difficulty = hard;
            // Forced errors from how hard the ball was, plus the unforced ones a weaker player
            // makes on balls that were not hard at all.
            float chance = MissChance(hard, p) + p.UnforcedError * (1 - hard);
            if (roll < chance)
            {
                result.Error = true;
                // The miss follows from what made the ball hard, the way a real one does.
                float stretchPart = .92f * stretch * stretch, pacePart = .38f * Mathf.Clamp01(pace),
                      heightPart = .35f * Mathf.Clamp01(Mathf.Abs(height)), qualityPart = .3f * quality * quality;
                // Where within the miss: the roll is already below `chance`, so rescale it.
                float within = Mathf.Clamp01(roll / Mathf.Max(.0001f, chance));
                float side = Mathf.Abs(ballX) > .05f ? Mathf.Sign(ballX) : (widthRoll < .5f ? -1 : 1);
                if (hard < .25f)
                {
                    // Nothing forced it: an unforced error, long or into the tape.
                    if (within < .5f) { result.Landing = new Vector3(ballX * .4f, TennisRules.BallRadius, 1.0f); result.Label = "Opponent nets it"; result.Reason = "UNFORCED ERROR"; }
                    else { result.Landing = new Vector3(Mathf.Lerp(-2.5f, 2.5f, widthRoll), TennisRules.BallRadius, -TennisRules.CourtHalfLength - Mathf.Lerp(.3f, 1.2f, within)); result.Label = "Opponent hits it long"; result.Reason = "UNFORCED ERROR"; }
                }
                else if (stretchPart >= Mathf.Max(pacePart, heightPart, qualityPart))
                {
                    if (stretch > .85f && within < .5f)
                    {
                        // Reaching back for a ball already past it: lunges and nets.
                        result.Landing = new Vector3(ballX * .4f, TennisRules.BallRadius, 1.0f);
                        result.Label = "Opponent nets it"; result.Reason = "LUNGING — INTO THE NET";
                    }
                    else
                    {
                        // On the run at full reach: the racket face opens and it sails wide.
                        result.Landing = new Vector3(side * Mathf.Lerp(4.45f, 5.2f, within), TennisRules.BallRadius, -Mathf.Lerp(6.5f, 10f, depthRoll));
                        result.Label = "Opponent sprays it wide"; result.Reason = "STRETCHED WIDE";
                    }
                }
                else if (heightPart >= Mathf.Max(pacePart, qualityPart))
                {
                    if (height < 0)
                    {
                        result.Landing = new Vector3(ballX * .4f, TennisRules.BallRadius, 1.0f);
                        result.Label = "Opponent nets it"; result.Reason = "DUG OUT LOW — INTO THE NET";
                    }
                    else
                    {
                        result.Landing = new Vector3(Mathf.Lerp(-2, 2, widthRoll), TennisRules.BallRadius, -TennisRules.CourtHalfLength - Mathf.Lerp(.35f, 1.4f, within));
                        result.Label = "Opponent hits it long"; result.Reason = "TOO HIGH TO CONTROL";
                    }
                }
                else if (within < .55f)
                {
                    // Late on a fast or heavy ball: the racket arrives behind it and nets.
                    result.Landing = new Vector3(ballX * .4f, TennisRules.BallRadius, 1.0f);
                    result.Label = "Opponent nets it";
                    result.Reason = pacePart >= qualityPart ? "RUSHED BY THE PACE" : "OVERPOWERED";
                }
                else
                {
                    result.Landing = new Vector3(Mathf.Lerp(-2, 2, widthRoll), TennisRules.BallRadius, -TennisRules.CourtHalfLength - Mathf.Lerp(.35f, 1.4f, within));
                    result.Label = "Opponent hits it long";
                    result.Reason = pacePart >= qualityPart ? "LATE ON THE PACE" : "OVERPOWERED";
                }
                result.Speed = 18.5f;
                return result;
            }
            // Under pressure it plays safe: a slower ball, back toward the middle and shorter,
            // which is the opening a player earns by moving it around.
            float defend = Mathf.Clamp01((hard - .3f) / .5f);
            // Which side: the open court by default; a smart player hits behind a runner or
            // keeps feeding the side that has been breaking down.
            float sideRoll = Mathf.Repeat(widthRoll * 5.31f + depthRoll * 2.17f, 1f);
            float targetSide = playerX >= 0 ? -1 : 1;
            if (sideRoll < p.WrongFoot * (1 - defend) && Mathf.Abs(playerVX) > 1.2f) targetSide = -Mathf.Sign(playerVX);
            else if (weakSide != 0 && Mathf.Repeat(sideRoll * 3.7f, 1f) < p.Hunt * .6f * (1 - defend)) targetSide = Mathf.Sign(weakSide);
            float width = Mathf.Lerp(Mathf.Lerp(.9f, 2.6f, p.Width), Mathf.Lerp(1.8f, 3.8f, p.Width), widthRoll) * (1 - .6f * defend);
            float depth = Mathf.Lerp(Mathf.Lerp(5.0f, 8.6f, p.Depth), Mathf.Lerp(8.0f, 11.1f, p.Depth), depthRoll) * (1 - .4f * defend);
            float speed = Mathf.Lerp(p.PaceMax, p.PaceMin, Mathf.Max(stretch, defend));
            float spin = Mathf.Lerp(p.SpinMin, p.SpinMax, Mathf.Repeat(widthRoll * 3.1f, 1f));
            float special = Mathf.Repeat(depthRoll * 6.73f, 1f);
            result.Label = defend > .6f ? "Opponent scrambles it back" : "Opponent returns";
            if (special < p.LobChance)
            {
                // A high, heavy loop to the baseline.
                depth = Mathf.Lerp(10.2f, 11.1f, depthRoll); speed = Mathf.Lerp(15, 17.5f, widthRoll); spin = .95f;
                result.Label = "Opponent loops it deep";
            }
            else if (special < p.LobChance + p.DropChance && defend < .3f)
            {
                // A drop shot, just over the net.
                depth = Mathf.Lerp(2.6f, 3.8f, depthRoll); width *= .7f; speed = Mathf.Lerp(10.5f, 12.5f, widthRoll); spin = -.8f;
                result.Label = "Opponent drops it short";
            }
            else if (special < p.LobChance + p.DropChance + p.SliceChance) spin = Mathf.Lerp(-.9f, -.45f, widthRoll);
            result.Landing = new Vector3(targetSide * Mathf.Min(width, 3.95f), TennisRules.BallRadius, -Mathf.Clamp(depth, 2.4f, 11.3f));
            result.Speed = speed;
            result.Spin = spin;
            return result;
        }

        /// Where it should be standing: on the predicted bounce once it has read the shot,
        /// drifting back toward the middle when nothing is coming.
        public static float Reposition(float current, float predictedX, bool reading, float dt) =>
            Reposition(current, predictedX, reading, dt, Speed);

        public static float Reposition(float current, float predictedX, bool reading, float dt, float speed)
        {
            float goal = reading ? Mathf.Clamp(predictedX, -TennisRules.CourtHalfWidth - .8f, TennisRules.CourtHalfWidth + .8f) : RestX;
            return Mathf.MoveTowards(current, goal, dt * speed);
        }
    }
}
