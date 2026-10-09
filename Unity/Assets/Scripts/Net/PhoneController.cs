using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using GolfArcade.Game;
using GolfArcade.Swing;
using GolfArcade.UI;
using UnityEngine;
using UnityEngine.UI;

namespace GolfArcade.Net
{
    /// The phone as the club, Wii-remote style: this screen streams the gyro to the display
    /// (a Mac running the game) at frame rate, carries the aim and club buttons, and mirrors the
    /// game back — status line, the power meter filling, and the backswing tension in the hand.
    /// It finds the display from its beacon on the Wi-Fi; failing that, the player types the
    /// address the display shows.
    public sealed class PhoneController : MonoBehaviour
    {
        UdpClient socket, beaconListener;
        Thread ackThread, beaconThread;
        volatile bool running;
        IPEndPoint display;
        volatile string discoveredHost; volatile int discoveredPort;
        DisplayAck ack; double ackAt = double.NegativeInfinity; volatile bool ackDirty;
        readonly object ackLock = new();
        PhoneMotionSource gyro;
        ushort sequence;
        byte lastPhase;
        /// When the network could not be opened (to try again), when this screen started
        /// looking, and when the Mac last answered.
        float socketsFailedAt = -1f, lookingSince, lastLinkedAt = -99f;
        /// A name typed for the Mac, being looked up off the main thread.
        volatile string resolving; volatile string resolvedHost, resolveError;

        HoldButton aimLeft, aimRight, clubUp, clubDown, connect, exit;
        Text title, status, hint;
        Image meterFill;
        InputField address;

        public static PhoneController Create()
        {
            var go = new GameObject("Phone controller");
            var c = go.AddComponent<PhoneController>();
            return c;
        }

        void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Screen.orientation = ScreenOrientation.Portrait;
            gyro = new PhoneMotionSource();
            gyro.Start();
            BuildUi();
            lookingSince = Time.unscaledTime;
            OpenSockets();
            string remembered = PlayerPrefs.GetString("display.address", "");
            if (!string.IsNullOrEmpty(remembered)) { address.text = remembered; TryConnect(remembered); }
        }

        /// The sockets to the Mac: one to send on (and hear it answer), one to hear its beacon.
        /// If the network won't open (no Wi-Fi yet), it is tried again every few seconds.
        void OpenSockets()
        {
            try
            {
                socket ??= NewSocket(0, false);
                beaconListener ??= NewSocket(ControllerProtocol.BeaconPort, true);
                if (!running)
                {
                    running = true;
                    ackThread = new Thread(ReceiveAcks) { IsBackground = true }; ackThread.Start();
                    beaconThread = new Thread(ListenForBeacon) { IsBackground = true }; beaconThread.Start();
                }
                socketsFailedAt = -1f;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Phone controller: the network won't open: {e.Message}");
                try { socket?.Close(); } catch { }
                try { beaconListener?.Close(); } catch { }
                socket = beaconListener = null;
                socketsFailedAt = Time.unscaledTime;
            }
        }

        static UdpClient NewSocket(int port, bool shared)
        {
            var u = new UdpClient(AddressFamily.InterNetwork);
            if (shared) u.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            u.Client.Bind(new IPEndPoint(IPAddress.Any, port));
            return u;
        }

        void OnDestroy()
        {
            running = false;
            Haptics.Release();
            try { socket?.Close(); } catch { }
            try { beaconListener?.Close(); } catch { }
            gyro?.Stop();
        }

        void BuildUi()
        {
            UiKit.Canvas(gameObject);
            var root = transform;
            UiKit.Panel(root, "Backdrop", new Color(0.07f, 0.2f, 0.1f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero)
                .rectTransform.offsetMax = Vector2.zero;
            title = UiKit.Label(root, "Title", 64, TextAnchor.UpperCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -170), new Vector2(1000, 80));
            title.text = "GOLF ARCADE  ·  CLUB";
            status = UiKit.Label(root, "Status", 40, TextAnchor.UpperCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -270), new Vector2(1000, 60));
            status.color = new Color(0.85f, 0.95f, 1f);
            hint = UiKit.Label(root, "Hint", 36, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 80), new Vector2(940, 200));
            hint.color = new Color(1, 1, 1, 0.85f);
            hint.text = "Hold the phone like a club.\nSettle, draw back, swing through.";

            // Address entry for when the beacon does not get through the Wi-Fi.
            var field = UiKit.Panel(root, "Address", new Color(0, 0, 0, 0.45f), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-330, -360), new Vector2(520, 90));
            address = field.gameObject.AddComponent<InputField>();
            var text = UiKit.Label(field.transform, "Text", 40, TextAnchor.MiddleLeft, new Vector2(0, 0), new Vector2(0, 0), new Vector2(20, 0), new Vector2(480, 90));
            text.fontStyle = FontStyle.Normal; text.supportRichText = false;
            var placeholder = UiKit.Label(field.transform, "Placeholder", 40, TextAnchor.MiddleLeft, new Vector2(0, 0), new Vector2(0, 0), new Vector2(20, 0), new Vector2(480, 90));
            placeholder.text = "Mac address or name, e.g. 192.168.1.20"; placeholder.color = new Color(1, 1, 1, 0.4f); placeholder.fontStyle = FontStyle.Normal;
            address.textComponent = text; address.placeholder = placeholder;
            address.keyboardType = TouchScreenKeyboardType.URL;
            address.characterLimit = 64;
            connect = UiKit.Button(root, "CONNECT", new Vector2(0.5f, 1), new Vector2(330, -405), new Vector2(260, 90), 34);
            connect.Pressed = () => { Haptics.Tick(); TryConnect(address.text); };

            // The meter, mirrored from the display, on the left like the game's own.
            var meterBg = UiKit.Panel(root, "Meter", new Color(0, 0, 0, 0.45f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(70, -250), new Vector2(60, 700));
            meterFill = UiKit.Panel(meterBg.transform, "Fill", new Color(0.35f, 0.85f, 0.35f, 0.95f), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 6), new Vector2(48, 0));
            meterFill.rectTransform.pivot = new Vector2(0.5f, 0);
            var meterLabel = UiKit.Label(meterBg.transform, "Power", 30, TextAnchor.MiddleCenter, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, -30), new Vector2(200, 40));
            meterLabel.text = "POWER";

            // Buttons: aim in the corners where the thumbs fall, club in the middle.
            aimLeft = UiKit.Button(root, "◀", new Vector2(0, 0), new Vector2(200, 260), new Vector2(340, 340));
            aimRight = UiKit.Button(root, "▶", new Vector2(1, 0), new Vector2(-200, 260), new Vector2(340, 340));
            clubUp = UiKit.Button(root, "▲", new Vector2(0.5f, 0), new Vector2(0, 560), new Vector2(220, 150));
            clubDown = UiKit.Button(root, "▼", new Vector2(0.5f, 0), new Vector2(0, 380), new Vector2(220, 150));
            var clubLabel = UiKit.Label(root, "ClubLabel", 30, TextAnchor.MiddleCenter, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-100, 650), new Vector2(200, 40));
            clubLabel.text = "CLUB";
            foreach (var b in new[] { aimLeft, aimRight, clubUp, clubDown }) b.Pressed = Haptics.Tick;

            exit = UiKit.Button(root, "✕", new Vector2(1, 1), new Vector2(-90, -90), new Vector2(110, 110), 48);
            exit.Pressed = () => UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }

        /// The Mac by its address (192.168.1.20) or its name (my-mac.local), which is looked up
        /// without holding the screen.
        void TryConnect(string host)
        {
            host = (host ?? "").Trim();
            if (host.Length == 0) return;
            if (IPAddress.TryParse(host, out var ip))
            {
                display = new IPEndPoint(ip, ControllerProtocol.StatePort);
                PlayerPrefs.SetString("display.address", host);
                PlayerPrefs.Save();
                return;
            }
            if (resolving != null) return;
            resolving = host; resolveError = null;
            new Thread(() =>
            {
                try
                {
                    foreach (var a in Dns.GetHostAddresses(host))
                        if (a.AddressFamily == AddressFamily.InterNetwork) { resolvedHost = a.ToString(); break; }
                    if (resolvedHost == null) resolveError = $"Can't find “{host}” on this Wi-Fi";
                }
                catch (Exception) { resolveError = $"Can't find “{host}” on this Wi-Fi"; }
                finally { resolving = null; }
            }) { IsBackground = true }.Start();
        }

        void ListenForBeacon()
        {
            var from = new IPEndPoint(IPAddress.Any, 0);
            while (running)
            {
                byte[] data;
                try { data = beaconListener.Receive(ref from); }
                catch { if (!running) return; continue; }
                if (!ControllerProtocol.TryDecodeBeacon(data, data.Length, out var port)) continue;
                discoveredHost = from.Address.ToString();
                discoveredPort = port;
            }
        }

        void ReceiveAcks()
        {
            var from = new IPEndPoint(IPAddress.Any, 0);
            while (running)
            {
                byte[] data;
                try { data = socket.Receive(ref from); }
                catch { if (!running) return; continue; }
                if (!ControllerProtocol.TryDecodeAck(data, data.Length, out var a)) continue;
                lock (ackLock) { ack = a; ackDirty = true; }
            }
        }

        void Update()
        {
            // the network, if it wouldn't open: again every three seconds
            if (socketsFailedAt >= 0 && Time.unscaledTime - socketsFailedAt > 3f) OpenSockets();
            // a name typed for the Mac, looked up
            if (resolvedHost != null)
            {
                display = new IPEndPoint(IPAddress.Parse(resolvedHost), ControllerProtocol.StatePort);
                PlayerPrefs.SetString("display.address", address.text.Trim()); PlayerPrefs.Save();
                resolvedHost = null;
            }
            // A beacon names the display: take it unless the player typed one — or the one they
            // typed hasn't answered for a while (the Mac has a new address on the Wi-Fi).
            if (discoveredHost != null)
            {
                bool typed = PlayerPrefs.GetString("display.address", "") != "";
                bool lost = Time.unscaledTime - lastLinkedAt > 4f;
                if (display == null || !typed || (lost && discoveredHost != display.Address.ToString()))
                {
                    if (IPAddress.TryParse(discoveredHost, out var found)) display = new IPEndPoint(found, discoveredPort);
                    if (typed && lost) { PlayerPrefs.SetString("display.address", ""); PlayerPrefs.Save(); }
                }
                discoveredHost = null;
            }

            if (display != null && socket != null && gyro.TryRead(out var sample))
            {
                var state = new ControllerState
                {
                    Sequence = ++sequence,
                    Time = sample.Time,
                    Attitude = sample.Attitude,
                    RotationRate = sample.RotationRate,
                    Gravity = sample.Gravity,
                    Buttons = (aimLeft.IsHeld ? ControllerButtons.AimLeft : 0) | (aimRight.IsHeld ? ControllerButtons.AimRight : 0)
                            | (clubUp.IsHeld ? ControllerButtons.ClubUp : 0) | (clubDown.IsHeld ? ControllerButtons.ClubDown : 0),
                    HasGyro = gyro.IsAvailable,
                };
                var bytes = ControllerProtocol.EncodeState(state);
                try { socket.Send(bytes, bytes.Length, display); } catch { }
            }
            else if (display != null && socket != null && !gyro.IsAvailable)
            {
                // No gyro at all (simulator): still send the buttons so aiming works.
                var bytes = ControllerProtocol.EncodeState(new ControllerState { Sequence = ++sequence, Time = Time.unscaledTimeAsDouble, HasGyro = false,
                    Buttons = (aimLeft.IsHeld ? ControllerButtons.AimLeft : 0) | (aimRight.IsHeld ? ControllerButtons.AimRight : 0) | (clubUp.IsHeld ? ControllerButtons.ClubUp : 0) | (clubDown.IsHeld ? ControllerButtons.ClubDown : 0) });
                try { socket.Send(bytes, bytes.Length, display); } catch { }
            }

            DisplayAck a; bool fresh;
            lock (ackLock) { a = ack; fresh = ackDirty; ackDirty = false; }
            if (fresh) ackAt = Time.unscaledTimeAsDouble;
            bool linked = Time.unscaledTimeAsDouble - ackAt < NetworkMotionSource.ConnectionTimeout;
            if (linked) lastLinkedAt = Time.unscaledTime;
            float searching = Time.unscaledTime - Mathf.Max(lookingSince, lastLinkedAt);

            if (socket == null) status.text = "No network yet — join the Wi-Fi the Mac is on";
            else if (resolveError != null) status.text = resolveError;
            else if (resolving != null) status.text = $"Looking up {resolving}…";
            else if (display == null) status.text = searching < 12f ? "Looking for the Mac on Wi-Fi…  or type its address"
                                                                    : "Can't see the Mac — see below";
            else if (!linked) status.text = searching < 8f ? $"Calling {display.Address}…" : $"No answer from {display.Address} — looking again…";
            else status.text = $"Connected  ·  {a.Status}";

            // Mirror the game: the meter fills in the hand, the tension buzzes, impact thumps.
            float load = linked ? Mathf.Clamp01(a.Load) : 0;
            meterFill.rectTransform.sizeDelta = new Vector2(48, load * 688);
            meterFill.color = load > 0.98f ? new Color(1f, 0.55f, 0.2f) : Color.Lerp(new Color(0.35f, 0.85f, 0.35f), new Color(1f, 0.9f, 0.25f), load);
            byte phase = linked ? a.Phase : (byte)0;
            if (phase == 2) Haptics.Tension(load); else Haptics.Release();
            if (phase == 3 && lastPhase != 3) Haptics.Impact(load);
            lastPhase = phase;
            hint.text = !linked && searching >= 12f
                        ? "Is Golf Arcade open on the Mac, on the same Wi-Fi?\nIf so: Settings › Privacy & Security › Local Network › Golf Arcade — turn it on."
                      : !linked ? "Open Golf Arcade on the Mac.\nThis phone is the club."
                      : phase == 1 ? "Aim with ◀ ▶, pick a club with ▲ ▼.\nHold still, then swing."
                      : phase == 2 ? "Swing through!"
                      : phase == 3 ? "" : "Hold the phone like a club.";
        }
    }
}
