using System;

namespace GolfArcade.Net.Online
{
    /// One message on the /v1/play socket, either way (see server/src/rooms.js). Flat, with
    /// arrays and no nullables, so JsonUtility reads and writes it; the server ignores fields a
    /// message type does not use.
    ///
    /// App → server: hello{token}, create{course}, quick{course}, join{code}, leave, start,
    /// hole{hole, strokes}. Server → app: welcome{playerId}, room{…}, start{seed, course},
    /// hole{playerId, hole, strokes}, error{error}.
    [Serializable]
    public sealed class OnlineMessage
    {
        public string type = "";
        public string token = "";
        public string playerId = "";
        public string name = "";
        public string code = "";
        public string course = "";
        public string host = "";
        public int seed;
        public bool started;
        public bool finished;
        public bool isPublic;
        public int hole = -1;
        public int strokes;
        public string error = "";
        public OnlinePlayer[] players = Array.Empty<OnlinePlayer>();

        public static OnlineMessage Hello(string token) => new() { type = "hello", token = token };
        public static OnlineMessage Create(string course) => new() { type = "create", course = course };
        public static OnlineMessage Quick(string course) => new() { type = "quick", course = course };
        public static OnlineMessage Join(string code) => new() { type = "join", code = code };
        public static OnlineMessage Leave() => new() { type = "leave" };
        public static OnlineMessage Start() => new() { type = "start" };
        public static OnlineMessage Hole(int hole, int strokes) => new() { type = "hole", hole = hole, strokes = strokes };
    }

    /// A seat in a room: who, how they look, whether their phone is connected, and their card
    /// (strokes per hole, 0 for a hole not played yet).
    [Serializable]
    public sealed class OnlinePlayer
    {
        public string id = "";
        public string name = "";
        public int body;
        public int kit;
        public int shirt;
        public bool connected;
        public int[] strokes = Array.Empty<int>();
    }
}
