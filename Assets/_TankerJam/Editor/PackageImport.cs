// Menu: Tanker Jam > Setup > Import Package From Temp Path.
// Imports the .unitypackage whose absolute path is written in Temp/TankerJamImport.txt, without the
// interactive import dialog (automation). Result is logged as "Tanker Jam: imported ...".
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TankerJam.EditorTools
{
    public static class PackageImport
    {
        const string RequestPath = "Temp/TankerJamImport.txt";

        [MenuItem("Tanker Jam/Setup/Import Package From Temp Path")]
        public static void ImportFromRequest()
        {
            if (!File.Exists(RequestPath))
            {
                Debug.LogError($"Tanker Jam: write the package path into {RequestPath} first.");
                return;
            }
            string path = File.ReadAllText(RequestPath).Trim();
            if (!File.Exists(path))
            {
                Debug.LogError($"Tanker Jam: package not found: {path}");
                return;
            }
            AssetDatabase.importPackageCompleted -= OnDone;
            AssetDatabase.importPackageCompleted += OnDone;
            AssetDatabase.importPackageFailed -= OnFailed;
            AssetDatabase.importPackageFailed += OnFailed;
            AssetDatabase.ImportPackage(path, false);
        }

        static void OnDone(string name)
        {
            Debug.Log($"Tanker Jam: imported package {name}");
            File.WriteAllText("Temp/TankerJamImportResult.txt", "OK " + name);
        }

        static void OnFailed(string name, string error)
        {
            Debug.LogError($"Tanker Jam: package import failed {name}: {error}");
            File.WriteAllText("Temp/TankerJamImportResult.txt", "FAILED " + name + ": " + error);
        }
    }
}
