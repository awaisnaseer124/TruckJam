using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TankerJam.UI
{
    public enum PopupButtonStyle { Primary, Gold, Secondary }

    /// <summary>
    /// The end-of-level card. Win: stars, coins earned, Next. Jammed: a reason, then the unused boosters
    /// (or a coin price) before Retry. Buttons come from a small pool configured per call.
    /// </summary>
    public sealed class ResultPopup : MonoBehaviour
    {
        public readonly struct Option
        {
            public readonly string Label;
            public readonly PopupButtonStyle Style;
            public readonly Action OnClick;
            public Option(string label, PopupButtonStyle style, Action onClick) { Label = label; Style = style; OnClick = onClick; }
        }

        [SerializeField] UiTheme theme;
        [SerializeField] CanvasGroup group;
        [SerializeField] RectTransform card;
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text body;
        [SerializeField] GameObject starsRow;
        [SerializeField] Image[] stars;
        [SerializeField] GameObject rewardRow;
        [SerializeField] TMP_Text rewardLabel;
        [SerializeField] Button[] buttons;

        readonly Action[] handlers = new Action[4];
        Sequence showSeq;

#if UNITY_EDITOR
        public void EditorWire(UiTheme t, CanvasGroup g, RectTransform c, TMP_Text ti, TMP_Text b, GameObject sr, Image[] s, GameObject rr, TMP_Text rl, Button[] btns)
        {
            theme = t; group = g; card = c; title = ti; body = b; starsRow = sr; stars = s; rewardRow = rr; rewardLabel = rl; buttons = btns;
        }
#endif

        void Awake()
        {
            for (int i = 0; i < buttons.Length; i++)
            {
                int k = i;
                buttons[i].onClick.AddListener(() =>
                {
                    var h = handlers[k];
                    Hide();
                    h?.Invoke();
                });
            }
            gameObject.SetActive(false);
        }

        void OnDestroy() => showSeq?.Kill();

        public bool IsOpen => gameObject.activeSelf;

        public void ShowWin(int starCount, int coins, bool hard, Action next)
        {
            title.text = hard ? "HARD LEVEL CLEAR!" : "LEVEL CLEAR!";
            body.text = "Every truck filled up and drove off.";
            starsRow.SetActive(true);
            PlaceBody(withStars: true);
            for (int i = 0; i < stars.Length; i++) stars[i].color = i < starCount ? theme.StarOn : theme.StarOff;
            rewardRow.SetActive(true);
            rewardLabel.SetText("+{0}", coins);
            Open(new List<Option> { new Option("Next", PopupButtonStyle.Primary, next) });
        }

        public void ShowJam(string reason, List<Option> options)
        {
            title.text = "JAMMED";
            body.text = reason;
            starsRow.SetActive(false);
            rewardRow.SetActive(false);
            PlaceBody(withStars: false);
            Open(options);
        }

        /// <summary>Body text sits under the stars on a win, and moves up into their space when jammed.</summary>
        void PlaceBody(bool withStars)
        {
            var rt = body.rectTransform;
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, withStars ? -350f : -190f);
        }

        void Open(List<Option> options)
        {
            for (int i = 0; i < buttons.Length; i++)
            {
                bool on = i < options.Count;
                buttons[i].gameObject.SetActive(on);
                handlers[i] = on ? options[i].OnClick : null;
                if (!on) continue;
                buttons[i].GetComponentInChildren<TMP_Text>().text = options[i].Label;
                var img = buttons[i].GetComponent<Image>();
                var label = buttons[i].GetComponentInChildren<TMP_Text>();
                switch (options[i].Style)
                {
                    case PopupButtonStyle.Gold: img.color = theme.Gold; label.color = theme.GoldInk; break;
                    case PopupButtonStyle.Secondary: img.color = theme.Line; label.color = theme.Ink; break;
                    default: img.color = theme.Accent; label.color = Color.white; break;
                }
            }

            gameObject.SetActive(true);
            showSeq?.Kill();
            group.alpha = 0f;
            card.localScale = Vector3.one * 0.85f;
            showSeq = DOTween.Sequence().SetUpdate(true)
                .Append(group.DOFade(1f, 0.2f))
                .Join(card.DOScale(1f, 0.35f).SetEase(Ease.OutBack));
            if (starsRow.activeSelf)
                for (int i = 0; i < stars.Length; i++)
                {
                    stars[i].transform.localScale = Vector3.zero;
                    showSeq.Append(stars[i].transform.DOScale(1f, 0.25f).SetEase(Ease.OutBack));
                }
        }

        public void Hide()
        {
            showSeq?.Kill();
            gameObject.SetActive(false);
        }
    }
}
