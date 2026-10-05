// Keeps Data/LevelCatalog.asset in sync with Data/Levels/*.json.
// Runs automatically when level files change, or via Tanker Jam > Levels > Rebuild Catalog.
// Every level is verified (structure, limits, stored solution through GameSession); failures are excluded
// from the catalog and reported in the console and in Temp/TankerJamLevels.txt.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TankerJam.Core;
using TankerJam.Game;
using UnityEditor;
using UnityEngine;

namespace TankerJam.EditorTools
{
    public sealed class LevelImporter : AssetPostprocessor
    {
        public const string LevelFolder = "Assets/_TankerJam/Data/Levels";
        public const string CatalogPath = "Assets/_TankerJam/Data/LevelCatalog.asset";
        const string ReportPath = "Temp/TankerJamLevels.txt";

        static bool scheduled;

        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            bool touched = imported.Concat(deleted).Concat(moved).Concat(movedFrom)
                                   .Any(p => p.StartsWith(LevelFolder) && p.EndsWith(".json"));
            if (!touched || scheduled) return;
            scheduled = true;
            EditorApplication.delayCall += () =>
            {
                scheduled = false;
                RebuildCatalog();
            };
        }

        [MenuItem("Tanker Jam/Levels/Rebuild Catalog")]
        public static void RebuildCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<LevelCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            var report = new StringBuilder();
            var entries = new List<LevelCatalog.Entry>();
            int failed = 0;
            var paths = Directory.GetFiles(LevelFolder, "*.json").Select(p => p.Replace('\\', '/')).OrderBy(p => p).ToList();
            foreach (string path in paths)
            {
                var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                var errors = new List<string>();
                LevelDef level = null;
                try
                {
                    level = LevelJson.Parse(asset.text);
                    LevelVerifier.Verify(level, errors);
                }
                catch (System.Exception e)
                {
                    errors.Add("Parse error: " + e.Message);
                }

                string name = Path.GetFileNameWithoutExtension(path);
                if (errors.Count > 0)
                {
                    failed++;
                    report.AppendLine($"FAIL {name}: {string.Join(" | ", errors)}");
                    Debug.LogError($"Tanker Jam: level {name} rejected: {string.Join(" | ", errors)}", asset);
                    continue;
                }
                entries.Add(new LevelCatalog.Entry
                {
                    Json = asset,
                    Index = level.Index > 0 ? level.Index : entries.Count + 1,
                    Tier = level.Tier,
                    Hard = level.Hard,
                    Introduces = level.Introduces,
                    RandomWinRate = Mathf.Max(0f, level.RandomWinRate),
                    Trucks = level.Trucks.Count,
                    Par = level.Solution.Count,
                });
                report.AppendLine($"ok   {name}: tier {level.Tier}{(level.Hard ? " HARD" : "")} trucks {level.Trucks.Count} " +
                                  $"bays {level.Slots} vessels {level.Vessels.Count} rate {level.RandomWinRate:P0}" +
                                  (level.Introduces != null ? $" introduces {level.Introduces}" : ""));
            }

            entries.Sort((a, b) => a.Index.CompareTo(b.Index));
            catalog.Levels = entries;
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            string header = $"{System.DateTime.Now:HH:mm:ss} catalog: {entries.Count} levels, {failed} rejected";
            File.WriteAllText(ReportPath, header + "\n" + report);
            Debug.Log($"Tanker Jam: {header}");
        }
    }
}
