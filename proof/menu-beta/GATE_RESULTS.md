# Menu and Beta update — validation

Implemented in the existing iPhone golf simulator project on `final-build`.

- Menus hold still: no automatic character performances, room motion, door transitions, focus tweening, or XP counting animation. Explicit emotes and gameplay previews remain available.
- Home uses the current production male/female characters and the active player's last-played sport. Golf displays the golf outfit, stance, and club. Existing play history is migrated when no last-sport preference exists.
- Play → Single Player / Multiplayer → Online / Local. Online offers Quick Match / Play with Friends. Local preserves Pass the Phone Golf and Nearby Lobby.
- The + beside the Home character opens the existing Game Center invitation flow. Invitation sheets present on the phone even when the menu is on a TV.
- Sport previews switch with controller focus. Loading displays the selected venue. Bundled clips show actual Cliffside golf course introduction and Skyscraper tennis gameplay; other venues use their map image.
- Motion Club Beta branding appears in the app name, menus, loading, controller and TV presentation. Feedback opens `sms:+19085909023`; the menu offers a copy-number fallback when Messages is unavailable.

## Verified

GATE: Unit checks — PASS. Six focused checks passed: menu routes/back paths; controller preview focus; per-player last-sport/history migration; both character types and both sports remaining still until an explicit emote; feedback URL/app branding/bundled clips; phone and TV rendering.

GATE: Current female asset adoption — PASS. All 30 production asset checksums match the latest native adoption record. Existing adopted male garment package and production model loaders are preserved.

GATE: Visual proof — PASS. Seventeen native renders captured; Home characters, routes, previews, loading, and golf controller inspected for visible content and controls.

GATE: Swift parse and whitespace — PASS for the changed regression test sources and menu source changes.

The first touch walkthrough found a Home exit issue when browsing without a joined lobby. Home now cancels browsing and returns directly; active lobbies retain their leave confirmation. The route regression and complete touch walkthrough both passed after the fix.

GATE: Touch flow and Feedback handoff — PASS. The automated walkthrough covered Single Player, golf selection, Online Quick Match, Local Nearby, return Home, an explicit Wave emote, and Feedback opening Messages. The Messages composer was also captured after launch. No message was sent.

All seven distinct focused tests passed across the initial unit run and the successful targeted follow-up.

## Proof

- [Golf Home, phone](menu-proof/home-male-golf.png)
- [Female golf Home](menu-proof/home-female-golf.png)
- [Female tennis Home](menu-proof/home-female-tennis.png)
- [Golf Home, TV](menu-proof/home-golf-tv.png)
- [Play](menu-proof/route-party.png)
- [Multiplayer](menu-proof/route-multiplayer.png)
- [Online](menu-proof/route-onlineChoice.png)
- [Local](menu-proof/route-localChoice.png)
- [Controller-focused tennis preview, TV](menu-proof/picker-tennis-tv.png)
- [Golf loading preview](menu-proof/loading-golf.png)
- [Tennis loading preview](menu-proof/loading-tennis.png)
- [Golf controller Beta button](menu-proof/golf-controller-beta.png)
- [Messages composer opened by Feedback](menu-proof/feedback-composer.jpg)

## Test scope

Tests used a temporary copy of the current app source to prevent concurrent runtime-header edits from invalidating compilation. The 19 menu-related files matched the shared project after capture. Builds ran on the Golf Motion Review iOS simulator with the native UI; Unity gameplay and live network match delivery were outside this focused run. Game Center logged a server communication error in the simulator session, so live invitation delivery needs a signed-in device check. No invitations or text messages were sent.
