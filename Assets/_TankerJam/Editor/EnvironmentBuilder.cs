// Menu: Tanker Jam > Setup > Build Desert Environment.
// Creates the starting desert layout (props from Art/Environment/Desert, made by the team's modeller) as
// Prefabs/Environment/Env_Desert.prefab, its palette-atlas material, and the Theme_Desert asset, and plugs the
// theme into GameConfig. This bootstraps the look; afterwards edit Env_Desert.prefab directly in the Scene view.
// Re-running overwrites the prefab.
//
// Placement rules: decoration stays outside the play area - beside the ring road (|x| > 5), clear of the exit
// road (z around -7.1) and behind the vessels (z < -13.5). Positions below are in SCREEN terms (sx > 0 = screen
// right) and converted with BoardLayout.XSign.
using System.Collections.Generic;
using System.IO;
using TankerJam.Core;
using TankerJam.Game;
using UnityEditor;
using UnityEngine;

namespace TankerJam.EditorTools
{
    public static class EnvironmentBuilder
    {
        const string ModelPath = "Assets/_TankerJam/Art/Environment/Desert/FBX/desert props.fbx";
        const string AtlasPath = "Assets/_TankerJam/Art/Environment/Desert/FBX/texture.png";
        const string MaterialPath = "Assets/_TankerJam/Art/Environment/Desert/Desert_Atlas.mat";
        const string PrefabDir = "Assets/_TankerJam/Prefabs/Environment";
        const string ThemePath = "Assets/_TankerJam/Config/Theme_Desert.asset";
        const string ConfigPath = "Assets/_TankerJam/Config/GameConfig.asset";

        enum Seat { Ground, Dune }

        readonly struct Prop
        {
            public readonly string Mesh;
            public readonly float Sx, Z, Yaw, Scale;
            public readonly Seat Seat;
            public Prop(string mesh, float sx, float z, float yaw, float scale, Seat seat = Seat.Ground)
            {
                Mesh = mesh; Sx = sx; Z = z; Yaw = yaw; Scale = scale; Seat = seat;
            }
        }

        static readonly Prop[] Desert =
        {
            // Backdrop behind the vessels (top of the screen). The center stays open sand: the level label sits there.
            new Prop("Desert", -7.5f, -21.5f, 0f, 0.5f, Seat.Dune),
            new Prop("Desert-1", 7.5f, -22f, 180f, 0.5f, Seat.Dune),
            new Prop("tower-3", -6.8f, -17.2f, 20f, 0.26f),
            new Prop("tower-5", 6.9f, -17f, -15f, 0.26f),
            new Prop("watch tower", -5.3f, -14.2f, 0f, 0.3f),
            new Prop("fan towr", 5.4f, -14.4f, 0f, 0.32f),

            // Screen-left strip beside the ring road.
            new Prop("plam tree", -6f, -11f, 30f, 0.32f),
            new Prop("Plant-2", -5.7f, -9.2f, 0f, 0.3f),
            new Prop("Umbrela", -6.6f, -5f, 0f, 0.32f),
            new Prop("plam tree", -5.8f, -2.5f, 120f, 0.32f),
            new Prop("Stone", -5.6f, 0.6f, 40f, 0.2f),
            new Prop("plam tree", -5.9f, 3.3f, 200f, 0.3f),
            new Prop("plant", -5.5f, 5.4f, 0f, 0.8f),
            new Prop("Stone-2", -5.6f, 7.6f, 10f, 0.22f),

            // Screen-right strip.
            new Prop("plam tree", 6.1f, -10.6f, 250f, 0.33f),
            new Prop("Stone-2", 5.8f, -8.8f, 60f, 0.2f),
            new Prop("plam tree", 5.9f, -4.6f, 80f, 0.3f),
            new Prop("Plant-2", 5.6f, -1.6f, 90f, 0.3f),
            new Prop("Umbrela", 5.9f, 1.4f, 30f, 0.32f),
            new Prop("plam tree", 5.8f, 4.4f, 160f, 0.32f),
            new Prop("Stone", 5.6f, 7f, 0f, 0.2f),
        };

        [MenuItem("Tanker Jam/Setup/Build Desert Environment")]
        public static void Build()
        {
            Directory.CreateDirectory(PrefabDir);
            var meshes = new Dictionary<string, Mesh>();
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(ModelPath))
                if (o is Mesh m) meshes[m.name] = m;
            if (meshes.Count == 0)
            {
                Debug.LogError("Tanker Jam: no meshes found in " + ModelPath);
                return;
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("TankerJam/ToyLitTextured"));
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/_TankerJam/Art/Shaders/ToyLitTextured.shader");
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath));
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);

            var root = new GameObject("Env_Desert");
            int tris = 0;
            foreach (var p in Desert)
            {
                if (!meshes.TryGetValue(p.Mesh, out var mesh))
                {
                    Debug.LogWarning($"Tanker Jam: mesh '{p.Mesh}' not in the desert model; skipped.");
                    continue;
                }
                // Meshes are Z-up with the pivot at their center (FBX nodes use a -90 X rotation).
                float height = mesh.bounds.size.z * p.Scale;
                float y = p.Seat == Seat.Dune ? -height * 0.25f : height / 2f;
                var go = new GameObject(p.Mesh);
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = new Vector3(BoardLayout.XSign * p.Sx, y, p.Z);
                go.transform.localRotation = Quaternion.Euler(0f, p.Yaw, 0f) * Quaternion.Euler(-90f, 0f, 0f);
                go.transform.localScale = Vector3.one * p.Scale;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = material;
                go.isStatic = true;
                tris += mesh.triangles.Length / 3;
            }

            string prefabPath = $"{PrefabDir}/Env_Desert.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);

            var theme = AssetDatabase.LoadAssetAtPath<EnvironmentTheme>(ThemePath);
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<EnvironmentTheme>();
                AssetDatabase.CreateAsset(theme, ThemePath);
            }
            theme.Layout = prefab;
            theme.Ground = new Color(0.804f, 0.616f, 0.439f);   // modeller's "desert ground 2" color
            theme.LotLight = new Color(0.96f, 0.90f, 0.79f);
            theme.LotDark = new Color(0.93f, 0.86f, 0.73f);
            theme.CastShadows = false;
            EditorUtility.SetDirty(theme);

            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            config.Environment = theme;
            config.ToyLitTextured = material.shader;
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            Debug.Log($"Tanker Jam: desert environment built ({Desert.Length} props, {tris} triangles) -> {prefabPath}");
        }
    }
}
