using UnityEngine;

namespace TankerJam.Game
{
    /// <summary>Small procedural meshes shared by many objects (built once per run).</summary>
    public static class SharedMeshes
    {
        static Mesh unitCylinder, thinCylinder, box, pumpWheel, needle;

        /// <summary>Radius 1, height 1, centered; scale X/Z by radius and Y by height.</summary>
        public static Mesh UnitCylinder => unitCylinder ? unitCylinder : unitCylinder = Build(k =>
            k.Cylinder(Matrix4x4.identity, 1f, 1f, 1f, 32, MeshKit.Tint), "UnitCylinder");

        /// <summary>Like UnitCylinder with fewer sides, for hoses and streams.</summary>
        public static Mesh ThinCylinder => thinCylinder ? thinCylinder : thinCylinder = Build(k =>
            k.Cylinder(Matrix4x4.identity, 1f, 1f, 1f, 10, MeshKit.Tint), "ThinCylinder");

        public static Mesh Box => box ? box : box = Build(k => k.Box(Vector3.zero, Vector3.one, MeshKit.Tint), "Box");

        /// <summary>Pump hand wheel: yellow torus with three spokes, in the local YZ plane (spins around X).</summary>
        public static Mesh PumpWheel(Palette pal)
        {
            if (pumpWheel) return pumpWheel;
            return pumpWheel = Build(k =>
            {
                var c = MeshKit.Fixed(pal.TapYellow);
                k.Torus(Matrix4x4.Rotate(Quaternion.Euler(0, 0, 90f)), 0.32f, 0.06f, 24, 8, c);
                for (int i = 0; i < 3; i++)
                    k.Box(Matrix4x4.Rotate(Quaternion.Euler(i * 60f, 0, 0)), new Vector3(0.05f, 0.6f, 0.05f), c);
            }, "PumpWheel");
        }

        /// <summary>Gauge needle pivoting at its base (local origin), pointing up.</summary>
        public static Mesh Needle(Palette pal)
        {
            if (needle) return needle;
            return needle = Build(k => k.Box(new Vector3(0, 0.08f, 0), new Vector3(0.03f, 0.16f, 0.02f), MeshKit.Fixed(pal.Needle)), "Needle");
        }

        static Mesh Build(System.Action<MeshKit> fill, string name)
        {
            var k = new MeshKit();
            fill(k);
            var m = k.ToMesh(name);
            m.hideFlags = HideFlags.DontSave;
            return m;
        }
    }
}
