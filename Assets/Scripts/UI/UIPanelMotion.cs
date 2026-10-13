using System;
using DG.Tweening;
using UnityEngine;

namespace ThreeSouls.UI
{
    /// <summary>팝업의 입력 차단과 비배율 시간의 짧은 열기·닫기를 관리합니다.</summary>
    public sealed class UIPanelMotion : MonoBehaviour
    {
        [SerializeField] private RectTransform Content;
        [SerializeField] private CanvasGroup Group;
        [SerializeField] private bool AnimateOnEnable = true;
        private Sequence Motion;
        private Vector3 RestScale;
        private Vector2 RestPosition;
        private bool Initialized;
        private bool Closing;

        private void Awake()
        {
            Capture();
        }

        private void Capture()
        {
            if (Initialized == true)
            {
                return;
            }
            if (Content == null)
            {
                Content = transform as RectTransform;
            }
            if (Group == null)
            {
                Group = GetComponent<CanvasGroup>();
            }
            RestScale = Content.localScale;
            RestPosition = Content.anchoredPosition;
            Initialized = true;
        }

        public static void Show(GameObject target)
        {
            if (target == null)
            {
                return;
            }
            var motion = target.GetComponent<UIPanelMotion>();
            var active = target.activeInHierarchy;
            target.SetActive(true);
            if (motion != null && active == true)
            {
                motion.PlayOpen();
            }
        }

        public static void Hide(GameObject target, Action completed = null)
        {
            if (target == null)
            {
                return;
            }
            var motion = target.GetComponent<UIPanelMotion>();
            if (motion == null || target.activeInHierarchy == false || Application.isPlaying == false)
            {
                target.SetActive(false);
                completed?.Invoke();
                return;
            }
            motion.PlayClose(() =>
            {
                target.SetActive(false);
                completed?.Invoke();
            });
        }

        public void PlayOpen()
        {
            ThreeSouls.Audio.GameAudio.Play(ThreeSouls.Audio.SoundCue.Open);
            Capture();
            Stop();
            Closing = false;
            Group.interactable = false;
            Group.blocksRaycasts = true;
            Group.alpha = 0f;
            Content.localScale = RestScale * 0.97f;
            Content.anchoredPosition = RestPosition + Vector2.down * 10f;
            Motion = DOTween.Sequence().SetUpdate(true);
            Motion.Join(Group.DOFade(1f, 0.16f));
            Motion.Join(Content.DOScale(RestScale, 0.20f).SetEase(Ease.OutCubic));
            Motion.Join(Content.DOAnchorPos(RestPosition, 0.20f).SetEase(Ease.OutCubic));
            Motion.OnComplete(() =>
            {
                Restore();
                Motion = null;
            });
        }

        public void PlayClose(Action completed)
        {
            if (Closing == true)
            {
                return;
            }
            Capture();
            Stop();
            Closing = true;
            ThreeSouls.Audio.GameAudio.Play(ThreeSouls.Audio.SoundCue.Close);
            Group.interactable = false;
            Group.blocksRaycasts = true;
            Motion = DOTween.Sequence().SetUpdate(true);
            Motion.Join(Group.DOFade(0f, 0.12f));
            Motion.Join(Content.DOScale(RestScale * 0.98f, 0.12f).SetEase(Ease.InQuad));
            Motion.OnComplete(() =>
            {
                Motion = null;
                completed?.Invoke();
            });
        }

        private void OnEnable()
        {
            Capture();
            if (Application.isPlaying == true && AnimateOnEnable == true)
            {
                PlayOpen();
            }
        }

        private void OnDisable()
        {
            Stop();
            Restore();
        }

        private void OnDestroy()
        {
            Stop();
        }

        private void Stop()
        {
            Motion?.Kill();
            Motion = null;
        }

        private void Restore()
        {
            if (Initialized == false || Content == null || Group == null)
            {
                return;
            }
            Closing = false;
            Content.localScale = RestScale;
            Content.anchoredPosition = RestPosition;
            Group.alpha = 1f;
            Group.interactable = true;
            Group.blocksRaycasts = true;
        }
    }
}
