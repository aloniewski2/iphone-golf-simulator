# The menus: Adnan's Island Sports Club

Every menu in the golf game is on Adnan's "Island Sports Club" system, the one visual system of his
SwiftUI shell (`GolfArcade/Unity/ClubDesign.swift` and `ClubScreens.swift` on
`origin/tennisgameplaydone-needtofixcharacters`), rebuilt in Unity's uGUI so our screens (online play, the
phone as the club, unlocks, the Open) keep working.

## The system (`Unity/Assets/Scripts/UI/Club.cs`)

| | |
|---|---|
| Palette | lagoon `0E2A47` / deep `081A2E`, sun `FFD21F` / `D99A00`, cream `F7F4EC` / `D8D0BC`, ink `16123A`, muted `6B6780`, coral `FF5B4A`, green `1FBF75`, violet `5B2BD9`, sky `38C6FF` |
| Type | Bricolage Grotesque for display (wght 800, wdth 78) and titles (760, 92); Rubik for the rest (500–800). Static instances of his variable fonts in `Resources/Fonts/Club` (made with fontTools' instancer; OFL licences beside them). The in-round HUD uses Rubik too, as his tennis HUD does. |
| Objects | his rendered 3D icons, `Resources/Club/club-*.png` (crest, golf, play, kit, friends, trophy, lock, quick, ...) |
| Scenes | his painted scenes, `Resources/Club/scene-*.jpg`: the clubhouse (`home`), the locker room (`locker`), and `cliff`, `trophy`, `stands`, `pavilion` for later |
| Sounds | `Resources/Club/Sounds`: tick on a move, pop on a press, whoosh between screens |
| Components | `Club.Header` (crest, ISLAND SPORTS CLUB, "Clubhouse › …", the player chip), `Club.Card` (cream slab, tint strip, object on a glow, badge, locked), `Club.Button` (sun / cream / quiet slab that presses into its base), `Club.Pill`, `Club.Badge`, `Club.Shade`, `Club.Enter` (cards rising in one after another), `ClubWipe` (the sun/coral/violet stripe wipe) |

Sizes follow his phone ("compact") layout at three canvas units to his point (the canvas is 1080 × 2340).

## The stage (`Game/ClubStage.cs`, `Shaders/ClubBackdrop.shader`, `ClubDisc.shader`)

On the clubhouse and in the locker the game camera draws only the golfer (moved to layer 30), standing on his
lit disc, in front of the painted scene on a quad far behind them: his `ClubBackdrop` (the scene covering the
screen and drifting, light shafts, dust, the lagoon tint) as a shader. The golfer stays the live 3-D figure,
so every look shows at once. `CameraRig.FrameStage` puts them in the gap the screen leaves for them
(`HomeScreen.StageBand`), looking down a touch onto the disc. The course screen keeps the live hole circling
under his header and shades.

## The screens

* **Clubhouse** (`UI/HomeScreen.cs`, his `ClubHomeScreen`): header and chip, Big screen, your golfer, the
  greeting, Solo / 2 players / Online / The Open, the PLAY slab, the course card, Your look, Records.
* **Course** (`CourseScreen`, his golf hub): "Clubhouse › Cliffside", the hole's name and a sun capsule,
  cream arrows, THIS HOLE / FULL ROUND pills, BACK and SELECT slabs, the padlock card on a locked course.
* **Your look** (`UI/Locker.cs`, his `LockerStudio` in `ClubCharacterScreen`): in the painted locker room.
* **Results** (`RoundCard` in `UI/ArcadeCards.cs`, his `ClubResultsScreen`): the headline huge in sun, the
  card on a cream slab, the stats on cream cards, NEXT HOLE / PLAY AGAIN / MAIN MENU slabs.
* **Profile, 2 players, Online, the Open** (`UI/Lobby.cs`): his header, panels, slabs and pills.
* **Unlocks** (`UI/UnlockToast.cs`): a club card with the reward's object and an UNLOCKED! badge.

`HomeScreenTests.EveryMenuPage` captures them all (`Library/Captures/review/club-*.png`).

## Not carried over

His SwiftUI shell's other areas (the sport picker, tennis's adventure ladder and story, the rival portraits,
settings, how-to cards) have no golf screen to go on; the portraits were not downloaded.
