using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TankerJam.UI
{
    /// <summary>
    /// A booster in the bottom bar: red count badge while the player owns some, a coin price tag at zero,
    /// and a pulsing outline while armed (VIP waiting for a truck tap).
    /// </summary>
    public sealed class BoosterButton : MonoBehaviour
    {
        [SerializeField] Button button;
        [SerializeField] GameObject countBadge;
        [SerializeField] TMP_Text countLabel;
        [SerializeField] GameObject priceTag;
        [SerializeField] TMP_Text priceLabel;
        [SerializeField] Graphic armedOutline;
        [SerializeField] CanvasGroup group;

        public event Action Clicked;

        Tween pulse;

#if UNITY_EDITOR
        public void EditorWire(Button b, GameObject badge, TMP_Text count, GameObject tag, TMP_Text price, Graphic outline, CanvasGroup cg)
        {
            button = b; countBadge = badge; countLabel = count; priceTag = tag; priceLabel = price; armedOutline = outline; group = cg;
        }
#endif

        void Awake()
        {
            button.onClick.AddListener(() => Clicked?.Invoke());
            armedOutline.gameObject.SetActive(false);
            pulse = armedOutline.transform.DOScale(1.06f, 0.5f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine)
                                .SetAutoKill(false).SetUpdate(true).SetLink(gameObject).Pause();
        }

        void OnDestroy() => pulse?.Kill();

        /// <param name="count">Boosters owned.</param>
        /// <param name="price">Coin price shown when count is zero.</param>
        /// <param name="usable">Whether pressing does anything right now (dims otherwise).</param>
        public void Show(int count, int price, bool usable, bool armed)
        {
            countBadge.SetActive(count > 0);
            countLabel.SetText("{0}", count);
            priceTag.SetActive(count == 0);
            priceLabel.SetText("{0}", price);
            group.alpha = usable || armed ? 1f : 0.55f;
            if (armed != armedOutline.gameObject.activeSelf)
            {
                armedOutline.gameObject.SetActive(armed);
                if (armed) pulse.Restart();
                else pulse.Pause();
            }
        }

        public RectTransform Rect => (RectTransform)transform;
    }
}
