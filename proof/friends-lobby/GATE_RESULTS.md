# Friends lobby + controller lock: gate results, 2026-10-11

Branch `build-10/11`, uncommitted. Fixes Adnan's report of 2026-10-11:
- the phone switches away from the controller when inviting;
- characters freeze and only one player sees the lobby;
- golf benches open tennis;
- invites go through Game Center;
- friends don't show as online;
- invite notifications are invisible;
- with no TV there is no walkable menu.

## What was wrong (root causes)

| Report | Cause |
|---|---|
| "people aren't registered online" | 1) The Club Hub service was never hosted: `ClubHubServerURL` is empty, so nobody can be online. 2) Returning players were never signed in to Game Center at launch (only during first-run onboarding), so the hub saw them as signed out. 3) Apple's identity signature is made over the **teamPlayerID**, but the app sent the gamePlayerID, so every real sign-in would have failed once hosted. 4) Presence needed **both** friends to list each other, and Game Center only lists friends who allowed the friends list, which nobody was asked for until they opened the Friends screen. |
| "still routing to Game Center" | With no service, the in-game Invite buttons were disabled, so the only working path was Apple's invite sheet. Joining also depended on Game Center (the sheet, or an unproven private `playerGroup` room). |
| "phone switches / freezes / one sees the lobby" | 1) Apple's sheet covered the controller (the stick was gone: characters "freeze"). 2) Accepting a Game Center invite opened the old online-lobby screens. 3) `sync()` opened the lobby screen whenever the phone was not driving the plaza. 4) After a party match, `showMenu()` put the old TV menu and the results/lobby screens on one phone. |
| "golf ports to tennis" | The golf **online** bench opened the generic online menu (`onlineChoice` → `onlineQuick` → entry screen, tennis first). The golf round bench went through the old golf hub screen. |
| "in game notis are invisible" | Invites never arrived without the service. Where they did, the banner was an overlay on the root view, so any sheet (the Friends list, ☰, Apple's sheets) covered it. |
| no TV → old phone menu | `HubSession.wanted` required a TV; the phone fell back to `TennisPhoneMenu`. |

## What changed

- **Service (`club-hub/`).**
  - Party rooms: a relay for a friends' party's lobby, plaza and match traffic. Only the opener, invited friends and holders of the code may join.
  - Offline invites wait up to 15 min and are delivered when the friend opens the game.
  - Friends listed on either side count.
  - Frame size raised to 192 KB, with its own relay rate limit.
  - `fly.toml` and a deploy guide.
  - Contract: `club-hub/PROTOCOL.md`, "Party rooms".
- **App: friends.**
  - One-tap Invite for online **and** offline friends. Nothing in the app can open Game Center's invite or accept UI any more (the code was removed).
  - Join is one tap. The friend's phone enters the host's party over the relay, stays on its controller, and its hero is warped beside the host's (`hubMeet`).
  - Silent Game Center sign-in at launch. The friends-list prompt is asked once, after the first plaza arrival.
  - The hub identity is the teamPlayerID.
- **App: controller lock.**
  - `SportsHome` shows the Plaza controller whenever no match is on; the old TV remote and the phone menu are gone from the root.
  - No lobby or online screen ever opens.
  - After an online match, the result stays on the TV and the phone shows **Back to the Plaza**. The party returns to its bench together.
- **Benches.** Each bench is one sport and one mode:
  - golf round opens the course picker;
  - golf online and tennis online are a party bench, or with no party a quick match for that sport, all inside the controller;
  - sitting down together is ready, then a 2.5 s count;
  - friends elsewhere in the plaza don't hold the bench and are not pulled into the match.
- **Notifications.** The invite banner has its own window above everything; touches beside it pass through. The TV toast is unchanged.
- **Phone only.** With no TV the plaza runs on the phone:
  - Unity draws in the top 56 % of the screen, under a see-through app window; the same controls sit below it.
  - The PLAY hall and the benches are shut, with a "Connect a TV with AirPlay to play" message.
  - Connecting a TV moves the plaza onto it.

## Gates

| Gate | Result | Evidence |
|---|---|---|
| SERVICE: rooms relay, offline invites, either-side friends, limits | **PASS** 90/90 | `club_hub_npm_test.txt` (`cd club-hub && npm test`) |
| INVITE_RELAY: invite → one tap → in the host's party; plaza traffic both ways; leave | **PASS** | `ClubHubTests.testOneTapJoinPutsBothPhonesInOnePartyOverTheRelay` (in-memory service) |
| INVITE_RELAY_LIVE: the same against the **real** service on localhost, plus an offline friend's invite that arrives as they open the game, and a 3-person party | **PASS** | `ClubHubLiveServiceTests.testOneTapJoinIntoTheHostsPartyOverTheRealRelay` (`swift_tests_changed_suites.txt`) |
| REGISTERED: a friend who never allowed the friends list is still seen online, both ways | **PASS** | `ClubHubTests.testOneSideListingTheOtherIsEnoughToSeeEachOther`, service presence tests |
| OFFLINE_INVITE: one tap invites an offline friend; banner on next launch | **PASS** | `ClubHubTests.testAnOfflineFriendGetsTheInviteWhenTheyOpenTheGame`, service invite tests |
| CONTROLLER_LOCK: outside a match the phone is always the controller; old screens never show (TV and phone-only) | **PASS** | `HubSessionTests.testThePlazaIsTheOnlyMenuAndOldScreensNeverShow`, `HubPartyTests.testAFriendJoiningKeepsBothPhonesInThePlaza`, `controller_audit.txt` |
| NO_GC_INVITES: no Game Center invite / accept UI path left | **PASS** | `controller_audit.txt` (the same audit on the old code finds 30+ paths: `controller_audit_on_old_code.txt`) |
| BENCH_ROUTES: every bench is its own sport + mode; golf never opens tennis | **PASS** | `HubRouteTests.testEveryOldMenuDestinationIsReachable`, `HubPartyTests.testFourFriendsSeatedInTheGolfBay…` (golf config), audit §4 |
| SIT_TOGETHER: sitting together starts; friends elsewhere stay in the plaza; Back to the Plaza brings the party back | **PASS** | `HubPartyTests` (sitting, elsewhere, back-to-plaza, four friends), `friends-party-bench.png`, `friends-match-over.png` |
| BANNER_VISIBLE: the invite banner draws above a sheet; touches beside it go through, the banner takes its own | **PASS** | `FriendsLobbyProofTests.testTheInviteBannerShowsAboveASheet`, `friends-banner-over-sheet.png`. This test also caught a pass-through bug that would have swallowed every touch on the phone; fixed. |
| PHONE_ONLY (app): no TV → plaza wanted on the phone; start says phoneOnly; no bench seat | **PASS** | `HubSessionTests.testNoTVThePlazaOpensOnThePhone`, `friends-phone-only-plaza.png` (Unity's real still under the controller) |
| SEE_THROUGH (simulator, real app): with no TV the app's window lets the plaza window below show through above the controls; the banner sits on top; a tap on Not now goes to the banner, a tap on Menu goes through to the controller | **PASS** | `-phonePlazaCheck -phonePlazaInvite` launch (a painted stand-in for Unity, which the simulator does not have): `sim_phone_only_seethrough_with_invite.png`, `sim_phone_only_menu_after_banner.png` |
| PHONE_ONLY (Unity): plaza in the top 56 %, PLAY door / ☰ / benches refused with a message, locker + clubhouse open; with a TV PLAY opens | **PASS** 4/4 | `unity_phone_only_playmode.txt`, `unity_phone_only_plaza.png`, `unity_phone_only_play_shut_screen.png` |
| MEET: joining takes you beside your friend (their room; the plaza if their room is shut on a phone) | **PASS** | Unity `JoiningAFriendTakesYouToThem`, `OnAPhoneAFriendInASportRoomIsMetInThePlaza`, `unity_meet_friend_locker.png` |
| NO_REGRESSION Unity hub (bay loads, party sync, doors, arrival, toast, buttons) | **PASS** 15/15 | `unity_hub_regression_playmode_run1.txt` (its one failure, the phone viewport, was fixed and re-run: 4/4 above) |
| NO_REGRESSION Swift (changed + hub + multiplayer + menu + onboarding suites) | **PASS** 162/163 | `swift_tests_changed_suites.txt`. The one failure, `MenuBetaTests.testFeedbackAndBundledGameplay`, was already failing: `CFBundleDisplayName` was taken out of SportsInfo.plist by a local Xcode edit. |
| NO_REGRESSION full Swift suite | **PASS** (no new failures) | Full run: 378 tests, 26 failing cases. 25 are hero / locker SceneKit snapshot cases plus the MenuBeta plist case. The same 25 fail on a baseline build with this work reverted (`swift_baseline_head_hero_locker_suites.txt`). The 26th, `NewMenuSnapshotTests.testOnlineLobbyClickRemoteAndTouchPanels`, expected the old Leave confirm screen; it was updated to the new behaviour and passes. |

## NOT RUN (needs Adnan / devices)

- **HOSTING.** The service is not deployed. Until it is (`club-hub/README.md`: Fly.io, a few commands), `ClubHubServerURL` stays empty and the Friends list says friends online are unavailable. Afterwards, put `wss://<app>.fly.dev/v1/hub` in SportsInfo.plist.
- **Real Apple identity verification** against Apple's certificate and a real teamPlayerID. This needs a device and the hosted service.
- **Two real phones:**
  - invite, join, walk around in different buildings, sit together, match, back to the bench;
  - party matches over the relay: tennis latency through the service has not been measured;
  - the phone-only plaza on a device: Unity in the phone window, see-through, memory and heat. The simulator has no Unity.
- **Push for closed apps** needs the Push capability and an APNs key. Until then an offline friend sees the invite when they open the game, within 15 minutes.
