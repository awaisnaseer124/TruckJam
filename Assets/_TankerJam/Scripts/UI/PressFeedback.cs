using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TankerJam.UI
{
    /// <summary>Squash on press, spring back on release. Tweens are built once and reused (no per-press allocation).</summary>
    public sealed class PressFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] float pressedScale = 0.92f;
        Tweener down, up;

        void Awake()
        {
            down = transform.DOScale(pressedScale, 0.08f).SetEase(Ease.OutQuad).SetAutoKill(false).SetUpdate(true).SetLink(gameObject).Pause();
            up = transform.DOScale(1f, 0.25f).SetEase(Ease.OutBack).SetAutoKill(false).SetUpdate(true).SetLink(gameObject).Pause();
        }

        void OnDestroy()
        {
            down?.Kill();
            up?.Kill();
        }

        public void OnPointerDown(PointerEventData e)
        {
            up.Pause();
            down.Restart();
        }

        public void OnPointerUp(PointerEventData e)
        {
            down.Pause();
            up.ChangeStartValue(transform.localScale);
            up.Restart();
        }
    }
}
