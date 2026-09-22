using UnityEngine;

namespace GolfArcade.Tennis
{
    /// Real tennis scoring for a single set decided as best-of-five games, so the first
    /// player to three games wins the match.
    ///
    /// Deliberately pure: no Unity objects, no time, no randomness. Every rule here is
    /// exercised by EditMode tests, which is the only way to be confident about deuce,
    /// advantage and service rotation without playing hundreds of points by hand.
    public struct TennisMatch
    {
        public const int GamesToWin = 3, SetGames = 5;

        public int PlayerPoints, OpponentPoints;
        public int PlayerGames, OpponentGames;
        /// True while the player is the server. One player serves a whole game, as in
        /// real tennis, and the serve changes hands when the game does.
        public bool PlayerServes;
        public bool Complete;
        public bool PlayerWonMatch;

        public static TennisMatch New(bool playerServesFirst = true) =>
            new TennisMatch { PlayerServes = playerServesFirst };

        /// The server always starts a game on the deuce (right-hand) court and alternates
        /// side with every point played.
        public bool DeuceCourt => (PlayerPoints + OpponentPoints) % 2 == 0;

        /// Award one point. Returns true when that point also finished a game.
        public bool AwardPoint(bool toPlayer)
        {
            if (Complete) return false;
            if (toPlayer) PlayerPoints++; else OpponentPoints++;
            int high = Mathf.Max(PlayerPoints, OpponentPoints), low = Mathf.Min(PlayerPoints, OpponentPoints);
            // A game needs four points and a clear two-point margin, which is what makes
            // deuce and advantage work without special-casing them.
            if (high < 4 || high - low < 2) return false;
            bool playerTookGame = PlayerPoints > OpponentPoints;
            if (playerTookGame) PlayerGames++; else OpponentGames++;
            PlayerPoints = OpponentPoints = 0;
            PlayerServes = !PlayerServes;
            if (PlayerGames >= GamesToWin || OpponentGames >= GamesToWin)
            { Complete = true; PlayerWonMatch = PlayerGames > OpponentGames; }
            return true;
        }

        static string Call(int points) => points switch { 0 => "0", 1 => "15", 2 => "30", _ => "40" };

        /// Umpire's call for the current point, server's score first as in real tennis.
        public string PointCall
        {
            get
            {
                int server = PlayerServes ? PlayerPoints : OpponentPoints;
                int receiver = PlayerServes ? OpponentPoints : PlayerPoints;
                if (server >= 3 && receiver >= 3)
                {
                    if (server == receiver) return "DEUCE";
                    return server > receiver ? "AD IN" : "AD OUT";
                }
                return Call(server) + "–" + Call(receiver);
            }
        }

        public string GameCall => $"GAMES {PlayerGames}–{OpponentGames}";

        public string Scoreboard => Complete
            ? (PlayerWonMatch ? "YOU WIN THE SET " : "OPPONENT WINS THE SET ") + $"{PlayerGames}–{OpponentGames}"
            : $"{GameCall}  ·  {PointCall}  ·  {(PlayerServes ? "YOUR SERVE" : "OPPONENT SERVES")}";
    }
}
