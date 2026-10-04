using UnityEngine;

namespace TankerJam.Game
{
    /// <summary>
    /// UV layout of the decal atlas (512 x 256), shared by the editor baker that draws it and the runtime
    /// meshes that sample it. Left column: roof arrow. Top row: capacity badges 2/4/6. Bottom row: "VIP".
    /// </summary>
    public static class DecalAtlasLayout
    {
        public const int Width = 512, Height = 256;

        public static readonly Rect Arrow = new Rect(0f, 0f, 0.25f, 1f);
        public static readonly Rect Vip = new Rect(0.25f, 0f, 0.75f, 0.5f);

        public static Rect Badge(int capacity)
        {
            int slot = capacity == 2 ? 0 : capacity == 4 ? 1 : 2;
            return new Rect(0.25f + slot * 0.25f, 0.5f, 0.25f, 0.5f);
        }
    }
}
