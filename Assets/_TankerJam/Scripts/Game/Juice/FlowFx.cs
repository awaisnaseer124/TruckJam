// Oil "blobs" travelling vessel tap -> manifold -> pump -> header -> hose while a unit pumps (prototype
// spawnBlob). No GameObjects: blobs are structs in a fixed array, drawn with Graphics.RenderMeshInstanced,
// one instanced draw call per oil color. Paths are precomputed per (vessel, bay) when a level loads.
using System.Collections.Generic;
using TankerJam.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace TankerJam.Game
{
    public sealed class FlowFx
    {
        const int MaxBlobs = 160;
        const float Speed = 13f;
        const float Radius = 0.12f;

        struct Blob
        {
            public float S;
            public int Path;
            public int Color;
        }

        /// <summary>A polyline in 3D with cumulative lengths, sampled by distance.</summary>
        sealed class Path3
        {
            public readonly List<Vector3> Points = new List<Vector3>(12);
            public readonly List<float> Cumulative = new List<float>(12);
            public float Length;

            public void Clear() { Points.Clear(); Cumulative.Clear(); Length = 0f; }

            public void Add(Vector3 p)
            {
                if (Points.Count > 0) Length += Vector3.Distance(Points[Points.Count - 1], p);
                Points.Add(p);
                Cumulative.Add(Length);
            }

            public Vector3 At(float s)
            {
                int i = 1;
                while (i < Points.Count - 1 && Cumulative[i] < s) i++;
                float seg = Cumulative[i] - Cumulative[i - 1];
                float u = seg > 0f ? (s - Cumulative[i - 1]) / seg : 0f;
                return Vector3.LerpUnclamped(Points[i - 1], Points[i], u);
            }
        }

        readonly Blob[] blobs = new Blob[MaxBlobs];
        readonly List<Path3> paths = new List<Path3>();
        readonly Palette palette;
        readonly Material[] materials;
        readonly Matrix4x4[][] batches;
        readonly int[] batchCounts;
        readonly Mesh mesh;
        int count, bayCount;

        public int Count => count;

        /// <summary>Instanced draw calls issued by the last Render (one per color with blobs).</summary>
        public int BatchCount { get; private set; }

        public FlowFx(MaterialLibrary mats, Palette pal)
        {
            palette = pal;
            int colors = pal.Oils.Length;
            materials = new Material[colors];
            batches = new Matrix4x4[colors][];
            batchCounts = new int[colors];
            for (int i = 0; i < colors; i++)
            {
                materials[i] = mats.Blob(pal.Oils[i].Key[0]);
                batches[i] = new Matrix4x4[MaxBlobs];
            }
            mesh = SharedMeshes.Sphere;
        }

        /// <summary>Precomputes one path per (vessel, bay) for the level's layout.</summary>
        public void Build(BoardLayout L)
        {
            count = 0;
            bayCount = L.BayCount;
            int needed = L.VesselCount * L.BayCount;
            while (paths.Count < needed) paths.Add(new Path3());
            var p = L.P;
            float my = p.ManifoldY, vz = p.VesselZ + p.VesselRadius, pz = p.PumpZ;
            for (int v = 0; v < L.VesselCount; v++)
                for (int b = 0; b < L.BayCount; b++)
                {
                    var path = paths[v * L.BayCount + b];
                    path.Clear();
                    float vx = L.VesselX(v), bx = L.BayX(b);
                    path.Add(new Vector3(vx, my, vz + 0.05f));
                    path.Add(new Vector3(vx, my, vz + 0.25f));
                    path.Add(new Vector3(0f, my, vz + 0.25f));
                    path.Add(new Vector3(0f, my, pz - 0.5f));
                    path.Add(new Vector3(0f, 1.2f, pz + 0.4f));
                    path.Add(new Vector3(0f, p.HeaderY, pz + 0.4f));
                    path.Add(new Vector3(0f, p.HeaderY, L.HeaderZ));
                    path.Add(new Vector3(bx, p.HeaderY, L.HeaderZ));
                    path.Add(new Vector3(bx, p.HeaderY, p.HoseZ));
                }
        }

        public void Spawn(int vessel, int bay, char colorKey)
        {
            if (count >= MaxBlobs || bay < 0) return;
            int c = palette.IndexOf(colorKey);
            if (c < 0) return;
            blobs[count++] = new Blob { S = 0f, Path = vessel * bayCount + bay, Color = c };
        }

        public void Tick(float dt)
        {
            for (int i = count - 1; i >= 0; i--)
            {
                blobs[i].S += dt * Speed;
                if (blobs[i].S >= paths[blobs[i].Path].Length) blobs[i] = blobs[--count]; // swap-remove
            }
        }

        /// <summary>Submits the blobs for this frame (call once per rendered frame).</summary>
        public void Render()
        {
            BatchCount = 0;
            if (count == 0) return;
            for (int c = 0; c < batchCounts.Length; c++) batchCounts[c] = 0;
            var scale = Vector3.one * (Radius * 2f);
            for (int i = 0; i < count; i++)
            {
                ref var b = ref blobs[i];
                var pos = paths[b.Path].At(b.S);
                batches[b.Color][batchCounts[b.Color]++] = Matrix4x4.TRS(pos, Quaternion.identity, scale);
            }
            for (int c = 0; c < batchCounts.Length; c++)
            {
                if (batchCounts[c] == 0) continue;
                var rp = new RenderParams(materials[c])
                {
                    shadowCastingMode = ShadowCastingMode.Off,
                    receiveShadows = false,
                    worldBounds = new Bounds(Vector3.zero, Vector3.one * 100f),
                };
                Graphics.RenderMeshInstanced(rp, mesh, 0, batches[c], batchCounts[c]);
                BatchCount++;
            }
        }
    }
}
