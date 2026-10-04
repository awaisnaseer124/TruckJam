# Tanker Jam — MVP Implementation Plan

Source of truth: *Tanker Jam: Unity implementation guide* (GDD), `TankerJamStarter.zip`, and
`Tools/reference-prototype.html` (the "vessels" web prototype). This plan turns those into an
architecture that survives past the MVP: more levels, new mechanics, live-ops, and real art.

---

## 0. Findings from the review (read first)

What the starter gives us, verified:

| Item | Status |
| --- | --- |
| `GameRules.cs` (Core rules) | Faithful port of the prototype's `logicSettle` / `freeRun`. Correct, but allocates per call and scans every truck per cell (fine for 7×7, worth an occupancy grid). |
| `LevelJson.cs` | Works; dependency-free. Load-time only, so allocations are acceptable. |
| `levelgen.py` | Runs on Python 3.13; reproduces `level_005.json` **byte-identically** from seed 400. A tier-1 level (2 colors, 4 bays) generates in < 0.2 s. |
| `TankerJamPrefabBuilder.cs` | Never run in Unity. Builds trucks from ~20–30 separate primitive renderers each → 11 trucks ≈ 300 renderers, which **breaks the 150 draw-call budget** on its own. Must be reworked to bake combined meshes. |
| `LevelSolutionTests.cs` | Good gate. Missing: parity traces vs Python, `CheckExit` cases. |

Gaps and conflicts the GDD doesn't resolve (decisions taken here, flag if you disagree):

1. **Layout is hard-coded in the prototype** (`SLOTX` = 5 bays, `VX` = 4 vessels, `RX=4.3` for a 7×7 lot).
   The difficulty table needs **2–4 regular bays** and the generator supports any vessel count.
   → Layout becomes a pure function of the level (`BoardLayout`), not scene-placed objects.
2. **5th color** (levels 41–50 need 4–5 colors) has no key. → Add `G` (green, e.g. `#3DDC84`) to
   `COLOR_KEYS` in Python and to the Palette. Palette is data-driven so this is a one-line change.
3. **"New mechanics" (levels 26–40)** are unspecified. → Core gets extension points (see §3.4) but
   no mechanic is built in the MVP.
4. **Physical vs logical bays.** Rules free a slot the instant it's full; the screen frees the bay
   only when the truck drives off. The prototype keeps this split inside view code. → We move it into
   a pure-C# `GameSession` so jam/win logic is unit-testable too.
5. **DOTween** (user-provided package) is used for UI and juice tweens. Core truck motion stays custom
   springs + arc-length paths ported 1:1 from the prototype (zero-alloc, deterministic). Splines package
   is skipped. DOTween rules: `SetAutoKill`/recycling on, capacity preset at boot, no tweens created per frame.
6. **Audio assets don't exist.** → Placeholder clips synthesized at load time (port of the prototype's
   WebAudio synth via `AudioClip.Create`), swappable for recorded clips through the `AudioCatalog`.
7. **Unity version** is `6000.6.2f1` (not the 6000.0 LTS named in the GDD). Fine for MVP; pin it.
8. **Not a git repo.** → `git init` + Unity `.gitignore` + LFS for binaries before the first commit.

---

## 1. Guiding principles

1. **Decide at the tap, animate afterward.** All game decisions happen in `TankerJam.Core` (no
   `UnityEngine`). Views only play out the result. Core is bit-for-bit equal to the Python solver.
2. **Data drives everything.** Level JSON → layout → spawned views. No level-specific scene editing.
   Every magic number from the prototype lives in a tuning ScriptableObject.
3. **One-way dependencies.** `Core ← Game ← UI`, `Meta` and `Services` behind interfaces. Views
   never mutate rules state; they send commands to the session.
4. **Mobile budget is a design constraint, not a polish pass.** < 150 draw calls, < 100k tris,
   zero GC allocations per frame during play, 60 fps on a 2022 Samsung A-series.
5. **Every phase ends at a testable gate** (GDD milestones), with automated tests guarding the rules.

---

## 2. Project layout & assemblies

```
Assets/_TankerJam/
  Art/          Materials, Meshes (baked), Textures (arrow, badge, lot), Shaders (LiquidFill, Glass)
  Audio/        Sfx, Music, AudioMixer
  Config/       GameTuning, Palette, TruckCatalog, LayoutConfig, EconomyConfig, AudioCatalog (SOs)
  Data/Levels/  level_001.json … + LevelCatalog.asset
  Prefabs/      Trucks, Vessel, Pump, Hose, Bay, Cone, UI
  Scenes/       Boot, Game
  Scripts/
    Core/       TankerJam.Core       (noEngineReferences)  rules, session, layout math, paths
    Game/       TankerJam.Game       views, input, game loop, level builder, FX, camera
    Meta/       TankerJam.Meta       progress, economy, boosters inventory, persistence
    Services/   TankerJam.Services   IAnalytics (Null impl) — monetization is out of scope
    UI/         TankerJam.UI         HUD, booster bar, popups, home overlay
    App/        TankerJam.App        composition root (Boot), scene flow
  Editor/       TankerJam.Editor     prefab baker, level importer/validator, level preview window
  Tests/
    EditMode/   Core rules, session, layout, path, level catalog
    PlayMode/   Smoke tests driving GameSession through real views
Tools/levelgen/ levelgen.py, make_traces.py, build_curve.py (new), curve.json (new)
```

Dependency graph (asmdef references):

```
Core  ←  Game  ←  UI  ←  App
  ↑       ↑       ↑       ↑
  └──── Meta ─────┴───────┤
          Services ───────┘   (interfaces only consumed by Meta/UI; implementations in App)
```

Scenes: **Boot** (composition root, loads config, services, save) → **Game** (home screen is an overlay
in the Game scene; avoids a scene load between levels, standard for this genre). Level switches reuse
pooled objects instead of reloading the scene.

---

## 3. Core layer (`TankerJam.Core`, pure C#)

### 3.1 Types

| Type | Responsibility |
| --- | --- |
| `LevelDef`, `TruckDef`, `Cell`, `Facing` | Immutable level description (existing). Add `ColorKey` as `char` kept for solver parity. |
| `LevelJson` | Parse (existing). Add validation errors with readable messages. |
| `LotGrid` | `int[]` occupancy (−1 empty, −2 cone, else truck id). `CheckExit` becomes O(path length). Updated on `Remove(truckId)`. |
| `GameRules` | Existing logic, refactored to write units into a caller-supplied `List<Unit>` buffer and use a non-LINQ removal, **without changing order semantics**. Parity tests land *before* the refactor. |
| `GameSession` | Owns `GameRules` + physical bays + booster state. The only object views talk to. |
| `BoardLayout` | Pure math: given level size, vessel count, regular bay count → world-space (x,z) for lot cells, ring road rect, bay X list, hose Z, pump, vessels, exit road, camera framing bounds. Constants from `LayoutConfig`. |
| `PolylinePath` | Port of `rounded()` + arc-length sampling. Pre-allocated arrays, `Sample(s, out pos, out heading)` with a cached segment cursor (no alloc, O(1) amortized). |
| `RouteBuilder` | Port of `ringPath` / `toSlotPath` / `leavePath` using `BoardLayout`. Writes into a reusable `PolylinePath`. |

### 3.2 `GameSession` API (commands in, events out)

```csharp
// Commands (from input / UI)
TapResult Tap(int truckId);          // Blocked(free) | NoBay | Assigned(bay, units) | VipAssigned(units)
bool ArmVip();  void DisarmVip();
bool OpenExtraBay();
void ReleaseBay(int bayIndex);       // view reports the truck physically cleared the bay
EndState Evaluate();                 // called by the loop after 0.4 s of calm → None | Won | Jammed(reason)

// State queries (for HUD/hints)
IReadOnlyList<BayState> Bays;  IReadOnlyList<char> BottomColors();  int VipLeft, ExtraLeft;
```

Events are plain C# (`event Action<…>`) or a small struct queue drained by the game loop each frame
(preferred: no delegate allocations, deterministic order). Bay model:
`[VIP][Regular × level.slots][Extra]` — the indices are generated, not hard-coded.

### 3.3 Determinism & parity

- `make_traces.py` output (20 tap sequences + unit order per level) is committed as test fixtures;
  an EditMode test replays them through `GameRules` and compares unit strings exactly.
- Every level in the catalog replays its stored `sol` to a win (existing test, kept).

### 3.4 Extension points for future mechanics (no implementation in MVP)

- `TruckDef` gets an optional `Tags`/`Mechanic` field parsed from JSON (ignored if absent).
- `LotGrid` cell types are an enum (Empty, Cone, Truck) so new blockers (e.g. gates, timed barriers) slot in.
- Session tap pipeline is an ordered list of checks (`VipOverride → ExitCheck → BayCheck`), so a
  mechanic adds a check without editing the others.
- Python solver and C# rules evolve together; parity tests enforce it.

---

## 4. Game layer (`TankerJam.Game`, Unity views)

### 4.1 Loop & ownership

- **`GameLoop`** (single `MonoBehaviour.Update`) ticks, in the prototype's order:
  `Pump → Vessels → Trucks → Hoses → Flow → EndCheck`. Views are plain classes or components with
  `Tick(float dt)` — no per-object `Update()`. Prototype sub-steps at ≤ 34 ms; we keep that to make
  springs stable on frame hitches.
- **`LevelBuilder`** reads `LevelDef` + `BoardLayout`, pulls views from pools, places them. Static
  scenery (lot, roads, pad, pipes, stands) is generated into **one combined mesh per material** at
  level start (layout varies per level, so runtime `Mesh.CombineMeshes` / static batching utility).
- **`InputController`** (Input System, EnhancedTouch): touch-down → `Physics.Raycast` on the `Trucks`
  layer (one box collider per truck) → `TruckView.Id` → `session.Tap(id)`. Taps on busy trucks ignored.
- **`CameraFitter`**: FOV 36, ~53° pitch, distance solved so `BoardLayout.Bounds` fits both width and
  height for any aspect, plus HUD safe-area margins.

### 4.2 Views

| View | Driven by | Notes |
| --- | --- | --- |
| `TruckView` | State machine: `Lot, Bumping, Driving, Lifting, Parked, Filling, Full, Leaving, Gone` | Motion from GDD: accel 9, vmax 6.5, brake `sqrt(2·7·rem)`, loaded −30%, heading smoothing ×12, spring slosh & pitch. Reports `ReleaseBay` at 1.6 m into the leave path. |
| `LiquidView` | `MaterialPropertyBlock`: `_FillHeight`, `_Tilt`, `_Wobble` | Shared material per color. |
| `VesselView` | Layer list with remaining amount 0..1 | One mesh per layer (prototype) → MVP; later a single vessel shader with a color-band texture (1 draw call per vessel). |
| `PumpView` | Logical unit queue | Plays strictly in order, 0.34 s/unit, only when the unit's truck is `Filling`. Drives wheel/needle. |
| `HoseView` | Bay occupancy | Extend 0.4 s, stream cylinder while pumping. |
| `FlowFx` | Pump | Blobs drawn with `Graphics.RenderMeshInstanced` from a struct array (≤ 160) → **1 draw call**, no GameObjects. Paths precomputed per (vessel, bay). |
| `BayView` | Session bay state | VIP gold / regular / locked "+" visuals. |
| `ConeView` | Static | Merged into static scenery mesh. |

### 4.3 Shaders (Shader Graph, URP)

- **LiquidFill**: Lit, opaque + alpha clip, two-sided, plane = `_FillHeight + x·_Tilt + sin(x·9+t·18)·_Wobble`
  in object space; back faces lighter for the flat-top look. SRP-Batcher compatible (all props in CBUFFER).
- **Glass**: Unlit/simple-lit transparent, fresnel rim + baked highlight strip; no refraction / depth texture.

---

## 5. Data & configuration

| Asset | Contents |
| --- | --- |
| `LevelCatalog` (SO) | Ordered `TextAsset` levels + metadata from the solver: par, random win rate, tier, `isHard`, `introduces` (booster/mechanic tutorial flags). Built by the importer, not by hand. |
| `Palette` (SO) | Color key → color, liquid/body/shell/blob materials. P, Y, C, V (+ G reserved). |
| `TruckCatalog` (SO) | Length → prefab/mesh set (visual only; capacity stays in Core for parity). |
| `LayoutConfig` (SO) | All spacing constants from the prototype (`RX` derivation, `HZ`, `EXITZ`, vessel radius, bay width…). |
| `GameTuning` (SO) | Speeds, accelerations, durations, spring constants, calm time, pump unit time. |
| `EconomyConfig` (SO) | Starting boosters 2/2, VIP 900 / Extra 600 coins, +40 per win (×2 Hard), intro levels 6 and 10. |
| `AudioCatalog` (SO) | Event → clips, volume, pitch range, max voices. |

### Level pipeline

1. `Tools/levelgen/curve.json` — the GDD difficulty table (per level: bays, colors, trucks, lengths,
   rate band, hard flag on every 5th).
2. `build_curve.py` — loops the curve, calls `make_level`, writes `level_NNN.json` with `sol`,
   `randomWinRate`, `tier`, `hard`. Re-runnable, seed-stable.
3. Unity `LevelImporter` (AssetPostprocessor + window): on import, parses, replays `sol`, checks colors
   against Palette and vessel/bay counts against LayoutConfig limits, then rebuilds `LevelCatalog`.
   Invalid levels are reported, never silently shipped.
4. **Level Preview window** (editor): pick a level, see the lot grid and vessels, step through the
   stored solution — designer tool, cheap to build on top of `GameSession`.
5. Debug in-game: level select, "play stored solution", time scale, show FPS/draw calls (dev builds only).

---

## 6. Meta, UI, services

- **Meta**: `IPersistence` (PlayerPrefs + JSON impl; cloud later), `ProgressService` (current level,
  stars), `Wallet` (coins), `BoosterInventory`. Saved on change, versioned save schema.
- **UI (uGUI)**: Canvas Scaler 1080×1920, match 0.5, `SafeArea` component, separate canvases for static
  vs frequently-changing elements. Top bar (Retry / level / sound), status pill, booster bar with count
  badges or price, Result popup (Win: stars/coins/Next; Jammed: offer Extra bay / VIP → Retry).
  Fonts: Bungee + Barlow (OFL, Google Fonts) as TMP assets.
- **Services**: **no ads and no IAP** in this project. Boosters are earned with coins from wins only.
  The Jammed popup offers unused boosters, or buying one with coins, then Retry. `IAnalyticsService`
  exists with a Null implementation so events can be added later without touching gameplay.
- **Audio**: `AudioManager` with an `AudioMixer` (Music/SFX), pooled one-shot sources, one glug source
  with per-unit pitch (170→690 Hz mapping), looping pour-hiss whose volume follows the pump. Mute saved.

---

## 7. Mobile performance plan

| Area | Measure |
| --- | --- |
| Draw calls | Bake each truck into ≈ 4 renderers (body multi-submesh, shell, liquid, wheels per axle). Static scenery combined per material. Blobs instanced. Target ≤ 90 calls at peak. |
| Materials | One per oil color shared across trucks/vessels/blobs; per-instance values via MPB; SRP Batcher on. |
| Transparency | Only tank shells, vessel glass, bay outlines. Explicit sort order; no depth texture. |
| Shadows | One directional light, 1024 map, 25 m distance, 1 cascade; blobs/streams/decals cast none. |
| CPU | Single tick loop, cached references, no LINQ/`GetComponent`/string ops per frame, pooled everything, no `Instantiate` during play. Verify 0 B GC/frame in Profiler. |
| Render settings | URP Mobile asset: MSAA 4x, HDR off, render scale 1.0 (drop to 0.85 on low-tier via quality tier), post-processing off. |
| Player | IL2CPP, ARM64, Linear, portrait lock, managed stripping Medium, remove unused packages (Visual Scripting, AI Navigation, Timeline, Collab). |
| Frame pacing | `Application.targetFrameRate = 60`; Adaptive Performance optional later. |

---

## 8. Testing

- **EditMode**: stored solution wins (all levels); Python trace parity (20 sequences/level);
  `CheckExit` (edges, cones, all facings); `GameSession` (no bay → bump, VIP on blocked truck, extra
  bay, jam reasons, win); `BoardLayout` (bay/vessel counts 2–5 produce non-overlapping geometry);
  `PolylinePath` (length, sampling, endpoints).
- **PlayMode**: auto-play the stored solution through real views at 4× speed → Win; tap spam during
  motion; Retry mid-animation; VIP while bays are busy.
- **Device checklist** from the GDD (60 fps, notch layout, audio overlap, mute persistence).

---

## 9. Build order (phases & gates)

### Phase 0 — Foundation (≈ 1–2 days)
- git + LFS, `.gitignore`; import starter into `Assets/_TankerJam`; asmdefs per §2.
- Player/URP/quality settings per §7; remove unused packages; import DOTween (user-provided).
- Port Python traces into fixtures; add parity + `CheckExit` tests. **All green before refactor.**

### Phase 1 — Greybox loop (GDD weeks 1–2) → **Gate: level 5 wins end-to-end in Unity**
- Core: `LotGrid`, alloc-free `GameRules`, `GameSession`, `BoardLayout`, `PolylinePath`, `RouteBuilder` + tests.
- Prefab baker (rework of `TankerJamPrefabBuilder`) producing combined-mesh greybox prefabs.
- `LevelBuilder`, `GameLoop`, `InputController`, `CameraFitter`, `TruckView` motion (drive/bump/leave),
  `PumpView`, `VesselView`, simple hose, plain color fill (no shader yet), win/jam detection, Retry.

### Phase 2 — Feel (weeks 3–4) → **Gate: feels as good as the web prototype**
- LiquidFill + Glass shaders, slosh/pitch springs, hose stream, instanced flow blobs, roof arrow &
  capacity badge decals, VIP lift arc, brake/bump juice, confetti, synthesized audio + AudioManager,
  lighting/palette pass, side-by-side comparison against the HTML prototype.

### Phase 3 — Content & meta (weeks 5–6) → **Gate: playtest with 5 new players**
- `curve.json` + `build_curve.py` → levels 1–50; importer/validator + LevelCatalog + preview window.
- Progress save, coins, booster inventory & intro grants (VIP @6, Extra @10), HUD/booster bar/popups,
  home overlay, tutorial hand on level 1 and booster intros, debug menu.

### Phase 4 — Polish & device pass (post-MVP)
- Analytics events, device perf pass on target phone, release build.

**MVP = Phases 0–3. No ads, no in-app purchases.**

---

## 10. Decisions (confirmed 2026-10-04)

1. MVP = Phases 0–3. **No ads and no in-app purchases.**
2. 5th color: green `G #3DDC84`.
3. Tweens: **DOTween** (package provided by the user).
4. Unity Editor is driven directly through the Unity MCP connection (scenes, prefabs, tests).
