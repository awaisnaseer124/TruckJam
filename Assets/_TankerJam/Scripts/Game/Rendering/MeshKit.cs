// Builds low-poly meshes from primitives with per-vertex colors, so many parts can share one material
// and one draw call (see ToyLit.shader for the vertex color convention). Used by the greybox prefab
// baker in the editor and by SceneryBuilder at level load. Not for per-frame use.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TankerJam.Game
{
    public sealed class MeshKit
    {
        /// <summary>Vertex color that takes the material tint (alpha 1, white).</summary>
        public static readonly Color32 Tint = new Color32(255, 255, 255, 255);

        /// <summary>A fixed vertex color (given in sRGB) that ignores the material tint. Stored linear, because
        /// vertex colors are not color-space converted like material colors are (project uses Linear).</summary>
        public static Color32 Fixed(Color srgb)
        {
            var c = srgb.linear;
            return new Color32((byte)(c.r * 255f + 0.5f), (byte)(c.g * 255f + 0.5f), (byte)(c.b * 255f + 0.5f), 0);
        }

        /// <summary>The material tint blended toward white: 0 = tint, 1 = white.</summary>
        public static Color32 TintLightened(float towardWhite) => new Color32(255, 255, 255, (byte)((1f - towardWhite) * 255));

        readonly List<Vector3> verts = new List<Vector3>();
        readonly List<Vector3> normals = new List<Vector3>();
        readonly List<Color32> colors = new List<Color32>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<int> tris = new List<int>();

        public int VertexCount => verts.Count;

        public void Clear()
        {
            verts.Clear(); normals.Clear(); colors.Clear(); uvs.Clear(); tris.Clear();
        }

        int AddVertex(Matrix4x4 m, Vector3 p, Vector3 n, Color32 c, Vector2 uv)
        {
            verts.Add(m.MultiplyPoint3x4(p));
            normals.Add(m.MultiplyVector(n).normalized);
            colors.Add(c);
            uvs.Add(uv);
            return verts.Count - 1;
        }

        void Tri(int a, int b, int c) { tris.Add(a); tris.Add(b); tris.Add(c); }

        // ---------- primitives ----------

        public void Box(Vector3 center, Vector3 size, Color32 color) => Box(Matrix4x4.TRS(center, Quaternion.identity, Vector3.one), size, color);

        public void Box(Matrix4x4 m, Vector3 size, Color32 color)
        {
            Vector3 h = size * 0.5f;
            Face(m, new Vector3(0, 0, 1), new Vector3(-1, 0, 0), new Vector3(0, 1, 0), h, color);
            Face(m, new Vector3(0, 0, -1), new Vector3(1, 0, 0), new Vector3(0, 1, 0), h, color);
            Face(m, new Vector3(1, 0, 0), new Vector3(0, 0, 1), new Vector3(0, 1, 0), h, color);
            Face(m, new Vector3(-1, 0, 0), new Vector3(0, 0, -1), new Vector3(0, 1, 0), h, color);
            Face(m, new Vector3(0, 1, 0), new Vector3(1, 0, 0), new Vector3(0, 0, 1), h, color);
            Face(m, new Vector3(0, -1, 0), new Vector3(1, 0, 0), new Vector3(0, 0, -1), h, color);
        }

        void Face(Matrix4x4 m, Vector3 n, Vector3 u, Vector3 v, Vector3 h, Color32 c)
        {
            Vector3 center = Vector3.Scale(n, h);
            Vector3 du = Vector3.Scale(u, h), dv = Vector3.Scale(v, h);
            int a = AddVertex(m, center - du - dv, n, c, new Vector2(0, 0));
            int b = AddVertex(m, center + du - dv, n, c, new Vector2(1, 0));
            int d = AddVertex(m, center + du + dv, n, c, new Vector2(1, 1));
            int e = AddVertex(m, center - du + dv, n, c, new Vector2(0, 1));
            Tri(a, d, b); Tri(a, e, d);
        }

        /// <summary>Cylinder (or cone/frustum) along the local Y axis of <paramref name="m"/>, centered at its origin.</summary>
        public void Cylinder(Matrix4x4 m, float radiusBottom, float radiusTop, float height, int segments, Color32 color,
                             bool capBottom = true, bool capTop = true, bool inward = false)
        {
            float y0 = -height / 2f, y1 = height / 2f;
            float slope = (radiusBottom - radiusTop) / height;
            int start = verts.Count;
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                float cx = Mathf.Cos(a), cz = Mathf.Sin(a);
                var n = new Vector3(cx, slope, cz).normalized;
                if (inward) n = -n;
                AddVertex(m, new Vector3(cx * radiusBottom, y0, cz * radiusBottom), n, color, new Vector2((float)i / segments, 0));
                AddVertex(m, new Vector3(cx * radiusTop, y1, cz * radiusTop), n, color, new Vector2((float)i / segments, 1));
            }
            for (int i = 0; i < segments; i++)
            {
                int b0 = start + i * 2, t0 = b0 + 1, b1 = b0 + 2, t1 = b0 + 3;
                if (inward) { Tri(b0, b1, t0); Tri(t0, b1, t1); }
                else { Tri(b0, t0, b1); Tri(t0, t1, b1); }
            }
            if (capBottom && radiusBottom > 0f) Disc(m, y0, radiusBottom, segments, Vector3.down, color);
            if (capTop && radiusTop > 0f) Disc(m, y1, radiusTop, segments, Vector3.up, color);
        }

        void Disc(Matrix4x4 m, float y, float r, int segments, Vector3 n, Color32 color)
        {
            int c = AddVertex(m, new Vector3(0, y, 0), n, color, new Vector2(0.5f, 0.5f));
            int start = verts.Count;
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                AddVertex(m, new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r), n, color,
                          new Vector2(Mathf.Cos(a) * 0.5f + 0.5f, Mathf.Sin(a) * 0.5f + 0.5f));
            }
            for (int i = 0; i < segments; i++)
            {
                if (n.y > 0) Tri(c, start + i + 1, start + i);
                else Tri(c, start + i, start + i + 1);
            }
        }

        /// <summary>Cylinder stretched between two points (pipes, hoses).</summary>
        public void Pipe(Vector3 a, Vector3 b, float radius, int segments, Color32 color)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len < 1e-4f) return;
            var m = Matrix4x4.TRS((a + b) * 0.5f, Quaternion.FromToRotation(Vector3.up, d / len), Vector3.one);
            Cylinder(m, radius, radius, len, segments, color);
        }

        /// <summary>Torus in the local XZ plane of <paramref name="m"/>.</summary>
        public void Torus(Matrix4x4 m, float radius, float tube, int radial, int tubular, Color32 color)
        {
            int start = verts.Count;
            for (int i = 0; i <= radial; i++)
            {
                float u = i * Mathf.PI * 2f / radial;
                var center = new Vector3(Mathf.Cos(u) * radius, 0, Mathf.Sin(u) * radius);
                for (int j = 0; j <= tubular; j++)
                {
                    float v = j * Mathf.PI * 2f / tubular;
                    var n = new Vector3(Mathf.Cos(u) * Mathf.Cos(v), Mathf.Sin(v), Mathf.Sin(u) * Mathf.Cos(v));
                    AddVertex(m, center + n * tube, n, color, new Vector2((float)i / radial, (float)j / tubular));
                }
            }
            int row = tubular + 1;
            for (int i = 0; i < radial; i++)
                for (int j = 0; j < tubular; j++)
                {
                    int a = start + i * row + j, b = a + row, c = b + 1, d = a + 1;
                    Tri(a, d, b); Tri(d, c, b);
                }
        }

        /// <summary>Surface of revolution around local Y. Profile points are (radius, y), bottom to top.</summary>
        public void Lathe(Matrix4x4 m, IList<Vector2> profile, int segments, Color32 color)
        {
            int start = verts.Count;
            int rows = profile.Count;
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                float cx = Mathf.Cos(a), cz = Mathf.Sin(a);
                for (int k = 0; k < rows; k++)
                {
                    Vector2 prev = profile[Mathf.Max(0, k - 1)], next = profile[Mathf.Min(rows - 1, k + 1)];
                    Vector2 t = (next - prev).normalized;
                    var n = new Vector3(t.y * cx, -t.x, t.y * cz).normalized;
                    var p = profile[k];
                    AddVertex(m, new Vector3(cx * p.x, p.y, cz * p.x), n, color, new Vector2((float)i / segments, (float)k / (rows - 1)));
                }
            }
            for (int i = 0; i < segments; i++)
                for (int k = 0; k < rows - 1; k++)
                {
                    int a = start + i * rows + k, b = a + rows, c = b + 1, d = a + 1;
                    Tri(a, d, b); Tri(d, c, b);
                }
        }

        /// <summary>Flat quad facing local +Y of <paramref name="m"/>, sized in local X (width) and Z (length).</summary>
        public void Quad(Matrix4x4 m, float width, float length, Color32 color, Rect uv)
        {
            float w = width / 2f, l = length / 2f;
            var n = Vector3.up;
            int a = AddVertex(m, new Vector3(-w, 0, -l), n, color, new Vector2(uv.xMin, uv.yMin));
            int b = AddVertex(m, new Vector3(w, 0, -l), n, color, new Vector2(uv.xMax, uv.yMin));
            int c = AddVertex(m, new Vector3(w, 0, l), n, color, new Vector2(uv.xMax, uv.yMax));
            int d = AddVertex(m, new Vector3(-w, 0, l), n, color, new Vector2(uv.xMin, uv.yMax));
            Tri(a, d, c); Tri(a, c, b);
        }

        public void Quad(Vector3 center, float width, float length, Color32 color) =>
            Quad(Matrix4x4.TRS(center, Quaternion.identity, Vector3.one), width, length, color, new Rect(0, 0, 1, 1));

        /// <summary>Flat convex polygon facing +Y at height y. Points in XZ, ordered by increasing angle (atan2(z, x)).</summary>
        public void FlatPolygon(IList<Vector2> pointsXZ, float y, Color32 color)
        {
            var m = Matrix4x4.identity;
            var c = Vector2.zero;
            foreach (var p in pointsXZ) c += p;
            c /= pointsXZ.Count;
            int center = AddVertex(m, new Vector3(c.x, y, c.y), Vector3.up, color, new Vector2(0.5f, 0.5f));
            int start = verts.Count;
            foreach (var p in pointsXZ) AddVertex(m, new Vector3(p.x, y, p.y), Vector3.up, color, new Vector2(0.5f, 0.5f));
            int n = pointsXZ.Count;
            for (int i = 0; i < n; i++) Tri(center, start + (i + 1) % n, start + i);
        }

        /// <summary>Outline points of a rounded rectangle centered at <paramref name="center"/> (XZ), increasing angle.</summary>
        public static void RoundedRectPoints(List<Vector2> into, Vector2 center, float width, float height, float radius, int arcSteps = 8)
        {
            into.Clear();
            float hx = width / 2f, hz = height / 2f, r = Mathf.Clamp(radius, 0f, Mathf.Min(hx, hz));
            var corners = new[] { new Vector2(hx - r, hz - r), new Vector2(-hx + r, hz - r), new Vector2(-hx + r, -hz + r), new Vector2(hx - r, -hz + r) };
            for (int q = 0; q < 4; q++)
                for (int k = 0; k <= arcSteps; k++)
                {
                    float a = (q * 90f + k * 90f / arcSteps) * Mathf.Deg2Rad;
                    into.Add(center + corners[q] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
                }
        }

        /// <summary>Outline points of a circle (XZ), increasing angle.</summary>
        public static void CirclePoints(List<Vector2> into, Vector2 center, float radius, int segments = 64)
        {
            into.Clear();
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                into.Add(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
            }
        }

        // ---------- output ----------

        public Mesh ToMesh(string name, Mesh reuse = null)
        {
            var mesh = reuse != null ? reuse : new Mesh();
            mesh.Clear();
            mesh.name = name;
            mesh.indexFormat = verts.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
