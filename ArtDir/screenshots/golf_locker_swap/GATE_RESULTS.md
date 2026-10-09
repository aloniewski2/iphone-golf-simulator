# Golf / tennis locker swap — GATE_RESULTS

Implemented 2026-10-05 (EDT).

The locker now passes its selected sport into the character renderer. The renderer reloads the appropriate outfit even when the player profile is unchanged. Golf uses the existing golf outfit variant: polo, trousers or skort, cap or visor, glove and golf shoes. A held club replaces the tennis racket. Switching back rebuilds the tennis hero. Shelf thumbnails use the same sport-specific model, and the Club close-up targets the club geometry.

The existing player identity, skin colour, handedness and colour selections are preserved. The base character geometry was not edited.

## Gates

- GATE: Sport-specific preview PASS — final native test replaces the tennis model with the golf model for male and female players, checks headwear/glove and club geometry, checks that racket parts are absent, then restores tennis.
- GATE: Left-handed equipment PASS — club and outfit mirror with the existing hero root.
- GATE: Equipment framing PASS — Club close-up targets the centre of the full club rather than the empty racket group.
- GATE: Native build / launch PASS — iPhone 17 Pro simulator, GolfArcade scheme.
- GATE: Locker regression PASS — final run includes the sport swap test and existing emote loadout screenshot test; 2 passed, 0 failed.
- GATE: Visual proof PASS — inspected male and female golf locker captures and recorded live Tennis → Golf → Tennis switching.
- GATE: Resource export PASS — isolated Unity 6000.3.24f1 export completed for both existing golf outfit variants, including the exporter's CPU-skin versus BakeMesh checks.
- GATE: Whitespace PASS — git diff --check on touched source, tests and Xcode projects.

## Proof

- [Live switching recording](tennis-golf-swap.mp4)
- [Live golf locker with saved colours](live_golf.jpg)
- [Male golf](male_golf.png) · [Male tennis](male_tennis.png)
- [Female golf](female_golf.png) · [Female tennis](female_tennis.png)
- [Native test results](native-tests.txt)
- [Installed resource hashes](asset-manifest.json)

The four PNG captures render the real SwiftUI locker in a simulator window with a default-colour fixture. The MP4 records actual taps in the running simulator app with the existing saved profile.

## Integration

Golf meshes/maps came from the existing golf-look work, copied into an isolated Unity export to avoid modifying that active task's project. The native data loader supports GolfHero_Male / GolfHero_Female alongside the unchanged MatchHero assets; golf material version 2 is selected only by its exported metadata. New resources were added to the native and integrated Xcode project resource phases.

This task fixes native locker rendering. It does not certify the separate golf gameplay/material/garment-fit task's full gate set or update an installed physical iPhone.

