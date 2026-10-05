// Menu: Tanker Jam > Levels > Score All Levels.
// Solves and scores every level in Data/Levels with the C# solver (the source of truth since F2) and
// writes a report to Temp/TankerJamScores.txt: solve status, search effort, random win rate (with the stored
// value for comparison), jam depth, tier and Hard flag. Read-only: level files are not modified.
using System.IO;
using System.Linq;
using System.Text;
using TankerJam.Core;
using UnityEditor;
using UnityEngine;

namespace TankerJam.EditorTools
{
    public static class LevelScoreReport
    {
        const string ReportPath = "Temp/TankerJamScores.txt";

        [MenuItem("Tanker Jam/Levels/Score All Levels")]
        public static void ScoreAll()
        {
            var sb = new StringBuilder();
            sb.AppendLine("level      ver tier hard trucks bays  solve        nodes      ms  rate(C#) rate(stored)  depth");
            int failed = 0;
            var paths = Directory.GetFiles(LevelImporter.LevelFolder, "*.json").OrderBy(p => p).ToList();
            for (int i = 0; i < paths.Count; i++)
            {
                string name = Path.GetFileNameWithoutExtension(paths[i]);
                EditorUtility.DisplayProgressBar("Scoring levels", name, (float)i / paths.Count);
                try
                {
                    var level = LevelJson.Parse(File.ReadAllText(paths[i]));
                    var score = LevelSolver.Score(level);
                    if (score.Solve.Status != SolveStatus.Solved) failed++;
                    sb.AppendLine($"{name,-10} {level.Version,3} {level.Tier,4} {(level.Hard ? "yes" : "no"),4} {level.Trucks.Count,6} {level.Slots,4}  " +
                                  $"{score.Solve.Status,-11} {score.Solve.NodesVisited,7} {score.Solve.Milliseconds,6:0}  " +
                                  $"{score.RandomWinRate,8:P0} {(level.RandomWinRate >= 0 ? level.RandomWinRate.ToString("P0") : "-"),12}  {score.JamDepth,5}");
                }
                catch (System.Exception e)
                {
                    failed++;
                    sb.AppendLine($"{name,-10} ERROR {e.Message}");
                }
            }
            EditorUtility.ClearProgressBar();
            string header = $"{System.DateTime.Now:HH:mm:ss} scored {paths.Count} levels, {failed} not solved";
            File.WriteAllText(ReportPath, header + "\n" + sb);
            Debug.Log($"Tanker Jam: {header}. See {ReportPath}");
        }
    }
}
