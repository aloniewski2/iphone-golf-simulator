using UnityEngine;

namespace GolfArcade.Tennis
{
    /// Runs the match's sound like a broadcast mixer: the music swells between points and
    /// sinks under a rally, the stands murmur and hush for the serve, the crowd reacts to the
    /// point, the umpire calls the score and the commentator jumps on the big moments. The
    /// game only reports what happened (TennisGame calls the On* methods); all the judgement
    /// about what that should sound like lives here.
    public sealed class TennisAudioDirector : MonoBehaviour
    {
        TennisGame game;
        public TennisMusic Music { get; private set; }
        public TennisAmbience Ambience { get; private set; }
        public TennisAnnouncer Announcer { get; private set; }
        bool matchPoint, welcomed;
        int pointsThisMatch, lastRally;

        public static TennisAudioDirector Create(TennisGame game)
        {
            var d = new GameObject("Tennis audio").AddComponent<TennisAudioDirector>();
            d.transform.SetParent(game.transform, false);
            d.game = game;
            d.Music = TennisMusic.Create(d.transform);
            d.Ambience = TennisAmbience.Create(d.transform);
            d.Announcer = TennisAnnouncer.Create(d.transform);
            return d;
        }

        /// A new point is about to be served.
        public void OnPointStarting(TennisMatch match, bool isMatchPoint)
        {
            Announcer.Clear();
            matchPoint = isMatchPoint;
            lastRally = 0;
            if (match.PlayerGames + match.OpponentGames == 0 && match.PlayerPoints + match.OpponentPoints == 0)
            {
                pointsThisMatch = 0;
                if (!welcomed) { Announcer.Say("welcome", .8f, 4); welcomed = true; Announcer.Say("play", 1.2f, 6); }
                else Announcer.Say("play", .9f, 3);
                return;
            }
            if (isMatchPoint) { Announcer.Say("match_point", .6f, 3); Announcer.Say("quiet_please", .8f, 4); return; }
            // Game point for the server, break point for the receiver.
            int server = match.PlayerServes ? match.PlayerPoints : match.OpponentPoints;
            int receiver = match.PlayerServes ? match.OpponentPoints : match.PlayerPoints;
            if (server >= 3 && server > receiver) Announcer.Say("game_point", .7f, 3);
            else if (receiver >= 3 && receiver > server) Announcer.Say("break_point", .7f, 3);
        }

        public void OnFault(bool doubleFault)
        {
            Announcer.Say(doubleFault ? "double_fault" : "fault", .05f, 1.5f);
            if (doubleFault) Ambience.Groan(.5f);
        }

        /// The point is over. `call` is the one-word reason the HUD shows (ACE, OUT, NET,
        /// WINNER, DOUBLE FAULT, MISSED, POINT).
        public void OnPointOver(TennisMatch match, bool toPlayer, string call, int rally, bool gameWon, bool smash)
        {
            pointsThisMatch++;
            bool winner = call == "WINNER", ace = call == "ACE";
            bool longRally = rally >= 8;
            float excitement = Mathf.Clamp01(rally / 10f + (winner || ace ? .35f : 0) + (match.Complete ? .5f : 0));
            // The crowd: the home player's points are cheered, the opponent's get polite
            // applause; near things get an "ooh".
            if (toPlayer) Ambience.Cheer(.4f + excitement * .6f);
            else if (call == "OUT" || call == "NET") Ambience.Ooh(.6f + excitement * .4f);
            else if (longRally) Ambience.Groan(.7f);
            else Ambience.Cheer(.2f + excitement * .3f);

            if (match.Complete)
            {
                Music.Play(match.PlayerWonMatch ? TennisMusic.Sting.MatchWon : TennisMusic.Sting.MatchLost, .9f);
                Announcer.Say("game_set_match", .5f, 3);
                Announcer.Say(match.PlayerWonMatch ? "you_win" : "you_lose", .6f, 6);
                return;
            }
            if (toPlayer) Music.Play(winner || ace ? TennisMusic.Sting.Winner : gameWon ? TennisMusic.Sting.Game : TennisMusic.Sting.Point, winner || ace ? .8f : .55f);
            else if (gameWon) Music.Play(TennisMusic.Sting.Game, .35f);

            // The commentator first, then the umpire's score.
            float delay = .25f;
            string hype = ace ? "ace" : smash && toPlayer ? "smash" : winner ? "winner" : longRally ? "rally"
                        : call == "OUT" && !toPlayer ? "close" : null;
            if (call == "OUT" && hype == null) { Announcer.Say("out", 0, 1.2f); delay = .5f; }
            if (hype != null) { Announcer.Say(hype, delay, 1.5f); delay += .9f; }
            if (gameWon) Announcer.Say("game", delay, 2.5f);
            else Announcer.Say(TennisAnnouncer.ScoreKey(match), delay, 2.5f);
        }

        /// A footstep from either player.
        public void OnFootstep(float speed, bool player) => Ambience.Step(speed, player);

        void Update()
        {
            if (!game) return;
            var flow = game.Flow;
            bool serving = game.Serving, rally = flow == TennisGame.Phase.Rally;
            if (rally && game.RallyShots >= 6 && game.RallyShots != lastRally && game.RallyShots % 4 == 2)
            {
                // A rally that keeps going draws the crowd in.
                Ambience.Ooh(.35f);
                lastRally = game.RallyShots;
            }
            // Music: full between points, the lead drops for the serve, a low groove under play.
            if (matchPoint && (serving || rally)) Music.SetMix(.12f, .45f, 0);
            else if (rally) Music.SetMix(.32f, .22f, 0);
            else if (serving) Music.SetMix(.62f, .55f, .12f);
            else Music.SetMix(.9f, .8f, .85f);
            Music.Duck(Announcer.Speaking ? .45f : 1);
            // The stands chat between points and go quiet for play.
            Ambience.Hush(serving ? (matchPoint ? .95f : .7f) : rally ? .6f : 0);
        }
    }
}
