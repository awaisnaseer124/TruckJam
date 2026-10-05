// Solver and difficulty scorer for any level (grid or free-form), replacing Tools/levelgen's Python solver.
//
// Lot: in the geometric lot a truck is blocked exactly when some truck still on the board (or an obstacle)
// lies in its sweep, and that relation depends only on the layout. So it is precomputed once as bitmasks:
// "can i exit?" = (blockers[i] & remaining) == 0. Oil: a compact copy of GameRules' settle (same order:
// slots by arrival, vessels left to right, bottom layer only), proven identical by tests.
// Search: depth-first over tap orders using only the regular bays, memoized on
// (remaining trucks, vessel bottoms, slot colors/fills) like the Python solver.
using System;
using System.Collections.Generic;

namespace TankerJam.Core
{
    public enum SolveStatus { Solved, Unsolvable, LimitReached }

    public sealed class SolveResult
    {
        public SolveStatus Status;
        public List<int> Solution = new List<int>();
        public int NodesVisited;
        public double Milliseconds;
    }

    public sealed class LevelScore
    {
        public SolveResult Solve;
        /// <summary>Share of random games won (random exitable truck each tap).</summary>
        public float RandomWinRate;
        /// <summary>Rounds of "remove every truck that can exit" needed to clear the lot (layout only).</summary>
        public int JamDepth;
        public int Samples;
    }

    public sealed class LevelSolver
    {
        public const int MaxTrucks = 64;
        public const int MaxVessels = 12;
        public const int MaxSlots = 6;      // memo key packs 6 slots x 10 bits

        readonly LevelDef level;
        readonly int n, vesselCount, slotLimit;
        readonly ulong[] blockers;
        readonly bool[] stuck;
        readonly int[] color, cap;           // per truck: palette index, capacity
        readonly int[][] tubes;              // per vessel: color indices, bottom first
        readonly List<char> colorKeys = new List<char>(8);

        public LevelSolver(LevelDef level)
        {
            this.level = level;
            n = level.Trucks.Count;
            vesselCount = level.Vessels.Count;
            slotLimit = level.Slots;
            if (n > MaxTrucks) throw new ArgumentException($"Solver supports up to {MaxTrucks} trucks (level has {n}).");
            if (vesselCount > MaxVessels) throw new ArgumentException($"Solver supports up to {MaxVessels} vessels.");
            if (slotLimit > MaxSlots) throw new ArgumentException($"Solver supports up to {MaxSlots} bays.");

            color = new int[n];
            cap = new int[n];
            for (int i = 0; i < n; i++)
            {
                color[i] = ColorIndex(level.Trucks[i].Color);
                cap[i] = level.Trucks[i].Capacity;
            }
            tubes = new int[vesselCount][];
            for (int v = 0; v < vesselCount; v++)
            {
                tubes[v] = new int[level.Vessels[v].Count];
                for (int u = 0; u < tubes[v].Length; u++) tubes[v][u] = ColorIndex(level.Vessels[v][u]);
                if (tubes[v].Length > 31) throw new ArgumentException("Vessel taller than 31 units.");
            }

            // Pairwise blocking from the geometric lot.
            blockers = new ulong[n];
            stuck = new bool[n];
            var shapes = new Obb[n];
            for (int i = 0; i < n; i++) shapes[i] = level.Trucks[i].Shape(level.Size);
            var obstacles = new List<Obb>();
            level.CollectObstacles(obstacles);
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                    if (j != i && Geometry2D.TimeOfImpact(shapes[i], shapes[i].Axis, shapes[j], out _))
                        blockers[i] |= 1UL << j;
                foreach (var o in obstacles)
                    if (Geometry2D.TimeOfImpact(shapes[i], shapes[i].Axis, o, out _)) stuck[i] = true;
            }
        }

        int ColorIndex(char key)
        {
            int i = colorKeys.IndexOf(key);
            if (i >= 0) return i;
            if (colorKeys.Count >= 8) throw new ArgumentException("Solver supports up to 8 colors.");
            colorKeys.Add(key);
            return colorKeys.Count - 1;
        }

        public ulong AllTrucks => n == 64 ? ulong.MaxValue : (1UL << n) - 1;

        public bool CanExit(int truck, ulong remaining) => !stuck[truck] && (blockers[truck] & remaining) == 0;

        /// <summary>Trucks that block <paramref name="truck"/> (for editor display and analysis).</summary>
        public ulong BlockersOf(int truck) => blockers[truck];

        // ---------------- compact state ----------------

        /// <summary>Search state. Slots hold truck ids in arrival order with their fill.</summary>
        public sealed class State
        {
            public ulong Remaining;
            public readonly int[] Heads;
            public readonly int[] SlotTruck = new int[MaxSlots + 1];
            public readonly int[] SlotFill = new int[MaxSlots + 1];
            public int SlotCount;

            public State(int vessels) { Heads = new int[vessels]; }

            public State Clone()
            {
                var s = new State(Heads.Length) { Remaining = Remaining, SlotCount = SlotCount };
                Array.Copy(Heads, s.Heads, Heads.Length);
                Array.Copy(SlotTruck, s.SlotTruck, SlotCount);
                Array.Copy(SlotFill, s.SlotFill, SlotCount);
                return s;
            }
        }

        public State Start() => new State(vesselCount) { Remaining = AllTrucks };

        public bool IsWon(State s) => s.Remaining == 0 && s.SlotCount == 0;

        /// <summary>Moves a truck from the lot into a logical slot and settles (identical to GameRules.Assign).</summary>
        public void Assign(State s, int truck, List<Unit> unitsOut = null)
        {
            s.Remaining &= ~(1UL << truck);
            s.SlotTruck[s.SlotCount] = truck;
            s.SlotFill[s.SlotCount] = 0;
            s.SlotCount++;
            while (true)
            {
                bool changed = true;
                while (changed)
                {
                    changed = false;
                    for (int si = 0; si < s.SlotCount; si++)
                    {
                        int t = s.SlotTruck[si];
                        if (s.SlotFill[si] >= cap[t]) continue;
                        for (int v = 0; v < vesselCount; v++)
                        {
                            int h = s.Heads[v];
                            if (s.SlotFill[si] < cap[t] && h < tubes[v].Length && tubes[v][h] == color[t])
                            {
                                s.Heads[v] = h + 1;
                                s.SlotFill[si]++;
                                changed = true;
                                unitsOut?.Add(new Unit(t, v, level.Trucks[t].Color));
                            }
                        }
                    }
                }
                int write = 0;
                for (int read = 0; read < s.SlotCount; read++)
                    if (s.SlotFill[read] < cap[s.SlotTruck[read]])
                    {
                        s.SlotTruck[write] = s.SlotTruck[read];
                        s.SlotFill[write] = s.SlotFill[read];
                        write++;
                    }
                bool removed = write < s.SlotCount;
                s.SlotCount = write;
                if (!removed) break;
            }
        }

        readonly struct Key : IEquatable<Key>
        {
            readonly ulong remaining, heads, slots;
            public Key(ulong r, ulong h, ulong s) { remaining = r; heads = h; slots = s; }
            public bool Equals(Key o) => remaining == o.remaining && heads == o.heads && slots == o.slots;
            public override bool Equals(object o) => o is Key k && Equals(k);
            public override int GetHashCode() => (remaining.GetHashCode() * 397 ^ heads.GetHashCode()) * 397 ^ slots.GetHashCode();
        }

        Key KeyOf(State s)
        {
            ulong h = 0;
            for (int v = 0; v < vesselCount; v++) h |= (ulong)s.Heads[v] << (5 * v);
            ulong k = 0;
            for (int i = 0; i < s.SlotCount; i++)
            {
                int t = s.SlotTruck[i];
                // Slot identity for memo purposes: color, fill, capacity (which truck it is doesn't matter).
                ulong packed = (ulong)color[t] | ((ulong)s.SlotFill[i] << 3) | ((ulong)cap[t] << 6);
                k |= packed << (10 * i);
            }
            return new Key(s.Remaining, h, k);
        }

        // ---------------- solve ----------------

        public SolveResult Solve(int nodeLimit = 2_000_000)
        {
            var result = new SolveResult();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var seen = new HashSet<Key>();
            var path = new List<int>(n);
            int nodes = 0;
            bool limited = false;

            bool Rec(State s)
            {
                if (++nodes > nodeLimit) { limited = true; return false; }
                if (IsWon(s)) return true;
                if (!seen.Add(KeyOf(s))) return false;
                if (s.SlotCount >= slotLimit) return false;
                for (int i = 0; i < n; i++)
                {
                    if ((s.Remaining & (1UL << i)) == 0 || !CanExit(i, s.Remaining)) continue;
                    var next = s.Clone();
                    Assign(next, i);
                    path.Add(i);
                    if (Rec(next)) return true;
                    path.RemoveAt(path.Count - 1);
                    if (limited) return false;
                }
                return false;
            }

            bool solved = Rec(Start());
            result.Status = solved ? SolveStatus.Solved : limited ? SolveStatus.LimitReached : SolveStatus.Unsolvable;
            if (solved) result.Solution.AddRange(path);
            result.NodesVisited = nodes;
            result.Milliseconds = watch.Elapsed.TotalMilliseconds;
            return result;
        }

        // ---------------- scoring ----------------

        /// <summary>One random game: each tap picks a random exitable truck; lost when the bays fill or nothing can move.</summary>
        public bool RandomPlay(Random rng, List<int> scratch)
        {
            var s = Start();
            while (true)
            {
                if (IsWon(s)) return true;
                if (s.SlotCount >= slotLimit) return false;
                scratch.Clear();
                for (int i = 0; i < n; i++)
                    if ((s.Remaining & (1UL << i)) != 0 && CanExit(i, s.Remaining)) scratch.Add(i);
                if (scratch.Count == 0) return false;
                Assign(s, scratch[rng.Next(scratch.Count)]);
            }
        }

        public float RandomWinRate(int samples, int seed = 1)
        {
            var scratch = new List<int>(n);
            int wins = 0;
            for (int k = 0; k < samples; k++)
                if (RandomPlay(new Random(seed + k), scratch)) wins++;
            return samples > 0 ? (float)wins / samples : 0f;
        }

        /// <summary>Rounds of removing every exitable truck until the lot is empty; -1 if it can never be cleared.</summary>
        public int JamDepth()
        {
            ulong remaining = AllTrucks;
            int depth = 0;
            while (remaining != 0)
            {
                ulong free = 0;
                for (int i = 0; i < n; i++)
                    if ((remaining & (1UL << i)) != 0 && CanExit(i, remaining)) free |= 1UL << i;
                if (free == 0) return -1;
                remaining &= ~free;
                depth++;
            }
            return depth;
        }

        /// <summary>Solve plus difficulty: what the level editor and batch tools show.</summary>
        public static LevelScore Score(LevelDef level, int samples = 400, int nodeLimit = 2_000_000)
        {
            var solver = new LevelSolver(level);
            return new LevelScore
            {
                Solve = solver.Solve(nodeLimit),
                RandomWinRate = solver.RandomWinRate(samples),
                JamDepth = solver.JamDepth(),
                Samples = samples,
            };
        }
    }
}
