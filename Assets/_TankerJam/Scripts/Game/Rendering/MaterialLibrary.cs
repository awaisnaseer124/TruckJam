// Shared materials, created once per run: one per oil color per role, so every truck, vessel layer and
// flow blob of a color shares a material (SRP Batcher friendly). Per-object values go through
// MaterialPropertyBlocks, never new materials.
using System.Collections.Generic;
using UnityEngine;

namespace TankerJam.Game
{
    public sealed class MaterialLibrary
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        static readonly int SpecId = Shader.PropertyToID("_SpecStrength");
        static readonly int EmissionId = Shader.PropertyToID("_Emission");
        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

        readonly GameConfig config;
        readonly Dictionary<char, Material> body = new Dictionary<char, Material>();
        readonly Dictionary<char, Material> shell = new Dictionary<char, Material>();
        readonly Dictionary<char, Material> liquid = new Dictionary<char, Material>();
        readonly Dictionary<char, Material> oil = new Dictionary<char, Material>();
        readonly Dictionary<char, Material> blob = new Dictionary<char, Material>();
        readonly Dictionary<Color, Material> plain = new Dictionary<Color, Material>();

        public Material VertexColored { get; }
        public Material Glass { get; }
        public Material Decals { get; }
        /// <summary>All of a vessel's oil layers in one draw (colors come from a property block).</summary>
        public Material VesselLiquid { get; }

        public MaterialLibrary(GameConfig config)
        {
            this.config = config;
            VertexColored = Make(config.ToyLit, "VertexColored", Color.white, 0.45f, 0.2f);
            // Front faces only, and faint: blending happens in linear space, where a white veil brightens far
            // more than in the prototype's gamma-space blending. The fresnel rim carries the "glass" read.
            Glass = MakeTransparent("Glass", new Color(1f, 1f, 1f, 0.07f), 0.95f, CullMode.Back);
            Glass.SetFloat("_Rim", 0.35f);
            Decals = new Material(config.Decal) { name = "Decals", enableInstancing = true };
            VesselLiquid = new Material(config.VesselLiquid) { name = "VesselLiquid" };
            if (config.DecalAtlas != null) Decals.SetTexture(BaseMapId, config.DecalAtlas);
        }

        enum CullMode { Off = 0, Back = 2 }

        Material Make(Shader shader, string name, Color color, float smoothness, float spec, float emission = 0f)
        {
            var m = new Material(shader) { name = name, enableInstancing = true };
            m.SetColor(BaseColorId, color);
            m.SetFloat(SmoothnessId, smoothness);
            if (m.HasProperty(SpecId)) m.SetFloat(SpecId, spec);
            if (m.HasProperty(EmissionId)) m.SetFloat(EmissionId, emission);
            return m;
        }

        Material MakeTransparent(string name, Color color, float smoothness, CullMode cull)
        {
            var m = Make(config.ToyTransparent, name, color, smoothness, 0.5f);
            m.SetFloat("_Cull", (float)cull);
            return m;
        }

        Color Oil(char key) => config.Palette.OilColorOf(key);

        /// <summary>Truck body: vertex colored, tinted parts take the oil color.</summary>
        public Material Body(char key) => Get(body, key, k => Make(config.ToyLit, $"Body_{k}", Oil(k), 0.55f, 0.25f));

        /// <summary>See-through tank shell in a lighter tint of the oil color (~60% opacity).</summary>
        public Material Shell(char key) => Get(shell, key, k =>
        {
            var c = Color.Lerp(Oil(k), Color.white, 0.35f);
            c.a = 0.6f;
            return MakeTransparent($"Shell_{k}", c, 0.9f, CullMode.Back);
        });

        public Material Liquid(char key) => Get(liquid, key, k =>
        {
            var m = Make(config.LiquidFill, $"Liquid_{k}", Oil(k), 0.8f, 0.35f);
            return m;
        });

        /// <summary>Plain oil color for vessel layers and flow blobs.</summary>
        public Material OilSurface(char key) => Get(oil, key, k => Make(config.ToyLit, $"Oil_{k}", Oil(k), 0.78f, 0.35f, 0.08f));

        /// <summary>Flow blobs: a slightly glowing oil color, instancing on (drawn with RenderMeshInstanced).</summary>
        public Material Blob(char key) => Get(blob, key, k => Make(config.ToyLit, $"Blob_{k}", Oil(k), 0.8f, 0.3f, 0.3f));

        /// <summary>Flat-colored lit material (props that don't use vertex colors).</summary>
        public Material Plain(Color color)
        {
            if (!plain.TryGetValue(color, out var m))
            {
                m = Make(config.ToyLit, "Plain", color, 0.45f, 0.2f);
                plain[color] = m;
            }
            return m;
        }

        static Material Get(Dictionary<char, Material> cache, char key, System.Func<char, Material> make)
        {
            if (!cache.TryGetValue(key, out var m))
            {
                m = make(key);
                cache[key] = m;
            }
            return m;
        }

        public void Dispose()
        {
            void Kill(IEnumerable<Material> ms) { foreach (var m in ms) Object.Destroy(m); }
            Kill(body.Values); Kill(shell.Values); Kill(liquid.Values); Kill(oil.Values); Kill(blob.Values); Kill(plain.Values);
            Object.Destroy(VertexColored); Object.Destroy(Glass); Object.Destroy(Decals); Object.Destroy(VesselLiquid);
        }
    }
}
