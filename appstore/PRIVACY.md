# App Store Connect: privacy and compliance checklist (Motion Club)

Based on a read of the `final-build` code (Oct 8) and checked against Apple's [App Privacy Details](https://developer.apple.com/app-store/app-privacy-details/) page. Not legal advice, and Apple's wording changes, so confirm each
answer against the live form. **The one fact only you can confirm: does the release build talk to your own server?**

## What the code actually does
| Area | Finding | Evidence |
| --- | --- | --- |
| Analytics / ads / crash SDKs | None active. Unity's analytics package is installed but **disabled** (Analytics, Ads, Insights, Performance Reporting, Purchasing all `m_Enabled: 0`). No Firebase, Sentry, Meta, AdMob, IDFA or ATT code found. | `UnityConnectSettings.asset`, repo grep |
| In-app "analytics" | A **local-only** log (`Analytics.track` -> `SportsDiagnostics.log` in the app's Documents folder). Never uploaded; comment in code says identity and camera contents never enter it. | `Analytics.swift`, `SportsMotion.swift` |
| Camera | Rear camera for ARKit phone-position tracking, **processed on device**, not recorded or uploaded (the permission string says so). | `project.yml`, `SportsMotion.swift` |
| Motion | Core Motion swing data, on device only. | `SportsMotion.swift` |
| Online play (Quick Match / Play with Friends) | Through **Game Center / GameKit** (Apple). Match data goes peer-to-peer via GameKit, not to your server. | `GameCenterTransport.swift` |
| Nearby lobby | Local Wi-Fi via Bonjour (`_motionparty._tcp`), no server. | `LocalMultiplayerTransport.swift` |
| **Your own server** (Unity golf "Online") | Exists: anonymous sign-up creating a player (display name, golfer style, token hash, stats), finished rounds, leaderboard, rooms. **Default URL is a personal Mac on plain `http`**, so it looks like a dev setup, not a deployed backend. | `BackendClient.cs`, `server/` |
| Feedback | Opens Messages with a pre-filled SMS; the user sends it. | `proof/menu-beta/GATE_RESULTS.md` |

## "Do you collect data from this app?" -> pick one

**Scenario A: release build does not call your server (Game Center and local only). Likely today.**
- Choose **"No, we do not collect data from this app."** The label then reads *Data Not Collected*.
- Apple: "Collect" means "transmitting data off the device in a way that allows you and/or your third-party partners to access it for a period longer than what is necessary to service the transmitted request in real time." Data "processed only on device is not 'collected'", so the camera, motion and local log don't count. For Game Center: "You are not responsible for disclosing data collected by Apple", but anything you pull from GameKit and keep on your own systems would count.

**Scenario B: release build uses your server (profiles, leaderboard, online rooms).**
Choose **Yes**, then tick:
| Data type | Why | Linked to user? | Used to track? | Purpose |
| --- | --- | --- | --- | --- |
| **Identifiers > User ID** | Anonymous player ID plus the display name/handle shown to others | Yes | No | App Functionality |
| **User Content > Gameplay Content** | Rounds, scores, stats, leaderboard entries | Yes | No | App Functionality |

Apple's own wording for these two: **User ID** is "screen name, handle, account ID, assigned user ID ... or other user- or account-level ID"; **Gameplay Content** is "saved games, multiplayer matching or gameplay logic, or user-generated content in-game". A token or IP address that is only used for a request and not kept does not need disclosing, but the stored token hash and display name are retained, so they do.

Do **not** tick: Contact Info (unless you ask for a real name or email), Location, Health, Financial, Contacts, Photos, Browsing/Search history, Usage Data, Diagnostics, Device ID/IDFA, Purchases. Tracking answer for everything: **No**.
Also needed in Scenario B: a real **https** server (the default is `http`, which iOS blocks without an exception) and the account-deletion item below.

Either way, answer **No** to "used for tracking", and keep the label in step with the code. If you ever turn on Unity Analytics or any SDK, add Usage Data (Product Interaction), Diagnostics and Device ID as needed. Apple says you can change your answers in App Store Connect at any time, with no app update needed, and you are responsible for keeping them current.

## Other App Store Connect items to tick
- **Privacy Policy URL**: required for every app, even with no data collected. Needs a public page. (I can draft one for either scenario.)
- **Age rating**: no violence, gambling, mature content or chat. The only user-visible text from others is player names in rooms and leaderboards, so answer the user-generated-content question conservatively.
- **Export compliance**: HTTPS/WSS and standard system encryption only, so "uses encryption, exempt". Set `ITSAppUsesNonExemptEncryption = NO` in Info.plist so you aren't asked on every build (currently unset).
- **Game Center**: the entitlement is in the project. Enable Game Center for the app in App Store Connect and configure it.
- **App Review notes**: the game wants an external display and motion. Give the reviewer steps, name the touch/on-phone preview fallback, and say TV play uses AirPlay or a cable. Add a demo account only if you ship accounts.

## Fix before you submit (found in the code)
1. **No `PrivacyInfo.xcprivacy` in the repo.** The native code uses required-reason APIs: `systemUptime` (System Boot Time, 11 files), `UserDefaults` (14 files) and file timestamps (1 file). Add a manifest with `NSPrivacyTracking = false`, no collected types (or the Scenario B list), and reasons: UserDefaults `CA92.1`, System Boot Time `35F9.1`, File Timestamp `C617.1` if it reads timestamps inside the app container. Unity's exported project adds its own manifest, so run **Xcode > Product > Archive > Generate Privacy Report** and check the combined result.
2. **Nearby lobby needs local-network keys.** The app browses Bonjour but the shipped `SportsInfo.plist`/`project.yml` has no `NSLocalNetworkUsageDescription` or `NSBonjourServices` (`_motionparty._tcp`). The only usage string is in a Unity editor script and still says "Golf Arcade ... your Mac". Add both keys with current wording, or the lobby will fail and review may flag it.
3. **Account deletion (Guideline 5.1.1(v)) in Scenario B.** Sign-up creates an account on your server, and there is no delete endpoint or in-app delete. Add one (`DELETE /v1/me` plus a Settings button) or keep server profiles out of v1.
4. **Name and strings still say "Beta" / "Golf Arcade"** in places (display name, the old local-network string). Align before submission.

## Notes from checking Apple's page
- **Third-party code counts.** Apple says answers must cover "the practices of third-party partners whose code you integrate", even if you don't use them for analytics or ads. The Unity runtime is such code. Its analytics are off in this project, but confirm with Xcode's privacy report on the real archive.
- **Motion and camera data.** Apple lists the Motion and Fitness API under *Fitness*, and camera scans under *Surroundings*/*Body*. Both are on-device only here, so nothing is declared. If you ever send swing metrics, TV distance or other camera/motion-derived values to a server, that derived data has to be considered separately.
- **Feedback button.** It opens Messages and the user sends the text, so the app itself transmits nothing. If you ever send feedback through your own server instead, it becomes *Customer Support* data.
- **Optional-disclosure carve-outs do not help here.** They only cover infrequent, optional, user-initiated submissions, not data collected on an ongoing basis such as profiles, stats or a leaderboard.
- **Privacy Choices URL** is optional. If a server ships, it is a good home for the delete-my-data instructions.
