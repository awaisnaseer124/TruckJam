# levelgen (Python) - reference only

Since F2 (see Docs/FreeformBoardPlan.md) the **C# solver in `Assets/_TankerJam/Scripts/Core/LevelSolver.cs` is the
source of truth** for solving, scoring and verifying levels. It is used by the Unity importer, the level tools
(`Tanker Jam > Levels > Score All Levels`) and the tests, and it understands both grid (version 1) and
free-form (version 2) levels.

What these scripts are still for:
- `build_curve.py` + `curve.json`: generates the **grid** levels 1-50 from the difficulty curve. Grid levels are
  kept for the early curve; free-form pattern generation moves to C# in F5.
- `levelgen.py`: the original grid generator/solver, kept as the reference the C# port was checked against.
- `make_traces.py`: writes recorded tap sequences; the C# parity tests still replay them.

Anything new (free-form boards, the visual level editor, pattern generation) is built on the C# side.
