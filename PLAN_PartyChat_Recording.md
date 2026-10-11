# PLAN: Party chat (type + talk), recording, and the Apple checklist

Status: BUILT on branch `build-10/11` (2026-10-11). Device gates are NOT RUN: see `proof/party/GATE_RESULTS.md`.
Related: `PLAN_OnlineHub_FriendsInvites.md` (the party is the lobby the Friends hub builds).

## What Adnan asked for
1. A party chat where people can **type or talk, only to their own party**, with the precautions and functions Apple expects.
2. A **settings gear** for chat, and **voice + recording settings on every controller** (tennis, golf, the Plaza, the menus).
3. **One-click recording** from the controller, autosaved to the **camera roll** on stop, **with the party's voice in it**.
4. Recording must capture the **TV (external screen), not the phone controller**.

## Design

### Party channel (who can hear and read you)
Chat, voice frames and "I am recording" notices travel as three new packet kinds (`partyChat`, `partyVoice`, `partyRec`) over the
**lobby's own transport** (Game Center / Nearby), in every phase (lobby, loading, a match). The receiver accepts them only from a real
(non-guest) member of the same lobby. Nothing is stored or relayed by a server; messages live in memory for the session. Pass-the-phone
guests never get a party channel.

### Safety (what Apple wants around communication features)
| Rule | What the app does |
|---|---|
| 1.2 filter objectionable material | `ChatFilter`: masks profanity/slurs (also leetspeak and spacing), always hides links, emails, phone numbers; strips bidi/control characters; 140 chars; the sender's filter applies before sending (teens are always filtered) and the receiver's again on arrival. Basic by design: it is not a promise, which is why the next three exist. |
| 1.2 report | Report on every message and every person (reasons list); builds a report with the person's last 5 messages and opens a message to support for the player to send (never silent). Muted at once. |
| 1.2 block | Block/Mute on every message and in the members list; blocks also remove their old messages, silence their voice, and stop their friend invites (shared with the Friends hub). |
| 1.2 published contact | Safety section: support contact (`ClubSupportEmail` / `ClubSupportURL` in Info.plist; until set, the beta text-message line), privacy policy link (`ClubPrivacyPolicyURL`). **You must fill these in before App Store review.** |
| 5.1.4 / COPPA | Neutral age question (birth year only, kept on the phone, locked a week after "under 13" so it cannot be retyped). Under 13 and unknown age: **quick phrases only**, no typing, no voice, no typed text from others. 13-17: filter locked on. |
| Game Center parental limits | `isMultiplayerGamingRestricted` → no party features; `isPersonalizedCommunicationRestricted` / `isUnderage` → quick phrases only. Re-read when the app returns. |
| 5.1.1 / 2.5.14 microphone | Voice is **off by default**; the mic and the audio session are touched only after the player turns it on, accepts the rules and grants permission. Purpose string says who hears it and that a recording can include it. Visible red mic whenever transmitting. Modes: push-to-talk or open mic (voice-activity gated). |
| Recording consent | Every party member sees a red "X is recording" banner while someone records; recording includes others' voices only if the recorder keeps "Party voices" on (default on, as asked). This is a notice, not legal advice: laws about recording voices differ by country/state. |
| Photos | Add-only permission (`NSPhotoLibraryAddUsageDescription`), asked when recording starts, never reads the library. |

### Voice (`GolfArcade/Party/VoiceCodec.swift`, `PartyVoiceEngine.swift`, `PartyVoiceCoordinator.swift`)
16 kHz mono, 20 ms frames, a codec picked at runtime by probing which AVAudioConverter codec round-trips (AAC-ELD, iLBC, or built-in
PCM), jitter buffer with loss concealment, Apple's voice processing (echo cancellation: the TV plays game sound the phone mic can
hear), per-person volume and mute, voice-activity detection. The audio session is changed only while voice is on (`.playAndRecord` with
`.allowAirPlay` so the TV keeps the game's sound) and restored when it stops. The interplay with Unity's own audio session and AirPlay
routing is **unverified without a phone**: see `proof/party/PARTY_VOICE_NOTES.md`.

### Recording (`RecordingService.swift` + the Unity plugin `SportsSessionRecorder.mm`)
ReplayKit is not usable (Apple: it does not work with AirPlay, and it captures the phone). Instead the app's existing point-clip
recorder idea is extended: the native recorder reads Unity's **resolved TV surface** (the same Metal target that goes to the TV,
HUD included) on the display that is not the phone, encodes H.264, and mixes **game sound + party voices + your mic** on its own audio
clock into one AAC track. Start/stop is a C call (no match or Plaza session needed), so recording works in the Plaza and in both
sports. On stop the file goes to Photos (add-only); if Photos is off it is kept in the app with Share and Delete. Auto-finish (file
kept): app leaves the screen, thermal critical, low storage, TV disconnect, 90 minutes. A recording left by a crash is saved on next
launch. Quality: 720p/1080p at 30/60 fps. Default 1080p30 (the phone is already running the game and AirPlay).

### Controls and settings
`CaptureDock` (record, mic, chat, gear) sits over every phone screen: the Plaza, tennis and golf controllers, the menus. It shrinks to
a record dot and the mic during motion-controlled play so a swing cannot open a sheet. The gear opens one settings screen (voice, chat,
recording, safety); the same sections are in the pause menu, the controller settings form, and the main menu's **Audio & voice** tab.

## Apple submission checklist (only you can do these)
1. **Age rating** (App Store Connect → App Information → Age Rating): answer **Messaging and Chat: Yes**; Social Media: No; User-Generated
   Content: No (nothing is broadcast; chat is private to a party); Unrestricted Web Access: No. Apple computes the rating: look at the
   result before submitting. Re-answer on the next submission.
2. **App Privacy label**: declare **Audio Data** (voice chat, not linked to identity, not used for tracking; sent only to other players)
   and note that recordings stay on the device. If any third-party SDK is added later, update it.
3. **Privacy policy URL** (App Store Connect field) and the in-app link: set `ClubPrivacyPolicyURL`, `ClubSupportURL` or
   `ClubSupportEmail` in `GolfArcade/Unity/SportsInfo.plist`. Say in the policy: what voice/chat data exists, that it is peer-to-peer and
   not stored, the age rule, and how to contact you.
4. **Review notes**: explain that voice/chat are private to a party of invited friends, off by default, with report/block/mute, age and
   Game Center parental limits; give a way for the reviewer to test (two devices or a demo account).
5. **Capabilities**: none were added (microphone and Photos need only the Info.plist texts). Optional later: Declared Age Range
   (iOS 26) to replace the age question; it needs the capability and an entitlement.
6. Test on **two real phones with a TV** before any submission: `proof/party/GATE_RESULTS.md` lists what has not been run.

## Not built (deliberately)
Voice or typed chat to people outside the party; public rooms; proximity chat; a chat log saved anywhere; automatic moderation of
voice; Discord/FaceTime integration (SharePlay is the lighter alternative if a call is wanted); a TV chat overlay (the TV picture is
what gets recorded, so nothing from chat is drawn on it).
