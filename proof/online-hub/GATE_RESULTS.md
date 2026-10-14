# Online hub: friends, presence, in-game invites. Gate results, 2026-10-11

Branch `build-10/11` (not pushed). Plan: `PLAN_OnlineHub_FriendsInvites.md`. Protocol: `club-hub/PROTOCOL.md`.

| Gate | Result | Evidence |
|------|--------|----------|
| Service (club-hub/): presence, invites, push, resolve, landing page | PASS (82/82) | `cd club-hub && npm test`; run again by me after the agent's report |
| Client protocol + presence rules (mutual friends only, appear offline, DND, blocks) | PASS | `ClubHubTests` (22), over an in-memory service |
| In-game invite: banner on the phone, TV toast, held during a match / loading, Not now, expiry, newest-replaces, stacking | PASS | `ClubHubTests`; `club-invite-banner.png`, `tv-invite-toast.png` (Unity test `AToastShowsOnTheTvAndGoesAway`) |
| Real WebSocket client against the real service (presence, invite, join answer, code resolve, offline) | PASS | `ClubHubLiveServiceTests` with the service on localhost (`HUB_DEV_AUTH=1`) |
| Hub screens | PASS (render) | `club-hub.png`, `club-hub-sent.png`, `club-hub-first-time.png` |
| Plaza invites open the hub, no old menu screens | PASS | `HubPartyTests`; Hub/Multiplayer/Route suites 124 tests, 1 failure that is also on main (`MenuBetaTests/testFeedbackAndBundledGameplay`: CFBundleDisplayName moved out of SportsInfo.plist by a local Xcode edit) |
| Unity: TV toast, no regression in the plaza boot / arrival tests | PASS | PlayMode 4/4 and 1/1 in a project copy |
| **JOIN_NO_GC_UI: a friend taps Join and lands in the host's lobby with no Game Center screen** | **NOT RUN** | The private `playerGroup` Game Center room is written (`GameCenterTransport.privateRoom`, `MultiplayerService.openPrivateRoom / joinPrivateRoom`) but needs two real phones. This is the plan's Phase 0 spike. |
| Game Center friends list, friend request sheet, identity verification | NOT RUN | Need a real signed-in Game Center account on a device |
| Real Apple certificate download, real APNs delivery, real Postgres | NOT RUN | Need Apple setup / a deployed service |
| Push notification on a device | NOT RUN | The app has no `aps-environment` entitlement yet (needs the Push Notifications capability); registering fails harmlessly until then |
| Service address in the shipped build | NOT SET | `ClubHubServerURL` in SportsInfo.plist is empty: until a service is hosted the hub shows Game Center friends and "Online status is not available in this build"; Invite buttons need the service, "Invite from Game Center" still works |

Not built (in the plan, deliberately later): Join a friend's open lobby without an invite, Universal Links (the `motionclub://` link works; the https landing page is served by the service), App Clip, a TV friends screen, the 4-phone soak.
