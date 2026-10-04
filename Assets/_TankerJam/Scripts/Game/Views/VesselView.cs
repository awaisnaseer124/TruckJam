// A glass vessel's oil column. Layers are tracked logically (color + remaining amount); the whole column is
// ONE cylinder drawn with the VesselLiquid shader, which picks each layer's color by height from arrays in a
// MaterialPropertyBlock. Draining the bottom layer makes everything above sink smoothly; small squash while
// the tap is open and a wobble on the top surface (prototype syncVessel()).
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TankerJam.Game
{
    public sealed class VesselView
    {
        public const int MaxLayers = 16;
        static readonly int CountId = Shader.PropertyToID("_LayerCount");
        static readonly int TopsId = Shader.PropertyToID("_LayerTop");
        static readonly int ColorsId = Shader.PropertyToID("_LayerColor");

        struct Layer
        {
            public char Color;
            public float Amount; // 1 = full unit, 0 = gone
        }

        readonly List<Layer> layers = new List<Layer>(MaxLayers);
        readonly Transform root, column;
        readonly MeshRenderer renderer;
        readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        readonly float[] tops = new float[MaxLayers];
        readonly Vector4[] colors = new Vector4[MaxLayers];
        Palette palette;
        float radius, unitHeight, baseY;
        float wave, valve;
        bool colorsDirty;

        public VesselView(Transform parent, string name, Material liquid)
        {
            root = new GameObject(name).transform;
            root.SetParent(parent, false);
            var g = new GameObject("Oil");
            g.transform.SetParent(root, false);
            g.AddComponent<MeshFilter>().sharedMesh = SharedMeshes.UnitCylinder;
            renderer = g.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = liquid;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            column = g.transform;
        }

        public int LayerCount => layers.Count;

        public void Setup(Vector3 position, IReadOnlyList<char> units, float vesselRadius, float layerHeight, float baseHeight, Palette pal)
        {
            layers.Clear();
            palette = pal;
            root.gameObject.SetActive(true);
            root.localPosition = position;
            radius = vesselRadius;
            unitHeight = layerHeight;
            baseY = baseHeight + 0.02f;
            wave = valve = 0f;
            if (units.Count > MaxLayers) Debug.LogError($"Vessel has {units.Count} units; the shader supports {MaxLayers}.");
            for (int i = 0; i < units.Count && i < MaxLayers; i++) layers.Add(new Layer { Color = units[i], Amount = 1f });
            colorsDirty = true;
            Layout(0f);
        }

        public void Clear()
        {
            layers.Clear();
            root.gameObject.SetActive(false);
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
            layers.RemoveAt(0);
            colorsDirty = true;
        }

        public void Tick(float dt, float time)
        {
            wave *= Mathf.Pow(0.15f, dt);
            valve = Mathf.Max(0f, valve - dt * 2.5f);
            Layout(time);
        }

        void Layout(float time)
        {
            int n = layers.Count;
            if (n == 0)
            {
                if (renderer.enabled) renderer.enabled = false;
                return;
            }
            if (!renderer.enabled) renderer.enabled = true;

            float total = 0f;
            for (int i = 0; i < n; i++) total += Mathf.Max(0.001f, unitHeight * layers[i].Amount);
            float wob = Mathf.Sin(time * 9f) * wave * 0.03f;
            float height = Mathf.Max(0.002f, total + wob);

            float y = 0f;
            for (int i = 0; i < n; i++)
            {
                y += Mathf.Max(0.001f, unitHeight * layers[i].Amount);
                tops[i] = i == n - 1 ? 1f : y / height;
                if (colorsDirty) colors[i] = palette.OilColorOf(layers[i].Color).linear;
            }
            colorsDirty = false;

            float sq = 1f + Mathf.Sin(time * 16f) * valve * 0.015f;
            column.localScale = new Vector3(radius * sq, height, radius * sq);
            column.localPosition = new Vector3(0f, baseY + height / 2f, 0f);

            block.SetFloat(CountId, n);
            block.SetFloatArray(TopsId, tops);
            block.SetVectorArray(ColorsId, colors);
            renderer.SetPropertyBlock(block);
        }
    }
}
