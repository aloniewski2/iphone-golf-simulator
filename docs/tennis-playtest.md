# Tennis playtest routine

Run the same checks on every device build so builds can be compared. About 15 minutes.
Record results in a copy of the table at the bottom.

## 0. Automatic frame-time run (no player needed)

```bash
xcrun devicectl device process launch --terminate-existing --device <UDID> com.aloniewski.GolfArcade -- -benchTennis
```

The app starts tennis on the phone screen and plays itself. Every 10 seconds it writes a
`perf` line to `Documents/SportsDiagnostics.log` in the app container:

```
perf t=60s target=60 frames=600 avg=59.9fps low1%=57.8fps worst=21.3ms hitches=0 gc=0 monoGrowth=0KB device=iPhone18,2 tier=High
```

Let it run 10 minutes to see thermal throttling (compare the first and last lines). Pull the
log with `xcrun devicectl device copy from --device <UDID> --domain-type appDataContainer
--domain-identifier com.aloniewski.GolfArcade --source Documents/SportsDiagnostics.log
--destination ./SportsDiagnostics.log`.

**Pass:** avg ≥ 59 fps, 1% low ≥ 55 fps, hitches ≤ 1 per 10 s, gc = 0, and the last line
within 2 fps of the first. If the frame governor stepped quality down, the Unity log says
`[TennisQuality] ... stepped down`.

## 1. Swing response (TV connected, motion controls)

1. Stand still and swing a forehand. The racket should start moving with your arm, not
   after it. Record: does it feel instant / slightly late / late?
2. Take a big side-step without swinging, ten times. Count strokes the character starts and
   then cancels (a brief flinch) — the target is 0–1.
3. Swing a backhand with the camera toward the TV. It must play the two-handed backhand.

## 2. Serve

1. Serve 10 times, swinging as the ball peaks. Count serves in (target ≥ 8).
2. Watch one serve closely: the ball must leave the racket at contact, not before.
3. Let one toss drop without swinging — it must be caught and re-tossed, no fault.

## 3. Rally

1. Play three points at Standard. Record the longest rally (target ≥ 6 shots) and whether
   you won at least one point.
2. Watch the character between shots: it should split-step as the opponent hits, turn and
   run for wide balls, and plant its feet (no sliding) when it stops.
3. A ball well wide should produce a dive.

## 4. Feel and look

- Contact: fuzz and flash on every hit, a small camera punch on Excellent/Perfect, a thump in
  the phone on contact (haptics on).
- Bounce: dust puff and a fading mark; topspin kicks up, slice stays low.
- Players watch the ball; the loser of a point slumps, the winner celebrates.
- A winner or long rally plays a slow-motion replay (not every point).
- Match point shows the banner and the camera tightens.
- End of match: the results card waits for a swing to start the next match.

## 5. Record

| Build | Device | avg fps | 1% low | hitches/10s | swing feels | flinches/10 steps | serves in/10 | longest rally | notes |
|---|---|---|---|---|---|---|---|---|---|
|  |  |  |  |  |  |  |  |  |  |
