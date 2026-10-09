# netsim — run the multiplayer rules without Unity or a phone

```bash
dotnet test Tools/netsim/NetSim.csproj        # needs only the .NET 8 SDK
```

It compiles the game's **real** pure-C# multiplayer code and runs the game's **real** EditMode tests
(`Unity/Assets/Tests/EditMode/MultiplayerRulesTests.cs`) plus harness-only tests in `Tests/`. Sources are
linked from `Unity/Assets/Scripts`, never copied, so a change to the game is what gets tested.

| Piece | What it is |
|---|---|
| `Shim/UnityShim.cs` | A small stand-in for the UnityEngine types the rules use: `Mathf`, `Vector2/3`, `JsonUtility` (public fields only, like Unity's), and the clip names `TennisEmotes` mentions. |
| Linked game code | `Multiplayer/*` (tennis + golf host rules, packets), `Tennis/{TennisRules,TennisMatch,TennisBall,TennisEmotes,TennisTossCurve}`, the golf course/shot/swing model. |
| `Tests/` | Tests that only make sense here: virtual-time link simulation, delay/clock experiments. Anything that should also run in Unity goes in `Unity/Assets/Tests/EditMode/` and is linked here from `NetSim.csproj`. |

## What it proves, and what it does not
- It proves the host rules, packet validation, scoring, timing and anything built on them.
- It does **not** prove rendering, cameras, ARKit, Game Center, Bonjour, or Unity's own `JsonUtility`
  (the stand-in matches its field rules, not every detail). Those stay with Unity PlayMode, Xcode tests
  and real phones. See `PLAN_Multiplayer_OnlineLocal.md`, section 4, for the tiers.
- Files that need Unity types (MonoBehaviours such as `SportsMultiplayer`, `TennisGame`, the golf visuals,
  `Course/Match.cs` which uses the profile code) are deliberately not linked. If you need logic from one of
  them under test, move the pure part into its own file (as `TennisTossCurve` was) and link that.

## Adding a source file
Add a `<Compile Include="$(Scripts)Folder/File.cs" Link="Linked/Folder/File.cs" />` line to `NetSim.csproj`.
If it needs a UnityEngine member the shim lacks, add that member to `Shim/UnityShim.cs`.
