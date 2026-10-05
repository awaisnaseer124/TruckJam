// Editable level for the visual level editor (F4). Pure C#, so every editing operation is unit-tested
// without the editor UI. A draft is always free-form (trucks have poses) on a square lot; grid levels are
// converted on load. LevelDef stays the immutable "shipping" form: ToLevel() builds one for solving,
// playing and saving.
//
// Board space as everywhere else: origin at the lot center, +x screen right, +z toward the camera,
// angle 0 = toward the bays (up the screen), clockwise.
using System;
using System.Collections.Generic;

namespace TankerJam.Core
{
    public sealed class DraftTruck
    {
        public float X, Z, Angle;
        public int Len = 2;
        public char Color = 'P';

        public DraftTruck Copy() => (DraftTruck)MemberwiseClone();
        public Vec2 Center => new Vec2(X, Z);
        public Obb Shape => new Obb(Center, Geometry2D.Heading(Angle), (Len - TruckDef.LengthClearance) / 2f, TruckDef.CollisionWidth / 2f);
    }

    /// <summary>Per-truck problems the editor highlights.</summary>
    [Flags]
    public enum DraftIssue { None = 0, OutOfLot = 1, Overlap = 2, Stuck = 4 }

    public sealed class DraftReport
    {
        public DraftIssue[] Issues = Array.Empty<DraftIssue>();
        /// <summary>For each truck, how many trucks block its road out right now (layout only).</summary>
        public int[] BlockedBy = Array.Empty<int>();
        /// <summary>Rounds of "every free truck leaves" to clear the lot; -1 when some truck can never leave.</summary>
        public int JamDepth;
        public readonly SortedDictionary<char, int> UnitsNeeded = new SortedDictionary<char, int>();
        public readonly SortedDictionary<char, int> UnitsInVessels = new SortedDictionary<char, int>();
        public bool OilBalanced;
        public int ProblemCount;
    }

    public sealed class LevelDraft
    {
        public const float PositionStep = 0.05f;
        public const int MinLotSize = 5, MaxLotSize = 16;

        /// <summary>Side of the square lot in board units (1 unit = one truck cell).</summary>
        public int LotSize = 11;
        public int Slots = 3;
        public readonly List<DraftTruck> Trucks = new List<DraftTruck>();
        /// <summary>Cone centers (board space).</summary>
        public readonly List<Vec2> Cones = new List<Vec2>();
        public readonly List<List<char>> Vessels = new List<List<char>>();
        /// <summary>Winning tap order from the last successful solve; cleared by any edit that could break it.</summary>
        public readonly List<int> Solution = new List<int>();
        public float RandomWinRate = -1f;

        // Curve metadata carried through load/save.
        public int Index, Tier;
        public bool Hard;
        public string Introduces, Pattern;

        public float Half => LotSize / 2f;

        // ---------------- conversion ----------------

        public static LevelDraft From(LevelDef level)
        {
            var d = new LevelDraft
            {
                Slots = level.Slots, RandomWinRate = level.RandomWinRate,
                Index = level.Index, Tier = level.Tier, Hard = level.Hard, Introduces = level.Introduces, Pattern = level.Pattern,
            };
            var b = level.Board;
            d.LotSize = Math.Max(MinLotSize, (int)Math.Ceiling(Math.Max(b.Width, b.Height) - 1e-3f));
            foreach (var t in level.Trucks)
            {
                var p = t.GetPose(level.Size);
                d.Trucks.Add(new DraftTruck { X = p.X, Z = p.Z, Angle = Norm(p.Angle), Len = t.Len, Color = t.Color });
            }
            var obstacles = new List<Obb>();
            level.CollectObstacles(obstacles);
            foreach (var o in obstacles) d.Cones.Add(o.Center);
            foreach (var v in level.Vessels) d.Vessels.Add(new List<char>(v));
            d.Solution.AddRange(level.Solution);
            return d;
        }

        /// <summary>A version 2 level on a square lot. Truck ids follow list order.</summary>
        public LevelDef ToLevel()
        {
            var level = new LevelDef
            {
                Version = 2, Size = LotSize, Slots = Slots, Board = BoardShape.Rounded(LotSize, LotSize, 0f),
                RandomWinRate = RandomWinRate, Index = Index, Tier = Tier, Hard = Hard, Introduces = Introduces, Pattern = Pattern,
            };
            for (int i = 0; i < Trucks.Count; i++)
            {
                var t = Trucks[i];
                level.Trucks.Add(new TruckDef
                {
                    Id = i, Len = t.Len, Color = t.Color, HasPose = true,
                    FreePose = new TruckPose(t.X, t.Z, t.Angle), Facing = TruckDef.FacingOf(t.Angle),
                });
            }
            foreach (var c in Cones) level.Obstacles.Add(new Obstacle(c.X, c.Z, Obstacle.ConeHalfSize));
            foreach (var v in Vessels) level.Vessels.Add(new List<char>(v));
            level.Solution.AddRange(Solution);
            return level;
        }

        public LevelDraft Clone()
        {
            var copy = new LevelDraft
            {
                LotSize = LotSize, Slots = Slots, RandomWinRate = RandomWinRate,
                Index = Index, Tier = Tier, Hard = Hard, Introduces = Introduces, Pattern = Pattern,
            };
            foreach (var t in Trucks) copy.Trucks.Add(t.Copy());
            copy.Cones.AddRange(Cones);
            foreach (var v in Vessels) copy.Vessels.Add(new List<char>(v));
            copy.Solution.AddRange(Solution);
            return copy;
        }

        /// <summary>Call after any edit: the stored solution and score no longer describe the level.</summary>
        public void Invalidate()
        {
            Solution.Clear();
            RandomWinRate = -1f;
        }

        // ---------------- placement ----------------

        public static float Snap(float v, float step = PositionStep) => (float)Math.Round(v / step) * step;
        public static float Norm(float deg) => ((deg % 360f) + 360f) % 360f;

        public bool InsideLot(Obb shape)
        {
            for (int k = 0; k < 4; k++)
            {
                var c = shape.Corner(k);
                if (Math.Abs(c.X) > Half + 1e-3f || Math.Abs(c.Z) > Half + 1e-3f) return false;
            }
            return true;
        }

        static Obb ConeShape(Vec2 c) => new Obb(c, new Vec2(0, -1), Obstacle.ConeHalfSize, Obstacle.ConeHalfSize);

        /// <summary>Whether a truck would fit (inside the lot, touching nothing), ignoring trucks in <paramref name="ignore"/>.</summary>
        public bool Fits(DraftTruck t, ICollection<DraftTruck> ignore = null)
        {
            var shape = t.Shape;
            if (!InsideLot(shape)) return false;
            foreach (var o in Trucks)
                if (o != t && (ignore == null || !ignore.Contains(o)) && Geometry2D.Overlaps(shape, o.Shape)) return false;
            foreach (var c in Cones)
                if (Geometry2D.Overlaps(shape, ConeShape(c))) return false;
            return true;
        }

        /// <summary>Adds a truck (snapped) if it fits. Returns it, or null when it doesn't fit.</summary>
        public DraftTruck TryAdd(float x, float z, float angle, int len, char color)
        {
            var t = new DraftTruck { X = Snap(x), Z = Snap(z), Angle = Norm(angle), Len = len, Color = color };
            if (!Fits(t)) return null;
            Trucks.Add(t);
            Invalidate();
            return t;
        }

        /// <summary>Moves a truck to its pose rotated by <paramref name="degrees"/> clockwise about the lot center.</summary>
        public static DraftTruck Rotated(DraftTruck t, float degrees)
        {
            double r = degrees * Math.PI / 180.0, c = Math.Cos(r), s = Math.Sin(r);
            // Rotates Heading(a) onto Heading(a + degrees), so positions and headings turn together.
            return new DraftTruck
            {
                X = Snap((float)(t.X * c - t.Z * s)), Z = Snap((float)(t.X * s + t.Z * c)),
                Angle = Norm(t.Angle + degrees), Len = t.Len, Color = t.Color,
            };
        }

        /// <summary>Mirror image across the vertical center line (left/right).</summary>
        public static DraftTruck MirroredX(DraftTruck t) =>
            new DraftTruck { X = -t.X, Z = t.Z, Angle = Norm(360f - t.Angle), Len = t.Len, Color = t.Color };

        /// <summary>Mirror image across the horizontal center line (top/bottom).</summary>
        public static DraftTruck MirroredZ(DraftTruck t) =>
            new DraftTruck { X = t.X, Z = -t.Z, Angle = Norm(180f - t.Angle), Len = t.Len, Color = t.Color };

        static bool SamePose(DraftTruck a, DraftTruck b) =>
            Math.Abs(a.X - b.X) < 0.02f && Math.Abs(a.Z - b.Z) < 0.02f && Math.Abs(Norm(a.Angle - b.Angle + 180f) - 180f) < 0.5f;

        /// <summary>
        /// Adds copies of <paramref name="source"/> made by <paramref name="make"/>; copies that land exactly on
        /// a source truck (it lies on the symmetry axis) are skipped, copies that don't fit are reported.
        /// </summary>
        List<DraftTruck> AddCopies(IList<DraftTruck> source, Func<DraftTruck, DraftTruck> make, out int rejected)
        {
            var added = new List<DraftTruck>();
            rejected = 0;
            foreach (var t in source)
            {
                var c = make(t);
                bool duplicate = false;
                foreach (var o in Trucks) if (SamePose(o, c)) { duplicate = true; break; }
                if (duplicate) continue;
                if (Fits(c)) { Trucks.Add(c); added.Add(c); }
                else rejected++;
            }
            if (added.Count > 0) Invalidate();
            return added;
        }

        public List<DraftTruck> MirrorX(IList<DraftTruck> source, out int rejected) => AddCopies(source, MirroredX, out rejected);
        public List<DraftTruck> MirrorZ(IList<DraftTruck> source, out int rejected) => AddCopies(source, MirroredZ, out rejected);

        /// <summary>k-fold rotational symmetry: adds k-1 rotated copies of every source truck.</summary>
        public List<DraftTruck> RotateCopies(IList<DraftTruck> source, int k, out int rejected)
        {
            var all = new List<DraftTruck>();
            rejected = 0;
            var snapshot = new List<DraftTruck>(source);
            for (int i = 1; i < k; i++)
            {
                float deg = 360f * i / k;
                all.AddRange(AddCopies(snapshot, t => Rotated(t, deg), out int r));
                rejected += r;
            }
            return all;
        }

        // ---------------- diagnostics ----------------

        public DraftReport Analyze()
        {
            int n = Trucks.Count;
            var r = new DraftReport { Issues = new DraftIssue[n], BlockedBy = new int[n] };
            var shapes = new Obb[n];
            for (int i = 0; i < n; i++)
            {
                shapes[i] = Trucks[i].Shape;
                if (!InsideLot(shapes[i])) r.Issues[i] |= DraftIssue.OutOfLot;
            }
            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                    if (Geometry2D.Overlaps(shapes[i], shapes[j])) { r.Issues[i] |= DraftIssue.Overlap; r.Issues[j] |= DraftIssue.Overlap; }
                foreach (var c in Cones)
                {
                    var cone = ConeShape(c);
                    if (Geometry2D.Overlaps(shapes[i], cone)) r.Issues[i] |= DraftIssue.Overlap;
                    else if (Geometry2D.TimeOfImpact(shapes[i], shapes[i].Axis, cone, out _)) r.Issues[i] |= DraftIssue.Stuck;
                }
                for (int j = 0; j < n; j++)
                    if (j != i && Geometry2D.TimeOfImpact(shapes[i], shapes[i].Axis, shapes[j], out _)) r.BlockedBy[i]++;
            }

            r.JamDepth = HasPlacementProblems(r) ? -1 : JamDepthOf(shapes, r);

            foreach (var t in Trucks)
            {
                r.UnitsNeeded.TryGetValue(t.Color, out int k);
                r.UnitsNeeded[t.Color] = k + TruckDef.CapacityFor(t.Len);
            }
            foreach (var v in Vessels)
                foreach (char u in v)
                {
                    r.UnitsInVessels.TryGetValue(u, out int k);
                    r.UnitsInVessels[u] = k + 1;
                }
            r.OilBalanced = r.UnitsNeeded.Count == r.UnitsInVessels.Count;
            foreach (var kv in r.UnitsNeeded)
                if (!r.UnitsInVessels.TryGetValue(kv.Key, out int have) || have != kv.Value) r.OilBalanced = false;

            foreach (var issue in r.Issues) if (issue != DraftIssue.None) r.ProblemCount++;
            return r;
        }

        static bool HasPlacementProblems(DraftReport r)
        {
            foreach (var i in r.Issues) if ((i & (DraftIssue.OutOfLot | DraftIssue.Overlap)) != 0) return true;
            return false;
        }

        int JamDepthOf(Obb[] shapes, DraftReport r)
        {
            int n = shapes.Length;
            var gone = new bool[n];
            int left = n, depth = 0;
            var free = new List<int>();
            while (left > 0)
            {
                free.Clear();
                for (int i = 0; i < n; i++)
                {
                    if (gone[i] || (r.Issues[i] & DraftIssue.Stuck) != 0) continue;
                    bool clear = true;
                    for (int j = 0; j < n && clear; j++)
                        if (j != i && !gone[j] && Geometry2D.TimeOfImpact(shapes[i], shapes[i].Axis, shapes[j], out _)) clear = false;
                    if (clear) free.Add(i);
                }
                if (free.Count == 0) return -1;
                foreach (int i in free) gone[i] = true;
                left -= free.Count;
                depth++;
            }
            return depth;
        }

        // ---------------- colors and oil ----------------

        /// <summary>Recolors every truck so the colors are spread evenly (by oil units) and mixed around the board.</summary>
        public void AutoColor(IList<char> palette, int seed)
        {
            if (palette.Count == 0 || Trucks.Count == 0) return;
            var rng = new Random(seed);
            var order = new List<DraftTruck>(Trucks);
            for (int i = order.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }
            var units = new int[palette.Count];
            foreach (var t in order)
            {
                int best = 0;
                for (int c = 1; c < palette.Count; c++) if (units[c] < units[best]) best = c;
                t.Color = palette[best];
                units[best] += TruckDef.CapacityFor(t.Len);
            }
            Invalidate();
        }

        /// <summary>Puts exactly the trucks' oil into <paramref name="vesselCount"/> vessels in a random order (no solve).</summary>
        public void FillVessels(int vesselCount, Random rng)
        {
            var units = new List<char>();
            foreach (var t in Trucks)
                for (int u = 0; u < TruckDef.CapacityFor(t.Len); u++) units.Add(t.Color);
            for (int i = units.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (units[i], units[j]) = (units[j], units[i]);
            }
            Vessels.Clear();
            vesselCount = Math.Max(1, vesselCount);
            for (int v = 0, start = 0; v < vesselCount; v++)
            {
                int count = (units.Count - start) / (vesselCount - v);
                Vessels.Add(units.GetRange(start, count));
                start += count;
            }
            Invalidate();
        }

        /// <summary>Smallest vessel count that keeps every vessel within <paramref name="maxUnits"/> (at least 3).</summary>
        public int SuggestedVesselCount(int maxUnits = LevelVerifier.MaxVesselUnits)
        {
            int units = 0;
            foreach (var t in Trucks) units += TruckDef.CapacityFor(t.Len);
            return Math.Max(3, (units + maxUnits - 1) / maxUnits);
        }

        public sealed class FillResult
        {
            public bool Solved;
            public int Attempts;
            public LevelScore Score;
        }

        /// <summary>
        /// Tries random vessel orders until the level solves. With a target win rate, keeps the solvable order
        /// whose random-play win rate is closest to it (lower = harder); otherwise stops at the first win.
        /// </summary>
        public FillResult AutoFill(int vesselCount, int seed, int attempts = 40, float targetWinRate = -1f, int samples = 200)
        {
            var result = new FillResult();
            List<List<char>> best = null;
            float bestGap = float.MaxValue;
            var rng = new Random(seed);
            for (int a = 0; a < attempts; a++)
            {
                result.Attempts = a + 1;
                FillVessels(vesselCount, rng);
                var score = LevelSolver.Score(ToLevel(), samples, 200_000);
                if (score.Solve.Status != SolveStatus.Solved) continue;
                float gap = targetWinRate < 0f ? 0f : Math.Abs(score.RandomWinRate - targetWinRate);
                if (gap < bestGap)
                {
                    bestGap = gap;
                    result.Score = score;
                    best = new List<List<char>>();
                    foreach (var v in Vessels) best.Add(new List<char>(v));
                }
                if (targetWinRate < 0f) break;
            }
            if (best == null) return result;
            Vessels.Clear();
            Vessels.AddRange(best);
            ApplyScore(result.Score);
            result.Solved = true;
            return result;
        }

        /// <summary>Solves and scores the draft as it is; stores the solution when it solves.</summary>
        public LevelScore Solve(int samples = 400)
        {
            var score = LevelSolver.Score(ToLevel(), samples);
            Solution.Clear();
            RandomWinRate = -1f;
            if (score.Solve.Status == SolveStatus.Solved) ApplyScore(score);
            return score;
        }

        void ApplyScore(LevelScore score)
        {
            Solution.Clear();
            Solution.AddRange(score.Solve.Solution);
            RandomWinRate = score.RandomWinRate;
        }
    }
}
