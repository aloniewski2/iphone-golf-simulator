// Plays one serve and one perfectly timed return through the REAL host rules (NetworkTennisMatch + TennisRules)
// while adding the delays a real match has, using the two messages a phone really sends: "swing started"
// (beginSwing) and "swing confirmed" (swing). Built twice by run.sh: once from an old commit (BEFORE: the phone
// credits no screen delay) and once from the working tree (AFTER: it credits the screen delay).
//
//   screen delay  : the TV/AirPlay picture is this late, so the player swings this late
//   network delay : one-way time from the guest's phone to the host's phone
//   clock error   : how far the guest's idea of "host time" is off (from the 1/second ping)
using System;
using GolfArcade.Multiplayer;
using GolfArcade.Tennis;

static class Program {
    const double Sensor = .03;       // the phone hands a motion to the game this long after it happens
    const double ConfirmGap = .18;   // swing start -> swing confirmed

#if AFTER
    static double OnsetAge(double screen) => NetworkTuning.OnsetAge(screen);
    static double SwingAge(double screen) => NetworkTuning.SwingAge(Sensor, screen);
#else
    static double OnsetAge(double screen) => 0;                     // before: no screen credit
    static double SwingAge(double screen) => Math.Min(.25, Sensor); // before: sensor age only
#endif

    static NetworkInput In(NetworkTennisMatch m, string action, long id, double time, double age) =>
        new NetworkInput { action = action, eventID = id, time = time, age = age, point = m.State.point, contact = m.State.contact, power = .7f, aim = 0 };

    static void RunTo(NetworkTennisMatch m, double t) { while (m.State.time < t - 1e-9) m.Step(1.0 / 120); }

    static NetworkTennisMatch ServeToRally() {
        var m = new NetworkTennisMatch(1, 3);
        RunTo(m, .25);
        m.Input(0, In(m, "toss", 1, m.State.time, 0), m.State.time);
        RunTo(m, m.State.time + .75);
        m.Input(0, In(m, "swing", 2, m.State.time, 0), m.State.time);
        return m;
    }

    static double BallReachesReceiver() {
        var m = ServeToRally();
        for (int i = 0; i < 2400; i++) {
            m.Step(1.0 / 120);
            if (m.State.phase != "rally") return -1;
            if (m.State.bounces >= 1 && m.State.ball.z >= 11.55f) return m.State.time;
        }
        return -1;
    }

    // Seconds the point stays alive after an unreturned serve passes the receiver, and who gets it.
    static string UnreturnedServe(double tBall) {
        var m = ServeToRally();
        RunTo(m, tBall);
        double alive = 0;
        while (m.State.phase == "rally" && alive < 3) { m.Step(1.0 / 120); alive += 1.0 / 120; }
        return $"an unreturned serve stays in play {alive * 1000:F0} ms after passing the receiver and the point goes to seat {m.State.winner} (0 = the server)";
    }

    static string Return(double screen, double network, double tBall) {
        var m = ServeToRally();
        double onset = tBall - TennisRules.SweetTime + screen;           // the player swings on the cue they SEE
        double confirm = onset + ConfirmGap;
        double beginStamp = onset + Sensor, swingStamp = confirm + Sensor;
        RunTo(m, beginStamp + network);
        bool began = m.Input(1, In(m, "beginSwing", 3, beginStamp, OnsetAge(screen)), m.State.time);
        RunTo(m, swingStamp + network);
        long before = m.State.contact;
        bool confirmed = m.Input(1, In(m, "swing", 4, swingStamp, SwingAge(screen)), m.State.time);
        for (int i = 0; i < 240 && m.State.contact == before && m.State.phase == "rally"; i++) m.Step(1.0 / 120);
        if (m.State.contact > before) return m.State.reason;
        return began || confirmed ? "MISS" : "REJECTED";
    }

    static string Clock(double error, double network, double tBall) {
        var m = ServeToRally();
        double onset = tBall - TennisRules.SweetTime, confirm = onset + ConfirmGap;
        double beginStamp = onset + Sensor + error, swingStamp = confirm + Sensor + error;
        RunTo(m, onset + Sensor + network);
        bool began = m.Input(1, In(m, "beginSwing", 3, beginStamp, 0), m.State.time);
        RunTo(m, confirm + Sensor + network);
        long before = m.State.contact;
        bool confirmed = m.Input(1, In(m, "swing", 4, swingStamp, Sensor), m.State.time);
        for (int i = 0; i < 240 && m.State.contact == before && m.State.phase == "rally"; i++) m.Step(1.0 / 120);
        if (m.State.contact > before) return m.State.reason;
        return !began && !confirmed ? "DROPPED" : "MISS";
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
        string mode = args.Length > 0 ? args[0] : "screen";
        double tBall = BallReachesReceiver();
        if (tBall < 0) { Console.WriteLine("Could not set up the rally."); Environment.Exit(1); }
        Console.WriteLine(UnreturnedServe(tBall));
        Console.WriteLine();
        if (mode == "clock") {
            Table("CLOCK ERROR (a perfectly timed swing; + = the guest thinks the host is ahead)",
                new[] { -.10, -.05, 0, .03, .05, .07, .10, .15 }, "clock error \\ one-way network",
                new[] { .02, .05, .10 }, (e, n) => Clock(e, n, tBall));
            return;
        }
        Table("SCREEN DELAY x NETWORK DELAY (a perfectly timed swing on the cue the player sees)",
            new[] { 0, .05, .10, .15, .20, .30, .40 }, "screen delay \\ one-way network",
            new[] { .02, .05, .10, .15, .20 }, (s, n) => Return(s, n, tBall));
    }
}
