// Decoration around the board from an EnvironmentTheme. Instantiated once (not per level), shadows off by
// default, and static-batched into as few draw calls as there are materials. Decoration only: no colliders,
// never on the Trucks layer, so it can't affect taps.
using UnityEngine;
using UnityEngine.Rendering;

namespace TankerJam.Game
{
    public sealed class EnvironmentView
    {
        readonly Transform parent;
        EnvironmentTheme current;
        GameObject instance;

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
            // One batch per material (the palette atlas): meshes must be readable (FBX Read/Write on).
            StaticBatchingUtility.Combine(instance);
        }
    }
}
