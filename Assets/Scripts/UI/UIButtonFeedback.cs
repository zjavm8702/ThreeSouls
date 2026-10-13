using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ThreeSouls.UI
{
    /// <summary>버튼을 누를 때 잠깐 축소하며 비활성화 시 원래 크기로 복구합니다.</summary>
    public sealed class UIButtonFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private Button Target;
        private Vector3 RestScale;
        private Tween Motion;

        private void Awake()
        {
            Target = GetComponent<Button>();
            RestScale = transform.localScale;
            Target.onClick.AddListener(PlayClick);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (Target.IsInteractable() == false)
            {
                return;
            }
            Animate(0.965f, 0.07f);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Animate(1f, 0.10f);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Animate(1f, 0.10f);
        }

        private void Animate(float factor, float duration)
        {
            Motion?.Kill();
            Motion = transform.DOScale(RestScale * factor, duration).SetEase(Ease.OutQuad).SetUpdate(true);
        }

        private void OnDisable()
        {
            Motion?.Kill();
            Motion = null;
            transform.localScale = RestScale;
        }

        private void OnDestroy()
        {
            if (Target != null) Target.onClick.RemoveListener(PlayClick);
            Motion?.Kill();
        }

        private void PlayClick() => ThreeSouls.Audio.GameAudio.Play(ThreeSouls.Audio.SoundCue.Click);
    }
}
