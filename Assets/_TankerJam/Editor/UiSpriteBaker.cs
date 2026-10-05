// Generates the UI sprites (white, tinted by Image.color) as PNGs from signed shapes, and the UiTheme asset.
// Placeholder art with the right shapes and 9-slice borders; replace PNGs with final art at the same paths.
using System;
using System.Collections.Generic;
using System.IO;
using TankerJam.UI;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace TankerJam.EditorTools
{
    public static class UiSpriteBaker
    {
        const string Dir = "Assets/_TankerJam/Art/UI";
        public const string ThemePath = "Assets/_TankerJam/Config/UiTheme.asset";
        const string DefaultFont = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
        const int Size = 128, SS = 4;

        public static UiTheme BuildAll()
        {
            Directory.CreateDirectory(Dir);
            var theme = AssetDatabase.LoadAssetAtPath<UiTheme>(ThemePath);
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<UiTheme>();
                AssetDatabase.CreateAsset(theme, ThemePath);
            }

            theme.Rounded = Bake("rounded", (x, y) => RoundedRect(x, y, 64, 64, 60, 60, 40), border: 48);
            theme.Circle = Bake("circle", (x, y) => Circle(x, y, 64, 64, 60));
            theme.Ring = Bake("ring", (x, y) => Math.Min(Circle(x, y, 64, 64, 60), -Circle(x, y, 64, 64, 48)));
            theme.IconRetry = Bake("icon_retry", Retry);
            theme.IconSoundOn = Bake("icon_sound_on", (x, y) => Math.Max(Speaker(x, y), Math.Max(Arc(x, y, 62, 64, 22, -50, 50, 7), Arc(x, y, 62, 64, 40, -50, 50, 7))));
            theme.IconSoundOff = Bake("icon_sound_off", (x, y) => Math.Max(Speaker(x, y), Math.Max(Capsule(x, y, 78, 46, 110, 82, 7), Capsule(x, y, 78, 82, 110, 46, 7))));
            theme.IconCrown = Bake("icon_crown", (x, y) => Polygon(x, y, Scale24(3, 7, 7, 11, 12, 5, 17, 11, 21, 7, 19, 18, 5, 18)));
            theme.IconPlus = Bake("icon_plus", (x, y) => Math.Max(Capsule(x, y, 64, 24, 64, 104, 12), Capsule(x, y, 24, 64, 104, 64, 12)));
            theme.IconStar = Bake("icon_star", (x, y) => Polygon(x, y, Star(64, 66, 60, 26)));
            theme.IconCoin = Bake("icon_coin", (x, y) => Math.Max(Math.Min(Circle(x, y, 64, 64, 58), -Circle(x, y, 64, 64, 44)), Circle(x, y, 64, 64, 32)));
            theme.IconHand = Bake("icon_hand", (x, y) => Math.Max(Circle(x, y, 64, 64, 30), Math.Min(Circle(x, y, 64, 64, 60), -Circle(x, y, 64, 64, 48))));

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DefaultFont);
            if (theme.TitleFont == null) theme.TitleFont = font;
            if (theme.BodyFont == null) theme.BodyFont = font;
            EditorUtility.SetDirty(theme);
            AssetDatabase.SaveAssets();
            return theme;
        }

        // ---------------- shapes (signed: > 0 inside, in pixels) ----------------

        static float Circle(float x, float y, float cx, float cy, float r) => r - Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));

        static float RoundedRect(float x, float y, float cx, float cy, float hw, float hh, float r)
        {
            float dx = Mathf.Abs(x - cx) - (hw - r), dy = Mathf.Abs(y - cy) - (hh - r);
            float outside = Mathf.Sqrt(Mathf.Max(dx, 0) * Mathf.Max(dx, 0) + Mathf.Max(dy, 0) * Mathf.Max(dy, 0));
            float inside = Mathf.Min(Mathf.Max(dx, dy), 0);
            return r - (outside + inside);
        }

        static float Capsule(float x, float y, float ax, float ay, float bx, float by, float r)
        {
            float vx = bx - ax, vy = by - ay;
            float t = Mathf.Clamp01(((x - ax) * vx + (y - ay) * vy) / (vx * vx + vy * vy));
            float dx = x - (ax + vx * t), dy = y - (ay + vy * t);
            return r - Mathf.Sqrt(dx * dx + dy * dy);
        }

        static float Arc(float x, float y, float cx, float cy, float radius, float fromDeg, float toDeg, float half)
        {
            float a = Mathf.Atan2(y - cy, x - cx) * Mathf.Rad2Deg;
            if (a < fromDeg || a > toDeg) return -1f;
            float d = Mathf.Abs(Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - radius);
            return half - d;
        }

        static float Speaker(float x, float y) =>
            Polygon(x, y, new List<Vector2> { new Vector2(18, 48), new Vector2(40, 48), new Vector2(64, 26), new Vector2(64, 102), new Vector2(40, 80), new Vector2(18, 80) });

        static float Retry(float x, float y)
        {
            // Circular arrow: arc most of the way round, arrow head at the top-left end.
            float ring = Arc(x, y, 64, 64, 38, -150, 170, 9);
            float head = Polygon(x, y, new List<Vector2> { new Vector2(10, 74), new Vector2(46, 74), new Vector2(28, 102) });
            return Math.Max(ring, head);
        }

        static List<Vector2> Scale24(params float[] xy)
        {
            // GDD crown icon path (24x24, y down) -> 128 px, y up.
            var pts = new List<Vector2>();
            for (int i = 0; i < xy.Length; i += 2) pts.Add(new Vector2(xy[i] / 24f * 128f, 128f - xy[i + 1] / 24f * 128f));
            return pts;
        }

        static List<Vector2> Star(float cx, float cy, float outer, float inner)
        {
            var pts = new List<Vector2>();
            for (int i = 0; i < 10; i++)
            {
                float a = Mathf.PI / 2f + i * Mathf.PI / 5f;
                float r = i % 2 == 0 ? outer : inner;
                pts.Add(new Vector2(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r));
            }
            return pts;
        }

        /// <summary>Inside test for a simple polygon (returns +1 inside, -1 outside; edges get antialiased by supersampling).</summary>
        static float Polygon(float x, float y, List<Vector2> p)
        {
            bool inside = false;
            for (int i = 0, j = p.Count - 1; i < p.Count; j = i++)
                if ((p[i].y > y) != (p[j].y > y) && x < (p[j].x - p[i].x) * (y - p[i].y) / (p[j].y - p[i].y) + p[i].x)
                    inside = !inside;
            return inside ? 1f : -1f;
        }

        // ---------------- baking ----------------

        static Sprite Bake(string name, Func<float, float, float> shape, int border = 0)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            var px = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    int hits = 0;
                    for (int sy = 0; sy < SS; sy++)
                        for (int sx = 0; sx < SS; sx++)
                            if (shape(x + (sx + 0.5f) / SS, y + (sy + 0.5f) / SS) > 0f) hits++;
                    byte a = (byte)(255 * hits / (SS * SS));
                    px[y * Size + x] = new Color32(255, 255, 255, a);
                }
            tex.SetPixels32(px);
            string path = $"{Dir}/{name}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.spriteBorder = new Vector4(border, border, border, border);
            imp.spritePixelsPerUnit = 100;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
