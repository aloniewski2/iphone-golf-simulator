# Multiplayer backend integration

The backend supports Apple Game Center online parties and Bonjour local parties. The in-game menu has the entry screens (Play → Single Player / Local / Online; see below). The lobby holds four people total: up to four golfers, or two tennis competitors and up to two observers. Golf waiting players watch the current shot. One participating phone owns the simulation; no dedicated game server is used.

This source implementation has native protocol tests, Unity rule tests and rendered gameplay tests. It still requires signed, multi-device Game Center and local-network validation before release. A successful build does not establish internet responsiveness or matchmaking availability.

## What the October 2026 multiplayer pass changed

The plan is [`PLAN_Multiplayer_OnlineLocal.md`](../PLAN_Multiplayer_OnlineLocal.md); the evidence, and what is still unproven, is [`proof/multiplayer/GATE_RESULTS.md`](../proof/multiplayer/GATE_RESULTS.md). Everything below is implemented and was checked in a sandbox (the real tennis and golf rules, the wire formats, the camera math, syntax). The Swift tests, the Unity compile and everything on real phones are listed there as **NOT RUN** until someone runs them on a Mac and on devices.

- **Menus.** Play → **Single Player**, **Local** or **Online**. Local → **Golf · Pass the Phone** (2–4 players share one phone: pick the count and the round starts, with a "pass the phone" card between turns), **Tennis · Two Phones** (two phones, one lobby, starts when the friend joins), **Join a Friend**. If the app has no permission for the local network it says so and offers Settings.
- **Protocol version 4.** `MultiplayerPacket.version` (Swift) and `NetworkPacket.Version` (C#) are both 4 and must move together; a phone on another version reports "incompatible" and Unity drops its packets. All phones in a match must run the same build.
- **Tennis setup.** Lobby phases are now `lobby → loading → calibrating → playing` (golf skips `calibrating`). Each phone points at the TV and taps Ready (touch players report at once); the owner starts the match when every competitor has loaded, calibrated **and** has a steady connection. A player who drops and returns sets up again (45 s allowed, 15 s for a plain dropped connection).
- **One TV, two phones.** Each participant has `hasScreen`, `view` (`near` = own TV and own court, `split` = this TV shows both players, `none` = controller only), `calibrated` and `screenDelay`. A tennis lobby needs at least one phone with a TV. With one TV, that phone shows a **split screen** (a camera per player, each half framed so the whole court and both players stay in view) and the other phone is a hidden controller that applies the TV's delay. Two TVs behave as before. Only one phone needs to AirPlay.
- **Fair timing.** A swing is credited with the TV's picture delay (up to 0.30 s) and the host rewinds up to 0.40 s to judge it; the clock between phones is filtered (median of the best round trips, slewed); each phone sends a heartbeat 10×/s and a silent phone pauses the match within 0.4 s; the phone stays awake. Details and the measured effect: plan P1 and Appendix A.
- **Connection check.** During setup guests ping the owner 10×/s and grade the link (Good ≤ 80 ms / 30 ms jitter / 2% loss, Fair ≤ 150 / 60 / 5). The match starts after the link has been Fair or better for 1.5 s; after 8 s without that, everyone is told and the owner may **Play anyway**. The thresholds are guesses until field data exists (`MultiplayerTuning`).
- **Compact updates.** The host's 30-per-second tennis update is sent as `NetworkTennisWire` (366 bytes instead of about 1.2 KB, whole millimetres and milliseconds); checkpoints still use the full form and guests read either. The host tests the form with the build's own JSON when a match starts and falls back to the full form if it fails; the `unity network stats` line in `SportsDiagnostics.log` says `"compact":true|false`. An unreliable message that Apple refuses is resent reliably from then on.
- **Switches.** `NetworkTuning.CompactSnapshots` (C#), `TennisGame.SplitScreen` (C#: `false` gives one shared view), and the constants in `MultiplayerTuning` (Swift) and `NetworkTuning` (C#).
- **Not built:** predicting your own hit, immediate updates on contact, a native wake signal, blending retroactive hits (all need phones to judge); a swing-timing check for both players on one TV; TV badges for who is ready; a quiet re-queue for Quick Match; a lower quality level for split screen (waiting for frame-rate data). See the plan.
- **Legacy.** The Node server in `server/` and the Unity "Online" screen (`Unity/Assets/Scripts/Net/Online`) belong to the older stand-alone build and are not used by this path. They are kept and labelled, not deleted.
- **Field test.** [`proof/multiplayer/FIELD_TEST.md`](../proof/multiplayer/FIELD_TEST.md) is the script for the first test with real phones.

## Future SwiftUI/UIKit integration

All coordinator methods run on the main actor. `MultiplayerService.shared` is observable. The app calls `prepare()` once to register lifecycle/invitation listeners; it does not display authentication or matchmaking UI on launch.

```swift
let multiplayer = MultiplayerService.shared
multiplayer.setIdentity(name: profile.name,
                        female: profile.standardFemale,
                        left: profile.handedness == .left)

// Online party: the presenter is supplied by the future UI.
try await multiplayer.authenticate { controller in
    presentingViewController.present(controller, animated: true)
}
try multiplayer.createOnlineLobby()
let invitations = try multiplayer.inviteFriends()
presentingViewController.present(invitations, animated: true)

// After people connect, the owner configures seats and everyone becomes ready.
try multiplayer.configure(.golf)  // Or .tennis, venue: "resort" / "skyscraper" / "volcano".
try multiplayer.setReady(true)
try multiplayer.startMatch()      // Owner only, after all competitors are ready.
try multiplayer.returnToLobby()  // Owner only. Keeps the connection and party ID.
```

Use the actual profile selected by the future UI; the backend identity defaults to “Player.” Read the latest lobby after each operation. Sport changes clear readiness. `sets` means **sets needed to win**; `games` means games needed to win a short set (1 or 3), or the six-game format with a margin/tiebreak (6).

### Entry functions

| Purpose | Function |
| --- | --- |
| Authentication | `authenticate(present:)` |
| Solo online party draft | `createOnlineLobby()` |
| Apple friend invitation UI | `inviteFriends()` returns a controller |
| Accepted incoming invitation | Observe `pendingInvite`, then present `acceptInvitation()`'s controller |
| Public tennis | `quickMatch(.tennis)` |
| Public golf | `quickMatch(.golf, golfPlayers: 4)`; 2 or 3 can be requested explicitly |
| Cancel matchmaking | `cancelSearch()` |
| Discover local parties | `browseLocal()`, observe `discoveredLobbies` |
| Host local party | `hostLocal(name:)` |
| Join discovered local party | `joinLocal(_:)` |
| Sport/settings | Owner calls `configure(_:venue:sets:games:)` while idle |
| Competitor/observer | Owner calls `assignSeat(_:seat:)`; -1 is observer |
| Ready state | Each participant calls `setReady(_:)` |
| Watch queue | Observer calls `queueForNextMatch(_:)` |
| Start/end contest | Owner calls `startMatch()` / `returnToLobby()` |
| Leave party | `leave()` |
| Future public fill | Owner calls `findMorePlayers()` while idle with spare connections |

Observe `lobby`, `searching`, `lastError`, `pendingInvite`, `discoveredLobbies` and `roundTrip`. `localID`, `localSeat` and `isOwner` identify local capabilities. Render readiness and errors from this state instead of inferring success from controller dismissal. An unavailable Game Center capacity produces an error. A fifth person is not admitted, including as an observer.

For ordinary integration, leave `onMatchRequested` and `onReturnToLobby` unset: the service launches/stops the existing SportsSession, including its loading, phone preview and external-display paths. When overriding launch, call `runtimeLoaded()` only after Unity finishes loading/rendering. `runtimeUnavailable(_:)` interrupts a failed launch. The existing session checks for the multiplayer bridge to prevent an old Unity export silently starting solo gameplay. Finish an active solo session before readying or starting online play.

`onResult` receives JSON with `sport`, `reason`, `score`, `winner` (canonical tennis seat, -1 for interruption/golf), and golf `golfers` including scorecards/DNF. Keep these results separate from campaign progression. Calling `returnToLobby()` resets readiness and gives the next contest a fresh ID. Queued tennis viewers take a free/losing seat; the winner stays by default. The owner may assign seats explicitly before the next ready vote.

## Transport and lifecycle

GameKit uses `GKMatch` peer-to-peer networking and Apple's reliable/unreliable modes. Private invitation matching is sport-neutral; public queues use separate versioned player groups. The online party can persist across tennis and golf. One designated owner can invite/add players. Programmatic Quick Match requests a fixed group size; there is no timed partial-fill vote. Public fill through `addPlayers` is implemented as a future API and still needs a real-account proof.

Local play uses Network framework TCP with no-delay, Bonjour `_motionparty._tcp`, length-prefixed bounded messages, a lobby token, bounded reliable queues and replacement/coalescing of snapshots under pressure. The release target is same Wi-Fi. Direct offline peer discovery outside a shared network is not promised. Local play does not require Game Center or internet. Online and LAN parties are separate transports; they are not bridged together.

The owner also acts as simulation authority. Initial public ownership is agreed deterministically from Game Center IDs; private/local ownership remains the creator. There is no connection-quality host selection or seamless migration. A host background/transport failure interrupts the contest. Other competitors get a 15-second recovery window; a returning phone must load/acknowledge before resuming. Tennis closes an unrecovered contest without fabricating a win; golf marks a departed golfer DNF and continues with at least two golfers. Disconnected members are removed when returning to the lobby. Observers do not block loading or pause gameplay when leaving. Late arrivals into a spare connection observe an active contest after a full checkpoint. After an interrupted contest, return to the party before starting another one.

Every packet carries a protocol version, party/contest IDs, sender sequence, type, reliability and monotonic timestamp. Native transport identity replaces any claimed sender. Native routing validates owner/seat/phase, duplicate sequences, size and rate; Unity validates roles and inputs again. Tennis actions include point/contact IDs to reject obsolete contacts. Bump protocol version and matchmaking groups when rules/content become incompatible; all phones should use the same multiplayer build.

## Gameplay and bridge

`SportsMultiplayer` consumes native messages on Unity's main thread. Only the authority advances official ball, rules and scoring. Spectators cannot submit gameplay actions. Reliable full snapshots and golf paths initialize late viewers; replaceable snapshots run at about 30 Hz (tennis uses the compact form described above). Queue overflow reports an interruption rather than silently freezing a live contest.

Tennis has a symmetric two-human simulation sharing existing tennis scoring and presentation. It uses 120 Hz authority stepping, bounded 150 ms contact history, both serving sides, provisional animation separated from confirmed contact, local animation/movement prediction and near-side coordinate conversion. It is a separate multiplayer contact/trajectory model from the asymmetric solo/AI loop. Tune responsiveness and visual contact on phones; passing rule tests does not prove the proposed 150 ms RTT / 20 ms jitter / 1% loss envelope.

Golf evaluates the existing CourseShot engine once on the host and sends the path, lie, penalty and outcome. It maintains each player's ball/card, rotated tee order, farthest-from-cup turns, 60-second timeout strokes, par+5 completion cap and DNF. Clients follow authoritative paths instead of rerunning physics. The existing golf course is shared by identifier/seed, not transmitted as assets.

The native/Unity bridge exports `SportsNetworkPush`, `SportsNetworkPoll`, `SportsNetworkEmit`, `SportsNetworkPollOutput` and `SportsClock`. Both directions are capped at 128 messages and 60,000 bytes per message. An obsolete framework fails the launch check. Multiplayer session markers persist through shutdown so old network scenes cannot resume solo simulation or grant campaign completion.

## Build and proof

Open `GolfArcadeUnity.xcworkspace` for the playable phone build. Re-export Unity after managed or bridge changes, then run `Unity/Tools/build-integrated-ios.sh`. `project.yml` owns Game Center entitlements and Bonjour/privacy settings so XcodeGen preserves them; `project-unity.yml` inherits them. Native-only Simulator builds verify Swift/protocol logic, not Unity or Game Center gameplay.

Without a Mac, the rules, wire formats and camera math can be run with `dotnet test Tools/netsim/NetSim.csproj` (see `Tools/netsim/README.md`; CI runs it from `.github/workflows/netcode.yml`), and `python3 Tools/check-syntax.py` parses the changed Swift and C# files. These do not replace the Swift tests, the Unity compile or a device.

Evidence is under repository `work/multiplayer/`: native test log/xcresult, `multiplayer-rules.xml`, rendered `playmode.xml`, Unity export log and integrated build log. The final implementation report records the latest totals and build outcome.

Before release, enable Game Center for the existing App ID/app configuration, choose the project's signing team/profile with the entitlement, and test four distinct Game Center accounts on physical phones. Verify private/cold/warm invites, Quick Match, public fill, offline LAN discovery, permission denial, backgrounding, reconnect/host loss, four-person golf scorecards, two tennis competitors with two viewers, repeated sport switching and network impairment. Do not add a room directory, greater-than-four audience or party merging without separate infrastructure/design work.
