using System;
using UnityEngine;

namespace TankerJam.Game
{
    /// <summary>Oil color keys used by levels (P, Y, C, V, G) mapped to colors, plus the scene's neutral colors.</summary>
    [CreateAssetMenu(menuName = "Tanker Jam/Palette", fileName = "Palette")]
    public sealed class Palette : ScriptableObject
    {
        [Serializable]
        public struct OilColor
        {
            public string Key;
            public string DisplayName;
            public Color Color;
        }

        public OilColor[] Oils =
        {
            new OilColor { Key = "P", DisplayName = "pink", Color = Hex("#FF4FA8") },
            new OilColor { Key = "Y", DisplayName = "yellow", Color = Hex("#FFC21F") },
            new OilColor { Key = "C", DisplayName = "cyan", Color = Hex("#1EC8F2") },
            new OilColor { Key = "V", DisplayName = "purple", Color = Hex("#9A5BFF") },
            new OilColor { Key = "G", DisplayName = "green", Color = Hex("#3DDC84") },
        };

        [Header("Scene")]
        public Color Sky = Hex("#D9E7F2");
        public Color Ground = Hex("#E9F0F6");
        public Color LotLight = Hex("#C9D4E0");
        public Color LotDark = Hex("#C1CDDA");
        public Color Road = Hex("#6E7889");
        public Color BayPad = Hex("#8E99AA");
        public Color Vip = Hex("#FFB21A");
        public Color ExtraLocked = Hex("#36D17A");

        [Header("Props")]
        public Color Chassis = Hex("#3A4152");
        public Color Tire = Hex("#262B36");
        public Color Hub = Hex("#E6ECF2");
        public Color Window = Hex("#BFE9FF");
        public Color Metal = Hex("#C7D3DF");
        public Color MetalDark = Hex("#7D8CA0");
        public Color Pipe = Hex("#A9B8C9");
        public Color Hose = Hex("#2B3240");
        public Color PumpBlue = Hex("#2EA8FF");
        public Color PumpBlueDark = Hex("#1C7FC4");
        public Color TapYellow = Hex("#FFC21F");
        public Color Cone = Hex("#FF7A1A");
        public Color Needle = Hex("#FF3D6E");

        [Header("Lighting")]
        public Color HemiSky = Color.white;
        public Color HemiGround = Hex("#A8B6C8");
        [Range(0, 2)] public float HemiIntensity = 0.72f;
        [Range(0, 2)] public float SunIntensity = 0.85f;

        public int IndexOf(char key)
        {
            for (int i = 0; i < Oils.Length; i++)
                if (Oils[i].Key.Length > 0 && Oils[i].Key[0] == key) return i;
            return -1;
        }

        public Color OilColorOf(char key)
        {
            int i = IndexOf(key);
            return i >= 0 ? Oils[i].Color : Color.magenta;
        }

        public string NameOf(char key)
        {
            int i = IndexOf(key);
            return i >= 0 ? Oils[i].DisplayName : key.ToString();
        }

        /// <summary>Parses #RRGGBB in managed code (safe in field initializers, unlike ColorUtility).</summary>
        public static Color Hex(string hex)
        {
            int v = Convert.ToInt32(hex.TrimStart('#'), 16);
            return new Color(((v >> 16) & 255) / 255f, ((v >> 8) & 255) / 255f, (v & 255) / 255f, 1f);
        }
    }
}
