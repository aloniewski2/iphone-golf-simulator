using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GolfArcade.Profile;
using UnityEngine;

namespace GolfArcade.Net.Online
{
    /// This phone's connection to the game server's /v1/play socket. Lives across scene loads;
    /// messages are read on Unity's main thread (the awaits resume there) and applied to `Room`.
    /// A dropped connection while in a room reconnects on its own, and the server puts the
    /// player back in their seat with their card.
    public sealed class OnlineSession : MonoBehaviour
    {
        public enum Status { Offline, Connecting, Online }

        public static OnlineSession Instance { get; private set; }

        public readonly OnlineRoom Room = new();
        public Status State { get; private set; } = Status.Offline;
        /// Stopped trying for good (the address can't be a server): only TRY AGAIN starts it.
        public bool GaveUp { get; set; }
        public string StatusText { get; private set; } = "";
        public Action StateChanged;

        ClientWebSocket socket;
        CancellationTokenSource cts;
        string token = "";
        bool wanted;
        int attempt;
        readonly SemaphoreSlim sending = new(1, 1);
        /// This phone's scores in its room, kept so a hole holed while the connection was down
        /// is sent once it is back (the server keeps the first score it gets for a hole).
        readonly System.Collections.Generic.SortedDictionary<int, int> myHoles = new();
        /// Cut short the wait before the next try (TRY AGAIN).
        CancellationTokenSource waiting;
        /// The room the kept scores belong to.
        string keptFor = "";

        public static OnlineSession Ensure()
        {
            if (Instance) return Instance;
            var go = new GameObject("Online session");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<OnlineSession>();
            return Instance;
        }

        /// Connect as the signed-in profile (token from BackendClient.SignUp).
        public void Connect(string playerToken)
        {
            GaveUp = false;
            token = playerToken ?? "";
            wanted = true;
            attempt = 0;
            if (State == Status.Offline) _ = Run();
        }

        public void Disconnect()
        {
            wanted = false;
            if (Room.InRoom) Send(OnlineMessage.Leave());
            Room.Reset();
            myHoles.Clear();
            cts?.Cancel();
            waiting?.Cancel();
        }

        /// Try again now rather than after the back-off.
        public void RetryNow()
        {
            attempt = 0;
            waiting?.Cancel();
        }

        /// A hole holed: sent now, or as soon as the connection is back.
        public void SendHole(int hole, int strokes)
        {
            myHoles[hole] = strokes;
            Send(OnlineMessage.Hole(hole, strokes));
        }

        /// For the tests: the connection drops, as a Wi-Fi blip would.
        public void DropForTests()
        {
            try { socket?.Abort(); } catch (Exception) { }
        }

        /// Scores kept for resending, for the tests.
        public int KeptHoles => myHoles.Count;

        public void Send(OnlineMessage message)
        {
            if (socket == null || socket.State != WebSocketState.Open) return;
            _ = SendNow(JsonUtility.ToJson(message));
        }

        async Task SendNow(string json)
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            await sending.WaitAsync();
            try
            {
                var s = socket;   // (it may be closing as this is sent)
                if (s != null && s.State == WebSocketState.Open) await s.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cts?.Token ?? CancellationToken.None);
            }
            catch (Exception) { }   // a send that fails is a lost connection: Run notices and reconnects
            finally { sending.Release(); }
        }

        async Task Run()
        {
            while (wanted && this)
            {
                SetState(Status.Connecting, attempt == 0 ? "Connecting…" : "Reconnecting…");
                cts = new CancellationTokenSource();
                socket = new ClientWebSocket();
                socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);
                string why = null;
                try
                {
                    await socket.ConnectAsync(new Uri(BackendConfig.SocketUrl), cts.Token);
                    await SendNow(JsonUtility.ToJson(OnlineMessage.Hello(token)));
                    // anything holed while the connection was down, again (a repeat is ignored)
                    foreach (var kv in new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<int, int>>(myHoles))
                        await SendNow(JsonUtility.ToJson(OnlineMessage.Hole(kv.Key, kv.Value)));
                    attempt = 0;
                    SetState(Status.Online, "Online");
                    await Receive();
                }
                catch (UriFormatException) { why = "That game server address isn't right"; wanted = false; GaveUp = true; }
                catch (Exception e)
                {
                    // Any failure — refused, unreachable, dropped, a bad reply — is a lost
                    // connection to try again, never an end to trying.
                    if (wanted && !(e is OperationCanceledException)) { Debug.LogWarning($"Online: {e.GetType().Name}: {e.Message}"); why = Reason(e); }
                }
                finally
                {
                    try { socket?.Dispose(); } catch (Exception) { }
                    socket = null;
                }
                if (!wanted || !this) { if (why != null) SetState(Status.Offline, why); break; }
                // Back off: 1, 2, 4 … up to 15 seconds between tries.
                attempt++;
                SetState(Status.Connecting, attempt <= 2 ? "Connection lost — reconnecting…" : (why ?? "Can't reach the game server") + " — retrying…");
                waiting = new CancellationTokenSource();
                try { await Task.Delay(TimeSpan.FromSeconds(Math.Min(15, 1 << Math.Min(attempt - 1, 4))), waiting.Token); }
                catch (OperationCanceledException) { }
            }
            if (State != Status.Offline) SetState(Status.Offline, "Offline");
        }

        async Task Receive()
        {
            var buffer = new byte[8192];
            var text = new StringBuilder();
            while (socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    // 4003: the server does not know this token; retrying will not help.
                    if (result.CloseStatus == (WebSocketCloseStatus)4003) { wanted = false; ProfileStore.ForgetServerToken(token); }
                    return;
                }
                text.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                if (!result.EndOfMessage) continue;
                OnlineMessage message = null;
                try { message = JsonUtility.FromJson<OnlineMessage>(text.ToString()); }
                catch (Exception) { }
                text.Clear();
                // the game's own handling of a message must not take the connection down with it
                try { Room.Apply(message); }
                catch (Exception e) { Debug.LogException(e); }
                // another room (or none): its scores are not this one's
                if (Room.Code != keptFor) { myHoles.Clear(); keptFor = Room.Code; }
            }
        }

        /// A lost connection in words for the player.
        static string Reason(Exception e)
        {
            string m = (e.InnerException?.Message ?? e.Message ?? "").ToLowerInvariant();
            if (m.Contains("resolve") || m.Contains("host")) return "Can't find the game server — is the Mac on, on this Wi-Fi?";
            if (m.Contains("refused")) return "The game server isn't running on the Mac";
            if (m.Contains("timed out") || m.Contains("timeout")) return "The game server isn't answering";
            return "Can't reach the game server";
        }

        void SetState(Status state, string text)
        {
            State = state;
            StatusText = text;
            // a screen's trouble redrawing must not stop the connection
            try { StateChanged?.Invoke(); }
            catch (Exception e) { Debug.LogException(e); }
        }

        void OnDestroy()
        {
            wanted = false;
            cts?.Cancel();
            if (Instance == this) Instance = null;
        }
    }
}
