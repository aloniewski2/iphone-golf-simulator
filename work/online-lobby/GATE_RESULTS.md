Online lobby gate report — 2026-10-05

Completed in the existing Island menus and locker. The shared room uses the locker backdrop. All required gates pass; the prompt's optional Game Center device exception is recorded below. Visual acceptance remains subject to Adnan's review.

| Gate | Result and evidence |
| --- | --- |
| P0 | PASS — 25 multiplayer Swift tests; owner validation, optional loadout fields, golf course IDs, protocol v2 rejection, look throttling and 1.5 s emote limit. Four full looks: lobby JSON 2,038 bytes; packet 2,497 bytes < 60,000. Unity EditMode 249/249 and multiplayer PlayMode 5/5 pass. |
| P1 | PASS — six clips × two heroes × ten sampled frames, maximum bone rotation error 0.2987069° <= 2°; maximum rigid translation error 0.7872217 mm. Racket rigid tracks and blend back to ReadyIdle verified. Four Unity vs SceneKit films provided. |
| P2 | PASS — Party, Play Online, sign-in, Nearby, searching and errors reachable through the focus grid and touch; click-remote navigation test passes. Phone and TV screenshots provided. |
| P3 LOBBY_HEROES_LIVE | PASS — two real nearby peers plus two mock watchers; each has its own look. Worst observed Done-to-remote-hero look delay 0.336922 s < 1 s across the two measured runs. |
| P3 LOBBY_EMOTE_SYNC | PASS — worst observed selection-to-remote-hero start 0.264994 s < 0.5 s; full animation, echo deduplication, phase gate and owner-enforced cooldown verified. |
| P3 LOBBY_JOIN_LEAVE | PASS — delayed pop and Wave after other heroes move, fade on leave, closing the gap; final live lobby film shows four heroes without overlap or crop through the clothes panel transition. |
| P3 LOBBY_HOST_RULES | PASS — Start has a concrete waiting reason, seat swap is atomic, guests see read-only settings, Ready and queue rotation tests pass. |
| P3 STYLE_MATCH | PASS — modified UI additions use existing Island tokens and focus styles; style scan has zero violations. Before/after comparison and both room alternatives supplied. Three reusable components added to IslandKit.swift. |
| P3 PERF | PASS — actual SceneKit renderer callbacks: 600 frames / 10 s = 60 fps, 145.06 MB footprint; iPhone 15-class simulator (iPhone15,4), four heroes plus Scuba, 4× MSAA. |
| P4 | PASS — real two-simulator Nearby loop: lobby → both Ready → start → Unity tennis game → normal results → back to lobby → Ready/rematch → second match → forced TCP disconnect → reconnect grace/interrupted result. Full host and guest films and runtime traces saved. |
| P5 | PASS — --lobby-mock N (1–4) avoids network preparation; 52 screenshots (26 phone/TV state pairs), live two-peer lobby film and full loop films. GAMECENTER_DEVICE NOT RUN, as permitted when two devices are unavailable. |
| P6 | PASS — Swift build and focused native regressions, final Unity export check, scoped commit and non-force push completed. Remote branch verified at 23cbedabb4074effa797c67c0dab987761d7aef8. |

Proof package:

- [Style review](style-review.png) — four before/after comparisons, refreshed from final screenshots.
- [Locker and terrace comparison](room-comparison.png) — locker selected.
- [All 52 screenshots](online-lobby-screenshots.zip) — phone and actual TennisTVRoot rendered at 1280×720 points in a simulator window; TV captures are not a tvOS port.
- [Final lobby host film](nearby-lobby-host.mp4) and [guest film](nearby-lobby-guest.mp4) — real Bonjour join, four independent looks, both emotes, existing locker Customize/Done, join/leave and both Ready, with final camera framing. These short films stop at Ready; the full films below include Start and the match loop.
- [Full loop host film](full-loop-host.mp4) and [guest film](full-loop-guest.mp4) — accepted P4 capture. A brief panel-resize crop in this earlier capture was corrected and separately verified in the final lobby film and final screenshot/navigation/performance run.
- [Male Scuba parity](male_scuba_unity_vs_scenekit.mp4), [male Pushups parity](male_pushups_unity_vs_scenekit.mp4), [female Scuba parity](female_scuba_unity_vs_scenekit.mp4), [female Pushups parity](female_pushups_unity_vs_scenekit.mp4). The Unity side uses independently sampled Unity CPU skinning checked against BakeMesh, because batch-mode GPU skinning cached a stale frame after manual clip sampling.
- [Measured proof and baseline](PROOF_METRICS.txt).
- [All 95 committed paths](FILES_CHANGED.txt).

Validation:

Native multiplayer tests pass 25/25. The larger selected native run exercised 83 tests; its sole screenshot-recorder rounding failure was corrected and the affected suites rerun successfully. Subsequent focused runs passed 28/28, 6/6, 27/27 and the final screenshot/navigation/performance run 3/3. These are successive focused runs, not a claim of one final 83-test run. Unity EditMode passes 249/249 and multiplayer PlayMode 5/5. Initial bare .NET baseline failures are recorded in BASE.md; the real Unity runner passes after correcting one stale skin assertion in the test. No gameplay fix was made for that assertion.

The full loop used iPhone 17 Pro and iPhone 15 simulators on iOS 26.5 with real Nearby TCP transport and the actual Unity runtime exported in the APFS clone. A scratch integration project enabled the Unity simulator runtime for this proof; the main simulator runtime guard was preserved. The driver prewarmed Unity, generated real authoritative motion events, and broke the actual guest TCP connection. It did not fabricate snapshots or results. The two additional spectators used mock participant messages.

Source export used the current installed FBXs, refreshed into the APFS clone after concurrent Scuba/Thrust source updates. All eight current source FBX/metadata hashes remain unchanged from that refresh. No source emote/serve FBX, mesh, shader, course, QualitySettings, URP or work/ path appears in the commit.

New Island components: IslandLobbyRow, IslandLobbyPlayerCard, IslandEmoteTile, all in IslandKit.swift. They reuse Island colors, rounded font tokens, existing card radius and lime/navy focus treatment.

Committed scope: five multiplayer Swift/configuration files, application prepare wiring, fourteen menu/locker/preview Swift files, four generated hero manifests/rigs, the exporter, eight Unity multiplayer script/metadata files, tests, the prompt and 52 screenshots. The complete paths are in FILES_CHANGED.txt. Player.swift did not require a change. Foreign staged content was preserved exactly: 24,723 index entries outside the whitelist.

Commit: `23cbedabb4074effa797c67c0dab987761d7aef8` — Add Island online lobby with synchronized heroes and emotes.
Branch: `newmapsandmenus`.
Push: successful without force; origin remote verified at this SHA.
Repository: [iphone-golf-simulator on GitHub](https://github.com/aloniewski2/iphone-golf-simulator).

Remaining device check: GAMECENTER_DEVICE NOT RUN. Two connected physical iPhones signed into separate Game Center accounts are needed to verify sign-in, invite acceptance, quick matchmaking and device recovery. The available physical phone could not support a two-device test. This is the exception expressly permitted by phase 5.4, not a tested Game Center claim.

Integration caveat: the tested working tree includes pre-existing native bridge, project configuration and gameplay integration changes outside the prompt's staging whitelist. Those files were preserved and left uncommitted. This scoped commit is therefore not claimed to reproduce the complete tested app from a clean checkout by itself; the companion backend integration work must also be committed by its owner.

Raw repository evidence (kept out of Git):

- Baseline: `work/online-lobby/BASE.md`, twelve images in `work/online-lobby/before/`.
- Generated export/check: `work/online-lobby/export/`, `export-check.txt`, `export-current.log`.
- Parity: `work/online-lobby/emote_parity/`.
- Accepted full loop: `work/online-lobby/film/accepted/` (host/guest videos, TSV events and runtime logs).
- Final lobby framing: `work/online-lobby/film/lobby-final/`.
- Unity runner: `work/online-lobby/unity-edit-final.xml`, `unity-multiplayer-play.xml` and corresponding logs.
- Final native run: `/Users/adnanyonathan/Library/Developer/XcodeBuildMCP/workspaces/find-48da97fc7db1/logs/test_sim_2026-10-05T20-08-19-571Z_pid31471_49f8db09.log`.
- Committed screenshots: `ArtDir/screenshots/online_lobby/`.

Repository root: `/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator`.

Account requirement clarification: Game Center prerelease testing uses the same server environment as released games. Separate ordinary Game Center accounts are sufficient; special StoreKit sandbox accounts are not required. See [Apple’s Game Center testing guide](https://developer.apple.com/help/app-store-connect/configure-game-center/overview-of-testing-game-center/).
