// Menu: Tanker Jam > Setup > Build UI Prefabs.
// Generates the uGUI prefabs (HUD, ResultPopup, HomeOverlay, TutorialHand) from the UiTheme, laid out for a
// 1080x1920 reference canvas. This bootstraps the UI; after the first build, edit the prefabs directly
// (re-running overwrites them).
using TankerJam.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TankerJam.EditorTools
{
    public static class UiBuilder
    {
        public const string Dir = "Assets/_TankerJam/Prefabs/UI";
        static UiTheme T;

        [MenuItem("Tanker Jam/Setup/Build UI Prefabs")]
        public static void BuildAll()
        {
            System.IO.Directory.CreateDirectory(Dir);
            T = UiSpriteBaker.BuildAll();
            Save(BuildHud(), "HUD");
            Save(BuildResultPopup(), "ResultPopup");
            Save(BuildHome(), "HomeOverlay");
            Save(BuildTutorial(), "TutorialHand");
            AssetDatabase.SaveAssets();
            Debug.Log("Tanker Jam: UI prefabs built in " + Dir);
        }

        // ---------------- HUD ----------------

        static GameObject BuildHud()
        {
            var root = Stretch(null, "HUD");
            root.gameObject.AddComponent<TankerJam.UI.SafeArea>();
            var hud = root.gameObject.AddComponent<Hud>();

            var top = Rect(root, "TopBar", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -10), new Vector2(0, 150));
            var retry = MakeButton(top, "Retry", new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, 0), new Vector2(130, 130), T.Accent, T.AccentShadow);
            Icon(retry.Face, T.IconRetry, Color.white, 80);
            var sound = MakeButton(top, "Sound", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-30, 0), new Vector2(130, 130), T.Accent, T.AccentShadow);
            var soundIcon = Icon(sound.Face, T.IconSoundOn, Color.white, 80);

            var level = Label(top, "Level", "LEVEL 1", 64, T.Ink, T.TitleFont, TextAlignmentOptions.Center, true);
            Place(level.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -10), new Vector2(560, 90), new Vector2(0.5f, 1));
            var hard = Rect(top, "HardChip", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -100), new Vector2(150, 50));
            Img(hard, T.Rounded, T.Badge);
            Fill(Label(hard, "Text", "HARD", 30, Color.white, T.BodyFont, TextAlignmentOptions.Center, true).rectTransform);

            var coins = Rect(root, "Coins", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -175), new Vector2(230, 72));
            BorderedPanel(coins);
            var coinIcon = Icon(coins, T.IconCoin, T.Gold, 52);
            coinIcon.rectTransform.anchorMin = coinIcon.rectTransform.anchorMax = new Vector2(0, 0.5f);
            coinIcon.rectTransform.anchoredPosition = new Vector2(44, 0);
            var coinLabel = Label(coins, "Amount", "0", 40, T.Ink, T.BodyFont, TextAlignmentOptions.Left, true);
            Place(coinLabel.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(84, 0), new Vector2(140, 60), new Vector2(0, 0.5f));

            var status = Rect(root, "Status", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 250), new Vector2(940, 110));
            BorderedPanel(status);
            var statusGroup = status.gameObject.AddComponent<CanvasGroup>();
            statusGroup.blocksRaycasts = false;
            statusGroup.interactable = false;
            var statusText = Label(status, "Text", "", 34, T.Ink, T.BodyFont, TextAlignmentOptions.Center, false);
            Fill(statusText.rectTransform, 24, 10);
            statusText.enableAutoSizing = true;
            statusText.fontSizeMin = 22;
            statusText.fontSizeMax = 34;
            statusText.textWrappingMode = TextWrappingModes.Normal;

            var vip = MakeBooster(root, "VipBooster", new Vector2(-175, 36), T.Gold, T.GoldShadow, T.IconCrown, T.GoldInk, "VIP");
            var extra = MakeBooster(root, "ExtraBooster", new Vector2(175, 36), T.Accent, T.AccentShadow, T.IconPlus, Color.white, "Extra bay");

            hud.EditorWire(T, retry.Button, sound.Button, soundIcon, level, hard.gameObject, coinLabel, statusGroup, statusText, vip, extra);
            return root.gameObject;
        }

        static BoosterButton MakeBooster(RectTransform parent, string name, Vector2 pos, Color face, Color shadow, Sprite icon, Color ink, string text)
        {
            var b = MakeButton(parent, name, new Vector2(0.5f, 0), new Vector2(0.5f, 0), pos, new Vector2(310, 170), face, shadow, withOutline: true);
            var ic = Icon(b.Face, icon, ink, 70);
            ic.rectTransform.anchoredPosition = new Vector2(0, 26);
            var label = Label(b.Face, "Label", text, 38, ink, T.BodyFont, TextAlignmentOptions.Center, true);
            Place(label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -44), new Vector2(290, 56));

            var badge = Rect(b.Root, "Badge", new Vector2(1, 1), new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-14, -10), new Vector2(76, 76));
            Img(badge, T.Circle, T.Badge);
            var count = Label(badge, "Count", "2", 40, Color.white, T.BodyFont, TextAlignmentOptions.Center, true);
            Fill(count.rectTransform);

            var tag = Rect(b.Root, "Price", new Vector2(1, 1), new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-40, -6), new Vector2(170, 62));
            BorderedPanel(tag);
            var tagIcon = Icon(tag, T.IconCoin, T.Gold, 40);
            tagIcon.rectTransform.anchorMin = tagIcon.rectTransform.anchorMax = new Vector2(0, 0.5f);
            tagIcon.rectTransform.anchoredPosition = new Vector2(32, 0);
            var price = Label(tag, "Amount", "900", 32, T.Ink, T.BodyFont, TextAlignmentOptions.Left, true);
            Place(price.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(60, 0), new Vector2(100, 50), new Vector2(0, 0.5f));

            var bb = b.Root.gameObject.AddComponent<BoosterButton>();
            var group = b.Root.gameObject.AddComponent<CanvasGroup>();
            bb.EditorWire(b.Button, badge.gameObject, count, tag.gameObject, price, b.Outline, group);
            return bb;
        }

        // ---------------- result popup ----------------

        static GameObject BuildResultPopup()
        {
            var root = Stretch(null, "ResultPopup");
            Img(root, null, T.Dim, raycast: true);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            var popup = root.gameObject.AddComponent<ResultPopup>();

            var card = Rect(root, "Card", Center, Center, Center, Vector2.zero, new Vector2(880, 1000));
            Img(card, T.Rounded, T.Panel, raycast: true);

            var title = Label(card, "Title", "LEVEL CLEAR!", 72, T.Ink, T.TitleFont, TextAlignmentOptions.Center, true);
            Place(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -50), new Vector2(820, 110), new Vector2(0.5f, 1));

            var starsRow = Rect(card, "Stars", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -170), new Vector2(600, 170));
            var stars = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                stars[i] = Icon(starsRow, T.IconStar, T.StarOn, i == 1 ? 160 : 130);
                stars[i].rectTransform.anchoredPosition = new Vector2((i - 1) * 180, i == 1 ? 10 : -10);
            }

            var body = Label(card, "Body", "", 38, T.Muted, T.BodyFont, TextAlignmentOptions.Center, false);
            Place(body.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -350), new Vector2(760, 170), new Vector2(0.5f, 1));
            body.textWrappingMode = TextWrappingModes.Normal;

            var reward = Rect(card, "Reward", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -530), new Vector2(360, 90));
            var rewardIcon = Icon(reward, T.IconCoin, T.Gold, 70);
            rewardIcon.rectTransform.anchoredPosition = new Vector2(-80, 0);
            var rewardLabel = Label(reward, "Amount", "+40", 56, T.Ink, T.BodyFont, TextAlignmentOptions.Left, true);
            Place(rewardLabel.rectTransform, Center, Center, new Vector2(70, 0), new Vector2(200, 80));

            var column = Rect(card, "Buttons", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(740, 440));
            var layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.LowerCenter;
            layout.spacing = 22;
            layout.childControlHeight = layout.childControlWidth = false;
            layout.childForceExpandHeight = layout.childForceExpandWidth = false;
            var buttons = new Button[3];
            for (int i = 0; i < 3; i++)
            {
                var b = Rect(column, $"Button{i}", Center, Center, Center, Vector2.zero, new Vector2(720, 128));
                var img = Img(b, T.Rounded, T.Accent, raycast: true);
                buttons[i] = b.gameObject.AddComponent<Button>();
                buttons[i].targetGraphic = img;
                b.gameObject.AddComponent<PressFeedback>();
                Fill(Label(b, "Label", "Next", 44, Color.white, T.BodyFont, TextAlignmentOptions.Center, true).rectTransform);
            }

            popup.EditorWire(T, group, card, title, body, starsRow.gameObject, stars, reward.gameObject, rewardLabel, buttons);
            return root.gameObject;
        }

        // ---------------- home ----------------

        static GameObject BuildHome()
        {
            var root = Stretch(null, "HomeOverlay");
            var bg = T.Sky;
            bg.a = 0.88f;
            Img(root, null, bg, raycast: true);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            var home = root.gameObject.AddComponent<HomeOverlay>();

            var title = Label(root, "Title", "TANKER\nJAM", 170, T.Ink, T.TitleFont, TextAlignmentOptions.Center, true);
            Place(title.rectTransform, Center, Center, new Vector2(0, 470), new Vector2(980, 460));
            title.lineSpacing = -20;

            var level = Label(root, "Level", "LEVEL 1", 72, T.Accent, T.TitleFont, TextAlignmentOptions.Center, true);
            Place(level.rectTransform, Center, Center, new Vector2(0, 120), new Vector2(800, 100));

            var play = MakeButton(root, "Play", Center, Center, new Vector2(0, -130), new Vector2(620, 200), T.Accent, T.AccentShadow);
            Fill(Label(play.Face, "Label", "PLAY", 88, Color.white, T.TitleFont, TextAlignmentOptions.Center, true).rectTransform);

            var coins = Rect(root, "Coins", Center, Center, Center, new Vector2(0, -380), new Vector2(260, 84));
            BorderedPanel(coins);
            var coinIcon = Icon(coins, T.IconCoin, T.Gold, 60);
            coinIcon.rectTransform.anchoredPosition = new Vector2(-70, 0);
            var coinLabel = Label(coins, "Amount", "0", 46, T.Ink, T.BodyFont, TextAlignmentOptions.Left, true);
            Place(coinLabel.rectTransform, Center, Center, new Vector2(40, 0), new Vector2(150, 70));

            home.EditorWire(group, level, coinLabel, play.Button);
            return root.gameObject;
        }

        // ---------------- tutorial ----------------

        static GameObject BuildTutorial()
        {
            var root = Stretch(null, "TutorialHand");
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            var tut = root.gameObject.AddComponent<TutorialHand>();

            var bubble = Rect(root, "Bubble", Center, Center, Center, Vector2.zero, new Vector2(700, 180));
            BorderedPanel(bubble);
            var text = Label(bubble, "Text", "Tap a truck", 36, T.Ink, T.BodyFont, TextAlignmentOptions.Center, false);
            Fill(text.rectTransform, 28, 14);
            text.textWrappingMode = TextWrappingModes.Normal;

            var hand = Rect(root, "Hand", Center, Center, Center, Vector2.zero, new Vector2(150, 150));
            Img(hand, T.IconHand, T.Accent);

            tut.EditorWire(hand, bubble, text);
            return root.gameObject;
        }

        // ---------------- helpers ----------------

        static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

        struct ButtonParts
        {
            public RectTransform Root, Face;
            public Button Button;
            public Image Outline;
        }

        /// <summary>Rounded button with a darker "depth" shadow below it, press feedback, optional armed outline.</summary>
        static ButtonParts MakeButton(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size,
                                      Color face, Color shadow, bool withOutline = false)
        {
            var pivot = new Vector2(anchorMin.x == anchorMax.x ? anchorMin.x : 0.5f, anchorMin.y == anchorMax.y ? anchorMin.y : 0.5f);
            if (pivot.x == 0.5f && pivot.y == 0f) pivot = new Vector2(0.5f, 0f);
            var root = Rect(parent, name, anchorMin, anchorMax, pivot, pos, size);
            Image outline = null;
            if (withOutline)
            {
                var o = Rect(root, "Armed", Center, Center, Center, new Vector2(0, -4), size + new Vector2(28, 36));
                outline = Img(o, T.Rounded, Color.white);
            }
            var sh = Rect(root, "Shadow", Center, Center, Center, new Vector2(0, -10), size);
            Img(sh, T.Rounded, shadow);
            var f = Rect(root, "Face", Center, Center, Center, Vector2.zero, size);
            var img = Img(f, T.Rounded, face, raycast: true);
            var button = f.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            var colors = button.colors;
            colors.pressedColor = new Color(0.9f, 0.9f, 0.9f);
            button.colors = colors;
            root.gameObject.AddComponent<PressFeedback>();
            return new ButtonParts { Root = root, Face = f, Button = button, Outline = outline };
        }

        static RectTransform Stretch(RectTransform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            if (parent != null) rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        static RectTransform Rect(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        static void Place(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size, Vector2? pivot = null)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        static void Fill(RectTransform rt, float padX = 0, float padY = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = Center;
            rt.offsetMin = new Vector2(padX, padY);
            rt.offsetMax = new Vector2(-padX, -padY);
        }

        /// <summary>White rounded panel with a thin line-colored border, readable on the light ground.</summary>
        static void BorderedPanel(RectTransform rt)
        {
            Img(rt, T.Rounded, T.Line);
            var fill = Rect(rt, "Fill", Center, Center, Center, Vector2.zero, Vector2.zero);
            Fill(fill, 4, 4);
            Img(fill, T.Rounded, T.Panel);
            fill.SetAsFirstSibling();
        }

        static Image Img(RectTransform rt, Sprite sprite, Color color, bool raycast = false)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = raycast;
            if (sprite != null && sprite.border != Vector4.zero) img.type = Image.Type.Sliced;
            return img;
        }

        static Image Icon(RectTransform parent, Sprite sprite, Color color, float size)
        {
            var rt = Rect(parent, "Icon", Center, Center, Center, Vector2.zero, new Vector2(size, size));
            var img = Img(rt, sprite, color);
            img.preserveAspect = true;
            return img;
        }

        static TextMeshProUGUI Label(RectTransform parent, string name, string text, float size, Color color, TMP_FontAsset font,
                                     TextAlignmentOptions align, bool bold)
        {
            var rt = Rect(parent, name, Center, Center, Center, Vector2.zero, new Vector2(200, 60));
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            if (font != null) t.font = font;
            t.alignment = align;
            t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        static void Save(GameObject go, string name)
        {
            PrefabUtility.SaveAsPrefabAsset(go, $"{Dir}/{name}.prefab");
            Object.DestroyImmediate(go);
        }
    }
}
