// Menu: Tanker Jam > Setup > Build Greybox Assets.
// Bakes greybox truck prefabs (proportions from the web prototype) as a few combined meshes each, the
// decal atlas (roof arrow + capacity badges), the "Trucks" physics layer, and the config assets.
// Safe to re-run: meshes, textures and prefabs are overwritten; existing config values are kept.
using System.Collections.Generic;
using System.IO;
using TankerJam.Game;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace TankerJam.EditorTools
{
    public static class GreyboxBaker
    {
        const string Root = "Assets/_TankerJam";
        const string PrefabDir = Root + "/Prefabs/Trucks";
        const string MeshDir = Root + "/Art/Meshes";
        const string TexDir = Root + "/Art/Textures";
        const string MatDir = Root + "/Art/Materials";
        const string ShaderDir = Root + "/Art/Shaders";
        const string ConfigDir = Root + "/Config";
        const string AtlasPath = TexDir + "/DecalAtlas.png";
        public const string TruckLayer = "Trucks";


        [MenuItem("Tanker Jam/Setup/Build Greybox Assets")]
        public static void BuildAll()
        {
            foreach (var d in new[] { PrefabDir, MeshDir, TexDir, MatDir, ConfigDir }) Directory.CreateDirectory(d);
            int layer = EnsureLayer(TruckLayer);

            var config = EnsureConfig();
            var atlas = BakeDecalAtlas();
            config.DecalAtlas = atlas;

            var palette = config.Palette;
            var previewBody = SaveMaterial("Preview_Body", config.ToyLit, palette.OilColorOf('P'));
            var previewShell = SaveMaterial("Preview_Shell", config.ToyTransparent, new Color(1f, 0.6f, 0.8f, 0.6f));
            var previewLiquid = SaveMaterial("Preview_Liquid", config.LiquidFill, palette.OilColorOf('P'));
            var decalMat = SaveMaterial("Decals", config.Decal, Color.white);
            decalMat.SetTexture("_BaseMap", atlas);
            var mats = new PreviewMaterials { Body = previewBody, Shell = previewShell, Liquid = previewLiquid, Decals = decalMat };

            for (int len = 2; len <= 4; len++)
                config.TruckPrefabs[len - 2] = BuildTruckPrefab(len, palette, mats, layer);
            AssetDatabase.DeleteAsset($"{MeshDir}/Truck_Axle.asset"); // wheels are baked into the body now

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Tanker Jam: greybox assets built.");
        }

        struct PreviewMaterials { public Material Body, Shell, Liquid, Decals; }

        // ---------------- trucks ----------------

        static TruckRig BuildTruckPrefab(int len, Palette pal, PreviewMaterials mats, int layer)
        {
            float lw = len - 0.12f;
            float z0 = -lw / 2f + 0.06f, z1 = lw / 2f - 0.78f, tl = z1 - z0, tc = (z0 + z1) / 2f;
            const float r = 0.32f, ty = 0.63f;
            var alongZ = Quaternion.Euler(90f, 0f, 0f);

            // Body: one vertex-colored mesh.
            var k = new MeshKit();
            k.Box(new Vector3(0, 0.24f, 0), new Vector3(0.62f, 0.12f, lw), MeshKit.Fixed(pal.Chassis));
            k.Box(new Vector3(0, 0.61f, lw / 2f - 0.37f), new Vector3(0.76f, 0.62f, 0.7f), MeshKit.Tint);
            k.Box(new Vector3(0, 0.74f, lw / 2f - 0.01f), new Vector3(0.64f, 0.26f, 0.05f), MeshKit.Fixed(pal.Window));
            k.Box(new Vector3(0, 0.76f, lw / 2f - 0.42f), new Vector3(0.78f, 0.22f, 0.42f), MeshKit.Fixed(pal.Window));
            k.Box(new Vector3(0, 0.94f, lw / 2f - 0.37f), new Vector3(0.78f, 0.08f, 0.72f), MeshKit.TintLightened(0.35f));
            foreach (float z in new[] { z0 + 0.03f, z1 - 0.03f })
                k.Cylinder(Matrix4x4.TRS(new Vector3(0, ty, z), alongZ, Vector3.one), r + 0.02f, r + 0.02f, 0.07f, 24, MeshKit.Tint);
            k.Box(new Vector3(0, ty + r + 0.03f, tc), new Vector3(0.26f, 0.08f, 0.26f), MeshKit.Fixed(pal.Metal));
            // Saddle under the tank so it doesn't float over the chassis.
            k.Box(new Vector3(0, 0.36f, tc), new Vector3(0.5f, 0.14f, tl * 0.8f), MeshKit.Fixed(pal.Chassis));
            // Wheels are part of the body mesh: one draw call (and one shadow draw) per truck instead of 3-5.
            // They don't spin; at this camera distance the spin was imperceptible. Final-art prefabs may still
            // provide separate TruckRig.Axles, which TruckView spins.
            var axleZ = new List<float> { lw / 2f - 0.38f, -lw / 2f + 0.3f };
            if (len >= 3) axleZ.Add(-lw / 2f + 0.72f);
            if (len >= 4) axleZ.Add(-lw / 2f + 1.14f);
            foreach (float z in axleZ) AddAxle(k, new Vector3(0, 0.17f, z), pal);
            var bodyMesh = SaveMesh(k.ToMesh($"Truck{len}_Body"), $"Truck{len}_Body");

            k.Clear();
            k.Cylinder(Matrix4x4.TRS(Vector3.zero, alongZ, Vector3.one), r, r, tl, 24, MeshKit.Tint, false, false);
            var shellMesh = SaveMesh(k.ToMesh($"Truck{len}_Shell"), $"Truck{len}_Shell");

            k.Clear();
            k.Cylinder(Matrix4x4.TRS(Vector3.zero, alongZ, Vector3.one), r - 0.025f, r - 0.025f, tl - 0.08f, 24, MeshKit.Tint);
            var liquidMesh = SaveMesh(k.ToMesh($"Truck{len}_Liquid"), $"Truck{len}_Liquid");

            k.Clear();
            float arrowLen = Mathf.Min(1.5f, tl * 0.85f);
            k.Quad(Matrix4x4.Translate(new Vector3(0, ty + r + 0.085f, tc)), 0.48f, arrowLen, MeshKit.Tint, DecalAtlasLayout.Arrow);
            k.Quad(Matrix4x4.Translate(new Vector3(0, 0.985f, lw / 2f - 0.37f)), 0.36f, 0.36f, MeshKit.Tint, DecalAtlasLayout.Badge(TankerJam.Core.TruckDef.CapacityFor(len)));
            var decalMesh = SaveMesh(k.ToMesh($"Truck{len}_Decals"), $"Truck{len}_Decals");

            // Hierarchy.
            var root = new GameObject($"Truck_{len}");
            root.layer = layer;
            var rig = root.AddComponent<TruckRig>();
            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 0.5f, 0);
            col.size = new Vector3(0.8f, 1f, lw);

            var body = new GameObject("Body");
            body.transform.SetParent(root.transform, false);
            var bodyR = AddRenderer(body, bodyMesh, mats.Body, ShadowCastingMode.On);

            var shell = Child(body, "Shell", new Vector3(0, ty, tc));
            var shellR = AddRenderer(shell, shellMesh, mats.Shell, ShadowCastingMode.Off);
            var liquid = Child(body, "Liquid", new Vector3(0, ty, tc));
            var liquidR = AddRenderer(liquid, liquidMesh, mats.Liquid, ShadowCastingMode.Off);
            var decals = Child(body, "Decals", Vector3.zero);
            var decalR = AddRenderer(decals, decalMesh, mats.Decals, ShadowCastingMode.Off);

            rig.Length = len;
            rig.Body = body.transform;
            rig.Axles = new Transform[0];
            rig.BodyRenderer = bodyR;
            rig.ShellRenderer = shellR;
            rig.LiquidRenderer = liquidR;
            rig.DecalRenderer = decalR;
            rig.TapCollider = col;
            rig.TankCenterZ = tc;
            rig.TankCenterY = ty;
            rig.TankRadius = r;
            rig.TankLength = tl;
            rig.HoseTargetY = ty + r + 0.2f;
            rig.WheelRadius = 0.17f;

            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;

            string path = $"{PrefabDir}/Truck_{len}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<TruckRig>();
        }

        static void AddAxle(MeshKit k, Vector3 at, Palette pal)
        {
            var acrossX = Quaternion.Euler(0f, 0f, 90f);
            foreach (int s in new[] { -1, 1 })
            {
                var c = at + new Vector3(s * 0.36f, 0, 0);
                k.Cylinder(Matrix4x4.TRS(c, acrossX, Vector3.one), 0.17f, 0.17f, 0.14f, 16, MeshKit.Fixed(pal.Tire));
                k.Cylinder(Matrix4x4.TRS(c, acrossX, Vector3.one), 0.08f, 0.08f, 0.15f, 10, MeshKit.Fixed(pal.Hub));
            }
            k.Cylinder(Matrix4x4.TRS(at, acrossX, Vector3.one), 0.035f, 0.035f, 0.6f, 8, MeshKit.Fixed(pal.Chassis), false, false);
        }

        // ---------------- decal atlas ----------------

        static Texture2D BakeDecalAtlas()
        {
            const int w = DecalAtlasLayout.Width, h = DecalAtlasLayout.Height, ss = 3; // 3x3 supersampling
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color32[w * h];
            var ink = new Color(0.118f, 0.141f, 0.2f, 1f); // #1E2433
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    Color acc = Color.clear;
                    for (int sy = 0; sy < ss; sy++)
                        for (int sx = 0; sx < ss; sx++)
                        {
                            float fx = x + (sx + 0.5f) / ss, fy = y + (sy + 0.5f) / ss;
                            acc += SampleAtlas(fx / w, fy / h, ink);
                        }
                    acc /= ss * ss;
                    if (acc.a > 0f) { acc.r /= acc.a; acc.g /= acc.a; acc.b /= acc.a; }
                    px[y * w + x] = acc;
                }
            tex.SetPixels32(px);
            File.WriteAllBytes(AtlasPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(AtlasPath);
            var imp = (TextureImporter)AssetImporter.GetAtPath(AtlasPath);
            imp.textureType = TextureImporterType.Default;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = true;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.sRGBTexture = true;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
        }

        /// <summary>Premultiplied sample of the atlas at normalized (u, v).</summary>
        static Color SampleAtlas(float u, float v, Color ink)
        {
            if (u < 0.25f) return Arrow(u / 0.25f, v) ? Color.white : Color.clear;
            if (v < 0.5f) return VipText((u - 0.25f) / 0.75f * 384f, v / 0.5f * 128f) ? Color.white : Color.clear;
            int slot = (int)((u - 0.25f) / 0.25f);
            if (slot > 2) return Color.clear;
            float cu = (u - 0.25f - slot * 0.25f) / 0.25f, cv = (v - 0.5f) / 0.5f;
            int digit = slot == 0 ? 2 : slot == 1 ? 4 : 6;
            float dx = cu - 0.5f, dy = cv - 0.5f;
            if (dx * dx + dy * dy > 0.44f * 0.44f) return Color.clear;
            return Digit(digit, cu, cv) ? ink : Color.white;
        }

        /// <summary>"VIP" in thick rounded strokes, in pixel space of a 384 x 128 cell (y up).</summary>
        static bool VipText(float x, float y)
        {
            const float r = 11f, top = 108f, bottom = 20f, mid = 66f;
            // V
            if (Seg(x, y, 40, top, 78, bottom) < r || Seg(x, y, 116, top, 78, bottom) < r) return true;
            // I
            if (Seg(x, y, 176, top, 176, bottom) < r) return true;
            // P: stem + bowl
            if (Seg(x, y, 232, top, 232, bottom) < r) return true;
            if (Seg(x, y, 232, top, 300, top) < r || Seg(x, y, 232, mid, 300, mid) < r) return true;
            float cx = 300f, cy = (top + mid) / 2f, rad = (top - mid) / 2f;
            float d = Mathf.Abs(Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - rad);
            return x >= cx && d < r;
        }

        /// <summary>Distance from (px, py) to the segment (ax, ay)-(bx, by).</summary>
        static float Seg(float px, float py, float ax, float ay, float bx, float by)
        {
            float vx = bx - ax, vy = by - ay;
            float t = Mathf.Clamp01(((px - ax) * vx + (py - ay) * vy) / (vx * vx + vy * vy));
            float dx = px - (ax + vx * t), dy = py - (ay + vy * t);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>Arrow pointing toward +v (truck forward). u across, v along.</summary>
        static bool Arrow(float u, float v)
        {
            if (v > 0.06f && v < 0.6f && u > 0.3f && u < 0.7f) return true;
            if (v >= 0.55f && v < 0.96f)
            {
                float half = 0.44f * (0.96f - v) / 0.41f;
                return Mathf.Abs(u - 0.5f) < half;
            }
            return false;
        }

        /// <summary>Seven-segment digit inside a unit cell.</summary>
        static bool Digit(int d, float u, float v)
        {
            const float l = 0.33f, r = 0.67f, b = 0.2f, t = 0.8f, m = 0.5f, th = 0.085f;
            bool H(float y) => Mathf.Abs(v - y) < th && u > l - th && u < r + th;
            bool VL(float y0, float y1) => Mathf.Abs(u - l) < th && v > y0 - th && v < y1 + th;
            bool VR(float y0, float y1) => Mathf.Abs(u - r) < th && v > y0 - th && v < y1 + th;
            switch (d)
            {
                case 2: return H(t) || VR(m, t) || H(m) || VL(b, m) || H(b);
                case 4: return VL(m, t) || H(m) || VR(b, t);
                case 6: return H(t) || VL(b, t) || H(m) || VR(b, m) || H(b);
                default: return false;
            }
        }

        // ---------------- config ----------------

        static GameConfig EnsureConfig()
        {
            var palette = LoadOrCreate<Palette>($"{ConfigDir}/Palette.asset");
            var tuning = LoadOrCreate<GameTuning>($"{ConfigDir}/GameTuning.asset");
            var layout = LoadOrCreate<LayoutConfig>($"{ConfigDir}/LayoutConfig.asset");
            var config = LoadOrCreate<GameConfig>($"{ConfigDir}/GameConfig.asset");
            config.Palette = palette;
            config.Tuning = tuning;
            config.Layout = layout;
            config.ToyLit = AssetDatabase.LoadAssetAtPath<Shader>($"{ShaderDir}/ToyLit.shader");
            config.ToyTransparent = AssetDatabase.LoadAssetAtPath<Shader>($"{ShaderDir}/ToyTransparent.shader");
            config.LiquidFill = AssetDatabase.LoadAssetAtPath<Shader>($"{ShaderDir}/LiquidFill.shader");
            config.Decal = AssetDatabase.LoadAssetAtPath<Shader>($"{ShaderDir}/Decal.shader");
            config.VesselLiquid = AssetDatabase.LoadAssetAtPath<Shader>($"{ShaderDir}/VesselLiquid.shader");
            config.TruckLayer = TruckLayer;
            if (config.TruckPrefabs == null || config.TruckPrefabs.Length != 3) config.TruckPrefabs = new TruckRig[3];
            EditorUtility.SetDirty(config);
            return config;
        }

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        // ---------------- helpers ----------------

        static int EnsureLayer(string name)
        {
            int existing = LayerMask.NameToLayer(name);
            if (existing >= 0) return existing;
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            for (int i = 8; i < layers.arraySize; i++)
            {
                var p = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(p.stringValue))
                {
                    p.stringValue = name;
                    tagManager.ApplyModifiedPropertiesWithoutUndo();
                    return i;
                }
            }
            throw new System.InvalidOperationException("No free user layer for " + name);
        }

        static GameObject Child(GameObject parent, string name, Vector3 localPos)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent.transform, false);
            g.transform.localPosition = localPos;
            return g;
        }

        static MeshRenderer AddRenderer(GameObject g, Mesh mesh, Material mat, ShadowCastingMode shadows)
        {
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = g.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = shadows;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return r;
        }

        static Mesh SaveMesh(Mesh mesh, string name)
        {
            string path = $"{MeshDir}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(mesh, existing);
                existing.name = name;
                Object.DestroyImmediate(mesh);
                return existing;
            }
            mesh.name = name;
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        static Material SaveMaterial(string name, Shader shader, Color color)
        {
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            m.SetColor("_BaseColor", color);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
