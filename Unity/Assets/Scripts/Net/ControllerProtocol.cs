using System;
using System.Numerics;

namespace GolfArcade.Net
{
    /// The buttons on the phone's controller screen, as bits in the state packet.
    [Flags]
    public enum ControllerButtons : byte
    {
        None = 0,
        AimLeft = 1,
        AimRight = 2,
        ClubUp = 4,
        ClubDown = 8,
    }

    /// One frame from the phone: how it is held and turning, and which buttons are down.
    public struct ControllerState
    {
        public ushort Sequence;
        public double Time;
        public Quaternion Attitude;
        public Vector3 RotationRate;
        /// Gravity in the phone's frame (unit length; zero from a v1 controller).
        public Vector3 Gravity;
        public ControllerButtons Buttons;
        public bool HasGyro;
    }

    /// What the display sends back so the controller screen can mirror the game: the round's
    /// phase and the meter, so the phone shows the power filling in the player's hand.
    public struct DisplayAck
    {
        public byte Phase;   // 0 waiting, 1 aiming, 2 backswing, 3 flight, 4 result
        public float Load;   // meter 0–1
        public string Status;
    }

    /// Little-endian UDP packets between the phone (club) and the display. Kept tiny and
    /// self-describing: magic + version first, so strays on the port are dropped.
    public static class ControllerProtocol
    {
        public const int StatePort = 47800;   // phone → display: controller state
        public const int BeaconPort = 47801;  // display → LAN broadcast: "I am here"
        public const byte Version = 2;
        public const int StateLengthV1 = 4 + 1 + 2 + 8 + 16 + 12 + 1 + 1; // 45
        public const int StateLength = StateLengthV1 + 12;               // 57: v2 adds gravity
        public const int BeaconLength = 4 + 1 + 2;

        static readonly byte[] StateMagic = { (byte)'G', (byte)'C', (byte)'T', (byte)'L' };
        static readonly byte[] AckMagic = { (byte)'G', (byte)'A', (byte)'C', (byte)'K' };
        static readonly byte[] BeaconMagic = { (byte)'G', (byte)'B', (byte)'C', (byte)'N' };

        public static byte[] EncodeState(in ControllerState s)
        {
            var b = new byte[StateLength];
            int o = 0;
            Put(b, ref o, StateMagic); b[o++] = Version;
            Put(b, ref o, s.Sequence);
            Put(b, ref o, s.Time);
            Put(b, ref o, s.Attitude.X); Put(b, ref o, s.Attitude.Y); Put(b, ref o, s.Attitude.Z); Put(b, ref o, s.Attitude.W);
            Put(b, ref o, s.RotationRate.X); Put(b, ref o, s.RotationRate.Y); Put(b, ref o, s.RotationRate.Z);
            b[o++] = (byte)s.Buttons;
            b[o++] = (byte)(s.HasGyro ? 1 : 0);
            Put(b, ref o, s.Gravity.X); Put(b, ref o, s.Gravity.Y); Put(b, ref o, s.Gravity.Z);
            return b;
        }

        public static bool TryDecodeState(byte[] b, int length, out ControllerState s)
        {
            s = default;
            if (length < StateLengthV1 || !Match(b, StateMagic)) return false;
            byte version = b[4];
            if (version != 1 && version != Version) return false;
            if (version == Version && length < StateLength) return false;
            int o = 5;
            s.Sequence = BitConverter.ToUInt16(b, o); o += 2;
            s.Time = BitConverter.ToDouble(b, o); o += 8;
            s.Attitude = new Quaternion(F(b, ref o), F(b, ref o), F(b, ref o), F(b, ref o));
            s.RotationRate = new Vector3(F(b, ref o), F(b, ref o), F(b, ref o));
            s.Buttons = (ControllerButtons)b[o++];
            s.HasGyro = b[o++] != 0;
            if (version >= 2) s.Gravity = new Vector3(F(b, ref o), F(b, ref o), F(b, ref o));
            return Finite(s);
        }

        public static byte[] EncodeAck(in DisplayAck a)
        {
            var text = System.Text.Encoding.UTF8.GetBytes(a.Status ?? "");
            int n = Math.Min(text.Length, 120);
            var b = new byte[4 + 1 + 1 + 4 + 1 + n];
            int o = 0;
            Put(b, ref o, AckMagic); b[o++] = Version;
            b[o++] = a.Phase;
            Put(b, ref o, a.Load);
            b[o++] = (byte)n;
            Array.Copy(text, 0, b, o, n);
            return b;
        }

        public static bool TryDecodeAck(byte[] b, int length, out DisplayAck a)
        {
            a = default;
            if (length < 11 || !Match(b, AckMagic) || b[4] != Version) return false;
            a.Phase = b[5];
            a.Load = BitConverter.ToSingle(b, 6);
            int n = b[10];
            if (length < 11 + n) return false;
            a.Status = System.Text.Encoding.UTF8.GetString(b, 11, n);
            return float.IsFinite(a.Load);
        }

        /// The display's beacon carries the port it listens on.
        public static byte[] EncodeBeacon(ushort statePort)
        {
            var b = new byte[BeaconLength];
            int o = 0;
            Put(b, ref o, BeaconMagic); b[o++] = Version;
            Put(b, ref o, statePort);
            return b;
        }

        public static bool TryDecodeBeacon(byte[] b, int length, out ushort statePort)
        {
            statePort = 0;
            if (length < BeaconLength || !Match(b, BeaconMagic) || b[4] != Version) return false;
            statePort = BitConverter.ToUInt16(b, 5);
            return statePort != 0;
        }

        static bool Finite(in ControllerState s) =>
            double.IsFinite(s.Time) && float.IsFinite(s.Attitude.X) && float.IsFinite(s.Attitude.Y) && float.IsFinite(s.Attitude.Z) && float.IsFinite(s.Attitude.W)
            && float.IsFinite(s.RotationRate.X) && float.IsFinite(s.RotationRate.Y) && float.IsFinite(s.RotationRate.Z)
            && float.IsFinite(s.Gravity.X) && float.IsFinite(s.Gravity.Y) && float.IsFinite(s.Gravity.Z);

        static bool Match(byte[] b, byte[] magic) => b[0] == magic[0] && b[1] == magic[1] && b[2] == magic[2] && b[3] == magic[3];
        static void Put(byte[] b, ref int o, byte[] v) { Array.Copy(v, 0, b, o, v.Length); o += v.Length; }
        static void Put(byte[] b, ref int o, ushort v) { Array.Copy(Le(BitConverter.GetBytes(v)), 0, b, o, 2); o += 2; }
        static void Put(byte[] b, ref int o, float v) { Array.Copy(Le(BitConverter.GetBytes(v)), 0, b, o, 4); o += 4; }
        static void Put(byte[] b, ref int o, double v) { Array.Copy(Le(BitConverter.GetBytes(v)), 0, b, o, 8); o += 8; }
        static float F(byte[] b, ref int o) { float v = BitConverter.ToSingle(b, o); o += 4; return v; }
        static byte[] Le(byte[] v) { if (!BitConverter.IsLittleEndian) Array.Reverse(v); return v; }
    }
}
