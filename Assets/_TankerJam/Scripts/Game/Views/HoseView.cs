using UnityEngine;
using UnityEngine.Rendering;

namespace TankerJam.Game
{
    /// <summary>
    /// The hose above one bay. Extends down to a parked truck's hatch, retracts when the truck is full,
    /// and shows a thin colored stream from the nozzle to the liquid while a unit is pumping.
    /// </summary>
    public sealed class HoseView
    {
        readonly Transform hose, nozzle, stream;
        readonly MeshRenderer streamRenderer;
        float x, z, top, idleBottom;

        /// <summary>0 = retracted, 1 = fully down on the truck.</summary>
        public float Extension;

        public HoseView(Transform parent, int index, Palette pal, MaterialLibrary mats)
        {
            var root = new GameObject($"Hose{index}").transform;
            root.SetParent(parent, false);
            hose = Part(root, "Hose", SharedMeshes.ThinCylinder, mats.Plain(pal.Hose), out _);
            nozzle = Part(root, "Nozzle", SharedMeshes.Box, mats.Plain(pal.MetalDark), out _);
            nozzle.localScale = new Vector3(0.16f, 0.12f, 0.16f);
            stream = Part(root, "Stream", SharedMeshes.ThinCylinder, mats.Plain(Color.white), out streamRenderer);
            stream.gameObject.SetActive(false);
        }

        static Transform Part(Transform parent, string name, Mesh mesh, Material mat, out MeshRenderer renderer)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            renderer = g.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            return g.transform;
        }

        public void Place(float bayX, float hoseZ, float headerY)
        {
            x = bayX;
            z = hoseZ;
            top = headerY - 0.12f;
            idleBottom = headerY - 0.5f;
            Extension = 0f;
            stream.gameObject.SetActive(false);
            Apply(-1f);
        }

        public float NozzleY { get; private set; }

        /// <param name="targetY">Hatch height of the truck below, or a negative value when the bay is empty.</param>
        public void Apply(float targetY)
        {
            float bottom = targetY >= 0f ? targetY : idleBottom;
            float len = 0.3f + (top - bottom - 0.3f) * Extension;
            hose.localPosition = new Vector3(x, top - len / 2f, z);
            hose.localScale = new Vector3(0.055f, len, 0.055f);
            NozzleY = top - len - 0.04f;
            nozzle.localPosition = new Vector3(x, NozzleY, z);
        }

        public void ShowStream(bool on, Material oil, float surfaceY, float time)
        {
            if (!on)
            {
                if (stream.gameObject.activeSelf) stream.gameObject.SetActive(false);
                return;
            }
            if (!stream.gameObject.activeSelf) stream.gameObject.SetActive(true);
            streamRenderer.sharedMaterial = oil;
            float a = NozzleY - 0.06f;
            float h = Mathf.Max(0.01f, a - surfaceY);
            stream.localPosition = new Vector3(x + Mathf.Sin(time * 40f) * 0.008f, (a + surfaceY) / 2f, z);
            stream.localScale = new Vector3(0.045f, h, 0.045f);
        }
    }
}
