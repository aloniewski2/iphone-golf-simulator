// Plays one serve and one perfectly timed return through the REAL host rules
// (NetworkTennisMatch + TennisRules) while adding the delays a real match has.
//
//   screen delay  : the TV/AirPlay picture is this late, so the player swings this late
//   network delay : one-way time from the guest's phone to the host's phone
//   clock error   : how far the guest's idea of "host time" is off (from the 1/second ping)
//
// "credit" = the proposed fix: the guest adds its measured screen delay to the swing's age.
using System;
using GolfArcade.Multiplayer;
using GolfArcade.Tennis;

static class Program {
    static NetworkInput In(NetworkTennisMatch m, string action, long id, double time, double age) =>
        new NetworkInput { action = action, eventID = id, time = time, age = age, point = m.State.point, contact = m.State.contact, power = .7f, aim = 0 };

    static void Step(NetworkTennisMatch m, double seconds) {
        int n = (int)Math.Ceiling(seconds * 120 - 1e-9);
        for (int i = 0; i < n; i++) m.Step(1.0 / 120);
    }

    // Host serves from seat 0; the ball is then in flight toward the receiver (seat 1).
    static NetworkTennisMatch ServeToRally() {
        var m = new NetworkTennisMatch(1, 3);
        Step(m, .25);
        m.Input(0, In(m, "toss", 1, m.State.time, 0), m.State.time);
        Step(m, .75);
        m.Input(0, In(m, "swing", 2, m.State.time, 0), m.State.time);
        return m;
    }

    // Host time at which the served ball (after its bounce) reaches the receiver's racket plane.
    static double BallReachesReceiver() {
        var m = ServeToRally();
        for (int i = 0; i < 2400; i++) {
            m.Step(1.0 / 120);
            if (m.State.phase != "rally") return -1;
            if (m.State.bounces >= 1 && m.State.ball.z >= 11.55f) return m.State.time;
        }
        return -1;
    }

    // The player watches a picture `screen` seconds late and swings on the cue they SEE, so the
    // real swing is `screen` seconds after the ideal moment. The host hears of it `network` later.
    static string Screen(double screen, double network, double sensor, bool credit, double tBall) {
        var m = ServeToRally();
        double swingAt = tBall - TennisRules.SweetTime + screen;
        double arrives = swingAt + network;
        while (m.State.time < arrives - 1e-9) m.Step(1.0 / 120);
        long before = m.State.contact;
        bool accepted = m.Input(1, In(m, "swing", 3, swingAt, sensor + (credit ? screen : 0)), m.State.time);
        for (int i = 0; i < 240 && m.State.contact == before && m.State.phase == "rally"; i++) m.Step(1.0 / 120);
        if (m.State.contact > before) return m.State.reason;
        return accepted ? "MISS" : "REJECTED";
    }

    static string Clock(double error, double network, double tBall) {
        var m = ServeToRally();
        double swingAt = tBall - TennisRules.SweetTime;
        double arrives = swingAt + network;
        while (m.State.time < arrives - 1e-9) m.Step(1.0 / 120);
        long before = m.State.contact;
        bool accepted = m.Input(1, In(m, "swing", 3, swingAt + error, .03), m.State.time);
        for (int i = 0; i < 240 && m.State.contact == before && m.State.phase == "rally"; i++) m.Step(1.0 / 120);
        if (m.State.contact > before) return m.State.reason;
        return accepted ? "MISS" : "DROPPED";
    }

    static void Table(string title, double[] rows, string rowName, double[] cols, Func<double, double, string> cell) {
        Console.WriteLine(title);
        Console.Write($"{rowName,-34}");
        foreach (var c in cols) Console.Write($"{c * 1000,9:F0} ms");
        Console.WriteLine();
        foreach (var r in rows) {
            Console.Write($"{r * 1000,+7:F0} ms{"",-26}");
            foreach (var c in cols) Console.Write($"{cell(r, c),12}");
            Console.WriteLine();
        }
        Console.WriteLine();
    }

    static void Main(string[] args) {
        string mode = args.Length > 0 ? args[0] : "current";
        double tBall = BallReachesReceiver();
        if (tBall < 0) { Console.WriteLine("Could not set up the rally."); Environment.Exit(1); }
        if (mode == "clock") {
            Table("CLOCK ERROR with today's rules (a perfectly timed swing; + = guest thinks the host is ahead)",
                new[] { -.10, -.05, 0, .03, .05, .07, .10, .15 }, "clock error \\ one-way network",
                new[] { .02, .05, .10 }, (e, n) => Clock(e, n, tBall));
            return;
        }
        bool credit = mode == "proposed";
        Table(credit ? "PROPOSED: screen delay credited, rewind limit 0.40 s (perfectly timed swing on the cue the player sees)"
                     : "TODAY: screen delay ignored, rewind limit 0.15 s (perfectly timed swing on the cue the player sees)",
            new[] { 0, .10, .20, .30, .40 }, "screen delay \\ one-way network",
            new[] { .02, .05, .10, .15, .20 }, (s, n) => Screen(s, n, .03, credit, tBall));
    }
}
