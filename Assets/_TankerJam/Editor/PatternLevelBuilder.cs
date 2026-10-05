// Menu: Tanker Jam > Levels > Generate Shape Levels (curve v2).
// Regenerates the shape levels (Data/Generation/curve_v2.json, levels firstLevel..lastLevel) into
// Data/Levels/level_NNN.json with the C# pattern generator. Deterministic: the same curve file always
// produces the same levels. Levels before firstLevel (the grid tutorial levels) are never touched.
// Report: Temp/TankerJamGenerate.txt. The catalog importer verifies everything afterwards.
using System.Diagnostics;
using System.IO;
using System.Text;
using TankerJam.Core;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace TankerJam.EditorTools
{
    public static class PatternLevelBuilder
    {
        public const string CurvePath = "Assets/_TankerJam/Data/Generation/curve_v2.json";
        const string ReportPath = "Temp/TankerJamGenerate.txt";

        public static PatternCurve LoadCurve() => PatternCurve.Parse(File.ReadAllText(CurvePath));

        /// <summary>No confirmation: the output is deterministic and the level files are under version control.</summary>
        [MenuItem("Tanker Jam/Levels/Generate Shape Levels (curve v2)")]
        static void GenerateMenu() => GenerateAll();

        /// <summary>Generates every curve level and writes the files. Returns the number of failures.</summary>
        public static int GenerateAll()
        {
            var curve = LoadCurve();
            var report = new StringBuilder();
            report.AppendLine("level tier hard pattern          trucks vessels  rate   band         depth attempts   ms");
            int failed = 0, count = curve.LastLevel - curve.FirstLevel + 1;
            var total = Stopwatch.StartNew();
            try
            {
                AssetDatabase.StartAssetEditing();
                for (int level = curve.FirstLevel; level <= curve.LastLevel; level++)
                {
                    EditorUtility.DisplayProgressBar("Generating shape levels", $"Level {level}", (float)(level - curve.FirstLevel) / count);
                    var watch = Stopwatch.StartNew();
                    var r = curve.Generate(level);
                    curve.Band(level, out float lo, out float hi, out _);
                    if (!r.Ok)
                    {
                        failed++;
                        report.AppendLine($"{level,5} FAILED: {r.Failure}");
                        continue;
                    }
                    var def = r.Draft.ToLevel();
                    File.WriteAllText($"{LevelImporter.LevelFolder}/level_{level:000}.json", LevelJson.ToJsonV2(def));
                    report.AppendLine($"{level,5} {def.Tier,4} {(def.Hard ? "yes" : "no"),4} {r.Pattern,-16} {def.Trucks.Count,6} {def.Vessels.Count,7} " +
                                      $"{r.Score.RandomWinRate,5:P0}  {lo:0.00}-{hi:0.00}  {r.JamDepth,5} {r.Attempts,8} {watch.ElapsedMilliseconds,5}");
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
            }
            AssetDatabase.Refresh();
            string header = $"{System.DateTime.Now:HH:mm:ss} generated {count - failed}/{count} shape levels in {total.ElapsedMilliseconds} ms";
            File.WriteAllText(ReportPath, header + "\n" + report);
            Debug.Log($"Tanker Jam: {header}. See {ReportPath}");
            return failed;
        }
    }
}
