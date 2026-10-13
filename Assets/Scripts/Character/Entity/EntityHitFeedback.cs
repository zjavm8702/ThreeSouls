using System;
using DG.Tweening;
using UnityEngine;

namespace ThreeSouls.Character
{
    /// <summary>
    /// 피격 순간의 밝은 실루엣과 붉은 플래시를 재생하고 반환 시 원래 표현을 복원합니다.
    /// </summary>
    internal sealed class EntityHitFeedback
    {
        private readonly SpriteRenderer Renderer;
        private readonly Material HitMaterial;
        private readonly Material DefaultMaterial;
        private readonly MaterialPropertyBlock OriginalProperties = new();
        private readonly MaterialPropertyBlock HitProperties = new();
        private Sequence Flash;
        private static readonly int FlashAmount = Shader.PropertyToID("_FlashAmount");
        private static readonly int FlashColor = Shader.PropertyToID("_FlashColor");

        internal EntityHitFeedback(SpriteRenderer renderer, Material hitMaterial)
        {
            Renderer = renderer;
            HitMaterial = hitMaterial != null ? hitMaterial : throw new ArgumentNullException(nameof(hitMaterial));
            DefaultMaterial = renderer.sharedMaterial;
        }

        internal void Play(ThreeSouls.Combat.CombatDamageInfo hit)
        {
            Clear();
            Renderer.GetPropertyBlock(OriginalProperties);
            Renderer.GetPropertyBlock(HitProperties);
            Renderer.sharedMaterial = HitMaterial;
            SetColor(new Color(1f, 0.95f, 0.82f));
            SetAmount(1f);
            Flash = DOTween.Sequence();
            var color = hit.IsSkill ? new Color(0.45f, 0.65f, 1f) :
                hit.IsCritical ? new Color(1f, 0.72f, 0.1f) : new Color(1f, 0.24f, 0.18f);
            Flash.Append(DOVirtual.Color(new Color(1f, 0.95f, 0.82f), color, 0.04f, SetColor));
            Flash.Append(DOTween.To(() => 1f, SetAmount, 0f, hit.IsCritical || hit.IsSkill ? 0.17f : 0.11f).SetEase(Ease.OutQuad));
            Flash.OnKill(Restore);
        }

        internal void Clear()
        {
            if (Flash != null)
            {
                Flash.Kill();
            }
        }

        private void SetColor(Color color)
        {
            HitProperties.SetColor(FlashColor, color);
            Renderer.SetPropertyBlock(HitProperties);
        }

        private void SetAmount(float amount)
        {
            HitProperties.SetFloat(FlashAmount, amount);
            Renderer.SetPropertyBlock(HitProperties);
        }

        private void Restore()
        {
            Flash = null;

            if (Renderer != null)
            {
                Renderer.sharedMaterial = DefaultMaterial;
                Renderer.SetPropertyBlock(OriginalProperties);
            }
        }
    }
}
