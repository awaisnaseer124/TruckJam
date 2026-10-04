# Tanker Jam: Unity starter package

This package gives you the parts of the web prototype that carry over to Unity unchanged.
Pair it with the "Tanker Jam: Unity implementation guide" doc.

## What's inside

| Path | What it is | Status |
| --- | --- | --- |
| `Assets/_TankerJam/Scripts/Core/GameRules.cs` | The game rules in pure C#: lot exits, bays, bottom-layer settle | Compiled and checked against the Python solver: 20/20 random games give identical units, and the stored solution wins |
| `Assets/_TankerJam/Scripts/Core/LevelJson.cs` | Loads level JSON (no packages needed) | Compiled and checked |
| `Assets/_TankerJam/Editor/TankerJamPrefabBuilder.cs` | Menu **Tanker Jam > Build Greybox Prefabs**: trucks (2, 3, 4 cells), vessel, pump, cone, materials | Written for Unity 6 + URP; not yet run inside Unity |
| `Assets/_TankerJam/Tests/EditMode/` | NUnit tests: every level's stored solution must win | Written for the Unity Test Framework |
| `Assets/_TankerJam/Data/Levels/level_005.json` | The vessels test level from the prototype | Solvable with 3 bays; random win rate 30% |
| `Tools/levelgen/levelgen.py` | Level generator, solver and difficulty scorer (Python 3, no packages) | Reproduces level_005 exactly from seed 400 |
| `Tools/levelgen/make_traces.py` | Writes random tap sequences to compare C# and Python rules | |
| `Tools/reference-prototype.html` | The web prototype, for side-by-side comparison | Open in a browser |

## Setup

1. Create a Unity 6 LTS project from the **Universal 3D** template.
2. Copy the `Assets/_TankerJam` folder into your project's `Assets` folder.
3. Install the **Test Framework** package if it isn't already installed (Window > Package Manager).
4. Run **Tanker Jam > Build Greybox Prefabs**. Prefabs appear in `Assets/_TankerJam/Prefabs/Greybox`.
5. Run the tests: Window > General > Test Runner > EditMode > Run All.

## Making new levels

```
cd Tools/levelgen
python levelgen.py --seed 500 --trucks 10 --lens 2,2,3,3,4 --colors 4 --vessels 4 --slots 3 \
                   --min-rate 0.25 --max-rate 0.45 --out ../../Assets/_TankerJam/Data/Levels/level_006.json
```

The difficulty table in the guide lists the parameters for each range of levels.

## Level JSON

- `cars`: `x`, `y` (top-left cell, y grows toward the camera), `len` (2/3/4), `d` (U/D/L/R facing), `color` (P/Y/C/V)
- `cones`: blocked cells
- `tubes`: one list per vessel, **bottom layer first**, one entry per unit
- `slots`: regular bays open at the start
- `sol`: a winning tap order (truck indexes); `randomWinRate`: share of random games that win

## Rule to keep

Decide everything in `GameRules` at the moment of the tap, and let the views play it out afterward.
`Assign()` returns the units in the exact order the pump must play them.
