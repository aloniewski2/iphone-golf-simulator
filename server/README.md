# Golf Arcade server

The backend for the Unity game: **profiles**, **round history and stats**, a **leaderboard**, and
**online rooms** where two to four phones play the same course at the same time. One small Node
process (HTTP + WebSocket on one port) and one Postgres database.

```
phone (Unity) ── HTTPS ──▶ /v1/players, /v1/me, /v1/rounds, /v1/leaderboard
              ── WSS ────▶ /v1/play  (rooms: create / join by code / quick match, live scores)
                                │
                            Postgres  (players, rounds)
```

## How online play works

Everyone plays on their own phone, with their own golfer, at the same time. The host's room has a
**seed**; every phone builds the round from it (`Match.WindFor`), so each hole has the same wind for
everybody. When a player holes out, their phone sends `{type:"hole", hole, strokes}`. The server
checks it and passes it on to the others, and their HUD shows it ("Sam: Birdie!"). The server keeps
every card. A phone that drops out and reconnects gets its seat and its scores back. A player who is
gone for two minutes stops holding up the round. When every card is in, the server works out who
won and records the round for each player.

The swing and the ball flight never touch the network. That keeps the game playable on a phone
connection, and the server stays tiny.

## API

| | |
|---|---|
| `POST /v1/players` `{name, body, kit, shirt}` | Sign up (anonymous). Returns `{player, token}`. The app keeps the token on the profile. |
| `GET /v1/me` | The signed-in player with stats and their last 10 rounds. |
| `PATCH /v1/me` `{name?, body?, kit?, shirt?}` | Rename, or change golfer (0 male, 1 female), kit colour and shirt colour (0–5 each, `GolferStyle`'s order). |
| `POST /v1/rounds` `{course, strokes[], result?}` | A finished solo or same-phone round. |
| `GET /v1/players/:id` | Anyone's public profile. |
| `GET /v1/leaderboard?course=cliffside&limit=20` | Best round per player. A course key is its full round (`cliffside`: holes 7, 12–15); `cliffside-12` is one hole on its own. |
| `GET /healthz` | For the host's health check. |
| `WS /v1/play` | First message `{type:"hello", token}`, then `create`, `quick`, `join{code}`, `start`, `hole{hole,strokes}`, `leave`. |

Authenticated calls send `Authorization: Bearer <token>`. Only a SHA-256 hash of the token is
stored. Sign-ups are rate-limited per address (10 a minute), and all requests are limited to 240 a
minute.

## Run it locally

```bash
cd server
npm install
npm start                      # in memory: no database needed
# or with Postgres:
DATABASE_URL=postgres://user:pass@localhost:5432/golf?sslmode=disable npm start
```

Then in the game, open **Online** and put `http://<your-mac's-LAN-address>:8080` in the server box.
For a phone build that talks to plain `http`, turn on *Project Settings → Player → Other Settings →
Allow downloads over HTTP* (development builds only). A deployed server uses `https`, which needs
nothing.

Tests: `npm test`. That covers the rules, the rooms, and the HTTP and WebSocket API end to end. Set
`TEST_DATABASE_URL` to a scratch Postgres database to run the store tests against Postgres too.
The **server** GitHub workflow runs both on every pull request that touches `server/`.

## Deploy it

It is a standard container (`Dockerfile`), so it runs anywhere. Set `DATABASE_URL`, and set
`TRUST_PROXY=1` behind the host's load balancer. The tables are created on first start.

**Google Cloud** (if your credits are Google's):

```bash
gcloud run deploy golf-arcade --source server --region us-central1 --allow-unauthenticated \
  --min-instances 1 --max-instances 1 --timeout 3600 \
  --set-env-vars TRUST_PROXY=1,DATABASE_URL=postgres://…
```

Keep **one instance**. Rooms live in the server's memory, so every phone in a room must reach the
same process. `--timeout 3600` keeps WebSockets open for a whole round. The same goes for Fly.io,
Render, DigitalOcean App Platform, AWS App Runner and Azure Container Apps: one always-on instance
plus a managed Postgres.

When it's up, put its `https://…` address in `BackendConfig.DefaultServerUrl`
(`Unity/Assets/Scripts/Net/Online/BackendClient.cs`) so players don't have to type it.

## What to spend $100 of cloud credit on

The game's physics, graphics and swing detection all run on the phone, so the backend is cheap.
In rough monthly cost:

| | ~/month | Why |
|---|---|---|
| **This server**: one small always-on container (0.25–0.5 vCPU, 512 MB) | $5–15 | Profiles, stats, leaderboard, online rooms. Thousands of concurrent phones fit on one small instance. |
| **Managed Postgres**, smallest tier | $7–15 | Players and rounds, with backups. (Neon or Supabase free tiers work too, and cost $0 while small.) |
| Logs and uptime check | $0–2 | The host's built-in logging plus a `/healthz` check. |

**About $15–30 a month, so $100 runs the whole backend for 4–6 months**, or longer on a free-tier
database. Worth it, in this order:

1. **Deploy this server + Postgres.** It turns on online play, profiles that sync, and the
   leaderboard.
2. **TestFlight friends on real phones.** Online play is the thing to test, and it costs nothing
   extra.
3. Later, only if needed: object storage (~$1) for shareable swing replays, or push notifications
   (free via APNs) for "your friend finished their round".

Not worth it yet: GPU instances or ML hosting (swing detection runs on the phone), Kubernetes, or a
multi-region setup. Also not worth it: a paid game-server product. Rounds are turn-light and score-only,
so a WebSocket relay covers it. Apple's **Game Center** is free and could be added later for
friend invites and achievements alongside this server. Set a **budget alert** at $20/month in the
cloud console so the credits can't run out by surprise.
