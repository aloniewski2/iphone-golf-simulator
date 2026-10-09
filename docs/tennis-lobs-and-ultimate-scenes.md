# Lob pursuit and ultimate scenes — 2026-09-27

## Shipped behavior

- Reachable high incoming balls now get a descending overhead intercept before their first bounce. The player runs to the existing Smash clip's measured racket offset and automatically attempts the overhead when close enough. This is a real swing/contact check, not an awarded hit. Ordinary returns, serves, unreachable balls and explicit dives retain their own handling.
- High-lob recognition uses a 3.3 m ballistic apex threshold. Planning accounts for reaction, current speed, stamina and court boundaries; the target height follows the locked hero's actual overhead reach. The ordinary planner previously favored a 1 m-high return, explaining why the character waited for lobs to fall.
- Perfect/forced-supercharged contacts, perfect serves and normal smashes no longer launch camera cuts. Titles/impact effects remain. Between-point reactions and serve setup are unchanged.
- Ultimates now have a 2.2 s three-shot sequence: face reveal (0–0.55 s), racket/torso charge (0.55–1.35 s), wider power pose (1.35–2.2 s). Court simulation freezes during the scene, then the normal camera returns before live contact. The existing hero/coil animation is reused.
- Skybreaker has orange accents and a lower charge tone; Rescue Lob mint and a higher tone; Curveball violet and a middle tone. Original synthesized rising cues; no third-party assets or voice lines.
- Gameplay HUD is hidden during the scene and restored on completion/disable. The dedicated ultimate title/effects remain visible. The existing arming and real-contact spending rules are preserved.

## Reference interpretation

- Nintendo's [official Smash fighter blog](https://www.smashbros.com/en_US/blog/index.html?category=cat02_fighter&pageCount=2) describes character-specific Final Smashes. Applied here as a recognizable named signature with a short staged reveal; no copied attack/art.
- Blizzard's [sound-design breakdown](https://news.blizzard.com/en-us/article/24262573/weekly-recall-let-s-break-it-down) discusses distinct targeting signals and an ultimate's separate sonic identity. Applied here as a different charge cue per ability. Overwatch is a readability/audio reference, not a claim that it freezes every match for a cutscene.

## Verification and limits

`TennisLobCinematicTests.LobChasePerfectCameraAndUltimate` passes: lob tracked, overhead attempted, one actual return, measured pending contact gap about 0.09 m. Also asserts no camera override for forced perfect/perfect serve/smash and time-scale restoration for all three scenes. The preview invokes each cinematic directly for review; it is not three naturally earned ultimates in one rally.

Preview: `ArtDir/anims/captures/LobSmash_UltimateScenes_60fps.mp4` (720p, 60 fps, silent Unity frame capture). Runtime charge audio is implemented but not present in this silent capture. Current V4 look and 1.2× visual tempo retained. No phone build installed.
