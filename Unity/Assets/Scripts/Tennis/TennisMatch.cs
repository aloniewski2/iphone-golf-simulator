using System.Collections.Generic;
using UnityEngine;

namespace GolfArcade.Tennis
{
    /// Real tennis scoring. The default match is a single short set decided as best-of-five
    /// games (first to three, no tiebreak); the campaign's later rounds play best-of-three and
    /// best-of-five six-game sets, where a set needs six games and a two-game margin and 6-6
    /// goes to a first-to-seven (by two) tiebreak.
    ///
    /// Deliberately pure: no Unity objects, no time, no randomness. Every rule here is
    /// exercised by EditMode tests, which is the only way to be confident about deuce,
    /// advantage, tiebreaks and service rotation without playing hundreds of points by hand.
    public struct TennisMatch
    {
        /// The short format's games (kept for callers that ask about the default match).
        public const int GamesToWin = 3, SetGames = 5;

        /// Sets needed to win the match (1, 2 or 3) and games per set (3 = the short
        /// first-to-three set; 6 = a real set with a margin of two and a tiebreak at 6-6).
        public int SetsToWin, GamesPerSet;

        public int PlayerPoints, OpponentPoints;
        /// Games in the set being played.
        public int PlayerGames, OpponentGames;
        public int PlayerSets, OpponentSets;
        /// Finished sets, "6–4" from the player's side, for the results.
        public List<string> SetScores;
        public bool Tiebreak;
        /// True while the player is the server. One player serves a whole game, as in
        /// real tennis, and the serve changes hands when the game does; in a tiebreak the
        /// serve changes after the first point and then every two points.
        public bool PlayerServes;
        public bool Complete;
        public bool PlayerWonMatch;
        bool tiebreakFirstServer;

        public static TennisMatch New(bool playerServesFirst = true) => New(playerServesFirst, 1, GamesToWin);

        public static TennisMatch New(bool playerServesFirst, int setsToWin, int gamesPerSet) =>
            new TennisMatch { PlayerServes = playerServesFirst, SetsToWin = Mathf.Max(1, setsToWin), GamesPerSet = Mathf.Max(1, gamesPerSet), SetScores = new List<string>() };

        bool Short => GamesPerSet < 6;
        /// A multi-set match (the HUD then shows sets as well as games).
        public bool MultiSet => SetsToWin > 1;

        /// The server always starts a game on the deuce (right-hand) court and alternates
        /// side with every point played.
        public bool DeuceCourt => (PlayerPoints + OpponentPoints) % 2 == 0;

        /// Award one point. Returns true when that point also finished a game.
        public bool AwardPoint(bool toPlayer)
        {
            if (Complete) return false;
            SetScores ??= new List<string>();
            if (SetsToWin < 1) SetsToWin = 1;
            if (GamesPerSet < 1) GamesPerSet = GamesToWin;
            if (toPlayer) PlayerPoints++; else OpponentPoints++;
            int high = Mathf.Max(PlayerPoints, OpponentPoints), low = Mathf.Min(PlayerPoints, OpponentPoints);
            if (Tiebreak)
            {
                int played = PlayerPoints + OpponentPoints;
                if (high < 7 || high - low < 2)
                {
                    // Serve changes after the first point, then every two.
                    if (played % 2 == 1) PlayerServes = !PlayerServes;
                    return false;
                }
                if (PlayerPoints > OpponentPoints) PlayerGames++; else OpponentGames++;
                PlayerPoints = OpponentPoints = 0; Tiebreak = false;
                // Whoever received first in the tiebreak serves the next set's first game.
                PlayerServes = !tiebreakFirstServer;
                EndSet(PlayerGames > OpponentGames);
                return true;
            }
            // A game needs four points and a clear two-point margin, which is what makes
            // deuce and advantage work without special-casing them.
            if (high < 4 || high - low < 2) return false;
            bool playerTookGame = PlayerPoints > OpponentPoints;
            if (playerTookGame) PlayerGames++; else OpponentGames++;
            PlayerPoints = OpponentPoints = 0;
            PlayerServes = !PlayerServes;
            if (Short)
            {
                if (PlayerGames >= GamesPerSet || OpponentGames >= GamesPerSet) EndSet(PlayerGames > OpponentGames);
                return true;
            }
            int gHigh = Mathf.Max(PlayerGames, OpponentGames), gLow = Mathf.Min(PlayerGames, OpponentGames);
            if (gHigh >= GamesPerSet && gHigh - gLow >= 2 || gHigh == GamesPerSet + 1) EndSet(PlayerGames > OpponentGames);
            else if (PlayerGames == GamesPerSet && OpponentGames == GamesPerSet) { Tiebreak = true; tiebreakFirstServer = PlayerServes; }
            return true;
        }

        void EndSet(bool playerTookSet)
        {
            SetScores.Add($"{PlayerGames}–{OpponentGames}");
            if (playerTookSet) PlayerSets++; else OpponentSets++;
            if (PlayerSets >= SetsToWin || OpponentSets >= SetsToWin)
            { Complete = true; PlayerWonMatch = PlayerSets > OpponentSets; return; }   // the last set's games stay on the board
            PlayerGames = OpponentGames = 0;
        }

        static string Call(int points) => points switch { 0 => "0", 1 => "15", 2 => "30", _ => "40" };

        /// Umpire's call for the current point, server's score first as in real tennis.
        public string PointCall
        {
            get
            {
                int server = PlayerServes ? PlayerPoints : OpponentPoints;
                int receiver = PlayerServes ? OpponentPoints : PlayerPoints;
                if (Tiebreak) return $"{server}–{receiver}";
                if (server >= 3 && receiver >= 3)
                {
                    if (server == receiver) return "DEUCE";
                    return server > receiver ? "AD IN" : "AD OUT";
                }
                return Call(server) + "–" + Call(receiver);
            }
        }

        public string GameCall => MultiSet ? $"SETS {PlayerSets}–{OpponentSets}  ·  GAMES {PlayerGames}–{OpponentGames}" : $"GAMES {PlayerGames}–{OpponentGames}";

        /// The final score from the player's side: "3–1" for a short match, "6–4 3–6 7–6" for sets.
        public string FinalScore
        {
            get
            {
                if (!MultiSet && SetScores != null && SetScores.Count == 1) return SetScores[0];
                return SetScores == null ? "" : string.Join(" ", SetScores);
            }
        }

        public string Scoreboard => Complete
            ? (PlayerWonMatch ? "YOU WIN " : "OPPONENT WINS ") + FinalScore
            : $"{GameCall}  ·  {(Tiebreak ? "TIEBREAK " : "")}{PointCall}  ·  {(PlayerServes ? "YOUR SERVE" : "OPPONENT SERVES")}";
    }
}
