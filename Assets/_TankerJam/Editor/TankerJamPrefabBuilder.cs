// Builds greybox prefabs that match the web prototype's proportions (1 unit = 1 grid cell).
// Menu: Tanker Jam > Build Greybox Prefabs. Re-run any time; it overwrites the prefabs.
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace TankerJam.EditorTools
{
    public static class TankerJamPrefabBuilder
    {
        const string PrefabDir = "Assets/_TankerJam/Prefabs/Greybox";
        const string MatDir = "Assets/_TankerJam/Art/Materials/Greybox";
        const string MeshDir = "Assets/_TankerJam/Art/Meshes";

        // Prototype palette
        static readonly Color Pink = Hex("#FF4FA8"), Yellow = Hex("#FFC21F"), Cyan = Hex("#1EC8F2"), Purple = Hex("#9A5BFF");
        static readonly Color Chassis = Hex("#3A4152"), Tire = Hex("#262B36"), Hub = Hex("#E6ECF2"), Window = Hex("#BFE9FF");
        static readonly Color Metal = Hex("#C7D3DF"), MetalDark = Hex("#7D8CA0"), PumpBlue = Hex("#2EA8FF"), White = Color.white, Cone = Hex("#FF7A1A");

        [MenuItem("Tanker Jam/Build Greybox Prefabs")]
        public static void BuildAll()
        {
            Directory.CreateDirectory(PrefabDir);
            Directory.CreateDirectory(MatDir);
            Directory.CreateDirectory(MeshDir);

            foreach (int len in new[] { 2, 3, 4 }) Save(BuildTruck(len), $"Truck_{len}");
            Save(BuildVessel(8), "Vessel");
            Save(BuildPump(), "Pump");
            Save(BuildCone(), "Cone");
            foreach (var (key, col) in new[] { ("P", Pink), ("Y", Yellow), ("C", Cyan), ("V", Purple) })
            {
                Mat($"Oil_{key}", col, 0.75f);
                Mat($"TruckBody_{key}", col, 0.55f);
                Mat($"TankShell_{key}", Color.Lerp(col, White, 0.35f), 0.9f, transparent: true, alpha: 0.6f);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Tanker Jam greybox prefabs built in " + PrefabDir);
        }

        // ---------- truck ----------
        static GameObject BuildTruck(int len)
        {
            float lw = len - 0.12f;
            var root = new GameObject($"Truck_{len}");
            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 0.5f, 0); col.size = new Vector3(lw, 1f, 0.8f);

            var body = Child(root, "Body");
            Part(PrimitiveType.Cube, body, "Chassis", new Vector3(0, 0.24f, 0), new Vector3(lw, 0.12f, 0.62f), Mat("Chassis", Chassis));
            Part(PrimitiveType.Cube, body, "Cab", new Vector3(lw / 2 - 0.37f, 0.61f, 0), new Vector3(0.7f, 0.62f, 0.76f), Mat("TruckBody_P", Pink, 0.55f));
            Part(PrimitiveType.Cube, body, "Windshield", new Vector3(lw / 2 - 0.01f, 0.74f, 0), new Vector3(0.05f, 0.26f, 0.64f), Mat("Window", Window, 0.9f));
            Part(PrimitiveType.Cube, body, "SideWindows", new Vector3(lw / 2 - 0.42f, 0.76f, 0), new Vector3(0.42f, 0.22f, 0.78f), Mat("Window", Window, 0.9f));

            // Tank along local X: cylinder primitive is 1 wide and 2 tall, so scale Y by length / 2.
            float x0 = -lw / 2 + 0.06f, x1 = lw / 2 - 0.78f, tl = x1 - x0, tc = (x0 + x1) / 2, r = 0.32f, ty = 0.63f;
            var tankRot = Quaternion.Euler(0, 0, 90);
            var liquid = Part(PrimitiveType.Cylinder, body, "Liquid", new Vector3(tc, ty, 0), new Vector3(2 * (r - 0.025f), (tl - 0.08f) / 2, 2 * (r - 0.025f)), Mat("Oil_P", Pink, 0.75f), tankRot);
            liquid.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            var shell = Part(PrimitiveType.Cylinder, body, "TankShell", new Vector3(tc, ty, 0), new Vector3(2 * r, tl / 2, 2 * r), Mat("TankShell_P", Color.Lerp(Pink, White, 0.35f), 0.9f, true, 0.6f), tankRot);
            shell.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            foreach (float x in new[] { x0 + 0.03f, x1 - 0.03f })
                Part(PrimitiveType.Cylinder, body, "EndCap", new Vector3(x, ty, 0), new Vector3(2 * r + 0.04f, 0.035f, 2 * r + 0.04f), Mat("TruckBody_P", Pink, 0.55f), tankRot);
            Part(PrimitiveType.Cube, body, "Hatch", new Vector3(tc, ty + r + 0.03f, 0), new Vector3(0.26f, 0.08f, 0.26f), Mat("Metal", Metal, 0.7f));
            var hatchPoint = Child(body, "HoseTarget"); hatchPoint.transform.localPosition = new Vector3(tc, ty + r + 0.2f, 0);

            var wheels = Child(root, "Wheels");
            var xs = new System.Collections.Generic.List<float> { lw / 2 - 0.38f, -lw / 2 + 0.3f };
            if (len >= 3) xs.Add(-lw / 2 + 0.72f);
            if (len >= 4) xs.Add(-lw / 2 + 1.14f);
            foreach (float x in xs)
                foreach (int s in new[] { -1, 1 })
                {
                    var w = Child(wheels, "Wheel"); w.transform.localPosition = new Vector3(x, 0.17f, s * 0.36f);
                    Part(PrimitiveType.Cylinder, w, "Tire", Vector3.zero, new Vector3(0.34f, 0.07f, 0.34f), Mat("Tire", Tire), Quaternion.Euler(90, 0, 0));
                    Part(PrimitiveType.Cylinder, w, "Hub", Vector3.zero, new Vector3(0.16f, 0.075f, 0.16f), Mat("Hub", Hub, 0.7f), Quaternion.Euler(90, 0, 0));
                }
            // Roof decals (white arrow, capacity badge) are added in phase 2 as quads with textures.
            var roof = Child(body, "RoofDecalAnchor"); roof.transform.localPosition = new Vector3(tc, ty + r + 0.08f, 0);
            return root;
        }

        // ---------- vessel ----------
        static GameObject BuildVessel(int maxLayers)
        {
            const float vr = 0.74f, unit = 0.42f, baseY = 1.0f;
            float h = maxLayers * unit + 0.45f;
            var root = new GameObject("Vessel");
            Part(PrimitiveType.Cylinder, root, "Stand", new Vector3(0, (baseY - 0.15f) / 2, 0), new Vector3(1.6f, (baseY - 0.15f) / 2, 1.6f), Mat("White", White, 0.5f));
            Part(PrimitiveType.Cylinder, root, "Band", new Vector3(0, baseY - 0.2f, 0), new Vector3(1.74f, 0.06f, 1.74f), Mat("PumpBlue", PumpBlue, 0.6f));
            var glass = Part(PrimitiveType.Cylinder, root, "Glass", new Vector3(0, baseY - 0.12f + h / 2, 0), new Vector3(2 * vr + 0.12f, h / 2, 2 * vr + 0.12f), Mat("Glass", White, 0.95f, true, 0.26f));
            glass.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            Part(PrimitiveType.Cylinder, root, "Lip", new Vector3(0, baseY - 0.12f + h + 0.05f, 0), new Vector3(2 * vr + 0.28f, 0.05f, 2 * vr + 0.28f), Mat("MetalDark", MetalDark, 0.6f));
            Part(PrimitiveType.Cube, root, "Tap", new Vector3(0, 0.6f, vr + 0.05f), new Vector3(0.3f, 0.26f, 0.3f), Mat("TapYellow", Yellow, 0.6f));
            var layers = Child(root, "Layers"); layers.transform.localPosition = new Vector3(0, baseY + 0.02f, 0);
            // VesselView spawns one cylinder per unit under "Layers": scale (2*vr, unit*amount/2, 2*vr), stacked upward.
            return root;
        }

        // ---------- pump ----------
        static GameObject BuildPump()
        {
            var root = new GameObject("Pump");
            Part(PrimitiveType.Cube, root, "Body", new Vector3(0, 0.6f, 0), new Vector3(1.4f, 1.1f, 1.0f), Mat("PumpBlue", PumpBlue, 0.6f));
            Part(PrimitiveType.Cube, root, "Lid", new Vector3(0, 1.2f, 0), new Vector3(1.5f, 0.12f, 1.1f), Mat("PumpBlueDark", Hex("#1C7FC4"), 0.6f));
            var wheel = Child(root, "Wheel"); wheel.transform.localPosition = new Vector3(0.74f, 0.75f, 0);
            Part(PrimitiveType.Cylinder, wheel, "Rim", Vector3.zero, new Vector3(0.64f, 0.03f, 0.64f), Mat("TapYellow", Yellow, 0.6f), Quaternion.Euler(0, 0, 90));
            Part(PrimitiveType.Cylinder, root, "Gauge", new Vector3(0, 0.75f, 0.52f), new Vector3(0.4f, 0.025f, 0.4f), Mat("White", White, 0.5f), Quaternion.Euler(90, 0, 0));
            return root;
        }

        // ---------- cone ----------
        static GameObject BuildCone()
        {
            var root = new GameObject("Cone");
            Part(PrimitiveType.Cube, root, "Base", new Vector3(0, 0.025f, 0), new Vector3(0.5f, 0.05f, 0.5f), Mat("ConeOrange", Cone, 0.5f));
            var cone = new GameObject("Body"); cone.transform.SetParent(root.transform, false); cone.transform.localPosition = new Vector3(0, 0.02f, 0);
            cone.AddComponent<MeshFilter>().sharedMesh = ConeMesh();
            cone.AddComponent<MeshRenderer>().sharedMaterial = Mat("ConeOrange", Cone, 0.5f);
            Part(PrimitiveType.Cylinder, root, "Band", new Vector3(0, 0.36f, 0), new Vector3(0.3f, 0.04f, 0.3f), Mat("White", White, 0.5f));
            root.AddComponent<BoxCollider>().size = new Vector3(0.5f, 0.6f, 0.5f);
            return root;
        }

        static Mesh ConeMesh()
        {
            string path = MeshDir + "/Cone.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing) return existing;
            const int seg = 20; const float r = 0.24f, h = 0.6f;
            var verts = new Vector3[seg + 2]; var tris = new int[seg * 6];
            verts[0] = new Vector3(0, h, 0); verts[seg + 1] = Vector3.zero;
            for (int i = 0; i < seg; i++) { float a = i * Mathf.PI * 2 / seg; verts[i + 1] = new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r); }
            for (int i = 0; i < seg; i++)
            {
                int a = i + 1, b = (i + 1) % seg + 1;
                tris[i * 6] = 0; tris[i * 6 + 1] = b; tris[i * 6 + 2] = a;
                tris[i * 6 + 3] = seg + 1; tris[i * 6 + 4] = a; tris[i * 6 + 5] = b;
            }
            var m = new Mesh { name = "Cone", vertices = verts, triangles = tris };
            m.RecalculateNormals(); m.RecalculateBounds();
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        // ---------- helpers ----------
        static GameObject Child(GameObject parent, string name)
        {
            var g = new GameObject(name); g.transform.SetParent(parent.transform, false); return g;
        }

        static GameObject Part(PrimitiveType type, GameObject parent, string name, Vector3 pos, Vector3 scale, Material mat, Quaternion? rot = null)
        {
            var g = GameObject.CreatePrimitive(type);
            g.name = name;
            Object.DestroyImmediate(g.GetComponent<Collider>());
            g.transform.SetParent(parent.transform, false);
            g.transform.localPosition = pos;
            g.transform.localRotation = rot ?? Quaternion.identity;
            g.transform.localScale = scale;
            g.GetComponent<Renderer>().sharedMaterial = mat;
            return g;
        }

        static Material Mat(string name, Color color, float smoothness = 0.4f, bool transparent = false, float alpha = 1f)
        {
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m) return m;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            m = new Material(shader) { name = name };
            color.a = alpha;
            m.SetColor("_BaseColor", color);
            m.color = color;
            m.SetFloat("_Smoothness", smoothness);
            if (transparent)
            {
                m.SetFloat("_Surface", 1);   // URP: Transparent
                m.SetFloat("_Blend", 0);     // Alpha
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)RenderQueue.Transparent;
            }
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static void Save(GameObject go, string name)
        {
            PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabDir}/{name}.prefab");
            Object.DestroyImmediate(go);
        }

        static Color Hex(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }
    }
}
#endif
