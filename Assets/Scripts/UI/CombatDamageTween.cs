using DG.Tweening;
using UnityEngine;

namespace ThreeSouls.UI
{
    /// <summary>
    /// 전달받은 UI의 데미지 상승, 확대와 페이드 모션만 구성합니다.
    /// </summary>
    public sealed class CombatDamageTween
    {
        private const float Duration = 0.65f;
        private const float RiseDistance = 80f;

        public Sequence Play(RectTransform rect, CanvasGroup group, Vector2 start)
        {
            return Play(rect, group, start, default, 0);
        }

        public Sequence Play(RectTransform rect, CanvasGroup group, Vector2 start, ThreeSouls.Combat.CombatDamageInfo hit, int lane)
        {
            var emphasis = hit.IsCritical || hit.IsSkill;
            var duration = emphasis ? 0.85f : Duration;
            rect.localScale = Vector3.one * (emphasis ? 0.45f : 0.7f);
            
            var sequence = DOTween.Sequence().SetAutoKill(true).Pause();
            
            sequence.Append(rect.DOAnchorPos(start + new Vector2(lane * (hit.IsCritical ? 18f : 10f), emphasis ? 110f : RiseDistance), duration).SetEase(Ease.OutQuad));
            sequence.Join(rect.DOScale(hit.IsCritical ? 1.2f : 1f, 0.13f).SetEase(Ease.OutBack));
            if (hit.IsCritical) sequence.Insert(0.13f, rect.DOScale(1f, 0.14f).SetEase(Ease.OutQuad));
            sequence.Insert(duration * 0.45f, group.DOFade(0f, duration * 0.55f));
            
            return sequence;
        }
    }
}
