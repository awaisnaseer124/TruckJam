# Free-form board: plan

Goal: boards like the reference (radial / mandala layouts, vehicles at any angle, angled bays), generated and
hand-designed in a visual level editor, while keeping what makes the project robust: rules decided at tap
time, every level verified solvable, difficulty scored, zero-GC play.

---

## 1. What changes, what stays

| Area | Today (grid) | Free-form |
| --- | --- | --- |
| Truck placement | Cell (x, y) + U/D/L/R | Center (x, z) + heading angle, length, width |
| Exit rule | Step cell by cell to the grid edge | Sweep the truck's footprint forward to the board boundary; blocked if the swept corridor overlaps another truck (or an obstacle) |
| Bump distance | Free cells | Distance along the heading to the first hit |
| Board shape | N x N square | Circle, rounded rectangle or polygon (+ obstacles) |
| Bays, vessels, pump, settle | Unchanged | **Unchanged**: `GameRules.Settle` and the bay/vessel logic don't care where trucks came from |
| Routes | Facing -> ring road | Heading ray -> where it hits the ring road -> same ring routing (`RouteBuilder` already works on points) |
| Level data | `level_NNN.json` v1 | v2 (below); v1 grid levels convert automatically |

The settle rules, session, pump, views, UI, meta and tests above the lot stay as they are. The work is the
lot model, its solver/generator, placement visuals and the editor.

## 2. Rules: geometric lot (Core, pure C#)

- **Truck** = oriented rectangle: center, heading (degrees, snapped to a step such as 15), length, width
  (visual width minus a small clearance so trucks can sit tight without "touching").
- **Exit check** = the corridor from the truck's front edge along its heading to the board boundary, as wide
  as the truck. Separating Axis Test (oriented rectangle vs oriented rectangle) against every other truck
  and obstacle still on the board. Returns `Clear` and the distance to the first hit (bump animation).
- **Determinism:** positions on a 0.05 grid, angles on the snap step, one shared epsilon. With integer-ish
  inputs the SAT results are stable on every device.
- **One implementation:** the solver and generator move from Python to C# (Core), used by the editor
  tools and tests. No more parity problem between two languages; Python stays only as a reference.
- **Performance:** a tap checks ~40 trucks x 1 SAT = trivial. Cache candidates per truck (corridor
  bounding box) for the solver.

## 3. Level format v2

```json
{
  "version": 2,
  "board": { "shape": "circle", "radius": 5.2 },
  "trucks": [ { "x": 1.25, "z": -0.5, "angle": 45, "len": 2, "color": "P" } ],
  "obstacles": [ { "x": 0, "z": 0, "radius": 0.4 } ],
  "tubes": [["P","Y"], ...],
  "slots": 3,
  "sol": [ ... ],
  "meta": { "index": 12, "tier": 3, "hard": false, "pattern": "rings-6" }
}
```

- Lengths stay 2 / 3 / 4 (capacity 2 / 4 / 6) in **truck units**; a board-level `scale` sets how big a
  unit is on screen, so dense boards just use smaller trucks.
- v1 grid levels convert 1:1 (cell centers, 0/90/180/270). A test proves the converted levels give the same
  exits and the same solutions as the grid rules.

## 4. Solving and scoring (C#)

- Same search as today: depth-first over tap orders with the real settle rule, memoized on
  (remaining trucks, slot fill, vessel tops). Lots of 30-40 trucks have many order-independent moves,
  so add **partial-order reduction** (trucks that don't affect each other's corridors or colors can be
  committed in a canonical order) and an iteration cap.
- Difficulty = random-play win rate (as today) + jam depth + bay pressure. Same curve and sawtooth.
- Runs in the editor (button) and in batch (menu: generate a range of levels).

## 5. Generation: patterns, not random scatter

The reference is symmetric. Random placement won't produce that; **patterns** will:

1. **Pattern** = a set of *seats* (position + axis angle + max length), authored or procedural:
   rings (N seats at radius r), spokes, grids at an angle, spirals, mirrored halves, k-fold rotational copies.
2. Generator picks seats, a length per seat, and a **direction along the seat's axis** (forward/backward;
   outward, inward or tangential). Symmetry is kept by choosing per *orbit* (all rotated copies together).
3. Reject overlaps, require a jam depth, color evenly, build vessels, solve, score into the tier's band.

## 6. Visuals

- Trucks placed at any angle (views already rotate freely). Lot drawn as the board shape (circle / rounded
  rect) instead of a checker grid; optional subtle ring markings.
- Exit route: forward along the heading to the board edge, then the existing ring-road routing.
- **Angled (45 deg) bays** with side-view filling (layout + route + hose positions; no rule change).
- **Tap tolerance for small trucks:** pick the nearest truck within a finger radius of the tap, not just
  an exact collider hit.
- Camera fitter already frames any board bounds.

## 7. Visual level editor (Unity editor window)

- Top-down canvas of the board: click to place, drag to move, scroll to rotate (snap), keys for length and color.
- **Symmetry tools:** mirror X/Z, k-fold rotation around the center (this is how the reference is built),
  duplicate ring.
- Live overlap and out-of-board highlighting; arrows show each truck's corridor.
- Panel: vessels editor (stack colors per vessel), unit balance per color, bays.
- Buttons: **Solve** (solvable? par, win rate, tier fit), **Fill colors & vessels** (auto), **Generate from
  pattern**, **Play** (opens the level in the Game scene), **Save** (writes v2 JSON; the importer verifies it).

## 8. Design questions to decide first

1. **Oil supply at high truck counts.** 36 trucks x ~3 units = ~110 units of oil. Four vessels x 16 layers
   = 64. Options: (a) more vessels (5-6), (b) taller vessels showing the bottom N layers (shader already
   handles 16; could scroll), (c) smaller capacities (1/2/3) for dense boards. Recommendation: (a) + (b).
2. **Board shape:** circle only (like the reference) or any shape per level? Recommendation: circle and
   rounded rectangle first; polygon later.
3. **Angle step:** 15 deg gives the most freedom; 30/45 reads cleaner. Recommendation: 15 deg in the
   data, editor snaps to 45 by default.
4. **Grid levels:** keep the 50 grid levels (converted) for the early curve, or replace everything with
   patterns? Recommendation: keep 1-10 as grids (easy to read), patterns from 11 on.

## 9. Build order (each step ends green and playable)

| Step | Work | Gate |
| --- | --- | --- |
| F1 | Geometric lot model + SAT exit check + v2 format + v1 converter | Converted 50 levels: identical exits and solutions (tests) |
| F2 | C# solver + scorer + verifier on v2; importer accepts v2 | All levels verified in C#; Python retired to reference |
| F3 | Free-angle placement, board-shape rendering, heading-based routes, tap tolerance, 45 deg bays | A hand-written radial level plays end to end (PlayMode test) |
| F4 | Visual level editor with symmetry tools, solve/score, play | Rebuild the reference layout in the editor and win it |
| F5 | Pattern generator + curve v2 + oil-supply change | Batch-generate a radial tier; difficulty in band |

---

## Status

**F1 done (2026-10-05).** Decisions taken: all recommendations in section 8.
- Core: `Geometry2D` (oriented rectangles, SAT overlap, exact time-of-impact sweep), `ILot` with `FreeLot`
  (geometric, runtime default) and `LotGrid` (grid oracle for tests), `TruckPose` / `BoardShape` / `Obstacle`,
  version 2 JSON read + write (`LevelJson.ToJsonV2`), free-form validation (inside the board, no overlaps).
- Gate passed: on all 51 levels the geometric lot gives the grid's exact answers (clear + free cells) at the
  start, after every solution step and over 20 random play-throughs each; every level round-trips through
  version 2 and still verifies. The 51 Python parity traces now run on the geometric lot.
- Conventions: board space origin at the board center, +x screen right, +z toward the camera, angle 0 =
  toward the bays, clockwise; truck collision = (cells - 0.1) x 0.8; cones = 0.7 squares; positions snap to
  0.05 on load; touching (penetration < 0.01) is not a hit.
- Not yet: visuals still place trucks from grid fields (F3), solver/generator still in Python (F2).

**F2 done (2026-10-05).**
- `LevelSolver` (Core, C#): pairwise blocking precomputed from the geometric lot (bitmask exit test),
  exact port of the settle rule, depth-first search memoized like the Python solver (max 64 trucks,
  12 vessels, 6 bays), random-play win rate, jam depth, `LevelSolver.Score` for tools.
- Gate passed: settle and exits match GameRules/FreeLot on every level; C# solves all 51 levels (<= 59
  nodes, ~1 ms) with solutions that replay as wins; win rates agree with the stored Python rates (within
  0.1; largest gap 8 points); unsolvable and jam-depth cases; a radial free-form level solves.
- `Tanker Jam > Levels > Score All Levels` writes Temp/TankerJamScores.txt. Python is reference only
  (Tools/levelgen/README.md); build_curve.py still generates the grid curve until F5.

**F3 done (2026-10-05).**
- Trucks are placed from their pose (grid or free-form) through `BoardLayout.ToWorld` / `TruckHeading`;
  routes leave along the truck's heading to the ring road (`RouteBuilder.RingEntry` with a direction).
- Board shapes drawn by `SceneryBuilder`: checker for grid levels, layered disc for circles, filled
  rounded rectangle; obstacles from `LevelDef.CollectObstacles`.
- 45 deg bays (`LayoutParams.BayAngle`, set to 45 in LayoutConfig; code default 0 keeps the prototype
  numbers): stalls, VIP label and the dashed extra bay rotate with the bay axis; trucks line up behind a
  tilted stall, park with the hatch under the hose, and leave along the axis. VIP lift turns to the axis.
- Tap tolerance: when the raycast misses, `TapInput.Nearest` picks the lot truck whose on-screen center
  line is within 3.5% of screen height of the tap.
- `SampleLevels.Radial` (8 spokes x 2 trucks around a center cone, solved on build);
  menu `Tanker Jam > Dev > Play Radial Sample`.
- Gate passed: the radial level wins end to end in PlayMode (`RadialFreeFormLevelWinsEndToEnd`); all grid
  tests still pass with 45 deg bays. Tests: 578 EditMode, 14 PlayMode, all green.

**Lot direction change (2026-10-05, user).** No round lots: every level uses one plain grey square lot
(no checker); the *truck arrangement* makes the shape (rings, hearts, any outline). Circle boards stay
readable in the data format but nothing uses them.
- `SceneryBuilder.BuildLot`: flat `Palette.Lot` grey with a thin white rim, for grid and free-form levels.
- Samples on an 11-12 unit square lot: `SampleLevels.Radial` (ring of spokes) and `SampleLevels.Heart`
  (27 trucks tracing two nested hearts, pointing outward); menu `Tanker Jam > Dev > Play Heart Sample`.
  The heart builder (trace an outline, stand trucks across it, skip overlaps, fill vessels, solve) is the
  seed of the F5 shape generator and the F4 editor's "trace outline" tool.
- Tests: 579 EditMode, 15 PlayMode (both samples win end to end), all green.

**F4 done (2026-10-05).** Visual level editor: `Tanker Jam > Level Editor` (Ctrl+Shift+L), or double-click
a level JSON under Data/.
- Model in Core, unit-tested without UI: `LevelDraft` (editable square-lot level; load any v1/v2 level,
  save v2; place/move/rotate with snapping; mirror left-right / top-bottom; k-fold rotated copies that
  skip collisions; diagnostics for overlaps, off-lot trucks, cone-stuck trucks, blockers per truck, jam
  depth, oil balance; auto color; vessel fill that searches for a solvable order, optionally aiming at a
  random-win-rate difficulty; solve + score) and `ShapeTracer` (ring, heart, star, square, diamond,
  triangle; trucks point outward, inward or along the line; dealt colors).
- Window (`Editor/LevelEditorWindow.cs`): top-down canvas (bays at the top), select / box select / drag,
  place and cone tools with a ghost preview, the selected truck's road out and its blockers, red/orange
  problem outlines; panel for lot size, bays, brush, selection edits, symmetry, shape tracing, colors and
  vessels (editable per vessel), check + solve report; undo/redo; Samples menu; Save (Drafts/ scratch
  folder or Data/Levels, verified first) and Play (opens the Game scene and plays the draft as a
  "TEST LEVEL" that never touches campaign progress; works while already playing too).
- Gate passed: the reference mandala is rebuilt with editor operations only (one 3-truck petal + 6-fold
  rotation, auto color, vessel fill), saved and reloaded losslessly, verified, and won end to end in
  PlayMode (`SampleLevels.Mandala`, Dev menu "Play Mandala Sample"). Every shipped grid level converts
  into the editor and still verifies.
- Tests: 599 EditMode, 16 PlayMode, all green. `Tanker Jam > Diagnostics > Capture Level Editor` saves a
  screenshot of the window to Temp/TankerJamEditor.png.

**F5 done (2026-10-05).** Shape levels 11-50 are generated in C#; levels 1-10 stay hand-tuned grid levels.
- Oil supply: up to 6 vessels (`LevelVerifier.MaxVessels`); a vessel may hold up to 30 units
  (`MaxVesselUnits`) but its glass shows 16 layers (`VisibleVesselUnits`, `LayoutParams.VesselVisibleUnits`);
  hidden units sink into view as the bottom drains (`VesselView`). Tools aim for 14 units per vessel.
- `PatternGenerator` (Core): layout families **petals** (a random cluster copied 3/4/6/8-fold),
  **mirror** (a random half mirrored 2- or 4-way) and **trace** (a dense outline of outward-facing
  trucks filling the lot, plus a second outline or a k-fold core that waits for it). A candidate must be
  exactly symmetric (no skipped copies), clean, deep enough, solvable, and its random-win rate must land
  in the band (vessel order searched toward a target rate). Seeded and deterministic.
- `PatternCurve` + `Data/Generation/curve_v2.json`: tiers 4-7 (lot 10-12, 3-5 colors, 4-5 vessels,
  10-24 trucks; traced outlines may use 10 more because they only read as shapes when dense), same
  sawtooth as the grid curve (every 5th level Hard at the low end of its band, the next a breather).
  Families alternate petals / mirror / trace through the curve.
- `Tanker Jam > Levels > Generate Shape Levels (curve v2)` writes Data/Levels/level_011..050.json and
  Temp/TankerJamGenerate.txt (about 10-20 s); the catalog importer verifies them (50 levels, 0 rejected).
  The level editor has a Generate section (like curve level N, choose layout family, seed to re-roll).
- The original grid levels 11-50 are kept in Tests/EditMode/Fixtures/Levels so the Python-parity, grid-vs-
  geometric and solver tests keep their coverage (`TestLevels` helper).
- Environment props that overlap a level's play area (bigger lots, 5-6 vessels) are hidden per level.
- Gate passed: each tier batch-generates in band (4 seeds per tier), every family is symmetric, shipped
  levels 11-50 match their tier, band, Hard flag and a reproducible stored rate; generated levels 23 and
  45 (5 vessels) and a tall-vessel level (21 units per vessel) win end to end in PlayMode.
- Tests: 770 EditMode, 20 PlayMode, all green.
- Tuning notes: traced levels currently come out as hearts and stars (convex outlines rarely reach the
  required depth with a core); the last tier (depth 3) is all petals and mirrors.
