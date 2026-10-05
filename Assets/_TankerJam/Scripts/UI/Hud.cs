using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TankerJam.UI
{
    /// <summary>
    /// In-level HUD: top bar (Retry, level number, sound), coin pill, status pill and the booster bar.
    /// Pure view: raises button events, shows what it is told.
    /// </summary>
    public sealed class Hud : MonoBehaviour
    {
        [SerializeField] UiTheme theme;
        [SerializeField] Button retryButton;
        [SerializeField] Button soundButton;
        [SerializeField] Image soundIcon;
        [SerializeField] TMP_Text levelLabel;
        [SerializeField] GameObject hardChip;
        [SerializeField] TMP_Text coinsLabel;
        [SerializeField] CanvasGroup statusGroup;
        [SerializeField] TMP_Text statusLabel;
        [SerializeField] BoosterButton vipButton;
        [SerializeField] BoosterButton extraButton;

        public event Action RetryClicked, SoundClicked, VipClicked, ExtraClicked;

        public BoosterButton Vip => vipButton;
        public BoosterButton Extra => extraButton;

        Tweener statusFade;
        Tween coinPunch;

#if UNITY_EDITOR
        public void EditorWire(UiTheme t, Button retry, Button sound, Image soundImg, TMP_Text level, GameObject hard, TMP_Text coins,
                               CanvasGroup status, TMP_Text statusText, BoosterButton vip, BoosterButton extra)
        {
            theme = t; retryButton = retry; soundButton = sound; soundIcon = soundImg; levelLabel = level; hardChip = hard;
            coinsLabel = coins; statusGroup = status; statusLabel = statusText; vipButton = vip; extraButton = extra;
        }
#endif

        void Awake()
        {
            retryButton.onClick.AddListener(() => RetryClicked?.Invoke());
            soundButton.onClick.AddListener(() => SoundClicked?.Invoke());
            vipButton.Clicked += () => VipClicked?.Invoke();
            extraButton.Clicked += () => ExtraClicked?.Invoke();
            statusGroup.alpha = 0f;
            statusFade = statusGroup.DOFade(1f, 0.25f).SetAutoKill(false).SetUpdate(true).SetLink(gameObject).Pause();
            coinPunch = coinsLabel.transform.DOPunchScale(Vector3.one * 0.25f, 0.4f, 6, 0.6f).SetAutoKill(false).SetUpdate(true).SetLink(gameObject).Pause();
        }

        void OnDestroy()
        {
            statusFade?.Kill();
            coinPunch?.Kill();
        }

        public void SetLevel(int number, bool hard)
        {
            levelLabel.SetText("LEVEL {0}", number);
            hardChip.SetActive(hard);
        }

        public void SetCoins(int coins, bool punch = false)
        {
            coinsLabel.SetText("{0}", coins);
            if (punch) coinPunch.Restart();
        }

        public void SetSound(bool on) => soundIcon.sprite = on ? theme.IconSoundOn : theme.IconSoundOff;

        public void SetStatus(string message)
        {
            bool show = !string.IsNullOrEmpty(message);
            if (show) statusLabel.text = message;
            statusFade.ChangeValues(statusGroup.alpha, show ? 1f : 0f);
            statusFade.Restart();
        }
    }
}
