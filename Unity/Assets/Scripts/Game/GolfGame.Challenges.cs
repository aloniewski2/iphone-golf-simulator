using System;
using System.Collections.Generic;
using GolfArcade.Course;
using GolfArcade.Shot;
using UnityEngine;

namespace GolfArcade.Game
{
    /// What there is to chase on a round (the match settings' "Bounties & rings"): golden rings
    /// standing over each tee shot that pay coins when the ball flies through them (a sliding glide
    /// ring pays more, the rainbow ring the clearing sky leaves after rain the most, all of a
    /// hole's rings in one shot a bonus), and a bounty on every hole — bonk the balloon, par the
    /// storm, find the fairway — that pays its coins the moment it's done. The round's coins are
    /// counted by the shot map, its bounties on the card after each hole, and the round's tally
    /// goes to the app (RoundSummary) for the player's XP. Solo rounds; a multiplayer round has none.
    public sealed partial class GolfGame
    {
        bool challengesWanted = true;
        RingView ringView;
        Bounty bounty;
        bool[] ringsTaken = Array.Empty<bool>();
        List<CourseRings.Pass> ringPasses = new();
        int ringPassesShown;
        /// The round's tally: coins, rings flown through, bounties done (of holes with one), holes
        /// played in rough weather.
        int roundCoins, roundRings, roundBounties, roundBountyHoles, roughHoles;
        bool rainbowLaid;
        /// The day's forecast round (yyyymmdd), set by the app; 0 an ordinary round.
        public int DailyRound { get; private set; }

        /// The round's coins, rings and bounties so far (for the HUD's tests and the app).
        public int RoundCoins => roundCoins;
        public int RoundRings => roundRings;
        public int RoundBounties => roundBounties;
        public Bounty HoleBounty => bounty;
        public RingView RingsShown => ringView;

        /// What a round ended with, as "key=value;…" (toPar, strokes, holes, birdies, coins,
        /// rings, bounties…): the app turns it into XP.
        public static event Action<string> RoundSummary;

        /// Twists, weather with things moving about, and rings with bounties, for the rounds started from now on.
        public void SetRoundEvents(bool twists, bool world, bool challenges) { twistsWanted = twists; worldWanted = world; challengesWanted = challenges; }

        /// The next round started here is the day's forecast round: the date is its seed (the same
        /// round for everyone that day) and everything is on.
        public void SetDailyRound(int date) { DailyRound = Math.Max(0, date); if (DailyRound > 0) SetRoundEvents(true, true, true); }

        void ResetChallenges()
        {
            roundCoins = roundRings = roundBounties = roundBountyHoles = roughHoles = 0;
        }

        /// The hole's rings and bounty, once what moves about it is placed.
        void StartHoleChallenges(int index)
        {
            bounty = null; ringPasses.Clear(); ringPassesShown = 0; rainbowLaid = false;
            var rings = events.RingsFor(index, hole, hole.Rules, HoleView.GroundHeight);
            if (rings.Length > 0)
            {
                bool own = hole.Movers == null;
                hole.Movers ??= new CourseMovers(Array.Empty<Mover>());
                hole.Movers.Rings = rings;
                ringView = RingView.Build(holeView.transform, hole.Movers, drivesClock: own);
            }
            else ringView = null;
            ringsTaken = new bool[rings.Length];
            MarkRings();
            bounty = events.BountyFor(index, hole, weather, hole.Movers, rings);
            if (bounty != null) roundBountyHoles++;
            if (events.World && weather.IsRough()) roughHoles++;
            ShowBounty();
            hud.SetCoins(events.Challenges ? roundCoins : null);
        }

        /// The rings still standing, on the shot map.
        void MarkRings()
        {
            var plan = hud.Map;
            plan.Rings.Clear();
            var rings = hole?.Movers?.Rings;
            // (a ring the ball in the air is still to reach is still standing)
            var ahead = new HashSet<int>();
            for (int k = ringPassesShown; k < ringPasses.Count; k++) ahead.Add(ringPasses[k].Ring);
            if (rings != null)
                for (int i = 0; i < rings.Length; i++)
                    if (i >= ringsTaken.Length || !ringsTaken[i] || ahead.Contains(i)) plan.Rings.Add(new Vector3((float)rings[i].X, (float)rings[i].Y, (float)rings[i].D));
            plan.Changed();
        }

        void ShowBounty()
        {
            if (bounty == null) { hud.SetBounty(null, 0, 0, null); return; }
            hud.SetBounty(bounty.Title, bounty.Coins, (int)bounty.State, bounty.Icon);
        }

        /// The shot just struck: the rings it will fly through (counted now, shown as it gets there).
        void PlanRingPasses()
        {
            ringPasses.Clear(); ringPassesShown = 0;
            var rings = hole.Movers?.Rings;
            if (rings == null || rings.Length == 0 || LastShot == null) return;
            ringPasses = CourseRings.Through(LastShot, rings, hole.Movers.PhaseAtLaunch, HoleView.GroundHeight, ringsTaken);
            foreach (var p in ringPasses) ringsTaken[p.Ring] = true;
        }

        /// Every frame of the flight: the rings it has got to, each paid out as the ball goes through.
        void ShowRingPasses(bool all = false)
        {
            var rings = hole.Movers?.Rings;
            while (rings != null && ringPassesShown < ringPasses.Count && (all || flightTime >= ringPasses[ringPassesShown].Time))
            {
                var pass = ringPasses[ringPassesShown++];
                var r = rings[pass.Ring];
                roundCoins += r.Coins; roundRings++;
                var (x, y, d) = r.At(hole.Movers.PhaseAtLaunch + pass.Time);
                var at = new Vector3((float)x, (float)y, (float)d);
                ringView?.Take(pass.Ring);
                MarkRings();
                var colour = r.Kind == RingKind.Glide ? RingView.Glide : r.Kind == RingKind.Rainbow ? new Color(1f, 0.5f, 0.85f) : RingView.Gold;
                effects.Burst(at, colour, new Color(1f, 0.95f, 0.6f), 40, 5f, 0.45f, 1.0f, 0.3f);
                bool sweep = ringPassesShown == ringPasses.Count && Array.TrueForAll(ringsTaken, t => t) && ringPasses.Count == rings.Length && rings.Length >= 3;
                if (sweep) roundCoins += 50;
                eventSounds?.Chime(r.Kind != RingKind.Gold || sweep);
                Haptics.Tick();
                string word = sweep ? "Hat trick!  +50" : r.Kind switch { RingKind.Rainbow => $"Rainbow!  +{r.Coins}", RingKind.Glide => $"Nice!  +{r.Coins}", _ => $"+{r.Coins}" };
                hud.ShowCallout(word, sweep ? new Color(1f, 0.55f, 0.85f) : colour);
                hud.SetCoins(roundCoins);
            }
        }

        /// The shot has finished (its penalty counted): what it did for the bounty.
        void ChallengeShot(CourseShot shot)
        {
            ShowRingPasses(all: true);
            if (bounty == null || bounty.State != BountyState.Open) return;
            bool balloon = false, gull = false, cart = false;
            foreach (var k in shot.Knocks)
            {
                if (k.Kind == ObstacleKind.Balloon) balloon = true;
                else if (k.Kind == ObstacleKind.Gull) gull = true;
                else if (k.Kind == ObstacleKind.Cart) cart = true;
            }
            var facts = new BountyShot
            {
                Stroke = holeStrokes, Par = hole.Par,
                Putt = shot.Club == GolfClub.Putter, Holed = shot.IsHoled,
                FromPuttingSurface = hole.LieAt(shot.Origin).IsPuttingSurface(),
                FromPinYards = shot.Origin.DistanceTo(hole.Pin), RestPinYards = shot.Rest.DistanceTo(hole.Pin), Total = shot.Total,
                Rest = shot.Lie, Rings = ringPasses.Count, Balloon = balloon, Gull = gull, Cart = cart,
            };
            if (bounty.Shot(facts))
            {
                roundCoins += bounty.Coins; roundBounties++;
                eventSounds?.Jingle();
                Haptics.Impact(0.5);
                hud.ShowCallout($"Bounty!  +{bounty.Coins}", GolfArcade.UI.GolfShotHud.Gold);
                hud.SetCoins(roundCoins);
            }
            ShowBounty();
        }

        /// The hole's done: a bounty not done by now is missed.
        void CloseHoleChallenges()
        {
            if (bounty == null) return;
            bounty.Close();
            ShowBounty();
        }

        /// The skies have cleared after rain: a rainbow ring, halfway along the next shot.
        void LayRainbowRing()
        {
            if (!events.Challenges || rainbowLaid || hole == null) return;
            var target = hole.LieAt(ballAt).IsPuttingSurface() ? hole.Pin : hole.RecommendedTarget(ballAt);
            var ring = RoundEvents.RainbowRing(hole, ballAt, target, hole.Rules, HoleView.GroundHeight);
            if (ring == null) return;
            rainbowLaid = true;
            bool own = hole.Movers == null;
            hole.Movers ??= new CourseMovers(Array.Empty<Mover>());
            var list = new List<Ring>(hole.Movers.Rings ?? Array.Empty<Ring>()) { ring };
            hole.Movers.Rings = list.ToArray();
            var taken = new bool[list.Count]; Array.Copy(ringsTaken, taken, Math.Min(ringsTaken.Length, taken.Length)); ringsTaken = taken;
            if (ringView) ringView.Refresh();
            else ringView = RingView.Build(holeView.transform, hole.Movers, drivesClock: own);
            MarkRings();
        }

        /// The round is over: its tally to the app, for XP.
        void SummariseRound()
        {
            if (Match == null || !Match.IsSolo || Card == null) return;
            int birdies = 0, eagles = 0, pars = 0, aces = 0, strokes = 0, par = 0;
            for (int i = 0; i < course.Holes.Length; i++)
            {
                int s = Card.StrokesOn(i) ?? 0, p = course.Holes[i].Par;
                strokes += s; par += p;
                if (s == 1) aces++; else if (s - p <= -2) eagles++; else if (s - p == -1) birdies++; else if (s == p) pars++;
            }
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            string line = string.Join(";",
                "toPar=" + (strokes - par).ToString(inv), "strokes=" + strokes.ToString(inv), "par=" + par.ToString(inv),
                "holes=" + course.Holes.Length.ToString(inv), "aces=" + aces.ToString(inv), "eagles=" + eagles.ToString(inv),
                "birdies=" + birdies.ToString(inv), "pars=" + pars.ToString(inv),
                "coins=" + roundCoins.ToString(inv), "rings=" + roundRings.ToString(inv),
                "bounties=" + roundBounties.ToString(inv), "bountyHoles=" + roundBountyHoles.ToString(inv),
                "rough=" + roughHoles.ToString(inv), "twists=" + events.Dealt.Length.ToString(inv),
                "daily=" + DailyRound.ToString(inv), "course=" + course.Key);
            RoundSummary?.Invoke(line);
        }
    }
}
