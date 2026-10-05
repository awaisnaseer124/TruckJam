using TMPro;
using UnityEngine;

namespace TankerJam.UI
{
    /// <summary>Colors, fonts and sprites for every screen. Swap fonts or colors here, not in prefabs.</summary>
    [CreateAssetMenu(menuName = "Tanker Jam/UI Theme", fileName = "UiTheme")]
    public sealed class UiTheme : ScriptableObject
    {
        [Header("Colors (GDD)")]
        public Color Accent = Hex(0x2EA8FF);
        public Color AccentShadow = Hex(0x1C7FC4);
        public Color Gold = Hex(0xFFB21A);
        public Color GoldShadow = Hex(0xC98500);
        public Color GoldInk = Hex(0x3A2400);
        public Color Badge = Hex(0xFF3D6E);
        public Color Panel = Color.white;
        public Color Ink = Hex(0x1E2433);
        public Color Muted = Hex(0x5D6880);
        public Color Line = Hex(0xC9D6E3);
        public Color Sky = Hex(0xD9E7F2);
        public Color Dim = new Color(0.06f, 0.08f, 0.14f, 0.5f);
        public Color StarOn = Hex(0xFFC21F);
        public Color StarOff = Hex(0xD5DEE8);

        [Header("Fonts (Bungee for titles, Barlow for UI text per GDD; LiberationSans until imported)")]
        public TMP_FontAsset TitleFont;
        public TMP_FontAsset BodyFont;

        [Header("Sprites")]
        public Sprite Rounded;   // 9-sliced rounded rectangle
        public Sprite Circle;
        public Sprite Ring;
        public Sprite IconRetry, IconSoundOn, IconSoundOff, IconCrown, IconPlus, IconStar, IconCoin, IconHand;

        static Color Hex(int v) => new Color(((v >> 16) & 255) / 255f, ((v >> 8) & 255) / 255f, (v & 255) / 255f, 1f);
    }
}
