// Hand-built free-form levels used by tests and the Dev menu before the visual editor exists (F3).
// The vessel order is shuffled from a seed until the solver finds a win, so the level is always playable
// and carries a solution for SolutionPlayer.
using System;
using System.Collections.Generic;

namespace TankerJam.Core
{
    public static class SampleLevels
    {
        /// <summary>
        /// Radial board: 8 spokes at 45-degree steps around a center cone, two trucks per spoke, all pointing
        /// outward, so each inner truck waits for the outer truck on its spoke. 16 trucks, 4 colors, 4 vessels.
        /// </summary>
        public static LevelDef Radial(int seed = 1)
        {
            char[] colors = { 'P', 'Y', 'C', 'G' };
            for (int attempt = 0; attempt < 64; attempt++)
            {
                var level = new LevelDef { Version = 2, Size = 11, Slots = 3, Board = BoardShape.Circle(5.5f), Pattern = "radial-8" };
                level.Obstacles.Add(new Obstacle(0f, 0f, Obstacle.ConeHalfSize));
                var units = new List<char>();
                int id = 0;
                for (int k = 0; k < 8; k++)
                {
                    float a = k * 45f;
                    Add(level, ref id, a, 4.1f, colors[k % 4], units);
                    Add(level, ref id, a, 2.1f, colors[(k + 1) % 4], units);
                }

                var rng = new Random(seed * 1000 + attempt);
                for (int i = units.Count - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    (units[i], units[j]) = (units[j], units[i]);
                }
                int per = units.Count / 4;
                for (int v = 0; v < 4; v++) level.Vessels.Add(units.GetRange(v * per, per));

                var score = LevelSolver.Score(level, 200);
                if (score.Solve.Status != SolveStatus.Solved) continue;
                level.Solution = score.Solve.Solution;
                level.RandomWinRate = score.RandomWinRate;
                return level;
            }
            throw new InvalidOperationException("No solvable vessel order found for the radial sample.");
        }

        static void Add(LevelDef level, ref int id, float angle, float radius, char color, List<char> units)
        {
            var at = Geometry2D.Heading(angle) * radius;
            level.Trucks.Add(new TruckDef
            {
                Id = id++, Len = 2, Color = color, HasPose = true,
                FreePose = new TruckPose(Snap(at.X), Snap(at.Z), angle), Facing = TruckDef.FacingOf(angle),
            });
            for (int u = 0; u < TruckDef.CapacityFor(2); u++) units.Add(color);
        }

        static float Snap(float v) => (float)Math.Round(v / 0.05) * 0.05f;
    }
}
