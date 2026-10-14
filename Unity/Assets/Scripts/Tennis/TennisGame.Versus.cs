using System;
using System.Collections.Generic;
using GolfArcade.Game;
using GolfArcade.Multiplayer;
using UnityEngine;

namespace GolfArcade.Tennis
{
    /// One message between the two players' phones in a versus match (see TennisGame.Versus).
    [Serializable] public sealed class VersusMessage
    {
        /// pose · swing · confirm · cancel · toss · catch · hit · whiff · point · fault · ready · emote · skip
        public string k;
        /// The sender's seat, the points played when it was sent, and the shared clock it refers to.
        public int s; public long n; public double t;
        // pose: where the sender's player stands and moves (their own court frame), the take-back and the serve routine
        public float x, z, vx, vz, prep, pt, wind;
        public bool bh, serve, held, walk;
        // swing / confirm
        public int kind; public float power; public bool prov;
        // toss / hit: positions and velocities in the sender's court frame
        public Vector3 p, v, d;
        public float spin, curve, rest, acc;
        public bool srv, ult, sup, smash;
        public int grade;
        // point / fault: called by the phone the ball was coming to
        public bool toSender, win, ace;
        public string reason;
        // emote
        public int clip;
    }

    /// VERSUS: two people, one match, and each plays it exactly as single player plays — the same camera behind their own player, the
    /// same serve, strokes, contact, physics, HUD and presentation. The only change from single player is the far end of the court: the
    /// AI rival is switched off and the other person's player stands there instead, driven by what their phone does.
    ///
    /// Each phone simulates its own player (its swings are judged with no network delay, as in single player) and tells the other phone
    /// what that player does: a 30 Hz pose (position, run, take-back, serve routine) and events (a swing starting, the toss, each shot
    /// with its exact launch, a whiff, an emote). The receiving phone mirrors it onto its far end (x and z flip) and drives the rival's
    /// actor through the same calls the AI used, so every authored clip plays.
    ///
    /// The phone of the player the ball is coming to calls it (in, out, net, two bounces, a fault), as single player's rules do, and
    /// tells the other phone, which runs the same point presentation. A shot is replayed from the shared clock time it left the
    /// strings, so both phones have the ball in the same place at the same moment. The next point starts once both phones are ready.
    public sealed partial class TennisGame
    {
        /// A versus match: the rival is a real person on another phone.
        public bool Versus { get; private set; }
        /// This phone's seat (0 or 1).
        public int VersusSeat { get; private set; }
        /// The other player's name, for logs and tests.
        public string VersusRivalName { get; private set; }
        /// Messages sent and received (tests, diagnostics).
        public int VersusSent { get; private set; }
        public int VersusReceived { get; private set; }
        /// Points this phone has played (both phones count the same points).
        public long VersusPoints => versusPoint;
        /// The score sequence as this phone saw it (tests).
        public readonly List<string> VersusScoreLog = new();
        /// Point calls this phone made (it was the ball's receiver) and ones it took from the other phone.
        public int VersusCallsMade { get; private set; }
        public int VersusCallsTaken { get; private set; }
        /// Strokes the rival played on this phone, by kind (tests: every authored stroke reaches the far end).
        public readonly Dictionary<TennisActor.Stroke, int> VersusRivalStrokes = new();
        /// Where the rival's shot started on their phone vs their strings here (the remote launch; drawn smoothly from the strings).
        public readonly ContactGaps VersusRivalGaps = new();
        /// What a viewer sees at the rival's contact: how far the drawn ball had to jump to sit on their strings (a held contact), or
        /// how far from their strings it left (a shot that arrived before the ball reached them). Tests: mean under ~0.3 m.
        public readonly ContactGaps VersusRivalVisibleGaps = new();
        /// Times our shot waited on the rival's strings for their phone's shot, and how long in all.
        public int VersusHolds { get; private set; }
        /// One line per rival contact (tests): held or not, their stroke, the gaps, where the ball and they were.
        public readonly List<string> VersusContactLog = new();
        public float VersusHeldSeconds { get; private set; }
        /// The drawn ball eases from where it was (on the rival's strings) onto the shared flight: physics untouched, no visible jump.
        Vector3 versusBallOffset; float versusBallOffsetAt;
        const float VersusBlend = .15f;

        readonly Queue<VersusMessage> versusInbox = new();
        VersusMessage remotePose;
        long versusPoint, remoteReady = -2, localReadySent = -2;
        double readySince = -1, nextPoseSend;
        bool versusStarted;
        // the rival's serve: their toss (mirrored to this court), when it left the hand
        bool remoteTossed; Vector3 remoteTossOrigin, remoteTossDrift, remoteServeHeldAt; double remoteTossAt;
        // the rival's swing is about to meet our shot: the ball waits on their strings for their phone's shot (at most RemoteHoldMax)
        bool holdingForRemote, heldThisContact; double holdSince;
        /// How late the other phone's messages arrive (smoothed; seconds): the hold at the rival's strings never waits longer than a
        /// message takes (plus a beat), so a miss carries on at once.
        double peerDelay = .08;
        double RemoteHoldMax => Math.Max(.06, Math.Min(.25, peerDelay * 1.6 + .03));
        // calls made by this phone vs applied from the other
        bool applyingRemote, judgingOwnMiss, inFault;
        // our player's swing as last told to the other phone
        bool sentSwinging, sentProvisional; float sentSwingAge;
        bool hitQueued; string lastGradeLine;
        string[] rivalEmotes = TennisEmotes.Normalize(null);
        HeroTennisDriver.Clip? rivalIntroClip;
        float serveRoutine;

        double VersusClock => SportsMultiplayer.Instance ? SportsMultiplayer.Instance.SharedClock : Time.realtimeSinceStartupAsDouble;
        static Vector3 Mirror(Vector3 v) => new(-v.x, v.y, -v.z);

        /// Start a versus match (SportsMultiplayer, once the lobby's configuration is known). The local player keeps the locker look the
        /// launch gave it; the rival is rebuilt as the other person's hero in their colours.
        public void ConfigureVersus(NetworkConfiguration c)
        {
            Versus = true; networkConfigured = false;
            NativeControlled = true; AutoPlay = false; Drill = false; AimPractice = false;
            VersusSeat = Mathf.Clamp(c.Seat(c.localID), 0, 1);
            NetworkParticipant me = null, them = null;
            foreach (var p in c.participants) { if (p.seat == VersusSeat) me = p; else if (p.seat == 1 - VersusSeat) them = p; }
            VersusRivalName = them?.name ?? "Rival";
            rivalEmotes = TennisEmotes.Normalize(them?.loadout?.emotes);
            rivalIntroClip = null;   // (the rival emotes in their introduction only if their player picks one)
            if (them != null) RebuildRival(them);
            if (hud) { if (me != null) hud.PlayerName = me.name.ToUpperInvariant(); hud.OpponentName = VersusRivalName.ToUpperInvariant(); hud.EventLabel = VersusLabel(c.sets); }
            MatchSets = Mathf.Clamp(c.sets, 1, 3); MatchGames = Mathf.Clamp(c.games, 1, 6);
            // seat 0 serves first, as the lobby has always had it
            match = TennisMatch.New(VersusSeat == 0, MatchSets, MatchGames); ResetMatchStats(); matchReported = false; ResetMatchAbilities();
            Hits = 0; Misses = 0; LongestRally = 0;
            versusInbox.Clear(); remotePose = null; versusPoint = 0; remoteReady = -2; localReadySent = -2; readySince = -1;
            versusStarted = false; remoteTossed = false; holdingForRemote = heldThisContact = false; hitQueued = false;
            sentSwinging = sentProvisional = false; VersusScoreLog.Clear(); VersusRivalStrokes.Clear();
            VersusSent = VersusReceived = 0; VersusCallsMade = VersusCallsTaken = 0;
            if (presentation) presentation.Bill(TennisVenue.Title, "CHALLENGER");
            // one TV for both players: two single-player views side by side
            if (c.LocalView == NetworkConfiguration.ViewSplit) BuildVersusSplit(VersusRivalName);
            BeginPoint();
        }

        /// The scoreboard's title, in single player's exhibition form ("EXHIBITION  ·  ONE SET").
        static string VersusLabel(int sets) => sets <= 1 ? "VERSUS  ·  ONE SET" : $"VERSUS  ·  FIRST TO {sets} SETS";
        /// The other person's player at the far end: their sex, skin and kit, moving like a player (not the AI's style).
        void RebuildRival(NetworkParticipant them)
        {
            if (!Opponent) return;
            Vector3 p0 = Opponent.transform.position; Quaternion r0 = Opponent.transform.rotation;
            Destroy(Opponent.gameObject);
            Opponent = new GameObject("Opponent — " + them.name).AddComponent<TennisActor>();
            Opponent.transform.SetPositionAndRotation(p0, r0);
            Opponent.Build(them.female, PlayerSkinFor(them.female), them.left, PlayerBody(them.female));
            Opponent.Motion = TennisActor.Style.Player;
            if (fx) HookActorFx(Opponent);
            { var s = TennisLook.AddContactShadow(Opponent.transform, .55f, .45f); s.HeightOverride = 0; s.Surface = Opponent.transform; }
            TennisHeroSetup.AttachRival(this);
            DressNetworkHero(Opponent, them);
            if (replay) replay.Build(new[] { Player.transform, Opponent.transform }, ball, GameplayCamera, OnReplayPose);
        }

        /// The other phone's message (SportsMultiplayer's "peer" packets). Applied at the start of the next frame, in order.
        public void ReceiveVersus(string json)
        {
            if (!Versus) return;
            VersusMessage m; try { m = JsonUtility.FromJson<VersusMessage>(json); } catch { return; }
            if (m == null || string.IsNullOrEmpty(m.k) || m.s == VersusSeat) return;
            versusInbox.Enqueue(m);
        }

        void VersusSend(VersusMessage m, bool reliable = true)
        {
            var net = SportsMultiplayer.Instance; if (!Versus || !net) return;
            m.s = VersusSeat; m.n = versusPoint; if (m.t == 0) m.t = VersusClock;
            net.SendPeer(JsonUtility.ToJson(m), reliable); VersusSent++;
        }

        // ---------------------------------------------------------------- each frame

        /// Before the simulation: what the other phone said since the last frame.
        void VersusFrameStart()
        {
            int guard = 0;
            while (versusInbox.Count > 0 && guard++ < 64) ApplyVersus(versusInbox.Dequeue());
        }

        /// After the simulation: tell the other phone what our player did.
        void VersusFrameEnd()
        {
            if (!Player) return;
            // the swing as it happens on our player: started (with its kind), confirmed, called off
            bool swinging = Player.Swinging;
            if (swinging && (!sentSwinging || Player.SwingAge + 1e-4f < sentSwingAge))
            {
                var kind = Player.Kind;
                VersusSend(new VersusMessage { k = "swing", kind = (int)kind, bh = Player.Backhand, power = Player.Power, prov = Player.Provisional,
                    t = VersusClock - Player.SwingAge / Mathf.Max(.01f, SpeedScale) });
                sentProvisional = Player.Provisional;
            }
            else if (swinging && sentProvisional && !Player.Provisional) { VersusSend(new VersusMessage { k = "confirm", power = Player.Power }); sentProvisional = false; }
            else if (!swinging && sentSwinging && sentProvisional) VersusSend(new VersusMessage { k = "cancel" });
            sentSwinging = swinging; sentSwingAge = swinging ? Player.SwingAge : 0;
            if (hitQueued)
            {
                hitQueued = false;
                // the ball leaves the strings once the hit-stop is over (the simulation stands still until then)
                VersusSend(new VersusMessage { k = "hit", reason = lastGradeLine, p = BallPosition, v = BallVelocity, spin = BallSpin, curve = ballCurve, rest = bounceRestitution,
                    srv = serveInFlight, ult = ultimateBall, sup = LastWasSupercharged, grade = (int)LastGrade, smash = Player.Kind == TennisActor.Stroke.Smash,
                    power = Player.Power, t = VersusClock + Mathf.Max(0, hitStop) });
            }
            if (VersusClock >= nextPoseSend)
            {
                nextPoseSend = VersusClock + 1 / 30.0;
                var at = Player.transform.position;
                VersusSend(new VersusMessage { k = "pose", x = at.x, z = at.z, vx = LateralSpeed, vz = moveVelocity.y, prep = Player.PrepareAmount, bh = Player.PrepareBackhand,
                    serve = Player.PrepareServe, held = Flow == Phase.PlayerServeHold, pt = phaseTimer, wind = windup, walk = serveWalkSpeed > .2f }, false);
            }
        }

        /// Both phones have finished the intro (or the last point's beat) before a serve: false holds play. Never longer than 8 s.
        bool VersusMayPlay(long point)
        {
            if (!Versus) return true;
            if (localReadySent < point) { localReadySent = point; readySince = VersusClock; VersusSend(new VersusMessage { k = "ready", n = point }); }
            if (remoteReady >= point) return true;
            if (VersusClock - readySince > 8) { Debug.LogWarning($"[Versus] the other phone never said ready for point {point}; playing on"); return true; }
            return false;
        }

        /// The intro: play starts when both phones have finished theirs.
        bool VersusStartGate()
        {
            if (!Versus || versusStarted) return true;
            if (!SportsMultiplayer.Instance || !SportsMultiplayer.Instance.Running) return false;
            if (VersusMayPlayStart()) { versusStarted = true; return true; }
            return false;
        }
        bool VersusMayPlayStart()
        {
            const long start = -1;
            if (localReadySent < start) { localReadySent = start; readySince = VersusClock; VersusSend(new VersusMessage { k = "ready", n = start }); }
            return remoteReady >= start || VersusClock - readySince > 8;
        }

        // ---------------------------------------------------------------- the other phone

        void ApplyVersus(VersusMessage m)
        {
            VersusReceived++;
            double age = VersusClock - m.t;
            if (m.k != "hit" && age >= 0 && age < 1) peerDelay += (age - peerDelay) * .05;   // (a hit's time is when the ball leaves, ahead of sending)
            switch (m.k)
            {
                case "pose": if (remotePose == null || m.t >= remotePose.t) remotePose = m; break;
                case "ready": remoteReady = Math.Max(remoteReady, m.n); break;
                case "swing": RemoteSwing(m); break;
                case "confirm": if (Opponent.Swinging) Opponent.Confirm(m.power); break;
                case "cancel": if (Opponent.Swinging && Opponent.Provisional) Opponent.CancelSwing(); break;
                case "toss":
                    if (Flow != Phase.OpponentServe) break;
                    remoteTossed = true; remoteTossOrigin = Mirror(m.p); remoteTossDrift = Mirror(m.d); remoteTossAt = m.t; heldThisContact = holdingForRemote = false;
                    break;
                case "catch": remoteTossed = false; break;
                case "hit": RemoteHit(m); break;
                case "whiff":
                    if (fx) fx.Whiff(Opponent.transform.position, Opponent.SweetSpot.position);
                    if (sounds) sounds.Gasp();
                    break;
                case "point": RemotePoint(m); break;
                case "fault": RemoteFault(m); break;
                // the other phone asked to skip its intro's flyover (or saw it through): `kind` is its intro number
                case "skip": if (presentation) presentation.RemoteSkip(m.kind); break;
                case "emote":
                {
                    var driver = Opponent ? Opponent.GetComponentInChildren<HeroTennisDriver>() : null;
                    var clip = (HeroTennisDriver.Clip)m.clip;
                    if (IntroPlaying) rivalIntroClip = clip;
                    else if (driver) driver.PlayEmote(clip);
                    break;
                }
            }
        }

        /// The rival starts a stroke: the same clip their own screen shows, caught up for the time the message took, and paced so their
        /// strings meet our ball as the AI's did.
        void RemoteSwing(VersusMessage m)
        {
            var kind = (TennisActor.Stroke)m.kind;
            if (Opponent.Swinging && !Opponent.Provisional) return;
            if (Opponent.Swinging) Opponent.CancelSwing();
            Opponent.Swing(Mathf.Max(.1f, m.power), m.bh, kind, m.prov);
            VersusRivalStrokes[kind] = VersusRivalStrokes.TryGetValue(kind, out int c) ? c + 1 : 1;
            float late = (float)Math.Max(0, VersusClock - m.t) * SpeedScale;
            if (late > 0) Opponent.AdvanceSwing(Mathf.Max(0, Mathf.Min(late, Opponent.TimeToContact - .02f)));
            if (kind == TennisActor.Stroke.Serve) { if (remoteTossed) PlanRemoteServeContact(); return; }
            if (Flow == Phase.Rally && !incoming && BallVelocity.z > .5f)
            {
                float natural = Opponent.TimeToContact;
                float lead = PlanContact(Opponent, natural * .6f, Mathf.Min(natural * 1.6f, .45f), out Vector3 meet);
                Opponent.PaceToContact(lead); Opponent.GuideContact(meet, lead);
            }
        }

        /// Where the rival's toss is, `t` game seconds after it left the hand (their own toss rule, on this court).
        Vector3 RemoteToss(float t) => remoteTossOrigin + remoteTossDrift * t + new Vector3(0, 9.81f * TennisRules.ServeApex * t - 4.905f * t * t, 0);
        float RemoteTossAge => (float)Math.Max(0, (VersusClock - remoteTossAt) * TennisRules.ServePace);

        void PlanRemoteServeContact()
        {
            float now = RemoteTossAge, best = float.MaxValue, lead = TennisBall.Step;
            Vector3 aim = Opponent.AuthoredContact;
            for (float t = TennisBall.Step; t <= 1.6f; t += TennisBall.Step)
            {
                float d = (RemoteToss(now + t) - aim).sqrMagnitude;
                if (d < best) { best = d; lead = t; }
            }
            Opponent.PaceToContact(lead, .05f);
            Opponent.GuideContact(RemoteToss(now + lead), lead);
        }

        /// The rival's shot (or serve) left their strings: the ball as their phone launched it, on this court, flown on to now.
        void RemoteHit(VersusMessage m)
        {
            if (Flow == Phase.PointOver || Flow == Phase.MatchOver) return;
            bool held = holdingForRemote || heldThisContact;
            if (holdingForRemote) VersusHeldSeconds += (float)(VersusClock - holdSince);
            holdingForRemote = false; heldThisContact = false;
            Vector3 strings = Opponent.SweetSpot ? Opponent.SweetSpot.position : BallPosition;
            Vector3 start = Mirror(m.p);
            Vector3 shown = ball ? ball.position : BallPosition;
            VersusRivalGaps.Add(Vector3.Distance(strings, start));
            if (!held) VersusRivalVisibleGaps.Add(Vector3.Distance(strings, shown));
            if (VersusContactLog.Count < 400)
                VersusContactLog.Add($"{(held ? "HELD" : "FREE")} srv={m.srv} kind={(Opponent.Swinging ? Opponent.Kind.ToString() : "-")} swinging={Opponent.Swinging} age={(Opponent.Swinging ? Opponent.ContactAge - TennisRules.SweetTime : 0):+0.00;-0.00} " +
                    $"shown-strings={Vector3.Distance(strings, shown):0.00} launch-strings={Vector3.Distance(strings, start):0.00} ballZ={shown.z:0.0} oppZ={Opponent.transform.position.z:0.0} dx={start.x - Opponent.transform.position.x:+0.0;-0.0} late={(VersusClock - m.t) * 1000:0}ms");
            if (m.srv) { remoteTossed = false; Opponent.ReachTossHand(Vector3.zero, 0); }
            int shots = RallyShots;
            InjectBall(start, Mirror(m.v));
            BallSpin = m.spin; ballCurve = -m.curve; bounceRestitution = m.rest;
            serveInFlight = m.srv; serveFromNearSide = false; opponentShot = default; sinceStrike = 0; opponentMissReason = null;
            RallyShots = m.srv ? 1 : shots + 1;
            ultimateBall = m.ult;
            if (ballTrail) { ballTrail.startWidth = m.ult ? .45f : .12f; ballTrail.startColor = Color.white; }
            if (rightHud && !m.srv && !string.IsNullOrEmpty(m.reason)) rightHud.ShowGrade((Timing)m.grade, m.sup, m.reason);
            fx.OpponentContact(start, m.ult); sounds.RivalHit(m.srv ? .8f : Mathf.Lerp(.5f, .9f, m.power)); contactHitter = Opponent; OpponentStruck?.Invoke();
            if (juice) juice.EndLiveShot();
            Player.SplitStep();
            Feedback = m.srv ? "Opponent serves" : "Incoming";
            // the ball has been flying on the other phone since it left their strings
            float ahead = Mathf.Min(.6f, (float)Math.Max(0, VersusClock - m.t) * SpeedScale);
            for (; ahead >= TennisBall.Step && Flow == Phase.Rally; ahead -= TennisBall.Step) SimulateBall(TennisBall.Step);
            previousBall = BallPosition;
            versusBallOffset = shown - BallPosition; versusBallOffsetAt = Time.unscaledTime;
            if (versusBallOffset.sqrMagnitude > 25) versusBallOffset = Vector3.zero;   // a ball from somewhere else entirely: no slide across the court
        }

        /// The other phone called a ball that was coming to its player.
        void RemotePoint(VersusMessage m)
        {
            if (Flow == Phase.PointOver || Flow == Phase.MatchOver) return;
            bool toPlayer = !m.toSender;
            Feedback = ReasonHere(m.reason, toPlayer, m.ace);
            applyingRemote = true; VersusCallsTaken++;
            try { AwardPoint(toPlayer, toPlayer && m.win); } finally { applyingRemote = false; }
        }

        /// The other phone called a serve fault (theirs on our serve, or their own missed toss).
        void RemoteFault(VersusMessage m)
        {
            if (Flow == Phase.PointOver || Flow == Phase.MatchOver) return;
            Feedback = m.reason;
            applyingRemote = true; VersusCallsTaken++;
            // the server was the other player when they missed their own toss; otherwise it was this phone's player
            try { Fault(!m.toSender); } finally { applyingRemote = false; }
            if (m.toSender) { remoteTossed = false; Opponent.ReachTossHand(Vector3.zero, 0); }
        }

        /// A call's words from this side of the net ("Opponent nets it" over there is our "NET" here).
        static string ReasonHere(string reason, bool toPlayer, bool ace)
        {
            if (string.IsNullOrEmpty(reason)) return toPlayer ? "WINNER" : "POINT";
            if (reason.StartsWith("Opponent nets")) return "NET";
            if (toPlayer && ace) return "ACE";
            if (toPlayer && (reason.StartsWith("DOUBLE BOUNCE") || reason.StartsWith("MISSED") || reason.StartsWith("WHIFF"))) return "WINNER";
            return reason;
        }

        /// May this phone call the ball now? Only the ball's receiver calls it, plus a server who missed their own toss.
        bool VersusJudges => !Versus || applyingRemote || incoming || judgingOwnMiss;

        /// After AwardPoint ran here as the ball's receiver: tell the other phone.
        void VersusPointCalled(bool toPlayer, bool winner, string call)
        {
            if (!Versus) return;
            versusPoint++;
            VersusScoreLog.Add($"{match.PlayerGames}-{match.OpponentGames} {match.Scoreboard}");
            if (applyingRemote || inFault) return;
            VersusCallsMade++;
            // the ball beat this phone's player: the other player's winner (an ace off a serve that was never returned)
            bool beaten = !toPlayer && (call == "DOUBLE BOUNCE" || (call != null && call.StartsWith("MISSED")));
            VersusSend(new VersusMessage { k = "point", toSender = toPlayer, win = beaten, ace = beaten && RallyShots <= 1, reason = call, n = versusPoint - 1 });
        }

        void VersusFaultCalled(bool nearServer)
        {
            if (!Versus || applyingRemote) return;
            VersusCallsMade++;
            // toSender: the fault is the sender's own (they missed their toss), so the other phone applies it to its rival
            VersusSend(new VersusMessage { k = "fault", toSender = nearServer, reason = Feedback });
        }

        /// The rival's half of a frame: where they are (their pose, mirrored), and nothing the AI would have decided.
        void UpdateRemoteOpponent(float dt)
        {
            sinceStrike += dt;
            var pose = remotePose;
            if (pose == null) { Opponent.Advance(dt, 0); return; }
            float age = Mathf.Clamp((float)(VersusClock - pose.t), 0, .22f);
            // their court frame: +x their right, +z toward the net; here they face -z, so both flip
            Vector3 target = Mirror(new Vector3(pose.x + pose.vx * age, .035f, pose.z + pose.vz * age));
            // Our shot reaching them: their phone has them where their strings meet it (honest contact there), but their pose is a message
            // old. As the ball arrives, line them up the way their own game does, so the racket here meets the ball here too.
            if (Flow == Phase.Rally && !incoming && BallVelocity.z > .5f && Opponent.SweetSpot)
            {
                float toArrive = (Opponent.transform.position.z - .65f - BallPosition.z) / BallVelocity.z;
                if (toArrive > -.05f && toArrive < .55f)
                {
                    Vector3 arrive = PredictBall(Mathf.Max(0, toArrive));
                    bool backhand = Opponent.Swinging ? Opponent.Backhand : (arrive.x >= Opponent.transform.position.x) != Opponent.LeftHanded;
                    var kind = Opponent.Swinging ? Opponent.Kind : TennisActor.Stroke.Drive;
                    float lineX = arrive.x - (Opponent.ContactPoint(kind, backhand) - Opponent.transform.position).x;
                    float w = 1 - Mathf.Clamp01(toArrive / .55f);
                    if (Mathf.Abs(lineX - target.x) < 1.6f) target.x = Mathf.Lerp(target.x, lineX, w * w * (3 - 2 * w));
                }
            }
            Vector3 current = Opponent.transform.position;
            Vector3 next = (target - current).sqrMagnitude > 9 ? target : Vector3.Lerp(current, target, 1 - Mathf.Exp(-dt * 30f));
            next.y = .035f;
            Opponent.transform.position = next;
            Opponent.Advance(dt, pose.vx, pose.vz);
        }

        /// The rival's take-back as the ball comes to them (their own preparation, told by their phone).
        void UpdateRemoteAttention()
        {
            var pose = remotePose;
            if (Flow != Phase.Rally || Opponent.Swinging || pose == null) { if (Flow == Phase.Rally && !Opponent.Swinging) Opponent.Prepare(0, false); return; }
            Opponent.Prepare(pose.prep, pose.bh);
        }

        /// The rival serving: bouncing the ball and winding up as their phone says, then their toss (one flight both phones share).
        void RemoteServeStep(float dt)
        {
            serveTimer += dt;
            previousBall = BallPosition;
            var pose = remotePose;
            if (!remoteTossed)
            {
                float windStart = TennisRules.ServeTossDelay - TennisServeRoutine.WindUp;
                float routine = 0, prepare = 0;
                if (pose != null && pose.held)
                {
                    float age = Mathf.Clamp((float)(VersusClock - pose.t), 0, .2f) * TennisRules.ServePace;
                    // on a shared TV the other player times their toss on the meter under their own feet, as in single player
                    if (VersusSplit && tossMeter) { if (pose.wind < 0 && !pose.walk) tossMeter.RunAt(Opponent.transform.position, pose.pt + age); else if (tossMeter.gameObject.activeSelf) tossMeter.Hide(); }
                    if (pose.wind < 0)
                    {
                        float loop = windStart + .6f;
                        routine = pose.walk ? 0 : Mathf.Min(Mathf.Repeat(pose.pt + age, loop), windStart - .001f);
                    }
                    else
                    {
                        float wind = pose.wind + age;
                        routine = windStart + TennisServeRoutine.WindUp * Mathf.Clamp01(wind / TennisRules.ServeWindUp);
                        prepare = Mathf.Clamp01(wind / TennisRules.ServeWindUp) * .4f;
                    }
                }
                TennisServeRoutine.Pose(Opponent, routine, out Vector3 held, out Vector3 palm);
                BallPosition = held; Opponent.ReachTossHand(palm, 1); Opponent.Prepare(prepare, false, true);
                return;
            }
            float tossAge = RemoteTossAge;
            if (VersusSplit && tossMeter && tossMeter.gameObject.activeSelf) tossMeter.Hide();
            // their serve's racket is at the ball: it waits on the strings for their phone's serve (as a rally shot does)
            if (Opponent.Swinging && Opponent.Kind == TennisActor.Stroke.Serve && !Opponent.Provisional && Opponent.SweetSpot
                && Opponent.ContactAge >= TennisRules.SweetTime - .02f && (!heldThisContact || holdingForRemote))
            {
                if (!heldThisContact)
                {
                    heldThisContact = holdingForRemote = true; holdSince = VersusClock; VersusHolds++;
                    Vector3 shown = ball ? ball.position : BallPosition, strings = Opponent.SweetSpot.position + Vector3.back * (TennisRules.BallRadius * 1.2f);
                    VersusRivalVisibleGaps.Add(Vector3.Distance(shown, strings));
                    remoteServeHeldAt = strings; versusBallOffset = shown - strings; versusBallOffsetAt = Time.unscaledTime;
                }
                if (VersusClock - holdSince <= RemoteHoldMax) { BallPosition = previousBall = remoteServeHeldAt; Opponent.ReachTossHand(Vector3.zero, 0); return; }
                holdingForRemote = false;
            }
            BallPosition = RemoteToss(tossAge);
            Opponent.ReachTossHand(TennisServeRoutine.TossPalm(Opponent, tossAge), Opponent.Swinging ? 0 : 1);
            if (!Opponent.Swinging) Opponent.Prepare(Mathf.Clamp01(.4f + .6f * tossAge / TennisRules.ServeApex), false, true);
            // their phone says when it is caught; if that is lost, put the ball back in the hand anyway
            if (tossAge > TennisRules.ServeCatch + 1f) remoteTossed = false;
        }

        /// Our shot has reached the rival and their swing is at contact: the ball waits on their strings for their phone's shot
        /// (a beat no longer than a hit-stop). True while it waits.
        bool VersusHoldForRival()
        {
            if (!Versus || incoming || Flow != Phase.Rally) return false;
            if (holdingForRemote)
            {
                if (VersusClock - holdSince <= RemoteHoldMax) return true;
                holdingForRemote = false;   // their phone never sent a shot: they missed, and their call follows
                return false;
            }
            // a stroke at its contact moment with the ball at the racket, or the ball already at their hitting line while they swing
            // (their swing's message can land after the ball does): what their phone is about to send back as a hit
            if (heldThisContact || !Opponent.Swinging || !Opponent.SweetSpot || BallVelocity.z <= .5f) return false;
            float age = Opponent.ContactAge - TennisRules.SweetTime;
            Vector3 onStrings = Opponent.SweetSpot.position + Vector3.back * (TennisRules.BallRadius * 1.2f);
            float gap = Vector3.Distance(onStrings, BallPosition);
            bool atContact = !Opponent.Provisional && age >= -.05f && age <= .14f && gap <= 1.2f;
            bool atLine = BallPosition.z >= Opponent.transform.position.z - .65f && Mathf.Abs(BallPosition.x - Opponent.transform.position.x) < 2.4f && gap <= 2f;
            if (!atContact && !atLine) return false;
            holdingForRemote = true; heldThisContact = true; holdSince = VersusClock; VersusHolds++;
            VersusRivalVisibleGaps.Add(gap);
            // the ball eases onto the strings it is already beside (never pulled across the court onto them)
            if (gap <= 2f)
            {
                Vector3 shown = ball ? ball.position : BallPosition;
                BallPosition = previousBall = onStrings;
                versusBallOffset = shown - onStrings; versusBallOffsetAt = Time.unscaledTime;
            }
            contactHitter = Opponent;
            return true;
        }

        /// A paused player (their phone's pause, or a dropped link) holds both phones.
        bool VersusSuspended => SportsMultiplayer.Instance && SportsMultiplayer.Instance.Suspended;
        /// The match is decided: the host's phone reports it (the phones' lobbies show the result).
        void SportsMultiplayer_VersusResult() { if (SportsMultiplayer.Instance) SportsMultiplayer.Instance.VersusResult(VersusSeat, match.PlayerWonMatch, match.FinalScore); }
        /// The lobby started the match: both phones play the intro from the top (the first serve waits for both).
        public void BeginVersusIntro() { versusStarted = false; if (presentation) presentation.Restart(); }

        /// The drawn ball's ease onto the shared flight after the rival's shot (UpdateBallVisual).
        Vector3 VersusBallOffset()
        {
            if (!Versus || versusBallOffset.sqrMagnitude < 1e-6f) return Vector3.zero;
            float k = (Time.unscaledTime - versusBallOffsetAt) / VersusBlend;
            if (k >= 1) { versusBallOffset = Vector3.zero; return Vector3.zero; }
            return versusBallOffset * (1 - k * k * (3 - 2 * k));
        }

        /// This phone asks to skip the intro's flyover (the presentation's intro number `round`): both phones must.
        public void VersusSkipVote(int round) => VersusSend(new VersusMessage { k = "skip", kind = round });
        /// Versus: the emote the rival's introduction plays (the other player's pick; null until they make one).
        public HeroTennisDriver.Clip? VersusRivalIntroClip => Versus ? rivalIntroClip : null;
    }
}
