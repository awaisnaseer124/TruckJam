// Builds the static part of a level from its BoardLayout: ground, lot checker, roads, bay pad, pipes,
// vessel stands and taps, pump housing and cones. Everything opaque goes into ONE vertex-colored mesh
// (one draw call); all vessel glass goes into one transparent mesh. Rebuilt per level, reusing meshes.
using System.Collections.Generic;
using TankerJam.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace TankerJam.Game
{
    public sealed class SceneryBuilder
    {
        readonly MeshKit kit = new MeshKit();
        readonly MeshKit glassKit = new MeshKit();
        readonly List<Vector2> profile = new List<Vector2>(16);
        Mesh staticMesh, glassMesh;
        MeshRenderer staticRenderer, glassRenderer;

        const float RoadY = 0.01f, LotY = 0.02f, PadY = 0.012f;

        public void Build(Transform parent, LevelDef level, BoardLayout L, Palette pal, MaterialLibrary mats, EnvironmentTheme theme = null)
        {
            if (staticRenderer == null)
            {
                staticRenderer = MakeRenderer(parent, "Scenery", mats.VertexColored, ShadowCastingMode.On);
                glassRenderer = MakeRenderer(parent, "VesselGlass", mats.Glass, ShadowCastingMode.Off);
            }
            kit.Clear();
            glassKit.Clear();
            var p = L.P;

            // Ground.
            kit.Quad(new Vector3(0, 0, L.LotCenter.Z - 6f), 90f, 90f, MeshKit.Fixed(theme != null ? theme.Ground : pal.Ground));

            // Ring road and exit road.
            float w = p.RoadWidth;
            var road = MeshKit.Fixed(pal.Road);
            float ringW = 2f * L.RingX + w, ringH = L.RingBottom - L.RingTop;
            kit.Quad(new Vector3(0, RoadY, L.RingTop), ringW, w, road);
            kit.Quad(new Vector3(0, RoadY, L.RingBottom), ringW, w, road);
            kit.Quad(new Vector3(-L.RingX, RoadY + 0.001f, (L.RingTop + L.RingBottom) / 2f), w, ringH, road);
            kit.Quad(new Vector3(L.RingX, RoadY + 0.001f, (L.RingTop + L.RingBottom) / 2f), w, ringH, road);
            kit.Quad(new Vector3(0, RoadY, p.ExitZ), 60f, w + 0.05f, road);
            var white = MeshKit.Fixed(Color.white);
            for (float x = -28f; x < 28f; x += 1.2f)
                kit.Box(new Vector3(x, 0.02f, p.ExitZ), new Vector3(0.6f, 0.02f, 0.07f), white);

            var light = MeshKit.Fixed(theme != null ? theme.LotLight : pal.LotLight);
            var dark = MeshKit.Fixed(theme != null ? theme.LotDark : pal.LotDark);
            if (level.IsGrid) BuildGridLot(L, light, dark, white);
            else BuildShapedLot(L, light, dark, white);

            BuildBayPad(L, pal);

            // Cones: grid cones and free-form obstacles (both in board space).
            var cone = MeshKit.Fixed(pal.Cone);
            obstacles.Clear();
            level.CollectObstacles(obstacles);
            foreach (var o in obstacles)
            {
                var c = L.ToWorld(o.Center);
                var at = new Vector3(c.X, 0, c.Z);
                kit.Box(at + new Vector3(0, 0.025f, 0), new Vector3(0.5f, 0.05f, 0.5f), cone);
                kit.Cylinder(Matrix4x4.Translate(at + new Vector3(0, 0.35f, 0)), 0.24f, 0.01f, 0.6f, 16, cone, true, false);
                kit.Cylinder(Matrix4x4.Translate(at + new Vector3(0, 0.36f, 0)), 0.16f, 0.13f, 0.08f, 16, white);
            }

            BuildPipes(L, pal);
            BuildVessels(L, pal);
            BuildPumpHousing(L, pal);

            staticMesh = kit.ToMesh("Scenery", staticMesh);
            staticRenderer.GetComponent<MeshFilter>().sharedMesh = staticMesh;
            glassMesh = glassKit.ToMesh("VesselGlass", glassMesh);
            glassRenderer.GetComponent<MeshFilter>().sharedMesh = glassMesh;
        }

        readonly List<Obb> obstacles = new List<Obb>();
        readonly List<Vector2> outline = new List<Vector2>(80);

        /// <summary>Grid levels: checker cells with a white border (prototype look).</summary>
        void BuildGridLot(BoardLayout L, Color32 light, Color32 dark, Color32 white)
        {
            var p = L.P;
            for (int y = 0; y < L.Size; y++)
                for (int x = 0; x < L.Size; x++)
                {
                    var c = L.CellCenter(x, y);
                    kit.Quad(new Vector3(c.X, LotY, c.Z), 1f, 1f, (x + y) % 2 == 1 ? dark : light);
                }
            float half = L.Size / 2f, b = 0.06f, lotZ = L.LotCenter.Z;
            kit.Quad(new Vector3(0, LotY + 0.002f, p.LotZ0 + b / 2f), L.Size, b, white);
            kit.Quad(new Vector3(0, LotY + 0.002f, p.LotZ0 + L.Size - b / 2f), L.Size, b, white);
            kit.Quad(new Vector3(-half + b / 2f, LotY + 0.002f, lotZ), b, L.Size, white);
            kit.Quad(new Vector3(half - b / 2f, LotY + 0.002f, lotZ), b, L.Size, white);
        }

        /// <summary>Free-form levels: the board outline (circle / rounded rect) with a white rim and a faint inner ring.</summary>
        void BuildShapedLot(BoardLayout L, Color32 light, Color32 dark, Color32 white)
        {
            const float rim = 0.08f;
            var board = L.Board;
            var c = new Vector2(L.LotCenter.X, L.LotCenter.Z);
            if (board.Kind == BoardKind.Circle)
            {
                MeshKit.CirclePoints(outline, c, board.Radius);
                kit.FlatPolygon(outline, LotY, white);
                MeshKit.CirclePoints(outline, c, board.Radius - rim);
                kit.FlatPolygon(outline, LotY + 0.002f, light);
                MeshKit.CirclePoints(outline, c, board.Radius * 0.5f);
                kit.FlatPolygon(outline, LotY + 0.004f, dark);
            }
            else
            {
                MeshKit.RoundedRectPoints(outline, c, board.Width, board.Height, board.Radius);
                kit.FlatPolygon(outline, LotY, white);
                MeshKit.RoundedRectPoints(outline, c, board.Width - 2f * rim, board.Height - 2f * rim, board.Radius - rim);
                kit.FlatPolygon(outline, LotY + 0.002f, light);
            }
        }

        /// <summary>Gray pad under the bay row, covering every (possibly tilted) stall.</summary>
        void BuildBayPad(BoardLayout L, Palette pal)
        {
            var p = L.P;
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            for (int i = 0; i < L.BayCount; i++)
            {
                var stall = new Obb(L.StallCenter(i), L.BayAxis, p.BayLength / 2f, p.BayWidth / 2f);
                for (int k = 0; k < 4; k++)
                {
                    var q = stall.Corner(k);
                    minX = Mathf.Min(minX, q.X); maxX = Mathf.Max(maxX, q.X);
                    minZ = Mathf.Min(minZ, q.Z); maxZ = Mathf.Max(maxZ, q.Z);
                }
            }
            const float margin = 0.25f;
            kit.Quad(new Vector3((minX + maxX) / 2f, PadY, (minZ + maxZ) / 2f), maxX - minX + 2f * margin, maxZ - minZ + 2f * margin, MeshKit.Fixed(pal.BayPad));
        }

        void BuildPipes(BoardLayout L, Palette pal)
        {
            var p = L.P;
            var pipe = MeshKit.Fixed(pal.Pipe);
            var post = MeshKit.Fixed(pal.MetalDark);
            float my = p.ManifoldY, vz = p.VesselZ, vr = p.VesselRadius;
            float x0 = L.VesselCount > 0 ? L.VesselX(0) : 0f, x1 = L.VesselCount > 0 ? L.VesselX(L.VesselCount - 1) : 0f;

            // Manifold under the vessel taps, into the pump.
            kit.Pipe(new Vector3(x0, my, vz + vr + 0.25f), new Vector3(x1, my, vz + vr + 0.25f), 0.11f, 14, pipe);
            for (int i = 0; i < L.VesselCount; i++)
                kit.Pipe(new Vector3(L.VesselX(i), my, vz + vr), new Vector3(L.VesselX(i), my, vz + vr + 0.25f), 0.09f, 12, pipe);
            kit.Pipe(new Vector3(0, my, vz + vr + 0.25f), new Vector3(0, my, p.PumpZ - 0.5f), 0.11f, 14, pipe);

            // Header from the pump over the bays.
            float hy = p.HeaderY, hz = L.HeaderZ, pz = p.PumpZ + 0.4f;
            float left = L.BayRowMinX - 0.6f, right = L.BayRowMaxX + 0.6f;
            kit.Pipe(new Vector3(0, 1.2f, pz), new Vector3(0, hy, pz), 0.12f, 14, pipe);
            kit.Pipe(new Vector3(0, hy, pz), new Vector3(0, hy, hz), 0.12f, 14, pipe);
            kit.Pipe(new Vector3(left, hy, hz), new Vector3(right, hy, hz), 0.12f, 14, pipe);
            foreach (float x in new[] { left, right })
                kit.Box(new Vector3(x, hy / 2f, hz), new Vector3(0.14f, hy, 0.14f), post);

            // Drops to each hose head.
            for (int i = 0; i < L.BayCount; i++)
            {
                float x = L.BayX(i);
                kit.Pipe(new Vector3(x, hy, hz), new Vector3(x, hy, p.HoseZ), 0.08f, 10, pipe);
                kit.Box(new Vector3(x, hy, p.HoseZ), new Vector3(0.26f, 0.22f, 0.26f), post);
            }
        }

        void BuildVessels(BoardLayout L, Palette pal)
        {
            var p = L.P;
            float vr = p.VesselRadius, vb = p.VesselBaseY, vh = L.VesselHeight;
            var stand = MeshKit.Fixed(Color.white);
            var band = MeshKit.Fixed(pal.PumpBlue);
            var tap = MeshKit.Fixed(pal.TapYellow);
            var ring = MeshKit.Fixed(pal.MetalDark);

            // Lathe profile: rounded bottom, straight wall, lip (prototype values).
            profile.Clear();
            float r = vr + 0.06f, b0 = vb - 0.12f;
            profile.Add(new Vector2(0.001f, b0));
            for (int k = 0; k <= 8; k++)
            {
                float a = k / 8f * Mathf.PI / 2f;
                profile.Add(new Vector2(r * Mathf.Sin(a), b0 + 0.3f - 0.3f * Mathf.Cos(a)));
            }
            profile.Add(new Vector2(r, b0 + vh));
            profile.Add(new Vector2(r + 0.08f, b0 + vh + 0.05f));
            profile.Add(new Vector2(r + 0.08f, b0 + vh + 0.15f));

            for (int i = 0; i < L.VesselCount; i++)
            {
                var at = new Vector3(L.VesselX(i), 0, p.VesselZ);
                var m = Matrix4x4.Translate(at);
                glassKit.Lathe(m, profile, 32, MeshKit.Tint);
                kit.Cylinder(Matrix4x4.Translate(at + new Vector3(0, (vb - 0.15f) / 2f, 0)), vr + 0.12f, vr * 0.9f, vb - 0.15f, 28, stand);
                kit.Cylinder(Matrix4x4.Translate(at + new Vector3(0, vb - 0.2f, 0)), vr + 0.13f, vr + 0.13f, 0.12f, 28, band);
                kit.Torus(Matrix4x4.Translate(at + new Vector3(0, b0 + vh * 0.62f, 0)), r + 0.04f, 0.05f, 32, 8, ring);
                kit.Box(at + new Vector3(0, p.ManifoldY + 0.05f, vr + 0.05f), new Vector3(0.3f, 0.26f, 0.3f), tap);
            }
        }

        void BuildPumpHousing(BoardLayout L, Palette pal)
        {
            var at = new Vector3(L.Pump.X, 0, L.Pump.Z);
            kit.Box(at + new Vector3(0, 0.6f, 0), new Vector3(1.4f, 1.1f, 1.0f), MeshKit.Fixed(pal.PumpBlue));
            kit.Box(at + new Vector3(0, 1.2f, 0), new Vector3(1.5f, 0.12f, 1.1f), MeshKit.Fixed(pal.PumpBlueDark));
            kit.Cylinder(Matrix4x4.TRS(at + new Vector3(0, 0.75f, 0.52f), Quaternion.Euler(90f, 0, 0), Vector3.one),
                         0.2f, 0.2f, 0.05f, 20, MeshKit.Fixed(Color.white));
        }

        static MeshRenderer MakeRenderer(Transform parent, string name, Material mat, ShadowCastingMode shadows)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.AddComponent<MeshFilter>();
            var r = g.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = shadows;
            r.receiveShadows = true;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return r;
        }
    }
}
