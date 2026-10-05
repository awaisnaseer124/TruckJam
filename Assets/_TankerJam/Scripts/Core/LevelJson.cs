// Reads and writes level JSON. Two formats share one LevelDef:
//  - version 1 (grid): "size", "cars" [{x, y, len, d, color}], "cones" [[x, y]]  (Tools/levelgen, web prototype)
//  - version 2 (free-form): "version": 2, "board" {shape, ...}, "trucks" [{x, z, angle, len, color}],
//    "obstacles" [{x, z, size}]  (board space: origin at the board center, z toward the camera,
//    angle 0 = toward the bays, clockwise)
// Shared: "tubes", "slots", "sol", "randomWinRate", "meta". Grid levels convert to version 2 with ToJsonV2.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TankerJam.Core
{
    public static class LevelJson
    {
        /// <summary>Positions snap to this step when free-form levels are read, so geometry is reproducible.</summary>
        public const float PositionStep = 0.05f;

        public static LevelDef Parse(string json)
        {
            var root = (Dictionary<string, object>)MiniJson.Parse(json);
            var level = new LevelDef();
            if (root.TryGetValue("version", out var version)) level.Version = Convert.ToInt32(version);
            if (root.TryGetValue("slots", out var slots)) level.Slots = Convert.ToInt32(slots);

            if (level.IsGrid) ParseGrid(root, level);
            else ParseFree(root, level);

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
            if (root.TryGetValue("meta", out var metaObj) && metaObj is Dictionary<string, object> meta)
            {
                if (meta.TryGetValue("index", out var index)) level.Index = Convert.ToInt32(index);
                if (meta.TryGetValue("tier", out var tier)) level.Tier = Convert.ToInt32(tier);
                if (meta.TryGetValue("hard", out var hard)) level.Hard = hard is bool b && b;
                if (meta.TryGetValue("introduces", out var intro)) level.Introduces = intro as string;
                if (meta.TryGetValue("pattern", out var pattern)) level.Pattern = pattern as string;
            }
            return level;
        }

        static void ParseGrid(Dictionary<string, object> root, LevelDef level)
        {
            level.Size = Convert.ToInt32(root["size"]);
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
        }

        static void ParseFree(Dictionary<string, object> root, LevelDef level)
        {
            var board = (Dictionary<string, object>)root["board"];
            string shape = board.TryGetValue("shape", out var s) ? (string)s : "circle";
            switch (shape)
            {
                case "circle":
                    level.Board = BoardShape.Circle(F(board, "radius"));
                    break;
                case "rounded":
                    level.Board = BoardShape.Rounded(F(board, "width"), F(board, "height"), F(board, "corner"));
                    break;
                case "rect":
                    level.Board = BoardShape.Rounded(F(board, "width"), F(board, "height"), 0f);
                    break;
                default:
                    throw new FormatException($"Unknown board shape '{shape}'.");
            }
            level.Size = (int)Math.Ceiling(Math.Max(level.Board.Width, level.Board.Height));

            var trucks = (List<object>)root["trucks"];
            for (int i = 0; i < trucks.Count; i++)
            {
                var t = (Dictionary<string, object>)trucks[i];
                float angle = Normalize(F(t, "angle"));
                level.Trucks.Add(new TruckDef
                {
                    Id = i,
                    Len = Convert.ToInt32(t["len"]),
                    Color = ((string)t["color"])[0],
                    HasPose = true,
                    FreePose = new TruckPose(Snap(F(t, "x")), Snap(F(t, "z")), angle),
                    Facing = TruckDef.FacingOf(angle),
                });
            }
            if (root.TryGetValue("obstacles", out var obs))
                foreach (Dictionary<string, object> o in (List<object>)obs)
                    level.Obstacles.Add(new Obstacle(Snap(F(o, "x")), Snap(F(o, "z")), F(o, "size") / 2f));
        }

        static float F(Dictionary<string, object> d, string key) => (float)Convert.ToDouble(d[key], CultureInfo.InvariantCulture);
        static float Snap(float v) => (float)Math.Round(v / PositionStep) * PositionStep;
        static float Normalize(float deg) => ((deg % 360f) + 360f) % 360f;

        // ---------------- writing ----------------

        /// <summary>Writes any level (grid or free-form) as version 2 JSON. Grid levels become poses on a square board.</summary>
        public static string ToJsonV2(LevelDef level)
        {
            var sb = new StringBuilder(2048);
            sb.Append("{\n \"version\": 2,\n");
            var b = level.Board;
            switch (b.Kind)
            {
                case BoardKind.Circle:
                    sb.Append(" \"board\": {\"shape\": \"circle\", \"radius\": ").Append(N(b.Radius)).Append("},\n");
                    break;
                default:
                    sb.Append(" \"board\": {\"shape\": \"").Append(b.Radius > 0f ? "rounded" : "rect").Append("\", \"width\": ")
                      .Append(N(b.Width)).Append(", \"height\": ").Append(N(b.Height));
                    if (b.Radius > 0f) sb.Append(", \"corner\": ").Append(N(b.Radius));
                    sb.Append("},\n");
                    break;
            }
            sb.Append(" \"slots\": ").Append(level.Slots).Append(",\n");

            sb.Append(" \"trucks\": [\n");
            for (int i = 0; i < level.Trucks.Count; i++)
            {
                var t = level.Trucks[i];
                var p = t.GetPose(level.Size);
                sb.Append("  {\"x\": ").Append(N(p.X)).Append(", \"z\": ").Append(N(p.Z)).Append(", \"angle\": ").Append(N(p.Angle))
                  .Append(", \"len\": ").Append(t.Len).Append(", \"color\": \"").Append(t.Color).Append("\"}")
                  .Append(i < level.Trucks.Count - 1 ? ",\n" : "\n");
            }
            sb.Append(" ],\n");

            var obstacles = new List<Obb>();
            level.CollectObstacles(obstacles);
            sb.Append(" \"obstacles\": [");
            for (int i = 0; i < obstacles.Count; i++)
            {
                var o = obstacles[i];
                sb.Append(i == 0 ? "\n" : ",\n").Append("  {\"x\": ").Append(N(o.Center.X)).Append(", \"z\": ").Append(N(o.Center.Z))
                  .Append(", \"size\": ").Append(N(o.HalfLength * 2f)).Append('}');
            }
            sb.Append(obstacles.Count > 0 ? "\n ],\n" : "],\n");

            sb.Append(" \"tubes\": [");
            for (int v = 0; v < level.Vessels.Count; v++)
            {
                sb.Append(v == 0 ? "" : ", ").Append('[');
                for (int u = 0; u < level.Vessels[v].Count; u++)
                    sb.Append(u == 0 ? "" : ",").Append('"').Append(level.Vessels[v][u]).Append('"');
                sb.Append(']');
            }
            sb.Append("],\n");

            sb.Append(" \"sol\": [");
            for (int i = 0; i < level.Solution.Count; i++) sb.Append(i == 0 ? "" : ", ").Append(level.Solution[i]);
            sb.Append("]");
            if (level.RandomWinRate >= 0f) sb.Append(",\n \"randomWinRate\": ").Append(N(level.RandomWinRate));

            sb.Append(",\n \"meta\": {\"index\": ").Append(level.Index).Append(", \"tier\": ").Append(level.Tier)
              .Append(", \"hard\": ").Append(level.Hard ? "true" : "false");
            if (level.Introduces != null) sb.Append(", \"introduces\": \"").Append(level.Introduces).Append('"');
            if (level.Pattern != null) sb.Append(", \"pattern\": \"").Append(level.Pattern).Append('"');
            sb.Append("}\n}\n");
            return sb.ToString();
        }

        static string N(float v) => ((double)v).ToString("0.###", CultureInfo.InvariantCulture);

        // ---------------- validation ----------------

        /// <summary>
        /// Structural checks a level must pass before it can be played. Appends readable problems to
        /// <paramref name="errors"/> and returns true when there are none. Solvability is checked separately
        /// by replaying <see cref="LevelDef.Solution"/>.
        /// </summary>
        public static bool Validate(LevelDef level, List<string> errors)
        {
            int before = errors.Count;
            if (level.Slots < 1) errors.Add($"Level needs at least one regular bay (slots = {level.Slots}).");
            if (level.Trucks.Count == 0) errors.Add("Level has no trucks.");
            if (level.Vessels.Count == 0) errors.Add("Level has no vessels.");
            foreach (var t in level.Trucks)
                if (t.Len < 2 || t.Len > 4) errors.Add($"Truck {t.Id} has unsupported length {t.Len}.");

            if (level.IsGrid) ValidateGridPlacement(level, errors);
            else ValidateFreePlacement(level, errors);

            ValidateOil(level, errors);
            for (int i = 0; i < level.Solution.Count; i++)
                if (level.Solution[i] < 0 || level.Solution[i] >= level.Trucks.Count)
                    errors.Add($"Solution step {i} references unknown truck {level.Solution[i]}.");
            return errors.Count == before;
        }

        static void ValidateGridPlacement(LevelDef level, List<string> errors)
        {
            if (level.Size < 3) errors.Add($"Grid size {level.Size} is too small.");
            var occupied = new HashSet<long>();
            foreach (var c in level.Cones)
            {
                if (!InBounds(level, c.X, c.Y)) errors.Add($"Cone {c} is outside the grid.");
                else if (!occupied.Add(Key(c.X, c.Y))) errors.Add($"Duplicate cone at {c}.");
            }
            foreach (var t in level.Trucks)
            {
                if (t.Len < 2 || t.Len > 4) continue;
                for (int i = 0; i < t.Len; i++)
                {
                    var c = t.CellAt(i);
                    if (!InBounds(level, c.X, c.Y)) errors.Add($"Truck {t.Id} cell {c} is outside the grid.");
                    else if (!occupied.Add(Key(c.X, c.Y))) errors.Add($"Truck {t.Id} overlaps another object at {c}.");
                }
            }
        }

        static void ValidateFreePlacement(LevelDef level, List<string> errors)
        {
            var shapes = new Obb[level.Trucks.Count];
            for (int i = 0; i < shapes.Length; i++)
            {
                shapes[i] = level.Trucks[i].Shape(level.Size);
                for (int k = 0; k < 4; k++)
                    if (!level.Board.Contains(shapes[i].Corner(k)))
                    {
                        errors.Add($"Truck {i} sticks out of the board.");
                        break;
                    }
            }
            var obstacles = new List<Obb>();
            level.CollectObstacles(obstacles);
            for (int i = 0; i < shapes.Length; i++)
            {
                for (int j = i + 1; j < shapes.Length; j++)
                    if (Geometry2D.Overlaps(shapes[i], shapes[j])) errors.Add($"Trucks {i} and {j} overlap.");
                for (int o = 0; o < obstacles.Count; o++)
                    if (Geometry2D.Overlaps(shapes[i], obstacles[o])) errors.Add($"Truck {i} overlaps obstacle {o}.");
            }
        }

        static void ValidateOil(LevelDef level, List<string> errors)
        {
            var need = new Dictionary<char, int>();
            foreach (var t in level.Trucks)
            {
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
        }

        static bool InBounds(LevelDef l, int x, int y) => x >= 0 && y >= 0 && x < l.Size && y < l.Size;
        static long Key(int x, int y) => ((long)x << 32) | (uint)y;
    }
}
