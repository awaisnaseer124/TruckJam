// Reads the level JSON produced by Tools/levelgen (same format as the web prototype).
// Uses MiniJson so Core needs no packages.
using System;
using System.Collections.Generic;

namespace TankerJam.Core
{
    public static class LevelJson
    {
        public static LevelDef Parse(string json)
        {
            var root = (Dictionary<string, object>)MiniJson.Parse(json);
            var level = new LevelDef();
            level.Size = Convert.ToInt32(root["size"]);
            if (root.TryGetValue("slots", out var slots)) level.Slots = Convert.ToInt32(slots);

            var cars = (List<object>)root["cars"];
            for (int i = 0; i < cars.Count; i++)
            {
                var c = (Dictionary<string, object>)cars[i];
                level.Trucks.Add(new TruckDef
                {
                    Id = i,
                    X = Convert.ToInt32(c["x"]),
                    Y = Convert.ToInt32(c["y"]),
                    Len = Convert.ToInt32(c["len"]),
                    Facing = (Facing)Enum.Parse(typeof(Facing), (string)c["d"]),
                    Color = ((string)c["color"])[0],
                });
            }
            if (root.TryGetValue("cones", out var cones))
                foreach (List<object> p in (List<object>)cones)
                    level.Cones.Add(new Cell(Convert.ToInt32(p[0]), Convert.ToInt32(p[1])));
            foreach (List<object> tube in (List<object>)root["tubes"])
            {
                var v = new List<char>();
                foreach (object u in tube) v.Add(((string)u)[0]);
                level.Vessels.Add(v);
            }
            if (root.TryGetValue("sol", out var sol))
                foreach (object id in (List<object>)sol) level.Solution.Add(Convert.ToInt32(id));
            if (root.TryGetValue("randomWinRate", out var rate))
                level.RandomWinRate = (float)Convert.ToDouble(rate);
            return level;
        }

        /// <summary>
        /// Structural checks a level must pass before it can be played. Appends readable problems to
        /// <paramref name="errors"/> and returns true when there are none. Solvability is checked separately
        /// by replaying <see cref="LevelDef.Solution"/>.
        /// </summary>
        public static bool Validate(LevelDef level, List<string> errors)
        {
            int before = errors.Count;
            if (level.Size < 3) errors.Add($"Grid size {level.Size} is too small.");
            if (level.Slots < 1) errors.Add($"Level needs at least one regular bay (slots = {level.Slots}).");
            if (level.Trucks.Count == 0) errors.Add("Level has no trucks.");
            if (level.Vessels.Count == 0) errors.Add("Level has no vessels.");

            var occupied = new HashSet<long>();
            foreach (var c in level.Cones)
            {
                if (!InBounds(level, c.X, c.Y)) errors.Add($"Cone {c} is outside the grid.");
                else if (!occupied.Add(Key(c.X, c.Y))) errors.Add($"Duplicate cone at {c}.");
            }

            var need = new Dictionary<char, int>();
            foreach (var t in level.Trucks)
            {
                if (t.Len < 2 || t.Len > 4) { errors.Add($"Truck {t.Id} has unsupported length {t.Len}."); continue; }
                for (int i = 0; i < t.Len; i++)
                {
                    var c = t.CellAt(i);
                    if (!InBounds(level, c.X, c.Y)) errors.Add($"Truck {t.Id} cell {c} is outside the grid.");
                    else if (!occupied.Add(Key(c.X, c.Y))) errors.Add($"Truck {t.Id} overlaps another object at {c}.");
                }
                need.TryGetValue(t.Color, out int n);
                need[t.Color] = n + t.Capacity;
            }

            var have = new Dictionary<char, int>();
            foreach (var v in level.Vessels)
                foreach (char u in v)
                {
                    have.TryGetValue(u, out int n);
                    have[u] = n + 1;
                }
            foreach (var kv in need)
            {
                have.TryGetValue(kv.Key, out int n);
                if (n != kv.Value) errors.Add($"Color {kv.Key}: trucks hold {kv.Value} units but vessels contain {n}.");
            }
            foreach (var kv in have)
                if (!need.ContainsKey(kv.Key)) errors.Add($"Color {kv.Key} is in the vessels but no truck takes it.");

            for (int i = 0; i < level.Solution.Count; i++)
                if (level.Solution[i] < 0 || level.Solution[i] >= level.Trucks.Count)
                    errors.Add($"Solution step {i} references unknown truck {level.Solution[i]}.");

            return errors.Count == before;
        }

        static bool InBounds(LevelDef l, int x, int y) => x >= 0 && y >= 0 && x < l.Size && y < l.Size;
        static long Key(int x, int y) => ((long)x << 32) | (uint)y;
    }
}
