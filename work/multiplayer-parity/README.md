# Multiplayer parity: how the two-phone tests run

These tools test `PLAN_Multiplayer_SinglePlayerParity.md` on one Mac with no phones. The scripts were written for the session scratchpad, so edit the `SP=` and `REPO=` lines before reusing them. Unity always runs in APFS clones of `Unity/` (`U`, plus `U2` for the second phone), never in the repo copy.

## Tennis: two editors, one per phone

Script: `versus_pair.sh <label> [ENV=...]`

- Seat `WSEAT` (0 by default) runs as a windowed editor in `U` and records its TV with `ScreenCapture.CaptureScreenshotAsTexture` at 1280×720.
- The other seat runs as a batch-mode editor in `U2`. It has no screen, like a controller-only phone. Batch mode never draws, so the test draws the game camera by hand until the launch finishes.
- The two editors talk over UDP on localhost with a simulated network:
  - `MP_LATENCY` (0.06), `MP_JITTER` (0.025), `MP_LOSS` (0.03, which drops pose updates only).
  - Their clocks are lined up from each process's start time.
  - Seat 0 starts the match.
- Each seat plays its own player through single player's controls only: toss, serve aim, walking the baseline, swing onset then confirmation, dives, emotes.

Settings:

- `MP_VIEW=near`: each phone has its own TV (online).
- `MP_VIEW=split`: seat 0 draws both players on one TV, seat 1 is controller-only.
- `MP_GAMES` (1/3/6), `MP_SECONDS`.

Test: `Unity/Assets/Tests/PlayMode/VersusPeerCapture.cs`. It is skipped unless `MP_VERSUS=1`. Each seat writes `seat<N>_summary.json`:

- the score after every point;
- the strokes the other player played on this screen;
- a line per contact by the other player;
- the calls this phone made and the calls it took from the other phone;
- errors, hitches.

`vsum.py <dir>` compares the two seats; the score sequences must be identical.

## Golf: one editor

Script: `run.sh <label> "<filter>" [ENV=...]`

Test: `Unity/Assets/Tests/PlayMode/MultiplayerExploreCapture.cs`, skipped unless `MP_EXPLORE=1`.

- `GolfTwoPhonesOneTv`: the host phone with the TV plus a guest phone. The guest's inputs go through the same simulated network, as its phone sends them.
- `GolfSolo`: one player, for comparison.

The log ends with:

- the aim trials;
- G1: how far waiting golfers moved while the player up aimed;
- G2: the TV's aim lag and its biggest single step;
- hitches;
- with `MP_PROBE=1` (`HitchProbe.cs`), the Unity profiler markers behind each frame over 250 ms.

`TennisSplitScreen` is the pre-parity capture from the first audit. It now drives the old host-rules path only if `SportsMultiplayer.TennisVersusEnabled` is turned off.

## Video tools

There is no ffmpeg on this Mac. Build the frame grabber once with `swiftc -O frames.swift -o frames`, then:

- `sheet.py <video> <out.jpg> <cols> <t1> <t2> ...`
- `burst.py <video> <out.jpg> <start> <step> <count> <x0> <x1> [cols]`

All Unity runs are muted (`GOLF_MUTE=1`), and they are serialised with `work/postcard-look/heavy.sh`.
