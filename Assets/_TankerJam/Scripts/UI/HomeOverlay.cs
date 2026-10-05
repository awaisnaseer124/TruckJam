using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TankerJam.UI
{
    /// <summary>Start screen over the board: title, coins and a big Play button for the next level.</summary>
    public sealed class HomeOverlay : MonoBehaviour
    {
        [SerializeField] CanvasGroup group;
        [SerializeField] TMP_Text levelLabel;
        [SerializeField] TMP_Text coinsLabel;
        [SerializeField] Button playButton;
        [SerializeField] RectTransform playRect;

        public event Action PlayClicked;
        Tween breathe;

#if UNITY_EDITOR
        public void EditorWire(CanvasGroup g, TMP_Text level, TMP_Text coins, Button play)
        {
            group = g; levelLabel = level; coinsLabel = coins; playButton = play; playRect = (RectTransform)play.transform;
        }
#endif

        void Awake()
        {
            playButton.onClick.AddListener(() => PlayClicked?.Invoke());
            breathe = playRect.DOScale(1.05f, 0.7f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetAutoKill(false).SetUpdate(true).Pause();
        }

        void OnDestroy() => breathe?.Kill();

        public bool IsOpen => gameObject.activeSelf;

        public void Show(int levelNumber, int coins)
        {
            levelLabel.SetText("LEVEL {0}", levelNumber);
            coinsLabel.SetText("{0}", coins);
            gameObject.SetActive(true);
            group.alpha = 1f;
            breathe.Restart();
        }

        public void Hide()
        {
            breathe.Pause();
            gameObject.SetActive(false);
        }
    }
}
