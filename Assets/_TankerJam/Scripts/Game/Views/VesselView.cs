// A glass vessel's oil layers. Each layer is one stacked cylinder; draining the bottom layer makes
// everything above sink smoothly. Small squash on the bottom layer while the tap is open and a wobble
// on the top surface (prototype syncVessel()).
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TankerJam.Game
{
    public sealed class VesselView
    {
        struct Layer
        {
            public char Color;
            public float Amount; // 1 = full unit, 0 = gone
            public Transform T;
        }

        readonly List<Layer> layers = new List<Layer>(16);
        readonly Stack<Transform> pool = new Stack<Transform>(16);
        readonly Transform root;
        float radius, unitHeight, baseY;
        float wave, valve;

        public VesselView(Transform parent, string name)
        {
            root = new GameObject(name).transform;
            root.SetParent(parent, false);
        }

        public int LayerCount => layers.Count;

        public void Setup(Vector3 position, IReadOnlyList<char> units, float vesselRadius, float layerHeight, float baseHeight, MaterialLibrary mats)
        {
            Clear();
            root.localPosition = position;
            radius = vesselRadius;
            unitHeight = layerHeight;
            baseY = baseHeight + 0.02f;
            wave = valve = 0f;
            foreach (char c in units)
            {
                var t = pool.Count > 0 ? pool.Pop() : NewLayerObject();
                t.gameObject.SetActive(true);
                t.GetComponent<MeshRenderer>().sharedMaterial = mats.OilSurface(c);
                layers.Add(new Layer { Color = c, Amount = 1f, T = t });
            }
            Layout(0f);
        }

        Transform NewLayerObject()
        {
            var g = new GameObject("Layer");
            g.transform.SetParent(root, false);
            g.AddComponent<MeshFilter>().sharedMesh = SharedMeshes.UnitCylinder;
            var r = g.AddComponent<MeshRenderer>();
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return g.transform;
        }

        public void Clear()
        {
            foreach (var l in layers)
            {
                l.T.gameObject.SetActive(false);
                pool.Push(l.T);
            }
            layers.Clear();
        }

        /// <summary>Removes up to <paramref name="amount"/> from the bottom layer. Returns what was actually removed.</summary>
        public float DrainBottom(float amount)
        {
            if (layers.Count == 0) return 0f;
            var bottom = layers[0];
            float take = Mathf.Min(amount, bottom.Amount);
            bottom.Amount -= take;
            layers[0] = bottom;
            valve = 1f;
            wave = 1f;
            if (bottom.Amount <= 1e-4f) RemoveBottom();
            return take;
        }

        /// <summary>Drops a nearly empty bottom layer (rounding at the end of a unit).</summary>
        public void TrimBottom()
        {
            if (layers.Count > 0 && layers[0].Amount <= 1e-3f) RemoveBottom();
        }

        public float BottomAmount => layers.Count > 0 ? layers[0].Amount : 0f;
        public char BottomColor => layers.Count > 0 ? layers[0].Color : '\0';

        void RemoveBottom()
        {
            var t = layers[0].T;
            t.gameObject.SetActive(false);
            pool.Push(t);
            layers.RemoveAt(0);
        }

        public void Tick(float dt, float time)
        {
            wave *= Mathf.Pow(0.15f, dt);
            valve = Mathf.Max(0f, valve - dt * 2.5f);
            Layout(time);
        }

        void Layout(float time)
        {
            float y = baseY;
            int n = layers.Count;
            for (int i = 0; i < n; i++)
            {
                var l = layers[i];
                float h = Mathf.Max(0.001f, unitHeight * l.Amount);
                float wob = i == n - 1 ? Mathf.Sin(time * 9f) * wave * 0.03f : 0f;
                float sq = i == 0 ? 1f + Mathf.Sin(time * 16f) * valve * 0.015f : 1f;
                l.T.localScale = new Vector3(radius * sq, h + wob, radius * sq);
                l.T.localPosition = new Vector3(0f, y + (h + wob) / 2f, 0f);
                y += h;
            }
        }
    }
}
