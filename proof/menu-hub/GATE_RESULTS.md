# GATE_RESULTS — PLAN_MenuHub_WalkableWorld (The Plaza)

Run 2026-10-10. Decisions taken: the plan's recommended D1–D5, plus D7 (evening only). **D6 (club name) is still open**; the crest is a star badge with the placeholder "THE CLUB".

**Where these numbers come from.** Unity gates ran in the Unity 6000.3.24f1 editor, in an APFS clone of `Unity/`. Phone gates ran as XCTest in the iOS simulator, using a scratch xcodegen project, because the repo's own test target doesn't compile at HEAD (`MotionClubReviewTests`). Party gates used an in-memory network. **No gate ran on the iPhone with a TV.** Every gate that the plan defines on the device is marked NOT RUN, and none of the editor numbers stand in for device numbers.

## Summary

| Phase | Result |
|---|---|
| 1 · Greybox walk | Editor PASS on HUB_BOOT, STICK_ANALOG, LATENCY, FEET, DOORS and NO_TV. **FPS: 10 min soak NOT RUN** (45 s editor soak only). Device: NOT RUN |
| 2 · Rooms + stations | PASS: ROUTE_ALL, LOCKER_LIVE |
| 3 · Bays + seamless load | PASS: NO_COVER, RETURN, CANCEL. **FAIL: HITCH. NOT RUN: MEM** (needs the device baseline) |
| 4 · Party | PASS: SYNC, ALL_SEATED, TOGETHER, LOOK (editor and in-memory network; 4 real phones NOT RUN) |
| 5 · Art pass | READABLE: author reading only. **LOOK_MATCH: OPEN, waiting on Adnan's sign-off** |
| 6 · Cleanup | PASS: NO_DEAD_IDS. **FAIL: "all existing menu tests pass"** (11 failures that already existed before this work, 0 new). Not done: the focus-ID typing and the `TennisMenu.swift` split |

## Phase 1 · Greybox walk — `phase1/`

- `GATE: HUB_BOOT (editor) PASS`. A `start{sport:"hub"}` boots the plaza in 0.9–2.8 s (`phase1_metrics.json`: `hub_boot_seconds_editor` 2.77). On the phone, `HubSessionTests.testATVOnTheHomeScreenAsksForThePlaza` shows that a TV on the home screen asks for the plaza. **Device: NOT RUN.**
- `GATE: STICK_ANALOG PASS`. Speeds:

  | Input | Speed | Clip |
  |---|---|---|
  | Short push | 1.08 m/s | Walk |
  | Full push | 3.25 m/s | Run |
  | Held full | 4.30 m/s | Sprint |

  Letting go stops in 0.209 s, inside the 0.25 s limit.
- `GATE: LATENCY (editor) PASS`. Stick to motion takes 1 frame (17 ms). **The phone→TV path on the device is NOT RUN.**
- `GATE: FEET PASS`. 10.0 % of moving frames slide more than 0.15 m/s, inside the 20 % limit. Trace: `p1_feet_trace.csv`.
- `GATE: DOORS (editor) PASS`. 10 doors were walked in and out. Worst door 0.417 s (limit 0.5 s); worst frame 26.4 ms (limit 33 ms). Log: `p1_doors.txt`.
- `GATE: FPS 60 for a 10 min soak — NOT RUN`. Only a 45 s editor soak was run:
  - average 59.7 fps, p99 18.6 ms, managed memory with no growth;
  - worst frame 238.5 ms, which happened once while the other-sex hero was being prepared.
- `GATE: NO_TV PASS`. `HubSessionTests.testNoTVKeepsThePhoneMenu`: without a TV the phone keeps the old menu.

## Phase 2 · Rooms + stations — `phone/`, `look/`

- `GATE: ROUTE_ALL PASS`. Every old menu destination takes at most 2 presses (limit 3): walk to the spot and press A, or use ☰ plus a shortcut. Full list in `phone/route_all.txt`; covered by `HubRouteTests.testEveryOldMenuDestinationIsReachable`.
- `GATE: LOCKER_LIVE PASS`. A locker pick is sent to the plaza hero straight away (`HubRouteTests.testALockerPickIsSentToThePlazaHeroAtOnce`). In Unity, `hubLook` re-dresses the hero in the same frame.

## Phase 3 · Bays + seamless load — `phase3/`

- `GATE: NO_COVER PASS`. 20 solo launches from bays showed 0 loading-cover frames (`HubBayLaunchTests.testTwentyBayLaunchesShowNoLoadingCover`). Unity side: the match loads additively behind the plaza and is hidden (HiddenMatch), then a dive into the bay screen hands off to it.
- `GATE: HITCH FAIL`. The limit is no plaza frame over 50 ms while seated loading. In the editor, 6 frames went over 50 ms; the worst was 2675 ms (`p3_metrics.txt`).
  - Causes: the rival female hero's per-instance preparation (MatchHeroLook and garments), and about 1.06 s of venue art.
  - Venue art was already cut from 6.6 s to 1.05 s: `perf_tennis_art.py` caches the BotanicalBatch prototypes, with identical output.
  - Not fixed: the rival hero preparation still has to be time-sliced or done ahead.
- `GATE: MEM NOT RUN`. The plan sets the budget from a device baseline, which doesn't exist yet. Editor only: total allocated went from 0.87 to 1.94 GB, but the editor inflates this.
- `GATE: RETURN PASS`. After a match you respawn seated on the same bench (Unity `HubBayTests` and Swift `testAFinishedMatchReturnsToTheSameBenchAndAStoppedOneStandsYouUp`).
- `GATE: CANCEL PASS`. Standing up mid-load unloads the match scene and leaves nothing loaded (`HubBayTests`, `CANCEL_editor=PASS`).
- Bay previews: `bays_with_previews.png`, `preview_clips_contact.png`. Six clips were captured from the real venues and play through the glitch shader.

## Phase 4 · Party — `phase4/`

- `GATE: SYNC (editor) PASS`. Four heroes ran at 60.0 fps. Remote position error vs the 120 ms interpolation target: p95 0.193 m, max 0.213 m (limit 0.3 m).
  - Known cost: a friend's hero spawning freezes the editor for about 2.2 s, the same preparation cost as the HITCH failure.
- `GATE: ALL_SEATED PASS`. Four phones on an in-memory bus (`HubPartyTests.testFourFriendsSeatedInTheGolfBayStartOneMatchAndComeBackToIt`):
  - three seated and ready: no start ("waiting for Dee");
  - all four seated but one not ready: no start;
  - all four seated and ready: the host's phone starts the match.
  - Online tennis is 1 v 1 (lobby capacity 2), so a party of four plays golf, and the host's bay sets the sport.
- `GATE: TOGETHER PASS`. In the same test, all 4 phones get the same `matchID`, launch from the bay, and come back to the same bench. Unity half: `AnOnlineMatchFromABayLoadsBehindThePlaza`.
- `GATE: LOOK (editor) PASS`. An online match shows each player in their lobby colours, the same ones the plaza shows: near hero shirt E8505B; far hero 3F62CC, skin 965835, female.
- Host handover PASS: when the host leaves an idle lobby, it passes to the next phone. The 31 existing `MultiplayerTests` still pass.
- **4 real phones on a network: NOT RUN.**

## Phase 5 · Art pass — `phase5/`, `look/`

- Side-by-side: `phase5/sbs_final_concept_vs_unity.png` (concept 03 on the left, Unity on the right). Stills: `phase5/look_*.png`.
- `GATE: READABLE — author reading only`. The art bible has no numeric rule for reading at TV distance. At 1080p the sign words (PLAY, LOCKER, CLUBHOUSE), the room icons and the "You" tag all read clearly.
- `GATE: LOOK_MATCH — OPEN (needs Adnan's sign-off)`. Remaining differences I can see:
  - The concept has stronger warm-gold contrast and longer shadows.
  - The concept fills its party pads with friends; Unity only does that with a party (see `phase4/p4_party_plaza.png`).
  - The concept has a big tennis ball where Unity has the club crest; the crest is deliberate, because the plaza is sport-neutral.
  - The plaza hero is bald, because the base heroes are bald and hair is a separate asset.

## Phase 6 · Cleanup — `phase6/`

- `GATE: NO_DEAD_IDS PASS`. `work/menu-hub/menu_audit.py` found no unhandled ids, no dead handlers and no unreachable screens (`phase6/menu_audit.txt`).
  - Removed screens: `.quickPlay`, `.golfLesson`, `ClubQuickPlayScreen`.
  - Removed 16 dead select ids, 13 dead adjust ids, `playerItems`/`cycleColor` and `characterRows`.
- `GATE: all existing menu tests pass — FAIL`. The menu suites have the same 11 failures before and after this work:
  - 1 in MenuFlowRegression (golf map count is 3, expected 4)
  - 2 in Onboarding
  - 4 in TennisCampaign
  - 4 in TennisMenuSnapshot
  
  These come from other sessions' work in progress (golf map count, party and onboarding redesign, SceneKit nil). This work adds **0 new failures**. Two failures that appeared at first were state leaking between tests from the new hub tests (a lobby left open on `MultiplayerService.shared`); `HubTestState.restore` now cleans it up. Lists are in `phase6/menu_tests.txt`.
- **Not done:** typing the focus IDs and splitting `TennisMenu.swift`. Both touch files that other sessions are editing right now.

## Not run / open items for Adnan

1. All device gates: boot on the TV, LATENCY on the phone→TV path, the 10 min FPS soak, MEM baseline, 4 real phones.
2. HITCH: the rival hero's per-instance preparation, about 2.2–2.7 s in the editor, needs to be time-sliced or done ahead.
3. LOOK_MATCH sign-off, and D6, the club name.
