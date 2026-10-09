using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using GolfArcade.Swing;

namespace GolfArcade.Net
{
    /// The display's end of the phone-as-club link: listens for controller packets on the LAN,
    /// hands the game every motion sample in order (so the swing detector sees the phone's own
    /// 60 Hz stream), keeps the latest button state, answers the phone with the game's phase
    /// and meter, and broadcasts a beacon so the phone can find this machine without typing an
    /// address. Pure .NET sockets on background threads; no Unity types.
    public sealed class NetworkMotionSource : IMotionSource, IDisposable
    {
        public const double ConnectionTimeout = 1.5;

        readonly UdpClient socket;
        readonly UdpClient beacon;
        readonly Thread receiveThread, beaconThread;
        readonly ConcurrentQueue<MotionSample> samples = new();
        readonly Func<double> clock;
        volatile bool running;
        double lastPacketAt = double.NegativeInfinity;
        IPEndPoint remote;
        ushort lastSequence;
        int dropped;
        ControllerButtons buttons;

        public int Port { get; }
        /// Whether the phone spoke recently.
        public bool IsConnected => clock() - lastPacketAt < ConnectionTimeout;
        public bool IsAvailable => IsConnected;
        /// Buttons currently held on the phone.
        public ControllerButtons Buttons => buttons;
        public int PacketsDropped => dropped;
        public string RemoteAddress => remote?.Address.ToString();

        /// `clock` is the game's monotonic time (Time.unscaledTimeAsDouble) so connection
        /// timeouts line up with frames; tests pass their own.
        public NetworkMotionSource(Func<double> clock, int port = ControllerProtocol.StatePort, bool sendBeacon = true)
        {
            this.clock = clock;
            socket = new UdpClient(AddressFamily.InterNetwork);
            socket.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            socket.Client.Bind(new IPEndPoint(IPAddress.Any, port));
            Port = ((IPEndPoint)socket.Client.LocalEndPoint).Port;
            receiveThread = new Thread(Receive) { IsBackground = true, Name = "GolfArcade controller rx" };
            if (sendBeacon)
            {
                beacon = new UdpClient(AddressFamily.InterNetwork) { EnableBroadcast = true };
                beaconThread = new Thread(Beacon) { IsBackground = true, Name = "GolfArcade beacon" };
            }
        }

        public void Start()
        {
            if (running) return;
            running = true;
            receiveThread.Start();
            beaconThread?.Start();
        }

        public void Stop()
        {
            running = false;
            try { socket.Close(); } catch { }
            try { beacon?.Close(); } catch { }
        }

        public void Dispose() => Stop();

        /// Next unread sample, oldest first; drain with a loop each frame.
        public bool TryRead(out MotionSample sample) => samples.TryDequeue(out sample);

        /// Tell the phone what the game is doing so its screen can mirror it.
        public void SendAck(byte phase, float load, string status)
        {
            var to = remote;
            if (to == null || !IsConnected) return;
            var bytes = ControllerProtocol.EncodeAck(new DisplayAck { Phase = phase, Load = load, Status = status });
            try { socket.Send(bytes, bytes.Length, to); } catch { }
        }

        void Receive()
        {
            var from = new IPEndPoint(IPAddress.Any, 0);
            while (running)
            {
                byte[] data;
                try { data = socket.Receive(ref from); }
                catch { if (!running) return; continue; }
                if (!ControllerProtocol.TryDecodeState(data, data.Length, out var s)) continue;
                if (remote == null || !remote.Equals(from)) { remote = from; lastSequence = (ushort)(s.Sequence - 1); }
                ushort expected = (ushort)(lastSequence + 1);
                if (s.Sequence != expected) Interlocked.Add(ref dropped, (ushort)(s.Sequence - expected));
                lastSequence = s.Sequence;
                lastPacketAt = clock();
                buttons = s.Buttons;
                if (s.HasGyro) samples.Enqueue(new MotionSample { Time = s.Time, Attitude = s.Attitude, RotationRate = s.RotationRate, Gravity = s.Gravity });
                // Never let a burst pile up: the detector wants the present, not the past.
                while (samples.Count > 240) samples.TryDequeue(out _);
            }
        }

        void Beacon()
        {
            var packet = ControllerProtocol.EncodeBeacon((ushort)Port);
            var to = new IPEndPoint(IPAddress.Broadcast, ControllerProtocol.BeaconPort);
            while (running)
            {
                try { beacon.Send(packet, packet.Length, to); } catch { }
                Thread.Sleep(500);
            }
        }

        /// This machine's LAN IPv4 addresses, for the "connect to …" line on the screen.
        public static string LocalAddresses()
        {
            var list = new System.Collections.Generic.List<string>();
            try
            {
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus != OperationalStatus.Up || nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    foreach (var a in nic.GetIPProperties().UnicastAddresses)
                        if (a.Address.AddressFamily == AddressFamily.InterNetwork) list.Add(a.Address.ToString());
                }
            }
            catch { }
            return list.Count == 0 ? "?" : string.Join("  ·  ", list);
        }
    }
}
