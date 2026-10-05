// A glass vessel's oil column. Layers are tracked logically (color + remaining amount); the whole column is
// ONE cylinder drawn with the VesselLiquid shader, which picks each layer's color by world height from
// arrays in a MaterialPropertyBlock.
// Motion is kept calm on purpose: layer boundaries ease toward where the oil really is (draining settles
// instead of sliding at a constant rate), the surface tilts on a soft spring when the pump starts pulling
// and keeps a barely visible idle sway, and bubbles fade in while the vessel drains.
// Tall vessels (more units than MaxLayers) keep every unit logically but draw only the bottom MaxLayers;
// the hidden ones sink into view as the bottom drains.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TankerJam.Game
{
    public sealed class VesselView
    {
        public const int MaxLayers = 16;
        /// <summary>Gap between the liquid and the glass wall (world units).</summary>
        const float GlassGap = 0.035f;
        /// <summary>How fast shown layer boundaries catch up with the real ones (1/s).</summary>
        const float Settle = 9f;
        /// <summary>Surface spring: stiffness and damping (underdamped: a few soft rocks, then rest).</summary>
        const float TiltStiffness = 38f, TiltDamping = 3.2f;
        const float TiltKick = 0.11f, IdleSway = 0.012f, MaxTilt = 0.16f;

        static readonly int CountId = Shader.PropertyToID("_LayerCount");
        static readonly int TopsId = Shader.PropertyToID("_LayerTop");
        static readonly int ColorsId = Shader.PropertyToID("_LayerColor");
        static readonly int CenterId = Shader.PropertyToID("_ColumnCenter");
        static readonly int HeightId = Shader.PropertyToID("_ColumnHeight");
        static readonly int TiltId = Shader.PropertyToID("_Tilt");
        static readonly int BubblesId = Shader.PropertyToID("_Bubbles");
        static readonly int PhaseId = Shader.PropertyToID("_Phase");

        struct Layer
        {
            public char Color;
            public float Amount; // 1 = full unit, 0 = gone
        }

        readonly List<Layer> layers = new List<Layer>(32);
        readonly Transform root, column;
        readonly MeshRenderer renderer;
        readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        readonly float[] tops = new float[MaxLayers];      // shown layer tops (world units above the base)
        readonly Vector4[] colors = new Vector4[MaxLayers];
        readonly float phase;
        Palette palette;
        float radius, unitHeight, baseY;
        float valve, bubbles;
        Vector2 tilt, tiltVelocity;
        bool colorsDirty, snap;

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
            phase = (name.GetHashCode() & 0xFFFF) / 6553.6f;
        }

        public int LayerCount => layers.Count;
        /// <summary>Units above the visible column (tall vessels).</summary>
        public int HiddenLayers => Mathf.Max(0, layers.Count - MaxLayers);

        public void Setup(Vector3 position, IReadOnlyList<char> units, float vesselRadius, float layerHeight, float baseHeight, Palette pal)
        {
            layers.Clear();
            palette = pal;
            root.gameObject.SetActive(true);
            root.localPosition = position;
            radius = vesselRadius - GlassGap;
            unitHeight = layerHeight;
            baseY = baseHeight + 0.02f;
            valve = bubbles = 0f;
            tilt = tiltVelocity = Vector2.zero;
            for (int i = 0; i < units.Count; i++) layers.Add(new Layer { Color = units[i], Amount = 1f });
            colorsDirty = true;
            snap = true;
            Layout(0f, 0f);
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
            if (valve <= 0f)
            {
                // The pump just started pulling: a soft push sets the surface rocking.
                float a = Random.value * Mathf.PI * 2f;
                tiltVelocity += new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * TiltKick * TiltStiffness * 0.1f;
            }
            var bottom = layers[0];
            float take = Mathf.Min(amount, bottom.Amount);
            bottom.Amount -= take;
            layers[0] = bottom;
            valve = 1f;
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
            // The shown boundaries move down one slot with the layers, so nothing jumps.
            for (int i = 0; i < MaxLayers - 1; i++) tops[i] = tops[i + 1];
            colorsDirty = true;
        }

        public void Tick(float dt, float time)
        {
            valve = Mathf.Max(0f, valve - dt * 2.5f);
            // Bubbles fade in quickly while draining and drift away slowly after.
            float target = valve > 0f ? 1f : 0f;
            bubbles = Mathf.MoveTowards(bubbles, target, dt * (target > bubbles ? 2.5f : 0.6f));

            // Surface: spring toward a barely visible idle sway.
            var rest = new Vector2(Mathf.Sin(time * 0.7f + phase), Mathf.Cos(time * 0.53f + phase * 1.3f)) * IdleSway;
            var accel = (rest - tilt) * TiltStiffness - tiltVelocity * TiltDamping;
            tiltVelocity += accel * dt;
            tilt += tiltVelocity * dt;
            tilt = Vector2.ClampMagnitude(tilt, MaxTilt);
            Layout(time, dt);
        }

        void Layout(float time, float dt)
        {
            int n = Mathf.Min(layers.Count, MaxLayers);
            if (n == 0)
            {
                if (renderer.enabled) renderer.enabled = false;
                return;
            }
            if (!renderer.enabled) renderer.enabled = true;

            // Ease every shown boundary toward the real one (frame-rate independent).
            float k = snap ? 1f : 1f - Mathf.Exp(-Settle * dt);
            snap = false;
            float y = 0f;
            for (int i = 0; i < n; i++)
            {
                y += Mathf.Max(0.001f, unitHeight * layers[i].Amount);
                tops[i] += (y - tops[i]) * k;
                if (colorsDirty) colors[i] = palette.OilColorOf(layers[i].Color).linear;
            }
            colorsDirty = false;
            float height = Mathf.Max(0.002f, tops[n - 1]);

            column.localScale = new Vector3(radius, height, radius);
            column.localPosition = new Vector3(0f, baseY + height / 2f, 0f);

            var c = root.position;
            block.SetFloat(CountId, n);
            block.SetFloatArray(TopsId, tops);
            block.SetVectorArray(ColorsId, colors);
            block.SetVector(CenterId, new Vector4(c.x, c.y + baseY, c.z, radius));
            block.SetFloat(HeightId, height);
            block.SetVector(TiltId, new Vector4(tilt.x, tilt.y, 0f, 0f));
            block.SetFloat(BubblesId, bubbles);
            block.SetFloat(PhaseId, phase);
            renderer.SetPropertyBlock(block);
        }
    }
}
