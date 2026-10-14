# Party chat, voice and TV recording: gate results, 2026-10-11

Branch `build-10/11` (not pushed). Plan and Apple checklist: `PLAN_PartyChat_Recording.md`.

| Gate | Result | Evidence |
|------|--------|----------|
| Chat only reaches the people in your lobby; nobody outside, nobody who is not a real member | PASS | `PartyChatTests` (26), over in-memory lobbies |
| Filter: masks profanity (disguised, doubled letters, phrases), leaves real words alone, always hides links / emails / phone numbers, strips hidden characters | PASS | `PartyChatTests` (found and fixed a real bug: doubled letters in "asshole" / "kill yourself") |
| Age and parental limits: under 13 / unknown = quick phrases only, Game Center limits honoured, teens always filtered, "under 13" answer locked a week | PASS | `PartyChatTests` |
| Report (reason + their last 5 messages, muted at once), Mute, Block (removes old messages, silences voice, blocks invites) | PASS | `PartyChatTests`; renders `party-members.png` |
| Rate limits and flood protection (sender 1/s and 6/10 s; receiver drops > 5 in 5 s) | PASS | `PartyChatTests` |
| Recording service: one tap, TV only (display 1), config reaches the recorder, party told on/off, Photos permission asked up front, autosave to Photos, kept in app if Photos is off, auto-stop reasons explained, low storage refused, crash leftovers recovered, TV loss stops it | PASS | `PartyChatTests` over a fake recorder and fake Photos |
| Voice codecs, packets, jitter buffer, voice detection, engine state machine on a fake audio backend | PASS (60 tests) | `PartyVoiceTests.swift` classes; the agent also ran a macOS harness (89 checks) and TSan |
| Mixer (game + party voices + mic into one 48 kHz stereo track, limiter, resampling, underrun/overrun, thread stress) | PASS (16 tests, 47,278 checks, plain / TSan / ASan) | `Unity/Tools/native-tests/run.sh`, re-run by me |
| Native recorder compiles against the iOS SDK and the exported Unity headers | PASS | `Unity/Tools/native-tests/compile_check.sh`, re-run by me |
| Unity iOS export compiles the new C# and picks up the new plugin files | PASS (0 errors) | export log; files present in `Unity-iPhone.xcodeproj` sources |
| Point-clip hook still feeds the point recorder the same way | REVIEWED, NOT RUN | I read the diff; the point recorder was not run on a device |
| Controllers: dock does not cover anything on tennis or golf | PASS (simulator) | `dock-tennis.png`, `dock-golf.png` (golf's dock moved into the status strip after the first render showed it covering golf's TV button) |
| Settings reachable from every controller and the main menu | PASS (code + tests for the menu rows) | dock gear (all screens), pause menu, golf pause menu, controller settings form, main menu > Audio & voice |
| **Recording the real TV picture (Metal capture of Unity's TV surface) on a phone with a TV** | **NOT RUN** | Needs a device, a TV and AirPlay |
| **A/V sync, 60 fps cost, heat, hour-long run, background / thermal / TV-unplug auto-finish** | **NOT RUN** | Device |
| **Microphone, echo cancellation, AirPlay routing of the TV's game sound once voice is on, Bluetooth, Unity audio-session interplay** | **NOT RUN** | Device. Known risk: Apple's echo cancellation does not know about the TV's AirPlay stream, so open mic can leak game sound to the party; push-to-talk is the default |
| **Voice between two real phones over Game Center; chat during a match** | **NOT RUN** | Two devices |
| **Photos autosave on a real library; add-only permission prompt** | **NOT RUN** | Device (tested with a fake) |
| Full device build/link of the integrated app | NOT RUN | Not enough free disk for a Release device build; compiled per file with clang against the iOS SDK, Unity export OK; the next archive compiles it for real |
| Apple submission items (age rating answers, privacy label, privacy policy URL, support contact) | OPEN | Yours to do: see the checklist in the plan. `ClubSupportEmail` / `ClubSupportURL` / `ClubPrivacyPolicyURL` in SportsInfo.plist are empty |
