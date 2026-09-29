# Toybox reference expansion — V5

## Status
Generated screen mockups for visual review, not runtime screenshots or an installed UI. Built-in image generation uses `../joy-directions-v2/03-toybox-arena.png` directly as the visual reference for every board.

## Coverage (each board reads left-to-right, top-to-bottom)
1. Main menus: Title, Home, Select a Sport, Tennis hub.
2. Match setup: Quick Match, Campaign map, Training, pre-match overview.
3. Locker: Outfit, Hair, Skin, Body size.
4. Match flow: interactive loading concept, drone intro, rally HUD, point feedback.
5. Results: Pause, Match Won, Match Lost, round completion / cosmetic reward concept.
6. Support: Settings, How to Play, Connect to TV, phone controller (active and postmatch).

## Fixed visual rules
- Reference image controls the panel depth, typography, character/world rendering, palette, and tactile materials.
- Royal-blue molded shells, recessed white surfaces, mango score tiles, raised lime primary actions. Match edge highlights and cast shadows as well as color.
- Display typography: heavy rounded geometric shapes; compact readable labels. No slogans.
- Same equipped blonde visor athlete across menu, practice loading, match, and results.
- Skin/body/hair customization is cosmetic; no gameplay statistics or power upgrades.

## Interaction specifications
- Loading: repeated phone swings play equipped character practice swings while loading; ready transition must not stall for practice. The mockup depicts this interaction, it does not implement it.
- Serve meter is unchanged and excluded from this redesigned collection. Loading progress is not a serve meter.
- Gameplay: Dive is the only special action. No ultimate.
- Point feedback retains gameplay POV. Serve intro behavior remains as currently implemented.
- Phone postmatch replaces racket/controller controls with Next Round, Rematch, and Main Menu buttons until acknowledged by the game.
- Online/party implementation remains deferred. No room-code or matchmaking capability implied.
- Reward panel is a visual concept, not confirmation that an unlock/reward flow exists.

## Production handoff
These are target images. New generations cannot guarantee pixel identity with the original. A runtime build should use approved exported artwork and matching typography/material assets rather than approximate the look with flat generic components. Treat hero/world rendering as a separate dependency when assessing runtime parity.

Full generation prompts are saved in `prompts.json`.
