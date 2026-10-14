using System;
using System.Linq;
using GolfArcade.Course;
using GolfArcade.Shot;
using GolfArcade.Tennis;
using GolfArcade.UI;
using UnityEngine;

namespace GolfArcade.Game
{
    /// The ball drops in a round of one golfer (UI/HoleOutCard.cs): the hole's result, the round so far and the way
    /// on are one card, and the round goes on from it to the next hole without a scorecard in between (the full
    /// card is for the round's end). Rounds with others, contests and online rounds keep their own cards.
    public sealed partial class GolfGame
    {
        HoleOutCard holeOutCard;
        bool holeOutShown;

        public HoleOutCard HoleOut => holeOutCard;
        public bool HoleOutShowing => holeOutCard && holeOutCard.Showing;

        bool UsesHoleOutCard => networkConfigured || (Match != null && Match.IsSolo && !Match.IsContest && setup?.Mode != PlayMode.Online);
        /// The card the hole-out shows: the golfer who holed (in a multiplayer round, theirs from the host), else this phone's.
        Scorecard HoleOutCardSource => networkConfigured && networkCard != null ? networkCard : Card;

        /// BIRDIE!, PAR, BOGEY, TRIPLE BOGEY …
        static string ScoreWord(int strokes, int par)
        {
            int toPar = strokes - par;
            if (strokes > 1 && toPar == 3) return "TRIPLE BOGEY";
            return Scorecard.ScoreName(strokes, par).ToUpperInvariant();
        }

        HoleOutCard.Data HoleOutData(CourseShot shot)
        {
            int strokes = holeStrokes, par = hole.Par;
            var tiles = new HoleOutCard.Tile[course.Holes.Length];
            int toPar = 0, thru = 0;
            for (int i = 0; i < tiles.Length; i++)
            {
                int? s = i == holeIndex ? strokes : HoleOutCardSource.StrokesOn(i);
                tiles[i] = new HoleOutCard.Tile { Ordinal = i + 1, Par = course.Holes[i].Par, Strokes = s, Current = i == holeIndex };
                if (s is int played) { toPar += played - course.Holes[i].Par; thru++; }
            }
            bool more = holeIndex + 1 < course.Holes.Length;
            double from = shot.Origin.DistanceTo(hole.Pin);
            string holedFrom = club == GolfClub.Putter ? $"{from * 3:F0} FT" : $"{from:F0} YD";
            var data = new HoleOutCard.Data
            {
                Score = ScoreWord(strokes, par),
                Eyebrow = string.IsNullOrEmpty(hole.Name) ? $"HOLE {holeIndex + 1} OF {course.Holes.Length}" : $"HOLE {holeIndex + 1} OF {course.Holes.Length}  ·  {hole.Name.ToUpperInvariant()}",
                Detail = strokes == 1 ? $"PAR {par}  ·  ACE FROM {holedFrom}" : $"PAR {par}  ·  HOLED FROM {holedFrom}",
                Strokes = strokes, Par = par, HoleToPar = strokes - par, ToPar = toPar, Thru = thru,
                Tiles = tiles,
                Emotes = resultEmotes.Select(TennisEmotes.Name).ToArray(),
            };
            string waiting = networkConfigured ? NetworkStillToHoleOut() : null;
            if (waiting != null)
            {
                // others are still playing this hole
                data.NextLabel = "NEXT PLAYER";
                data.NextDetail = $"{waiting.ToUpperInvariant()} TO PLAY  ·  HOLE {holeIndex + 1}";
            }
            else if (more)
            {
                var next = course.Holes[holeIndex + 1];
                data.NextLabel = "NEXT HOLE";
                data.NextDetail = $"{holeIndex + 2}  ·  {(string.IsNullOrEmpty(next.Name) ? "" : next.Name.ToUpperInvariant() + "  ·  ")}PAR {next.Par}  ·  {next.Length:F0} YD";
            }
            else
            {
                data.NextLabel = "SCORECARD";
                data.NextDetail = $"ROUND COMPLETE  ·  {course.Name.ToUpperInvariant()}  ·  {Scorecard.FormatToPar(toPar)}";
            }
            return data;
        }

        /// The card for the hole just holed, in place of the shot's stats panel.
        void ShowHoleOut(CourseShot shot)
        {
            if (!holeOutCard) holeOutCard = HoleOutCard.Create(transform, rig.Camera);
            shotResultPanel?.gameObject.SetActive(false);
            holeOutShown = true;
            var data = HoleOutData(shot);
            holeOutCard.Show(data, ContinueShotResult, PlayResultEmote);
            holeOutCard.SetCountdown(ResultWaitSeconds, ResultWaitSeconds);
        }

        /// How long the result holds before the game goes on by itself (an emote played extends it).
        public float ResultWaitSeconds => networkConfigured ? HoleOutSeconds : LocalResultSeconds;
        /// Local play: the result / emote window between swings is exactly this long, whatever the shot, score or emote.
        public const float LocalResultSeconds = 3f;
        /// For the offscreen captures after the camera's shape was changed: the view of the golfer for that shape, at once.
        public void ReframeResultForTests() { if (holeOutShown) { rig.FrameHoleOut(golfer.transform); rig.SnapNext(); rig.ApplyFrame(); } }
        public float ResultSeconds => stateTime;
        float HoleOutSeconds => Mathf.Max(3f, ResultReactionSeconds, resultHold);

        /// Seconds into the result that the game waits to, once an emote has been played (it plays out first).
        float resultHold;

        /// What the golfer does as the ball drops: an eagle or better and an ace throw their arms up, a birdie pumps a fist,
        /// a par waves, anything worse takes it on the chin.
        string HoleOutReaction(CourseShot shot)
        {
            int toPar = holeStrokes - hole.Par;
            // EMOTE STANDARD: an ace = the victory spin, eagle = jump for joy, birdie = fist pump, par = wave, bogey = slump, worse = an angry stomp
            string move = holeStrokes == 1 ? "Emote_Victory" : toPar <= -2 ? "Emote_JumpJoy" : toPar == -1 ? "Emote_FistPump" : toPar == 0 ? "Wave" : toPar == 1 ? "Emote_Slump" : "Emote_Stomp";
            if (!golfer.HasMove(move)) move = holeStrokes == 1 || toPar <= -2 ? "Cheer" : toPar == -1 ? "FistPump" : toPar == 0 ? "Wave" : "Idle";
            return golfer.HasMove(move) ? move : "Idle";
        }
    }
}
