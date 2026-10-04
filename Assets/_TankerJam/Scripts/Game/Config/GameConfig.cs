using UnityEngine;

namespace TankerJam.Game
{
    /// <summary>Root config asset: everything a level needs to be built and played.</summary>
    [CreateAssetMenu(menuName = "Tanker Jam/Game Config", fileName = "GameConfig")]
    public sealed class GameConfig : ScriptableObject
    {
        public Palette Palette;
        public GameTuning Tuning;
        public LayoutConfig Layout;

        [Header("Prefabs")]
        [Tooltip("Truck prefabs by length: index 0 = 2 cells, 1 = 3 cells, 2 = 4 cells. Each needs a TruckRig.")]
        public TruckRig[] TruckPrefabs = new TruckRig[3];

        [Header("Shaders")]
        public Shader ToyLit;
        public Shader ToyTransparent;
        public Shader LiquidFill;
        public Shader Decal;
        public Shader VesselLiquid;

        [Header("Textures")]
        public Texture2D DecalAtlas;

        [Header("Physics")]
        public string TruckLayer = "Trucks";

        public TruckRig TruckPrefab(int len) => TruckPrefabs[Mathf.Clamp(len - 2, 0, TruckPrefabs.Length - 1)];
    }
}
