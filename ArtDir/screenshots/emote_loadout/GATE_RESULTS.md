# Equipped emotes — verification

Implemented in the existing Party Tennis project on 2026-10-05 (EDT).

## Behavior

- Locker Room → Emotes has three saved slots and six available animations: Scuba, Thrust, Spike, Wave, Bring it, Pushups.
- Pick a slot, then an emote. Selecting an already equipped emote swaps slots, keeping three distinct choices. Done saves; Revert restores the previous loadout.
- Old profiles receive Wave, Scuba and Spike automatically.
- The phone controller shows the three equipped names. Buttons enable during the player intro and after that player wins a point, once per moment.
- Selected animations finish before the next serve. Choices made while an intro or stroke is settling are queued.
- Online matches validate the equipped slot at the host and carry playback state to remote players and spectators.

## Gates

- GATE: Native build and simulator launch PASS — GolfArcade on iPhone 17 Pro simulator.
- GATE: Locker persistence, migration, swaps and remote navigation PASS — 12 LockerTests, zero failures.
- GATE: Phone and TV locker layout PASS — one SwiftUI snapshot test, all three slots and six choices visible.
- GATE: Controller eligibility PASS — intro and winning-point enabled; rally disabled in the native snapshot test.
- GATE: Multiplayer rules PASS — 29 Unity EditMode tests, zero failures.
- GATE: Animation playback PASS — three Unity PlayMode tests, zero failures. All six animations on both bodies, late intro selection, remote actor playback and reliable controller slot command.
- GATE: Whitespace check PASS — git diff --check for touched source and test files.

Unity tests ran in an isolated copy using Unity 6000.3.24f1. PlayMode used graphics because the scene warmup requires rendering. The native build uses the simulator/test target.

## Proof

- [Phone locker](locker_phone.png)
- [TV locker](locker_tv.png)
- [Controller intro layout](controller_intro_layout.png)
- [Controller rally layout](controller_rally_layout.png)
- [Final Unity rule results](unity-rules.xml)
- [Final Unity playback results](unity-playback.xml)
- [Native test results](native-tests.txt)

Controller screenshots are isolated renders of the real SwiftUI row, not live gameplay or physical-device captures. Playback was checked in actual Unity scenes through runtime assertions.

## Device status

The installed iPhone app has not been updated by this task. C# changes require a fresh Unity iOS export and an integrated GolfArcadeUnity workspace rebuild. Physical iPhone, AirPlay and a real multi-device network session were not tested.

