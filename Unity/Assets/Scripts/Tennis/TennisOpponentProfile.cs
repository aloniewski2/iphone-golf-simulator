using UnityEngine;

namespace GolfArcade.Tennis
{
    /// Everything that makes one opponent play differently from another. The old opponent was
    /// one number that only changed how often it missed; every rival moved, read and chose
    /// shots identically, so the champion felt like the first-round wildcard with better luck.
    /// A profile covers the whole player: legs (reach, speed, reaction), hands (consistency,
    /// unforced errors), head (where it aims, how hard, what spin, whether it wrong-foots you
    /// or hunts your weaker side) and serve.
    ///
    /// `FromDifficulty` interpolates a club player (0) to a touring pro (1) for training's
    /// slider; the campaign's rivals start from their rung on that ladder and add a style.
    [System.Serializable]
    public struct OpponentProfile
    {
        public string Style;
        /// The ladder rung it was built from, 0..1 (legacy difficulty; also the serve bias).
        public float Skill;
        // Legs.
        public float Reach, Speed, Reaction;
        // Hands: misses begin above `Threshold` shot difficulty and climb to `MissScale`;
        // `UnforcedError` is the chance of missing a ball that was not hard at all.
        public float Threshold, MissScale, UnforcedError;
        // Head: target width and depth (0 = safe middle, 1 = within a stride of the lines),
        // pace range (m/s), spin range, and the special shots.
        public float Width, Depth, PaceMin, PaceMax, SpinMin, SpinMax;
        public float LobChance, DropChance, SliceChance;
        /// Chance of hitting behind a player already running the other way.
        public float WrongFoot;
        /// How strongly it steers the ball to the player's weaker side once it has seen it.
        public float Hunt;
        // Serve.
        public float ServeSpeed, SecondServeSpeed, ServeAccuracy, ServeWide, FirstFault, SecondFault;
        // Returning serve: extra reach and a quicker read than in a rally.
        public float ReturnReach, ReturnReaction;

        public static readonly OpponentProfile Club = new OpponentProfile
        {
            Style = "club", Skill = 0,
            Reach = 2.0f, Speed = 4.4f, Reaction = .30f,
            Threshold = .25f, MissScale = .8f, UnforcedError = .07f,
            Width = .05f, Depth = .15f, PaceMin = 14, PaceMax = 21, SpinMin = -.2f, SpinMax = .5f,
            ServeSpeed = 26, SecondServeSpeed = 19, ServeAccuracy = .3f, ServeWide = .25f, FirstFault = .16f, SecondFault = .06f,
            ReturnReach = .2f, ReturnReaction = .3f,
        };

        public static readonly OpponentProfile Pro = new OpponentProfile
        {
            Style = "pro", Skill = 1,
            Reach = 2.3f, Speed = 5.3f, Reaction = .18f,
            Threshold = .5f, MissScale = .52f, UnforcedError = .006f,
            Width = .85f, Depth = .85f, PaceMin = 20, PaceMax = 33, SpinMin = -.4f, SpinMax = .95f,
            WrongFoot = .3f, Hunt = .5f,
            ServeSpeed = 44, SecondServeSpeed = 30, ServeAccuracy = .9f, ServeWide = .4f, FirstFault = .06f, SecondFault = .01f,
            ReturnReach = .45f, ReturnReaction = .1f,
        };

        public static OpponentProfile Lerp(OpponentProfile a, OpponentProfile b, float t)
        {
            t = Mathf.Clamp01(t);
            float L(float x, float y) => Mathf.Lerp(x, y, t);
            return new OpponentProfile
            {
                Style = t < .5f ? a.Style : b.Style, Skill = L(a.Skill, b.Skill),
                Reach = L(a.Reach, b.Reach), Speed = L(a.Speed, b.Speed), Reaction = L(a.Reaction, b.Reaction),
                Threshold = L(a.Threshold, b.Threshold), MissScale = L(a.MissScale, b.MissScale), UnforcedError = L(a.UnforcedError, b.UnforcedError),
                Width = L(a.Width, b.Width), Depth = L(a.Depth, b.Depth), PaceMin = L(a.PaceMin, b.PaceMin), PaceMax = L(a.PaceMax, b.PaceMax),
                SpinMin = L(a.SpinMin, b.SpinMin), SpinMax = L(a.SpinMax, b.SpinMax),
                LobChance = L(a.LobChance, b.LobChance), DropChance = L(a.DropChance, b.DropChance), SliceChance = L(a.SliceChance, b.SliceChance),
                WrongFoot = L(a.WrongFoot, b.WrongFoot), Hunt = L(a.Hunt, b.Hunt),
                ServeSpeed = L(a.ServeSpeed, b.ServeSpeed), SecondServeSpeed = L(a.SecondServeSpeed, b.SecondServeSpeed),
                ServeAccuracy = L(a.ServeAccuracy, b.ServeAccuracy), ServeWide = L(a.ServeWide, b.ServeWide),
                FirstFault = L(a.FirstFault, b.FirstFault), SecondFault = L(a.SecondFault, b.SecondFault),
                ReturnReach = L(a.ReturnReach, b.ReturnReach), ReturnReaction = L(a.ReturnReaction, b.ReturnReaction),
            };
        }

        /// Training's difficulty slider: a club player (0) to a touring pro (1).
        public static OpponentProfile FromDifficulty(float difficulty) => Lerp(Club, Pro, difficulty);

        /// A campaign rival: its rung on the ladder, then its style on top.
        public static OpponentProfile Rival(string style, float rung)
        {
            var p = FromDifficulty(rung); p.Style = style;
            switch (style)
            {
                case "wildcard":        // big swings, little control
                    p.PaceMax += 4; p.PaceMin += 2; p.Width = Mathf.Max(p.Width, .35f); p.UnforcedError += .05f; p.FirstFault += .06f; break;
                case "moonballer":      // high, deep, heavy loops that push you back
                    p.PaceMin = 14; p.PaceMax = 19; p.SpinMin = .6f; p.SpinMax = 1f; p.Depth = 1; p.LobChance = .35f; p.UnforcedError *= .6f; break;
                case "needle":          // angles: pulls you wide, then into the open court
                    p.Width = 1; p.Depth = Mathf.Max(.45f, p.Depth - .2f); p.WrongFoot += .15f; break;
                case "server":          // the serve is the weapon; the rally is ordinary
                    p.ServeSpeed += 8; p.SecondServeSpeed += 4; p.ServeWide = .6f; p.ServeAccuracy = Mathf.Min(1, p.ServeAccuracy + .15f);
                    break;
                case "counter":         // gets everything back and waits for your mistake
                    p.Reach += .1f; p.Speed += .25f; p.Reaction -= .02f; p.UnforcedError *= .5f; p.Threshold += .02f;
                    p.PaceMax -= 4; p.Width = Mathf.Min(p.Width, .5f); break;
                case "hammer":          // pace: rushes you and dares you to block it back
                    p.PaceMin += 5; p.PaceMax += 6; p.Depth = Mathf.Max(p.Depth, .8f); p.UnforcedError += .01f; break;
                case "magician":        // slice, drop shots and changes of rhythm
                    p.SliceChance = .45f; p.DropChance = .18f; p.SpinMin = -.9f; p.WrongFoot += .15f; break;
                case "sniper":          // lives on the lines, and occasionally just over them
                    p.Width = 1; p.Depth = 1; p.PaceMax += 2; p.UnforcedError += .03f; p.Hunt += .2f; break;
                case "icequeen":        // all-court, adapts to you
                    p.Hunt = .75f; p.WrongFoot += .1f; p.Threshold += .02f; p.SliceChance = .2f; break;
                case "boss":            // the wall: almost nothing gets past, nothing comes back easy --
                                        // but a clean, fast ball into the corner can still stretch him
                    p.Reach = 2.4f; p.Speed = 5.4f; p.Reaction = .16f; p.Threshold = .53f; p.MissScale = .5f; p.UnforcedError = .004f;
                    p.Width = .92f; p.Depth = .95f; p.PaceMin = 24; p.PaceMax = 34; p.WrongFoot = .35f; p.Hunt = .7f;
                    p.ServeSpeed = 50; p.SecondServeSpeed = 36; p.ServeAccuracy = 1; p.ServeWide = .5f; p.FirstFault = .04f; p.SecondFault = .005f;
                    p.ReturnReach = .6f; p.ReturnReaction = .05f; p.SliceChance = .15f; p.DropChance = .06f; break;
            }
            p.Reaction = Mathf.Max(.04f, p.Reaction);
            p.UnforcedError = Mathf.Clamp(p.UnforcedError, 0, .3f);
            p.Threshold = Mathf.Clamp(p.Threshold, .1f, .8f);
            return p;
        }
    }
}
