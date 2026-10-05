// Menu: Tanker Jam > Diagnostics > Report Environment Models.
// Lists every mesh in Art/Environment (name, triangles, size, UVs, vertex colors) to Temp/TankerJamModels.txt,
// so props can be laid out at the right scale and checked against the triangle budget.
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace TankerJam.EditorTools
{
    public static class ModelReport
    {
        const string Folder = "Assets/_TankerJam/Art/Environment";
        const string ReportPath = "Temp/TankerJamModels.txt";

        [MenuItem("Tanker Jam/Diagnostics/Report Environment Models")]
        public static void Report()
        {
            var sb = new StringBuilder();
            int totalTris = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { Folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                sb.AppendLine($"MODEL {path}");
                foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (!(obj is Mesh mesh)) continue;
                    int tris = mesh.triangles.Length / 3;
                    totalTris += tris;
                    var size = mesh.bounds.size;
                    sb.AppendLine($"  mesh '{mesh.name}': tris {tris}, size {size.x:0.00} x {size.y:0.00} x {size.z:0.00}, " +
                                  $"center {mesh.bounds.center}, uv {(mesh.uv.Length > 0 ? "yes" : "no")}, colors {(mesh.colors32.Length > 0 ? "yes" : "no")}, submeshes {mesh.subMeshCount}");
                }
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (root != null)
                    foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
                    {
                        var t = mf.transform;
                        sb.AppendLine($"    node '{t.name}' mesh '{mf.sharedMesh?.name}' pos {t.localPosition} rot {t.localEulerAngles} scale {t.lossyScale}");
                    }
            }
            sb.Insert(0, $"total triangles {totalTris}\n");
            File.WriteAllText(ReportPath, sb.ToString());
            Debug.Log("Tanker Jam: model report written to " + ReportPath);
        }
    }
}
