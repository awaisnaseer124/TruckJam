// Bay outlines painted on the pad. One vertex-colored mesh for the whole row, rebuilt only when a bay's
// open state changes (VIP used, extra bay opened): VIP gold, regular white, locked extra bay green
// dashed with a "+".
using System.Collections.Generic;
using TankerJam.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace TankerJam.Game
{
    public sealed class BayRowView
    {
        const float Y = 0.03f, Line = 0.07f;

        readonly MeshKit kit = new MeshKit();
        readonly MeshKit decalKit = new MeshKit();
        readonly MeshFilter filter, decalFilter;
        readonly bool[] lastOpen = new bool[16];
        Mesh mesh, decalMesh;

        public BayRowView(Transform parent, MaterialLibrary mats)
        {
            var g = new GameObject("BayOutlines");
            g.transform.SetParent(parent, false);
            filter = g.AddComponent<MeshFilter>();
            var r = g.AddComponent<MeshRenderer>();
            r.sharedMaterial = mats.VertexColored;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.lightProbeUsage = LightProbeUsage.Off;

            var d = new GameObject("BayLabels");
            d.transform.SetParent(parent, false);
            decalFilter = d.AddComponent<MeshFilter>();
            var dr = d.AddComponent<MeshRenderer>();
            dr.sharedMaterial = mats.Decals;
            dr.shadowCastingMode = ShadowCastingMode.Off;
            dr.lightProbeUsage = LightProbeUsage.Off;
        }

        public void Rebuild(IReadOnlyList<Bay> bays, BoardLayout L, Palette pal)
        {
            kit.Clear();
            decalKit.Clear();
            float w = L.P.BayWidth, len = L.P.BayLength, z = L.BayPlaneZ;
            for (int i = 0; i < bays.Count; i++)
            {
                var bay = bays[i];
                float x = L.BayX(i);
                lastOpen[i] = bay.Open;
                if (bay.Kind == BayKind.Extra && !bay.Open)
                {
                    var green = MeshKit.Fixed(pal.ExtraLocked);
                    Dashed(x, z, w - 0.1f, len - 0.1f, green);
                    kit.Quad(new Vector3(x, Y, z), 0.5f, 0.1f, green);
                    kit.Quad(new Vector3(x, Y, z), 0.1f, 0.5f, green);
                    continue;
                }
                var c = MeshKit.Fixed(bay.Kind == BayKind.Vip ? pal.Vip : Color.white);
                Outline(x, z, w - 0.1f, len - 0.1f, c);
                if (bay.Kind == BayKind.Vip)
                {
                    // "VIP" along the bay near its bottom end, reading up the screen (local X -> world -Z).
                    var gold = (Color32)pal.Vip.linear;
                    gold.a = 255;
                    var m = Matrix4x4.TRS(new Vector3(x, Y + 0.002f, z + len / 2f - 0.95f), Quaternion.Euler(0f, 90f, 0f), Vector3.one);
                    decalKit.Quad(m, 1.29f, 0.43f, gold, DecalAtlasLayout.Vip);
                }
            }
            mesh = kit.ToMesh("BayOutlines", mesh);
            filter.sharedMesh = mesh;
            decalMesh = decalKit.ToMesh("BayLabels", decalMesh);
            decalFilter.sharedMesh = decalMesh;
        }

        /// <summary>Rebuilds only if an open state changed since the last build.</summary>
        public void Sync(IReadOnlyList<Bay> bays, BoardLayout L, Palette pal)
        {
            for (int i = 0; i < bays.Count; i++)
                if (bays[i].Open != lastOpen[i]) { Rebuild(bays, L, pal); return; }
        }

        void Outline(float x, float z, float w, float l, Color32 c)
        {
            kit.Quad(new Vector3(x, Y, z - l / 2f), w, Line, c);
            kit.Quad(new Vector3(x, Y, z + l / 2f), w, Line, c);
            kit.Quad(new Vector3(x - w / 2f, Y, z), Line, l, c);
            kit.Quad(new Vector3(x + w / 2f, Y, z), Line, l, c);
        }

        void Dashed(float x, float z, float w, float l, Color32 c)
        {
            const float dash = 0.22f, gap = 0.14f;
            for (float s = -l / 2f; s < l / 2f; s += dash + gap)
            {
                float e = Mathf.Min(s + dash, l / 2f), mid = (s + e) / 2f;
                kit.Quad(new Vector3(x - w / 2f, Y, z + mid), Line, e - s, c);
                kit.Quad(new Vector3(x + w / 2f, Y, z + mid), Line, e - s, c);
            }
            for (float s = -w / 2f; s < w / 2f; s += dash + gap)
            {
                float e = Mathf.Min(s + dash, w / 2f), mid = (s + e) / 2f;
                kit.Quad(new Vector3(x + mid, Y, z - l / 2f), e - s, Line, c);
                kit.Quad(new Vector3(x + mid, Y, z + l / 2f), e - s, Line, c);
            }
        }
    }
}
