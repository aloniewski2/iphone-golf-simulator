# The walkable lobby ("the club")

The home screen is a small 3D club you walk around as your own character. Each spot opens a menu the app already had; no new menus were built. It is native SceneKit (not Unity, which only starts for a match), and everyone in your party is in it, walking live.

## What is there

A round plaza with a bell tower in the middle (you always know where you are) and four short paths. None of them runs through another area.

| Place | Where | What it does (A in the ring) |
|---|---|---|
| Locker | west | three rings: **Gear**, **Colors**, **Emotes** open the Locker on that tab |
| Play Gate | north | **Tennis** and **Golf** open that sport's game picker; **How to Play** opens the guide |
| Party Terrace | east | raised deck; the **Party Board** opens the party menu; four spots where the party stands |
| Pro Shop | south-west | closed: "Coming soon" |

Every ring is about four seconds' jog from where you spawn. **Quick Menu** jumps to any of them (and Settings) in one tap.

## Controls

* **Phone alone:** touch anywhere for a joystick under your thumb, or tap where you want to go. **A** uses the ring you stand in. The camera follows behind and needs no steering.
* **Phone as the TV's remote:** hold an arrow to walk (two for a diagonal), **A** opens, **B** backs out, **Home** is the Quick Menu. While the Quick Menu is open the arrows move through it.
* **Classic menus:** Settings → Display → *Walkable club* off. If the world cannot be built, the classic menus take over by themselves (`LobbyWorld.available`).

## Matches and friends

* You pick a game at the gate and play as before. When the match ends you are dropped just in front of the gate you left from (`LobbyWorld.returnStation`, set when a match starts). After a party match everyone is back on the terrace.
* **Party:** when a lobby forms (Party Board → Online or Nearby), everyone in it is in the world, each with their own look (`MultiplayerParticipant.lobbyPlayer`). Positions go over the same connection as the lobby: a `pose` packet ten times a second, sent unreliably so a busy link keeps only the newest (`MultiplayerService.sendLobbyPose` / `receiveLobbyPose`). A nearby (Wi-Fi) party is a star around the host, so the host passes each position on. Emotes use the party's existing emote events.
* The 2D party screen (ready, match settings, start) is one tap away: the Party Board, or the **Party** chip over the world; it has a **The Club** button back.

## Where things live

* `GolfArcade/Lobby/LobbyLayout.swift`: the one data file. Stations, rings, walkable ground, obstacles, spawn, party spots, camera. Nothing else hard-codes a position; `LobbyLayout` also holds the rules (what is walkable, sliding along edges, the terrace ramp, the walking-distance search the tests use).
* `LobbyWorldBuilder.swift` + `LobbyKit.swift`: the scene, built from the layout out of simple modelled forms (materials, textures drawn once, a palm, signs). The art kit from Blender can replace these pieces without touching the layout.
* `LobbyAvatar.swift`: one person: their match hero from their look (rebuilt only when it changes), playing Ready, Walk and Run by ground speed, an emote on top.
* `LobbyWorld.swift`: the model and the clock: input, walking, camera, stations, the party, positions in and out.
* `LobbyWorldView.swift`: the screen (phone touch controls, TV overlay, Quick Menu, the remote's pad).
* `LobbyWorldProof.swift` (DEBUG): the two-simulator proof.
* Wired into the existing code: `MenuScreen.world` and `TennisMenu.homeScreen` (home is the world unless classic), the station openers at the bottom of `TennisMenu.swift`, `TennisMenuScreen`, `TennisRemote`, `OnlineLobbyMenu.sync`, `MultiplayerService` (pose packets).
* Walk and Run: `MatchHeroLockerExport.cs` now exports the `walk` and `run` clips into `MatchHero_<Sex>_Rig.lzfse` (appended; the clips that were already there are byte for byte as before).

## Changing it

* Move or add a station: edit `LobbyLayout.standard` (the ring, its action, and the walkable shape that reaches it). `LobbyWorldTests` checks the new ring is reachable, separate from the others, and within about four seconds of the spawn.
* New clothes, hats or tools cannot break it: the world only shows what the character already has.

## Testing

* `GolfArcadeTests/LobbyWorldTests.swift`: layout rules, the scene, every station opening its menu and Back coming home, the classic fallback, the avatar, the party.
* Two simulators (host and guest) in one lobby, each walking a route and seeing the other:
  `--world-proof host|guest --world-proof-output <dir>` (writes `<role>.tsv`).
* Looking at it in the simulator (DEBUG): `--lobby` skips the title screen; environment `LOBBY_STATION=<id>` starts at a station, `LOBBY_CAM="x,north,h,tx,tnorth,th"` a free camera, `LOBBY_TOP=<h>` straight down, `LOBBY_MOCK=<n>` a mock party, `LOBBY_EMOTE=<slot>` plays an emote, `LOBBY_TV=1` shows the TV's screen on the phone. Turn the app's Sound setting off for test runs.

## Not done yet

* The Pro Shop, and a real tutorial match (How to Play opens the guide).
* The TV draws the world into its 1280×720 canvas and scales it; a native-resolution surface would be sharper on 4K.
* Friends walk live only in the lobby phase; during a match nothing changes.
