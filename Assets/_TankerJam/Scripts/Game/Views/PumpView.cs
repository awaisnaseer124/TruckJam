using UnityEngine;
using UnityEngine.Rendering;

namespace TankerJam.Game
{
    /// <summary>Pump hand wheel and gauge needle. Spins fast and swings the needle while a unit is pumping.</summary>
    public sealed class PumpView
    {
        readonly Transform root, wheel, needle;
        float spin;

        public PumpView(Transform parent, Palette pal, MaterialLibrary mats)
        {
            root = new GameObject("Pump").transform;
            root.SetParent(parent, false);
            wheel = Part("Wheel", SharedMeshes.PumpWheel(pal), mats.VertexColored, new Vector3(0.74f, 0.75f, 0f));
            needle = Part("Needle", SharedMeshes.Needle(pal), mats.VertexColored, new Vector3(0f, 0.75f, 0.56f));
        }

        Transform Part(string name, Mesh mesh, Material mat, Vector3 pos)
        {
            var g = new GameObject(name);
            g.transform.SetParent(root, false);
            g.transform.localPosition = pos;
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = g.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.lightProbeUsage = LightProbeUsage.Off;
            return g.transform;
        }

        public void Place(Vector3 position) => root.localPosition = position;

        public void Tick(float dt, float time, bool pumping)
        {
            spin += dt * (pumping ? 12f : 1.2f);
            wheel.localRotation = Quaternion.Euler(spin * Mathf.Rad2Deg, 0f, 0f);
            float angle = pumping ? -0.9f + Mathf.Sin(time * 20f) * 0.15f : 0.6f;
            needle.localRotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg);
        }
    }
}
