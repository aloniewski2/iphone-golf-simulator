# Club Hub protocol v1

The contract between the app (`GolfArcade/Social/`) and the Club Hub service (`club-hub/`). Both sides are built against this file;
change it here first. See `PLAN_OnlineHub_FriendsInvites.md` for why.

Transport: one WebSocket per running app at `/v1/hub` (JSON text frames, one message per frame, UTF-8). Plus a few HTTP routes.
Presence and invites are small. A friends' party's own traffic (its lobby, where everyone walks in the Plaza, its matches) goes
through the same socket as **party room** frames (below); the service relays them and stores nothing.

## Ids

- `player` id = the Game Center **teamPlayerID** string (the id Apple's identity signature is made over; friends' ids come from
  `GKPlayer.teamPlayerID` too). Opaque to the service. (Older builds sent gamePlayerID, which never verified on a real device.)
- `inviteID` = UUID string made by the service. `lobbyCode` = 6 chars from `ABCDEFGHJKLMNPQRSTUVWXYZ23456789`, made by the host
  app: it is also the party's room code. `group` = UInt32 (as a JSON number), kept for older builds; the app sends 0.

## Client → server

Every message has `"t"` (type). The first message must be `hello`; anything else first closes the socket with code 4401.

| t | fields | meaning |
|---|---|---|
| `hello` | `v:1`, `auth`, `name`, `friends:[id]`, `presence`, `app:{version,build,protocol}`, `push?:{token,env:"sandbox"\|"production"}` | Sign in and go online. |
| `presence` | `state`, `lobby?`, `invisible?` | Update my state (see below). Sent on every change, and at least every 20 s (the heartbeat). |
| `friends` | `ids:[id]` | My Game Center friends changed (re-read after the app returns to the foreground). |
| `invite` | `to:id`, `sport:"tennis"\|"golf"`, `lobbyCode`, `group`, `capacity` | Ask the service to deliver an invite (see rules). |
| `inviteResponse` | `inviteID`, `accept:bool` | The invited player's answer. |
| `inviteCancel` | `inviteID` | The sender withdraws it. |
| `resolve` | `code` | Look up a lobby by its code (non-app invites, "Paste invite"). |
| `block` / `unblock` | `id` | Hide each other (no presence, no invites, either direction). |
| `push` | `token`, `env` | Register or change the APNs token. `token:""` removes it. |
| `roomOpen` | `room` | Open my party's room (or take it back after a reconnect). Its code is the party's lobby code. |
| `roomJoin` | `room` | Enter a friend's party room (allowed after an invite to it, or after resolving its code). |
| `roomLeave` | `room` | Leave the room. |
| `rd` | `room`, `d`, `to?:[id]` | One frame of the party's traffic (`d`, text up to 128 KB) to everyone else in the room, or to `to`. |
| `bye` | | Sign out and appear offline now. |

`auth`: `{ "dev": "<id>" }` is accepted only when the service runs with `HUB_DEV_AUTH=1` (tests, local). Otherwise Apple's identity
verification: `{ "playerID": "<teamPlayerID>", "bundleID": "...", "publicKeyURL": "https://static.gc.apple.com/public-key/gc-prod-N.cer",
"signature": "<base64>", "salt": "<base64>", "timestamp": <uint64 ms> }`. The service checks: `publicKeyURL` is https on a
`*.apple.com` host; the certificate is downloaded (cached by URL) and its public key verifies `signature` over
`playerID + bundleID + timestamp(8 bytes, big endian) + salt` (SHA-256, RSA PKCS#1 v1.5); the timestamp is within 5 minutes; the
bundleID is in `HUB_BUNDLE_IDS` (comma list; default accepts any when unset). `id` for the rest of the protocol = `playerID`.

`presence.state`: `plaza` (walking the menu hub), `lobby` (in a lobby that has not started), `match` (a match is on), `away` (the app
is in the background, still connected a few seconds), `dnd` (do not disturb). `presence.lobby` = `null` or
`{ "code", "sport", "size", "capacity", "open": bool, "group" }` (`open`: friends may Join without an invite).
`invisible: true` = appear offline: the service treats the player as offline for everyone else but still delivers invites to them.

Rate limits per connection: 30 messages / 10 s (more closes with 4429), `invite` 10 / minute and at most 3 pending to one player.
`rd` frames have their own limit: 400 a second (more closes with 4429).

## Server → client

| t | fields | meaning |
|---|---|---|
| `welcome` | `you:id`, `serverTime` (ms), `v:1` | After a valid `hello`. Followed at once by one `presence` per online friend. |
| `presence` | `id`, `online:bool`, `state?`, `lobby?`, `name?` | A friend came online / changed / went offline. Sent when **either** player lists the other in `friends` (Game Center only lists friends who allowed the game to read their list, so one side is often missing), neither blocked the other, and the friend is not `invisible`. |
| `invite` | `inviteID`, `from:{id,name}`, `sport`, `lobbyCode`, `group`, `capacity`, `expiresAt` (ms) | Deliver an invite. |
| `inviteUpdate` | `inviteID`, `status` | To the sender: `delivered`, `accepted`, `declined`, `expired`, `cancelled`, `busy`, `unavailable`, `pushed`, `queued` (the friend is offline: it waits for them). |
| `inviteGone` | `inviteID`, `status:"expired"\|"cancelled"` | To the receiver: it is no longer valid (remove the card). |
| `resolved` | `code`, `found:bool`, `from?:{id,name}`, `sport?`, `group?`, `capacity?` | Answer to `resolve`. |
| `roomPeers` | `room`, `peers:[{id,name}]` | Who is in the room now (you included), to every member on each change. `peers: []` after you left. |
| `rd` | `room`, `from`, `d` | A party frame from `from`. |
| `error` | `code`, `message`, `ref?` | A rejected message (`bad_message`, `not_friends`, `rate_limited`, `blocked`, `too_many_pending`, `unauthorized`, and for rooms `room_gone`, `room_denied`, `room_taken`, `room_full`; `ref` = the room code, or `rd`). |
| `pong` | | Reply to a bare `{"t":"ping"}`. |

## Invite rules (service side)

1. The sender and receiver must be friends (either lists the other in `friends`; for an offline receiver, the list they had when
   last seen or stored with their push token) and not blocked. Else `error not_friends`.
2. An invite lives **120 s**, then both sides get `expired`. Sender cancel, sender going offline, or the lobby changing code → `cancelled`.
3. A newer invite from the same sender to the same receiver replaces the older one (`cancelled` for the old).
4. Receiver online (a live socket): `invite` goes out; sender gets `delivered`. Receiver `state: match` → still delivered, the app
   holds it (the app decides; the service does not hold).
5. Receiver offline or only `away` for > 10 s, with a stored push token: an APNs push (below); sender gets `pushed`.
   Receiver offline, no token (or push not configured): the invite **waits** for them for 15 min (`HUB_OFFLINE_INVITE_TTL_MS`) and
   the sender gets `queued`; when they open the game it is delivered with a fresh 120 s lifetime and the sender gets `delivered`.
   It is withdrawn if the sender goes offline first.
6. If the receiver has a live socket the service sends **no** push (never both).
7. `inviteResponse accept:true` → sender gets `accepted`; the receiver then enters the party room named by `lobbyCode` (`roomJoin`).
   `accept:false` → `declined`. A response to an expired / unknown invite → `error bad_message` with `ref`.

## Party rooms (the relay)

A friends' party (the Plaza's party: walking together, sitting on a bench together, the match that follows) talks through its room.

1. The host sends `roomOpen` with its lobby code when it makes the party (on its first invite). Opener and members may re-open it.
2. An `invite` whose `lobbyCode` is a room the sender is in lets the invited player in. So does `resolve` of that code (share links).
3. The invited player sends `roomJoin` after `inviteResponse accept:true`. Anyone else gets `error room_denied`; an unknown code
   `room_gone`; more than 8 members `room_full`.
4. Every member gets `roomPeers` on each change. `rd` frames go to everyone else in the room (or `to`), in order, unaltered,
   never stored. The app's frames are its multiplayer packets (`MultiplayerPacket` JSON).
5. A member whose socket drops leaves the room (the others get `roomPeers`); after reconnecting the app sends `roomJoin` (or
   `roomOpen` for the opener) again. A room nobody has been in for 2 minutes is gone.

## HTTP

- `GET /healthz` → `{ "ok": true, "online": N, "rooms": R }`.
- `GET /j/:code` → an HTML landing page ("Sam invited you to play …", install button, the code in big type, a
  `motionclub://j/CODE` open link). Looks the code up in live lobbies; if not found it says the invite has ended. Never reveals more
  than the host's display name and sport. Install URL from `HUB_INSTALL_URL`.
- `GET /.well-known/apple-app-site-association` → served from `HUB_AASA_FILE` if set (later: Universal Links).

## Push (APNs)

Sent only when configured (`HUB_APNS_KEY_FILE`, `HUB_APNS_KEY_ID`, `HUB_APNS_TEAM_ID`, `HUB_APNS_TOPIC` = bundle id; token-based auth
over HTTP/2). Payload: `aps: { alert: { title: "Sam invited you", body: "Tennis · Online" }, sound: "default",
"interruption-level": "time-sensitive", category: "CLUB_INVITE", "thread-id": "invites", "mutable-content": 0 }` plus
`inviteID`, `lobbyCode`, `group`, `sport`, `capacity`, `from`. `apns-expiration` = invite expiry, `apns-collapse-id` = `from` id,
`apns-push-type: alert`, `apns-priority: 10`. A 410 / `BadDeviceToken` removes the token. If unconfigured the service logs
`push skipped` and still reports `pushed:false` internally (sender gets `unavailable`).

## Config (env)

`PORT` (8090), `HUB_DEV_AUTH`, `HUB_BUNDLE_IDS`, `HUB_INSTALL_URL`, `HUB_AASA_FILE`, `DATABASE_URL` (optional Postgres for blocks
and push tokens; in-memory without), the APNs variables above.

## Clarifications

- inviteUpdate carries `to` (the receiver's id).
- `error.ref` is the `inviteID` when the rejected message carried one, otherwise the rejected message's `t`.
- `inviteUpdate` `busy` means the receiver's state is `dnd`: nothing is delivered and no invite is kept. `busy` carries an `inviteID` that names no stored invite.
- `pushed` is sent only after APNs accepted the push. If APNs refuses it, the sender gets `unavailable` instead and the invite is dropped.
- A receiver who has been `away` for more than 10 s (or is offline) is pushed to, and gets no socket frame. When that player's socket comes back (new `hello`, or `presence` leaving `away`) every still-pending invite for them is sent as an `invite` frame and the sender gets `delivered`. If there is no way to push, an `away` receiver is sent the invite over the socket.
- An explicit `inviteCancel` is also acknowledged to the sender with `inviteUpdate cancelled`. When neither lists the other any more, or either blocks the other, pending invites between the two are cancelled.
- `error not_friends` is also returned when the receiver has blocked the sender (so a block is not revealed); `error blocked` is only for a player the sender blocked themselves.
- `too_many_pending` is a cap of 3 pending invites per receiver (counted across senders); a replacement of the sender's own invite does not count twice.
- A `presence` message that changes `lobby.code` (or sets `lobby:null`) cancels the sender's pending invites for the previous code; invites already sent for the new code stay.
- `hello`: a bad or missing field, an unsupported `v`, bad auth: `error` (`bad_message` / `unauthorized`) then close 4401. No `hello` within 10 s: close 4401. `presence` inside `hello` may be omitted (defaults to `plaza`). A second socket for the same player replaces the first, which is closed with 4409. A connection that sends nothing for 65 s is closed (1001), so keep the 20 s heartbeat. `bye` closes with 1000.
- Frames larger than 192 KB close the socket. `resolve` is limited to 20 per minute (`error rate_limited`, connection stays open); `invite` over 10 per minute is also `error rate_limited` without closing. The 30 messages / 10 s limit closes with 4429 after an `error rate_limited`.
- `block` and `unblock` are not acknowledged; the effect shows as `presence` offline/online. Names are trimmed, stripped of control characters and cut to 40 characters.
- The push payload also carries `expiresAt` (ms, the invite's expiry) so a tapped notification shows the same countdown.
