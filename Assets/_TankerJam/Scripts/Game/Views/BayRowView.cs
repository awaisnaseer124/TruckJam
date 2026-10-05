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
            float w = L.P.BayWidth, len = L.P.BayLength;
            // Stall frame: local +Z runs along the bay axis (toward the hose), so tilted bays just rotate.
            var rot = Quaternion.Euler(0f, BoardLayout.Yaw(L.BayAxis) * Mathf.Rad2Deg, 0f);
            for (int i = 0; i < bays.Count; i++)
            {
                var bay = bays[i];
                var c2 = L.StallCenter(i);
                frame = Matrix4x4.TRS(new Vector3(c2.X, Y, c2.Z), rot, Vector3.one);
                lastOpen[i] = bay.Open;
                if (bay.Kind == BayKind.Extra && !bay.Open)
                {
                    var green = MeshKit.Fixed(pal.ExtraLocked);
                    Dashed(w - 0.1f, len - 0.1f, green);
                    Local(0f, 0f, 0.5f, 0.1f, green);
                    Local(0f, 0f, 0.1f, 0.5f, green);
                    continue;
                }
                var c = MeshKit.Fixed(bay.Kind == BayKind.Vip ? pal.Vip : Color.white);
                Outline(w - 0.1f, len - 0.1f, c);
                if (bay.Kind == BayKind.Vip)
                {
                    // "VIP" along the bay near its open (camera-side) end, reading up the stall.
                    var gold = (Color32)pal.Vip.linear;
                    gold.a = 255;
                    var m = frame * Matrix4x4.TRS(new Vector3(0f, 0.002f, -(len / 2f - 0.95f)), Quaternion.Euler(0f, -90f, 0f), Vector3.one);
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

        static readonly Rect FullUv = new Rect(0, 0, 1, 1);
        Matrix4x4 frame;

        void Local(float x, float z, float w, float l, Color32 c) =>
            kit.Quad(frame * Matrix4x4.Translate(new Vector3(x, 0f, z)), w, l, c, FullUv);

        void Outline(float w, float l, Color32 c)
        {
            Local(0f, -l / 2f, w, Line, c);
            Local(0f, l / 2f, w, Line, c);
            Local(-w / 2f, 0f, Line, l, c);
            Local(w / 2f, 0f, Line, l, c);
        }

        void Dashed(float w, float l, Color32 c)
        {
            const float dash = 0.22f, gap = 0.14f;
            for (float s = -l / 2f; s < l / 2f; s += dash + gap)
            {
                float e = Mathf.Min(s + dash, l / 2f), mid = (s + e) / 2f;
                Local(-w / 2f, mid, Line, e - s, c);
                Local(w / 2f, mid, Line, e - s, c);
            }
            for (float s = -w / 2f; s < w / 2f; s += dash + gap)
            {
                float e = Mathf.Min(s + dash, w / 2f), mid = (s + e) / 2f;
                Local(mid, -l / 2f, e - s, Line, c);
                Local(mid, l / 2f, e - s, Line, c);
            }
        }
    }
}
