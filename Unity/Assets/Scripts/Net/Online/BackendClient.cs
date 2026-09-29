using System;
using System.Text;
using System.Threading.Tasks;
using GolfArcade.Profile;
using UnityEngine;
using UnityEngine.Networking;

namespace GolfArcade.Net.Online
{
    /// Where the game server is. Empty means offline: solo and two-player-at-home still work,
    /// online play and the leaderboard say they need a server. Set it in the Online screen, or
    /// put the deployed address in DefaultServerUrl before building.
    public static class BackendConfig
    {
        /// The game server: for now the one at home, on the Mac on the Wi-Fi by its Bonjour name
        /// (server/README.md, "Play online at home"); a deployed one would be its https address.
        public const string DefaultServerUrl = "http://Andrews-MacBook-Pro-8.local:8080";
        const string Key = "online.server";

        public static string ServerUrl
        {
            get => PlayerPrefs.GetString(Key, DefaultServerUrl).Trim().TrimEnd('/');
            set { PlayerPrefs.SetString(Key, (value ?? "").Trim().TrimEnd('/')); PlayerPrefs.Save(); }
        }

        public static bool IsConfigured => ServerUrl.StartsWith("http://") || ServerUrl.StartsWith("https://");

        /// The play socket for an http(s) server address.
        public static string SocketUrl => (ServerUrl.StartsWith("https://") ? "wss://" + ServerUrl.Substring(8) : "ws://" + ServerUrl.Substring(ServerUrl.IndexOf("://", StringComparison.Ordinal) + 3)) + "/v1/play";
    }

    // Response shapes (server/src/server.js), for JsonUtility.
    [Serializable] public sealed class ServerStats
    {
        public int roundsPlayed, holesPlayed, totalStrokes, bestToPar, holesInOne, eagles, birdies, pars, matchesWon, matchesTied, matchesLost;
        public bool hasBest;
    }
    [Serializable] public sealed class ServerPlayer { public string id = "", name = ""; public int body, kit, shirt; public ServerStats stats = new(); }
    [Serializable] sealed class SignupResponse { public ServerPlayer player; public string token = ""; }
    [Serializable] sealed class PlayerResponse { public ServerPlayer player; }
    [Serializable] sealed class ErrorResponse { public string error = ""; }
    [Serializable] public sealed class LeaderboardEntry { public string playerId = "", name = ""; public int bestToPar; }
    [Serializable] public sealed class Leaderboard { public string course = ""; public LeaderboardEntry[] entries = Array.Empty<LeaderboardEntry>(); }
    [Serializable] sealed class ProfileBody { public string name; public int body, kit, shirt; }
    [Serializable] sealed class RoundBody { public string course; public int[] strokes; public string result; }

    /// The server's REST side: sign a profile up, keep its name and look in step, send finished
    /// rounds, read the leaderboard. Every call returns null (and logs) when the server can't be
    /// reached, so the game never waits on the network.
    public static class BackendClient
    {
        /// Creates the server account for a profile and keeps its id and token on the profile.
        public static async Task<bool> SignUp(PlayerProfile profile)
        {
            if (profile.IsSignedIn) return true;
            var body = JsonUtility.ToJson(new ProfileBody { name = profile.Name, body = profile.Body, kit = profile.Kit, shirt = profile.Shirt });
            var json = await Send("POST", "/v1/players", body, null);
            if (json == null) return false;
            var response = Parse<SignupResponse>(json);
            if (response?.player == null || string.IsNullOrEmpty(response.token)) return false;
            profile.ServerId = response.player.id;
            profile.ServerToken = response.token;
            ProfileStore.Save();
            return true;
        }

        public static async Task<ServerPlayer> PushProfile(PlayerProfile profile)
        {
            if (!profile.IsSignedIn) return null;
            var body = JsonUtility.ToJson(new ProfileBody { name = profile.Name, body = profile.Body, kit = profile.Kit, shirt = profile.Shirt });
            var json = await Send("PATCH", "/v1/me", body, profile.ServerToken);
            return Parse<PlayerResponse>(json)?.player;
        }

        public static async Task<ServerPlayer> FetchProfile(PlayerProfile profile)
        {
            if (!profile.IsSignedIn) return null;
            var json = await Send("GET", "/v1/me", null, profile.ServerToken);
            return Parse<PlayerResponse>(json)?.player;
        }

        /// A round finished on this phone (solo or at home). Online rounds are recorded by the server.
        public static async Task SubmitRound(PlayerProfile profile, string courseId, int[] strokes, string result)
        {
            if (!profile.IsSignedIn) return;
            var body = JsonUtility.ToJson(new RoundBody { course = courseId, strokes = strokes, result = result ?? "" });
            await Send("POST", "/v1/rounds", body, profile.ServerToken);
        }

        public static async Task<Leaderboard> FetchLeaderboard(string courseId, int limit = 10)
        {
            var json = await Send("GET", $"/v1/leaderboard?course={UnityWebRequest.EscapeURL(courseId)}&limit={limit}", null, null);
            var board = Parse<Leaderboard>(json);
            if (board != null) board.entries ??= Array.Empty<LeaderboardEntry>();
            return board;
        }

        /// The last error, in words for the menus: what went wrong and what to do about it.
        public static string LastError { get; private set; } = "";

        /// A server's reply read as `T`; null for none, or for anything that isn't its JSON (a
        /// Wi-Fi login page, a proxy's error page).
        public static T Parse<T>(string json) where T : class
        {
            if (string.IsNullOrEmpty(json)) return null;
            try { return JsonUtility.FromJson<T>(json); }
            catch (Exception) { LastError = "The game server sent something it shouldn't have — is this the right address?"; return null; }
        }

        /// What a failed request means to the player.
        public static string Explain(long code, bool connectionFailed, string error, string serverError)
        {
            if (!string.IsNullOrEmpty(serverError)) return serverError;
            if (connectionFailed)
            {
                string e = (error ?? "").ToLowerInvariant();
                if (e.Contains("resolve")) return "Can't find the game server — is the Mac on, on this Wi-Fi?";
                if (e.Contains("timed out") || e.Contains("timeout")) return "The game server didn't answer — is it running on the Mac?";
                if (e.Contains("refused") || e.Contains("connect")) return "The game server isn't running — start it on the Mac";
                return "Can't reach the game server — check the Wi-Fi";
            }
            if (code == 401) return "Signed out by the server — signing in again";
            if (code == 429) return "Too many tries — wait a moment";
            if (code >= 500) return "The game server had a problem — try again";
            if (code == 404) return "The game server doesn't know that — is it up to date?";
            return string.IsNullOrEmpty(error) ? "Something went wrong with the game server" : error;
        }

        /// Requests made since the game started, for the tests (sign-in must not be retried in a loop).
        public static int Requests { get; private set; }

        static async Task<string> Send(string method, string path, string body, string token)
        {
            Requests++;
            if (!BackendConfig.IsConfigured) { LastError = "No game server set"; return null; }
            try { return await SendOnce(method, path, body, token); }
            catch (Exception e)
            {
                // (a malformed address, a request the platform refuses: never an exception out of here)
                LastError = e is ArgumentException or UriFormatException ? "That game server address isn't right" : "Can't reach the game server — check the Wi-Fi";
                Debug.LogWarning($"{method} {path}: {e.Message}");
                return null;
            }
        }

        static async Task<string> SendOnce(string method, string path, string body, string token)
        {
            using var request = new UnityWebRequest(BackendConfig.ServerUrl + path, method)
            {
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = 10,
            };
            if (body != null)
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                request.SetRequestHeader("Content-Type", "application/json");
            }
            if (!string.IsNullOrEmpty(token)) request.SetRequestHeader("Authorization", "Bearer " + token);

            var done = new TaskCompletionSource<bool>();
            request.SendWebRequest().completed += _ => done.TrySetResult(true);
            await done.Task;

            var text = request.downloadHandler?.text ?? "";
            if (request.result == UnityWebRequest.Result.Success) { LastError = ""; return text; }
            string serverError = null;
            try { var e = JsonUtility.FromJson<ErrorResponse>(text); if (!string.IsNullOrEmpty(e?.error)) serverError = e.error; } catch (Exception) { }
            string message = Explain(request.responseCode, request.result == UnityWebRequest.Result.ConnectionError, request.error, serverError);
            LastError = message;
            // The server no longer knows this token (a fresh database): sign up again next time.
            if (request.responseCode == 401 && !string.IsNullOrEmpty(token)) ProfileStore.ForgetServerToken(token);
            Debug.LogWarning($"{method} {path}: {request.responseCode} {request.error} — {message}");
            return null;
        }
    }
}
