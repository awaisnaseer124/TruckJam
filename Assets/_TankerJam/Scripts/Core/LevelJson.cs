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
            foreach (List<object> p in (List<object>)root["cones"])
                level.Cones.Add(new Cell(Convert.ToInt32(p[0]), Convert.ToInt32(p[1])));
            foreach (List<object> tube in (List<object>)root["tubes"])
            {
                var v = new List<char>();
                foreach (object u in tube) v.Add(((string)u)[0]);
                level.Vessels.Add(v);
            }
            if (root.TryGetValue("sol", out var sol))
                foreach (object id in (List<object>)sol) level.Solution.Add(Convert.ToInt32(id));
            return level;
        }
    }
}
