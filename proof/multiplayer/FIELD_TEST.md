# Field test — multiplayer on real phones

For the first session with real devices. Everything here was built and checked in a sandbox only; this is how you find out what holds up. Each test says what to do, what to write down, and what "pass" means. Fill in the result tables at the bottom and commit this file (or paste it into the gate log, `GATE_RESULTS.md`, as PASS / FAIL lines).

The plan behind it is `PLAN_Multiplayer_OnlineLocal.md`; the open questions are the **NOT RUN** lines in `GATE_RESULTS.md`.

## What you need

- Two iPhones (three or four for the golf rounds), the oldest one you support among them, on the same Wi-Fi for the Nearby tests.
- One TV with AirPlay (Apple TV or a TV with AirPlay), and if you can, a Mac with AirPlay Receiver or an HDMI adapter for test 8.
- Game Center accounts on two phones for the online tests, and a second network for one of them (cellular is fine).
- A Mac with Xcode for the build and for pulling the log.

## Before you start (once)

1. **Run the Swift tests on the Mac** and fix anything red first: `xcodebuild test -project GolfArcade.xcodeproj -scheme GolfArcade -only-testing:GolfArcadeTests -destination 'platform=iOS Simulator,name=iPhone 16 Pro'`. These cover the lobby, calibration, roles, connection check and menus and have **never been run**.
2. **Run the Unity tests** with `Unity/Tools/check.sh`. This also compiles the new split-screen and compact-update code for the first time. If the split camera copy fails to compile on a URP property name, delete that line (see gate G3.5).
3. **Install on each phone** with `SPORTS_TEAM=… SPORTS_DEVICE=… Unity/Tools/install-phone.sh` (after exporting Unity: *Golf Arcade → Build iOS Xcode Project*).
4. All phones must run the **same build** (protocol version 4); a phone on another build says "incompatible".

## Getting the numbers off a phone

The app writes a text log, `SportsDiagnostics.log`, in its Documents folder. Nothing is uploaded. In Xcode: *Window → Devices and Simulators →* the phone *→ GolfArcade → ⋯ → Download Container…*, then *Show Package Contents → AppData → Documents*. Two kinds of line matter:

```
multiplayer tennis owner: rtt median 38 ms, p95 61 ms, jitter 9 ms, ping loss 0.4% (… answered), quiet pauses 0 (resumed 0, dropped 0), largest packet 712 bytes, unreliable refused 0
unity network stats: {"role":"host","compact":true,"stampRejects":0,"maxSnapshotGapMs":58}
```

`rtt` is the round trip to the other phone. `quiet pauses` is how often the match froze because a phone went silent. `unreliable refused` is how often Apple refused a message as too big. `compact` says which update form the host used. `stampRejects` is how many swings the host threw out for an impossible timestamp (clocks out of step). `maxSnapshotGapMs` is the longest gap between the host's updates.

---

## Test 1 — Two phones, one TV, Nearby (the main feature)

1. Phone A: *Play → Local → Tennis · Two Phones*. Phone B: *Play → Local → Join a Friend* and pick A's lobby.
2. Only phone A is connected to the TV by AirPlay. Phone B has no screen connected.
3. Both phones should go through *loading → point at the TV → Ready → "waiting for …"*, then the match starts without anyone pressing Start.

Write down: time from "friend joined" to the first serve; anything either phone says that is confusing; what the TV shows during setup.

Pass if:
- The TV shows **both players, one in each half**, each player at the bottom of their half with their name, the whole court and both players visible, nothing blank or stretched, the grandstand crowd in both halves. The divider line and the two names are **on the TV, not on the phone's own screen**.
- Each phone's own score line is from **its own player's side**: on the controller of the player in the right-hand half, "YOUR SERVE" appears when *that* player serves and the games read with their games first.
- **Both players can serve, receive and swing**, and each phone's serve/receive prompts appear on the right phone. The toss meter shows under the server, turned to read left to right from that player's half (check it with each player serving).
- Phone B (no TV) does not get hot in ten minutes and is still showing its racket controller.
- The ball and both players move smoothly and a swing that looks on time on the TV scores as on time.
- Lock phone B's screen / switch apps for 5 seconds: the match pauses, then recovers; a player who returns is asked to aim again.

Then repeat with the TV on phone B instead, and with **no TV at all**: the Start button must say "Connect one phone to a TV (AirPlay) to play".

## Test 2 — Two phones, two TVs (nothing should have changed)

Each phone has its own TV (or a Mac as the screen). Each player should see their own end of the court in front, as before this work.

## Test 3 — Pass the phone (golf)

*Play → Local → Golf · Pass the Phone → 2 players*, then again with 4. The round must start at once (TV connected first), a "pass the phone" card must appear between turns, and the scorecard must have a row per player. If no TV is connected it must stop in the lobby and say how to connect one.

## Test 4 — Online tennis over Game Center

Two phones, different networks if possible (Wi-Fi ↔ cellular is the useful case). Play three points each way.

Write down: the log lines from both phones afterwards (rtt median / p95 / jitter / loss, quiet pauses), and how it **felt**: did your swing register when you swung? Did the opponent's ball ever jump?

Pass if: `quiet pauses` is 0 or 1 per match on a normal connection and swings that look on time register as on time.

## Test 5 — The connection check

Put one phone on a bad link (*Settings → Developer → Network Link Conditioner →* "Very Bad Network" or "3G", then *Play → Local → Tennis · Two Phones*).

Expected: both phones say "Waiting for a steady connection…"; after about 8 seconds everyone is told which connection is weak and the **owner** gets a **Play anyway** button. With the link conditioner off the match must start by itself about 3 seconds after setup.

Write down the rtt the log shows for a connection you consider good, fair and bad. The thresholds (Good ≤ 80 ms, Fair ≤ 150 ms, plus jitter and loss) are guesses; adjust `MultiplayerTuning.linkFair*` to the numbers you see.

## Test 6 — Split-screen performance (the oldest phone that can host the TV)

Play ten minutes on the phone that has the TV. In Xcode run the app and watch the FPS gauge (*Debug navigator → FPS*), or use Instruments → Core Animation.

Pass if: average ≥ 55 fps, 1% low ≥ 45, and the phone is not hot or throttling after ten minutes. If it fails: set `TennisGame.SplitScreen = false` to compare with the single shared view, and see plan 3e (a lower quality level for split screen).

## Test 7 — Update size (does Apple accept the small updates?)

Play a normal online match and read `largest packet` and `unreliable refused` in the log.

- `largest packet` should be about 0.5 KB for tennis and `unreliable refused` should be **0**. That means Apple accepted the compact updates as unreliable messages.
- For comparison, set `NetworkTuning.CompactSnapshots = false`, rebuild and play again. The full update is about 1.7 KB; if `unreliable refused` is above 0 now, you have found Apple's limit from the other side. Put the numbers in the gate log (G7.3.6) and set the flag back to `true`.
- `"compact":false` on the host line even though the flag is on means the host's start-of-match self-test failed on this build's JSON; tell whoever maintains the code.

## Test 8 — TV delay by receiver

Run the existing timing probe against an Apple TV, a Mac with AirPlay Receiver, and an HDMI adapter. Write down the delay each reports. The game credits up to 0.30 s of picture delay to a swing; if a normal TV reports more than that, raise `NetworkTuning.MaxDisplayCredit` and `MaxSwingRewind` together (rewind ≥ credit + network + sensor time) and re-run the harness (`dotnet test Tools/netsim/NetSim.csproj`).

## Test 9 — Everything else that was changed

- Reconnect: turn Wi-Fi off on one phone for 5 s during a point, then on. Match pauses, then continues; after 15 s of absence the player is dropped and the other is told.
- A player leaving during setup returns everyone to the lobby with a notice.
- The Local Network permission: deny it on first use. The menu should say so and offer *Open Settings*.
- Golf online and Nearby still work as before (one round with three phones).

---

## Results

| Test | Phones / network | Pass? | Notes |
| --- | --- | --- | --- |
| 1 One TV, two phones | | | |
| 2 Two TVs | | | |
| 3 Pass the phone | | | |
| 4 Online tennis | | | |
| 5 Connection check | | | |
| 6 Split-screen fps | | | |
| 7 Update size | | | |
| 8 TV delay | | | |
| 9 Reconnect / permissions / golf | | | |

| Link | rtt median | p95 | jitter | loss | quiet pauses | Felt like |
| --- | --- | --- | --- | --- | --- | --- |
| Nearby, same Wi-Fi | | | | | | |
| Online, Wi-Fi ↔ Wi-Fi | | | | | | |
| Online, Wi-Fi ↔ cellular | | | | | | |
| Online, cellular ↔ cellular | | | | | | |

## If something is off — where to turn the dial

| What you see | Try |
| --- | --- |
| Swings that look on time are marked early or late | Check the TV delay (test 8); `NetworkTuning.MaxDisplayCredit` and `MaxSwingRewind` |
| "Reconnecting…" often on a fine link | `MultiplayerTuning.silenceSeconds` (0.4 s) up to 0.6; look at `quiet pauses` |
| "Weak connection" on a connection that plays well | `MultiplayerTuning.linkFair*` thresholds |
| Ball jumps after the opponent hits | Note how often; plan P7.5 (blend) is the fix, not built yet |
| Choppy split screen | `TennisGame.SplitScreen = false`, then plan 3e |
| Refused messages | `NetworkTuning.CompactSnapshots`; `MultiplayerTuning.unreliableSafeBytes` |
| Setup takes too long | The 60 s notice (`calibrationNoticeSeconds`); the 45 s return window (`recalibrationSeconds`) |
