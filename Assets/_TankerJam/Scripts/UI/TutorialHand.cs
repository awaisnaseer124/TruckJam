using DG.Tweening;
using TMPro;
using UnityEngine;

namespace TankerJam.UI
{
    /// <summary>
    /// Pointing hand with a hint bubble. Points at a screen position (a truck) or a UI element (a booster).
    /// Taps pass through it (no raycast target), so the player taps the real thing underneath.
    /// </summary>
    public sealed class TutorialHand : MonoBehaviour
    {
        [SerializeField] RectTransform hand;
        [SerializeField] RectTransform bubble;
        [SerializeField] TMP_Text bubbleLabel;
        Canvas canvas;

        Tween bob;
        RectTransform target;
        Camera worldCamera;
        Vector3 worldPoint;
        bool followWorld;

#if UNITY_EDITOR
        public void EditorWire(RectTransform h, RectTransform b, TMP_Text label)
        {
            hand = h; bubble = b; bubbleLabel = label;
        }
#endif

        void Awake()
        {
            canvas = GetComponentInParent<Canvas>().rootCanvas;
            bob = hand.DOScale(0.85f, 0.45f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetAutoKill(false).SetUpdate(true).SetLink(gameObject).Pause();
            gameObject.SetActive(false);
        }

        void OnDestroy() => bob?.Kill();

        public bool IsShowing => gameObject.activeSelf;

        public void PointAtUi(RectTransform element, string hint)
        {
            target = element;
            followWorld = false;
            Show(hint);
        }

        public void PointAtWorld(Camera cam, Vector3 world, string hint)
        {
            target = null;
            worldCamera = cam;
            worldPoint = world;
            followWorld = true;
            Show(hint);
        }

        void Show(string hint)
        {
            bubbleLabel.text = hint;
            gameObject.SetActive(true);
            bob.Restart();
            LateUpdate();
        }

        public void Hide()
        {
            bob.Pause();
            gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            var root = (RectTransform)canvas.transform;
            Vector2 screen;
            if (followWorld && worldCamera != null) screen = worldCamera.WorldToScreenPoint(worldPoint);
            else if (target != null) screen = RectTransformUtility.WorldToScreenPoint(null, target.TransformPoint(target.rect.center));
            else return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out var local);
            hand.anchoredPosition = local;
            // Bubble clear of the target: above it in the lower half of the screen, below it in the upper half,
            // clamped inside the screen.
            float half = root.rect.width / 2f - bubble.rect.width / 2f - 24f;
            float gap = hand.rect.height / 2f + bubble.rect.height / 2f + (target != null ? 110f : 40f);
            float y = local.y < 0f ? local.y + gap : local.y - gap;
            bubble.anchoredPosition = new Vector2(Mathf.Clamp(local.x, -half, half), y);
        }
    }
}
