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
        public string StatusText { get; private set; } = "";
        public Action StateChanged;

        ClientWebSocket socket;
        CancellationTokenSource cts;
        string token = "";
        bool wanted;
        int attempt;
        readonly SemaphoreSlim sending = new(1, 1);

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
            cts?.Cancel();
        }

        public void Send(OnlineMessage message)
        {
            if (socket == null || socket.State != WebSocketState.Open) return;
            _ = SendNow(JsonUtility.ToJson(message));
        }

        async Task SendNow(string json)
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            await sending.WaitAsync();
            try { await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cts.Token); }
            catch (Exception e) when (e is WebSocketException || e is OperationCanceledException || e is ObjectDisposedException) { }
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
                try
                {
                    await socket.ConnectAsync(new Uri(BackendConfig.SocketUrl), cts.Token);
                    await SendNow(JsonUtility.ToJson(OnlineMessage.Hello(token)));
                    attempt = 0;
                    SetState(Status.Online, "Online");
                    await Receive();
                }
                catch (Exception e) when (e is WebSocketException || e is OperationCanceledException || e is UriFormatException || e is ObjectDisposedException)
                {
                    if (wanted) Debug.LogWarning($"Online: {e.Message}");
                }
                finally
                {
                    socket.Dispose();
                    socket = null;
                }
                if (!wanted || !this) break;
                // Back off: 1, 2, 4 … up to 15 seconds between tries.
                attempt++;
                SetState(Status.Connecting, "Connection lost — retrying…");
                await Task.Delay(TimeSpan.FromSeconds(Math.Min(15, 1 << Math.Min(attempt - 1, 4))));
            }
            SetState(Status.Offline, "Offline");
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
                catch (ArgumentException) { }
                text.Clear();
                Room.Apply(message);
            }
        }

        void SetState(Status state, string text)
        {
            State = state;
            StatusText = text;
            StateChanged?.Invoke();
        }

        void OnDestroy()
        {
            wanted = false;
            cts?.Cancel();
            if (Instance == this) Instance = null;
        }
    }
}
