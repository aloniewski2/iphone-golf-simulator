# PLAN: Online Hub: friends, presence, in-game invites

Status: BUILT on branch `build-10/11` (2026-10-11, not pushed): service `club-hub/`, app `GolfArcade/Social/`, TV toast in Unity. Results and what is NOT RUN
(the Game Center private-room join, push, hosting) are in `proof/online-hub/GATE_RESULTS.md`. Decisions taken without an answer: Game Center friends only,
presence visible by default with Appear offline, TV toast only, invites wait until a match ends, banner before push.

Original status: PLAN ONLY. Authority for the lobby itself stays `SportsLibrary/MULTIPLAYER.md`;
this plan adds a social layer on top of it. Written for Adnan to approve; section 8 lists the decisions that need an answer.

## 1. What Adnan asked for

1. Invites arrive **inside the game**, not as a Game Center banner the player is forced to tap, so joining is the same every time.
2. One button opens an **online hub** (the Fortnite pattern): who is online, who is offline, invites, adding friends.
3. **Friend requests**, and **inviting people who do not have the app**.

## 2. What the platform allows (checked against Apple's docs, not memory)

| Need | Game Center / GameKit gives us | Consequence |
|---|---|---|
| Friends list | `GKLocalPlayer.loadFriends()`: mutual friends only; needs the player's permission (`loadFriendsAuthorizationStatus`, system prompt, Info.plist `NSGKFriendListUsageDescription`). A friend only appears if they also allowed it **and** run a build with our bundle ID. | The friend graph comes from Game Center. Friends who never opened our app are invisible to us. |
| Add a friend | `GKLocalPlayer.presentFriendRequestCreator(from:)`: Apple's own screen. We cannot build a custom compose screen. | "Add friend" is one button that opens Apple's sheet. Accept/decline of requests happens in Apple's Game Center UI, not ours. |
| Recent players | `loadRecentPlayers()` | Free "Recently played with" list. |
| **Online / offline** | **Nothing.** GameKit has no presence API. | Needs our own presence service. |
| In-game invite | Programmatic invite (`GKMatchRequest.recipients` + `findMatch`) exists, but it is delivered as a **system notification**, the very thing we are replacing, and forum reports of it dropping invites are common. | Our own channel must carry the invite; Game Center only connects the phones afterwards. |
| Invite someone without the app | Not a Game Center feature. | Share link/code + a web landing page + a TestFlight/App Store link. |

## 3. Architecture: three layers

```
 Phone (SwiftUI)                                   Our small service ("club-hub")
 ┌─────────────────────────────────┐                ┌──────────────────────────────┐
 │ ClubHubView  (one button)       │                │ WebSocket: presence + invites │
 │   Online · In a game · Offline  │  heartbeat     │ HTTPS: link landing, health   │
 │   Invites · Add friend · Recent │ ◄───────────►  │ Postgres: accounts, blocks,   │
 │ FriendsService  (GameKit)       │  invite/accept │   push tokens                 │
 │ PresenceClient  (WebSocket)     │                │ APNs: invites to backgrounded │
 │ InviteCenter    (inbox, cards)  │                └──────────────────────────────┘
 │ MultiplayerService (existing)   │
 └───────────────┬─────────────────┘
                 │ connect lobby (Game Center GKMatch, unchanged gameplay path)
```

1. **Identity + friends: Game Center.** Game Center stays the friend graph and the way phones connect for play (tennis timing needs
   direct peer-to-peer, not a relay). The player signs in to our service by proving who they are with Apple's identity
   verification (`GKLocalPlayer.fetchItems(forIdentityVerificationSignature:)`); the service stores no passwords.
2. **Presence + invites: a small service of our own.** A WebSocket per running app: heartbeat gives online/offline, "in the Plaza /
   in a lobby / in a match / do not disturb", and the lobby's join code. Invites are small messages routed to the friend.
   Built on the existing `server/` scaffold (Node, WebSocket, Postgres, rate limits, hashed tokens), which is legacy for the old
   Unity build but is the right shape. New service, new protocol; the old routes are not reused.
3. **Joining: invisible Game Center room.** Accepting an invite must not show a Game Center screen. Plan: the host and every
   invitee call `findMatch` with the same private **`playerGroup`** derived from the lobby, minimum = maximum = the party size, so
   Game Center pairs exactly that party with no banner. The existing hello handshake then rejects anyone who is not on the
   invite list. Fallback if the spike fails: `GKMatchRequest.recipients` with the invitee auto-accepting in code, and as a last
   resort today's `GKMatchmakerViewController`.

## 4. The hub (one button)

Entry: the Plaza phone's **Party / Invite friends** pill becomes **Friends** (badge = pending invites). Also on the TV: a toast.

Sections, top to bottom:
- **Invites**: cards with sender, sport, "Join" / "Not now". An invite already open when you are in a match waits as a badge.
- **Your party** (when you have one): who is in, host crown, Leave.
- **Online**: friends running the app now, each with a state chip (In the Plaza · In a lobby · In a match · Away) and a one-tap
  action: **Invite**, or **Join** if they are in an open lobby. Busy friends show "In a match" and queue the invite.
- **Offline**: friends not running the app. **Nudge** sends a Game Center invite as a normal notification (our fallback for
  people outside the app).
- **Add friends**: opens Apple's friend-request sheet; **Recent players** list; **Invite someone without the app** (share link).
- **Settings**: appear offline, who may invite me (friends / nobody), block list, delete my hub data.

Friend requests are sent from Apple's sheet; incoming requests and accepts are Apple's UI. We refresh the list when the app returns
to the foreground, so a newly accepted friend shows up without any extra step.

## 5. Invite flow (the part that makes joining consistent)

1. Host taps **Invite** on an online friend. Phone creates the lobby (as today) and asks the service to deliver
   `{from, sport, lobbyCode, partyGroupID, expires 2 min}`.
2. Friend's phone, in the foreground: a **card slides in** over the phone UI and the same toast on their TV. Backgrounded: an
   APNs push opens the app straight to the card. App closed: same push.
3. Friend taps **Join** → joins the private room → lands in the host's lobby/Plaza (the party flow already built today). Host's
   phone shows "Sam joined". No Game Center screen appears at any step.
4. Edge cases (each needs a test): invite expires; host leaves before accept; party full (4 golf / 2 tennis + spectators);
   friend already in another party or a match (queues, then asks); double-tap; two invites at once (newest wins, other kept in
   Invites); version mismatch (`MultiplayerPacket.version`); airplane mode mid-join (clear error, retry).

## 5b. How an invite shows up (the two surfaces Adnan asked for)

The service decides which one to use: if the friend's app has a live connection, they get the **in-game notification**; if not,
they get the **push notification**. Never both for the same invite (no double buzz).

### 1. In-game notification (app open)

Like a console game's invite pop-up: it appears over whatever the player is doing, can be taken or ignored in one tap, and goes
away by itself.

| Where the player is | What happens |
|---|---|
| **Plaza** (phone = stick) | A banner slides down from the top of the phone: friend's name and look, "invites you to **Tennis · Online**", **Join** and **Not now**, a 30 s countdown bar. It sits above the status strip and never covers the stick, so a thumb that is walking keeps walking. Soft sound + haptic. The TV shows a matching toast in the corner ("Sam invited you · accept on your phone") and the sender's hero can wave. |
| **Any phone menu screen** (locker, settings, hub) | The same banner over the screen. |
| **Loading, a transition, the Plaza arrival fly-in** | Held, shown the moment it ends. |
| **In a match** (phone = racket / golf controller, or the TV is showing play) | **Not interrupted**: a swing must never be broken by a banner. A small dot appears on the pause/exit control and the invite waits in the hub; shown on the results screen or when the player returns to the Plaza. (Option: show it on the TV only, as a quiet toast. Needs a decision.) |
| Banner ignored or timed out | Moves to **Invites** in the hub (badge on the Friends button) until it expires (2 min). Join from there still works. |
| Several at once | Newest on top, the rest stacked as "+2 more" in the hub. |

Both phone and TV banners use the existing Motion Club look (navy, lime accent, Rubik/Bricolage), and honour Reduce Motion,
Sound off and Haptics off in Settings.

### 2. Push notification (app in the background or closed)

- A normal iOS notification: "**Sam** invited you to play **Tennis**" with two action buttons on long-press: **Join** and
  **Not now**. Tapping the notification, or **Join**, opens the app straight into the join (Plaza loading bar, then the party),
  with no menu to go through.
- Marked **time-sensitive** so it can break through a Focus, with a 2-minute expiry (an old invite never arrives late) and a
  collapse key so repeated invites from one friend replace each other.
- The notification **permission is asked at a good moment** (after the player adds their first friend or sends their first
  invite, with a plain-words explanation), never on first launch. If they decline, the in-game banner and the hub badge still
  work, and offline friends are reached with the Game Center "Nudge" fallback.
- If the app is open when the push arrives, iOS is told to suppress it, and the in-game banner shows instead.

### Three levels, so every friend can be reached

1. **In the app** → in-game banner (above). Instant.
2. **Has the app, not open, push allowed** → push notification (above). A few seconds.
3. **Push declined or never opened the app** → Game Center "Nudge": a normal Game Center invite notification, the old way.
   Still works, but it is the one we cannot style.

## 6. People without the app

- **Share link** (iOS share sheet: Messages, WhatsApp, anything): `https://<domain>/j/<CODE>`. Opens the app on a phone that has
  it (Universal Link, needs an associated-domains entitlement and a hosted `apple-app-site-association` file).
- **Landing page** for phones that don't: "Sam invited you to play Motion Club" + install button (today the app is beta, so the
  button is a **public TestFlight link**; later the App Store) + the short code.
- **After install:** iOS cannot pass the link through an install. v1: the code is shown big on the page; the app's first screen
  after install has a **Paste invite** button (the system paste button, no permission prompt). Phase 2 candidate: an App Clip.
- They must sign in to Game Center to play online; the first-run flow already offers that.

## 7. Phases (each ends with a binary gate and proof)

| # | Phase | Gate (PASS = proven on two phones) |
|---|---|---|
| 0 | **Spike**: (a) private-`playerGroup` room joins with zero Game Center UI; (b) `loadFriends` returns a real friend; (c) identity-signature sign-in to a stub service | JOIN_NO_GC_UI: friend taps a fake card and lands in the host's lobby, no Apple screen. If FAIL: switch to the fallback join in §3 before building anything else |
| 1 | Backend + presence: service, auth, heartbeat, state chips, block list, deployment, health | PRESENCE: friend shows Online within 5 s, Offline within 40 s of killing the app |
| 2 | Hub UI + friends: Friends button, Online/Offline/Recent lists, Add friend (Apple sheet), settings | HUB_LISTS, ADD_FRIEND (sheet opens, request sent) |
| 3 | In-game notification (§5b-1): banner on phone + TV toast, hold during matches/loading, accept → join, expiry/busy/full rules | INVITE_INGAME (banner never covers the stick; no banner mid-match), plus unit tests for every edge case in §5 |
| 4 | Push notification (§5b-2): APNs for background/closed, Join/Not now actions, permission prompt, Game Center Nudge for the rest | INVITE_PUSH: backgrounded phone gets the notification in < 3 s, **Join** lands in the party; suppressed when the app is open |
| 5 | Non-app invites: share link, landing page, Universal Link, Paste invite | INVITE_NOAPP: a phone with a fresh install joins from the link text |
| 6 | Hardening: rate limits, spam/block/report, privacy label, data deletion, 4-phone soak | SOAK: 4 phones, 30 min, join/leave churn, no stuck lobbies |

Tests follow the repo's habit: Swift tests over an in-memory bus for protocol and edge cases (like `HubPartyTests`), a fake
service for the UI snapshots into `proof/online-hub/`, and a device checklist for everything real. Anything not run on real phones
is marked NOT RUN in `GATE_RESULTS.md`; it is never reported as passing.

## 8. Decisions needed from Adnan

1. **Hosting**: we need a place for the service and a domain. Recommended: one small cloud host (about the cost of a coffee a
   month) + a domain such as `motionclub.app`. Alternative: CloudKit (no server) but presence there is clumsy and it still needs push.
2. **Apple setup** (only the account owner can do these): create an **APNs key**, add the **Push Notifications** and **Associated
   Domains** capabilities to the App ID, and (for non-app invites) a **public TestFlight link**.
3. **Friends outside Game Center**: v1 is Game Center friends only. Do you also want our own friend codes (so people can add each
   other without Game Center)? Everyone still needs Game Center to play online, so I recommend no for v1.
4. **Online/offline visibility**: default "friends see when I'm online", with an Appear offline switch. OK?
5. **Hub scope on the TV**: only a toast when an invite arrives, or also a TV friends screen? Recommended: toast only.
6. **Invites during a match**: wait until the match ends (recommended), or a quiet toast on the TV only?
7. **Push before the rest?** Push needs the Apple setup in item 2, which is the slowest part; the in-game banner does not. I would
   build the banner first (Phase 3) and push right after (Phase 4); say so if you want push earlier.

## 9. Risks and what we don't know yet

- The private-room join (§3) is unproven: that is exactly why Phase 0 comes first. If it fails we fall back, and the "no Game Center
  screen" promise weakens to "one automatic, pre-accepted prompt".
- Game Center friend lists only show friends who allowed it and run our bundle ID; the list will look emptier than the player's
  real Game Center list at first. The hub says so ("Friends who have Motion Club appear here").
- Presence is only as accurate as the app being in the foreground: iOS suspends backgrounded apps, so "Away" within about 30 s is
  the honest state, not "Online".
- The service becomes a thing to keep running and secure (rate limits, abuse, GDPR/CCPA deletion). The plan keeps it as small as
  possible: no gameplay data goes through it.
- No Game Center, no online play: players who decline sign-in see the hub with a "Sign in to Game Center" card.
- iOS cannot carry an invite through an app install without a third-party SDK or an App Clip; v1 uses a visible code.
