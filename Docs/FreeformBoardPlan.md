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
