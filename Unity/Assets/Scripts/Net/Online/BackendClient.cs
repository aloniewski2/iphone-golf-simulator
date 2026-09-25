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
        /// The deployed server (see server/README.md), e.g. "https://golf-arcade-xxxx.run.app".
        public const string DefaultServerUrl = "";
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
            var response = JsonUtility.FromJson<SignupResponse>(json);
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
            return json == null ? null : JsonUtility.FromJson<PlayerResponse>(json)?.player;
        }

        public static async Task<ServerPlayer> FetchProfile(PlayerProfile profile)
        {
            if (!profile.IsSignedIn) return null;
            var json = await Send("GET", "/v1/me", null, profile.ServerToken);
            return json == null ? null : JsonUtility.FromJson<PlayerResponse>(json)?.player;
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
            return json == null ? null : JsonUtility.FromJson<Leaderboard>(json);
        }

        /// The last error the server gave, for the menus to show.
        public static string LastError { get; private set; } = "";

        static async Task<string> Send(string method, string path, string body, string token)
        {
            if (!BackendConfig.IsConfigured) { LastError = "No server set"; return null; }
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
            string message = request.error;
            try { var e = JsonUtility.FromJson<ErrorResponse>(text); if (!string.IsNullOrEmpty(e?.error)) message = e.error; } catch (ArgumentException) { }
            LastError = message;
            // The server no longer knows this token (a fresh database): sign up again next time.
            if (request.responseCode == 401 && !string.IsNullOrEmpty(token)) ProfileStore.ForgetServerToken(token);
            Debug.LogWarning($"{method} {path}: {message}");
            return null;
        }
    }
}
