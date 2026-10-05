# levelgen (Python) - reference only

Since F2 (see Docs/FreeformBoardPlan.md) the **C# solver in `Assets/_TankerJam/Scripts/Core/LevelSolver.cs` is the
source of truth** for solving, scoring and verifying levels. It is used by the Unity importer, the level tools
(`Tanker Jam > Levels > Score All Levels`) and the tests, and it understands both grid (version 1) and
free-form (version 2) levels.

What these scripts are still for:
- `build_curve.py` + `curve.json`: generated the original **grid** levels 1-50. Since F5 only levels 1-10 ship
  as grid levels; levels 11-50 are free-form shape levels generated in C# from
  `Assets/_TankerJam/Data/Generation/curve_v2.json` (menu `Tanker Jam > Levels > Generate Shape Levels`).
  The original grid levels 11-50 are kept as test fixtures. Re-running build_curve.py would overwrite
  levels 11-50 with grid levels again; don't, unless that is the intent.
- `levelgen.py`: the original grid generator/solver, kept as the reference the C# port was checked against.
- `make_traces.py`: writes recorded tap sequences; the C# parity tests still replay them.

Anything new (free-form boards, the visual level editor, pattern generation) is built on the C# side.
