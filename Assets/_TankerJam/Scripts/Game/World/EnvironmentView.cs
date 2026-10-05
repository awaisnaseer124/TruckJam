// Decoration around the board from an EnvironmentTheme. Instantiated once (not per level), shadows off by
// default, and static-batched into as few draw calls as there are materials. Decoration only: no colliders,
// never on the Trucks layer, so it can't affect taps. Per level, props that would overlap the play area
// (bigger lots, more vessels) are hidden; the rest stay.
using System.Collections.Generic;
using TankerJam.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace TankerJam.Game
{
    public sealed class EnvironmentView
    {
        readonly Transform parent;
        EnvironmentTheme current;
        GameObject instance;
        readonly List<(GameObject prop, Bounds bounds)> props = new List<(GameObject, Bounds)>();
        readonly List<Rect> keepOut = new List<Rect>(4);

        public EnvironmentView(Transform parent) { this.parent = parent; }

        public void Apply(EnvironmentTheme theme)
        {
            if (theme == current) return;
            if (instance != null) Object.Destroy(instance);
            current = theme;
            instance = null;
            if (theme == null || theme.Layout == null) return;

            instance = Object.Instantiate(theme.Layout, parent);
            instance.name = theme.Layout.name;
            foreach (var c in instance.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
            foreach (var r in instance.GetComponentsInChildren<MeshRenderer>(true))
            {
                r.shadowCastingMode = theme.CastShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                r.receiveShadows = true;
                r.lightProbeUsage = LightProbeUsage.Off;
                r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
            // Footprints before batching (each prop is a direct child of the layout root).
            props.Clear();
            foreach (Transform child in instance.transform)
            {
                var renderers = child.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) continue;
                var b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
                props.Add((child.gameObject, b));
            }
            // One batch per material (the palette atlas): meshes must be readable (FBX Read/Write on).
            StaticBatchingUtility.Combine(instance);
        }

        /// <summary>Hides props that overlap this level's play area: ring road + lot, bay pad, vessels, pump.</summary>
        public void Fit(BoardLayout L)
        {
            if (instance == null) return;
            var p = L.P;
            const float margin = 0.3f;
            float road = p.RoadWidth / 2f + margin;
            keepOut.Clear();
            keepOut.Add(Rect.MinMaxRect(-L.RingX - road, L.RingTop - road, L.RingX + road, L.RingBottom + road));
            float bayHalf = Mathf.Max(Mathf.Abs(L.BayRowMinX), Mathf.Abs(L.BayRowMaxX)) + p.BayLength / 2f + margin;
            keepOut.Add(Rect.MinMaxRect(-bayHalf, p.HoseZ - p.BayLength - margin, bayHalf, L.RingTop));
            float vr = p.VesselRadius + 0.5f;
            float vesselHalf = L.VesselCount > 0 ? Mathf.Max(Mathf.Abs(L.VesselX(0)), Mathf.Abs(L.VesselX(L.VesselCount - 1))) + vr : vr;
            keepOut.Add(Rect.MinMaxRect(-vesselHalf, p.VesselZ - vr, vesselHalf, p.PumpZ + 1.2f));

            foreach (var (prop, b) in props)
            {
                var footprint = Rect.MinMaxRect(b.min.x, b.min.z, b.max.x, b.max.z);
                bool blocked = false;
                foreach (var k in keepOut) if (k.Overlaps(footprint)) { blocked = true; break; }
                if (prop.activeSelf == blocked) prop.SetActive(!blocked);
            }
        }
    }
}
