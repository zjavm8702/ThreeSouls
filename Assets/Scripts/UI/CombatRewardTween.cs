using DG.Tweening;
using UnityEngine;

namespace ThreeSouls.UI
{
    /// <summary>
    /// 전달받은 UI의 보상 흩어짐, 도착 지점으로의 비행과 도착 강조 모션만 구성합니다.
    /// </summary>
    public sealed class CombatRewardTween
    {
        private const float DropDuration = 0.25f;
        private const float FlyDuration = 0.55f;

        public Sequence Play(RectTransform rect, CanvasGroup group, Vector2 start, Vector2 destination,
            RectTransform target)
        {
            var scatter = new Vector2(UnityEngine.Random.Range(-55f, 55f), UnityEngine.Random.Range(55f, 95f));
            
            rect.localScale = Vector3.zero;
            
            var sequence = DOTween.Sequence().SetAutoKill(true).Pause();
           
            sequence.SetLink(target.gameObject, LinkBehaviour.KillOnDisable);
            sequence.Append(rect.DOScale(1f, 0.12f).SetEase(Ease.OutBack));
            sequence.Join(rect.DOAnchorPos(start + scatter, DropDuration).SetEase(Ease.OutQuad));
            sequence.Append(rect.DOAnchorPos(destination, FlyDuration).SetEase(Ease.InBack));
            sequence.Join(rect.DOScale(0.65f, FlyDuration));
            sequence.Join(group.DOFade(0.25f, FlyDuration).SetDelay(FlyDuration * 0.55f));
            sequence.Append(target.DOPunchScale(Vector3.one * 0.16f, 0.22f, 5, 0.5f));
            sequence.OnKill(() =>
            {
                if (target != null)
                {
                    target.localScale = Vector3.one;
                }
            });

            return sequence;
        }
    }
}
