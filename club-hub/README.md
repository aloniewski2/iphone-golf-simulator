# Club Hub

Small service behind the in-game friends list: who is online, what they are doing, in-game invites (with an APNs push when the
friend's app is closed) and the `/j/CODE` invite-link page. **No gameplay data goes through it.** The contract with the app is
[`PROTOCOL.md`](PROTOCOL.md); the why is in `../PLAN_OnlineHub_FriendsInvites.md`.

Plain Node ES modules, `ws` for WebSocket, `node:test` for tests. `pg` is an optional dependency, loaded only when `DATABASE_URL` is set.

```
src/index.js    HTTP + WebSocket server (/v1/hub, /healthz, /j/:code, AASA); `node src/index.js` runs it from the environment
src/hub.js      connections, presence (mutual friends only), invites (120 s, replace, cancel), blocks, rate limits
src/auth.js     dev auth, or Apple Game Center identity verification (cert download + RSA-SHA256 signature check)
src/push.js     APNs token-based sender over HTTP/2 (injectable transport)
src/store.js    in-memory store + optional Postgres (blocks, push tokens + last-known friends)
src/landing.js  the /j/CODE HTML page
test/           end-to-end tests over real WebSockets on an ephemeral port
```

## Run it locally

```sh
cd club-hub
npm install
HUB_DEV_AUTH=1 npm start          # http://localhost:8090, WebSocket ws://localhost:8090/v1/hub
npm test                          # no network, no database, no Apple needed
```

Try it by hand with any WebSocket client, e.g. `npx wscat -c ws://localhost:8090/v1/hub`, then send
`{"t":"hello","v":1,"auth":{"dev":"alice"},"name":"Alice","friends":["bob"],"presence":{"state":"plaza"},"app":{"version":"1","build":"1","protocol":1}}`.

`HUB_DEV_AUTH=1` lets anyone sign in as any id. It is for tests and local runs only; never set it where the internet can reach it.

## Environment

| Variable | Default | Meaning |
|---|---|---|
| `PORT` | `8090` | listen port |
| `HUB_DEV_AUTH` | unset | `1` accepts `auth: { dev: "<id>" }` |
| `HUB_BUNDLE_IDS` | unset | comma list of allowed app bundle ids for Apple sign-in. **Unset accepts any**; set it in production |
| `HUB_INSTALL_URL` | unset | App Store (or TestFlight) link for the landing page's install button |
| `HUB_AASA_FILE` | unset | file served at `/.well-known/apple-app-site-association` (Universal Links, later) |
| `DATABASE_URL` | unset | Postgres for blocks + push tokens; tables are created on start. Without it they live in memory |
| `HUB_APNS_KEY_FILE`, `HUB_APNS_KEY_ID`, `HUB_APNS_TEAM_ID`, `HUB_APNS_TOPIC` | unset | APNs `.p8` key, its key id, team id, and the app's bundle id. All four needed, otherwise pushes are skipped with a log line |
| `HUB_INVITE_TTL_MS` | `120000` | invite lifetime (only change for tests) |
| `HUB_TRUST_PROXY` | unset | `1` reads the client address for the `/j` rate limit from `X-Forwarded-For` (behind a load balancer) |

## Point the app at it

The app's hub URL is `wss://<your-host>/v1/hub` (use `ws://<mac-ip>:8090/v1/hub` for a local run, with dev auth). Invite links are
`https://<your-host>/j/CODE`; the page offers `motionclub://j/CODE` and the install button.

## Deploy

One container, one instance (state is in memory; see "not done"):

```sh
docker build -t club-hub .
docker run -p 8090:8090 -e HUB_BUNDLE_IDS=com.yourcompany.app -e HUB_INSTALL_URL=https://apps.apple.com/app/id... \
  -e DATABASE_URL=postgres://... -e HUB_APNS_KEY_FILE=/secrets/AuthKey.p8 -e HUB_APNS_KEY_ID=... -e HUB_APNS_TEAM_ID=... \
  -e HUB_APNS_TOPIC=com.yourcompany.app -v /path/to/secrets:/secrets:ro club-hub
```

Put TLS in front (Fly.io, Render, Cloud Run with session affinity and a single instance, or a VM with Caddy). WebSockets must be allowed
through the proxy and the idle timeout must exceed 20 s (the app's heartbeat). Mount the `.p8` read-only; never bake it into the image.

## What is NOT done

- **One process only.** Presence, sockets and pending invites are in memory. Running two instances splits the online list. Scaling out
  needs a shared pub/sub (Redis or Postgres LISTEN/NOTIFY) and is not built.
- **Postgres is untested against a real database.** The SQL is exercised only through a fake pool in `test/store.test.js`.
- **The real APNs HTTP/2 transport and the real Apple certificate download are untested** (the tests use fakes, and the JWT, headers and
  payload are checked). First real run needs a sandbox push to confirm.
- **No replay protection** on Apple signatures within their 5 minute window (a client may legitimately reuse one when reconnecting).
- **The Apple certificate is not chained to an Apple root.** Trust comes from the `https://*.apple.com` host check, as the protocol says.
- No Universal Links content (only the file-serving hook), no admin/metrics endpoints, no data deletion route, no push for anything but invites.
- Block lists and push tokens are loaded whole into memory at start; fine for thousands of players, not millions.
- Player ids are not validated beyond "non-empty string, 128 chars max"; presence is kept only while connected.

## Protocol notes

Choices where `PROTOCOL.md` was ambiguous. The ones that affect the app (1-14, 17) are also appended to PROTOCOL.md under "Clarifications".

1. **`busy`**: no client message produces it, so it means the receiver is in `dnd`. Nothing is delivered and no invite is stored.
2. **`error.ref`** is the `inviteID` when the message has one, otherwise the message type (`invite`, `block`, ...).
3. **"At most 3 pending to one player"** is read as 3 pending per receiver across senders. With "newer replaces older" a single sender can
   never have more than one pending invite to the same player, so a per-sender reading could never trigger.
4. **Blocked**: a sender who blocked the target gets `error blocked`; a sender blocked *by* the target gets `error not_friends`, so blocks are not revealed.
5. **Away > 10 s**: treated like offline (push, no socket frame, "never both"). When the player's socket returns, waiting invites are
   delivered as `invite` frames and the sender gets `delivered` (after the earlier `pushed`). With no way to push, an away receiver still gets the frame over the socket.
6. **`pushed` is sent after APNs accepts**, not when queued; a refused push turns into `unavailable` (and the invite is dropped). A 410, `Unregistered` or `BadDeviceToken` also deletes the token.
7. **Offline mutual-friend check**: the friend list is only known while a player is connected, so the service remembers the last list in
   memory, and stores it with the push token. If nothing is known about an offline receiver, there is no token either, so the answer is `unavailable`, not `not_friends`.
8. **Lobby code change**: a `presence` whose `lobby.code` differs from the previous non-null code cancels pending invites that do not match the new code. Invites with no previous lobby are never cancelled by a presence that merely arrives later.
9. **Cancel acknowledgement**: `inviteCancel` also gets `inviteUpdate cancelled` back to the sender; replacing an invite sends `cancelled` for the old id.
10. **Removing a friend or blocking** cancels pending invites between the two.
11. **Close codes** beyond the two in the spec: bad or failed `hello` and a missing `hello` (10 s) use 4401; replaced by a newer socket for the same player is 4409; idle for 65 s is 1001; `bye` is 1000. The 30/10 s limit sends `error rate_limited` before 4429. The `invite` limit (10/min) and a `resolve` limit I added (20/min) only send `error rate_limited`.
12. **`hello.presence` is optional** (defaults to `plaza`); invalid values are rejected with 4401 like any bad hello.
13. **Names** are trimmed, stripped of control/bidi characters, capped at 40 code points, `Player` if empty.
14. **`inviteUpdate.to`** (added by the app team during the build) is on every status sent to the sender.
15. **`resolve`** returns any live lobby by code, including an invisible host's (the code is the credential), and works for non-friends.
    It only returns `from`, `sport`, `group`, `capacity`. The landing page shows only the host name and sport.
16. **`/j/:code` always answers 200**: the "invite has ended" page is a normal page, not a 404, and it never echoes the code. It sends `no-store`, a locked-down CSP and is rate limited to 60/minute per address.
17. **Only text frames** are accepted; frames over 16 KB close the socket.
