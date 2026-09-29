using System;
using ThreeSouls.Character;
using UnityEngine;
using UnityEngine.UI;

namespace ThreeSouls.UI
{
    public sealed class UI_Item_HealthBar : BaseUIItem
    {
        [SerializeField] private Image ProgressHealthBarImage = null;
        [SerializeField] private float AdditionalScreenOffset = 0f;

        private BaseEntity Target = null;
        private RectTransform CachedRectTransform = null;
        private RectTransform ProgressRectTransform = null;
        private RectTransform CanvasRectTransform = null;
        private Canvas ParentCanvas = null;
        private Camera WorldCamera = null;

        public void Bind(BaseEntity entity)
        {
            Clear();
            Target = entity != null ? entity : throw new ArgumentNullException(nameof(entity));
            Target.CharHealth.HealthChanged += OnHealthChanged;
            OnHealthChanged(Target.CharHealth.CurrentHealth, Target.CharHealth.MaxHealth);
        }

        public override void Clear()
        {
            if (Target != null)
            {
                Target.CharHealth.HealthChanged -= OnHealthChanged;
                Target = null;
            }

            WorldCamera = null;
            SetProgress(1f);
        }

        private void OnHealthChanged(float currentHealth, float maxHealth)
        {
            SetProgress(maxHealth > 0 ? currentHealth / maxHealth : 0);
        }

        private void SetProgress(float normalizedHealth)
        {
            if (ProgressHealthBarImage == null)
            {
                return;
            }

            ProgressRectTransform ??= ProgressHealthBarImage.rectTransform;
            ProgressRectTransform.anchorMin = Vector2.zero;
            ProgressRectTransform.anchorMax = new Vector2(Mathf.Clamp01(normalizedHealth), 1f);
            ProgressRectTransform.offsetMin = Vector2.zero;
            ProgressRectTransform.offsetMax = Vector2.zero;
        }

        public bool TryGetDesiredPosition(out Vector2 desiredPosition)
        {
            desiredPosition = default;

            if (Target == null || Target.CharSpriteRenderer == null)
            {
                return false;
            }

            WorldCamera ??= Camera.main;
            ParentCanvas ??= GetComponentInParent<Canvas>();

            if (WorldCamera == null || ParentCanvas == null)
            {
                return false;
            }

            CachedRectTransform ??= (RectTransform)transform;
            CanvasRectTransform ??= (RectTransform)ParentCanvas.transform;

            var spriteBounds = Target.CharSpriteRenderer.bounds;
            var spriteTop = new Vector3(spriteBounds.center.x, spriteBounds.max.y, spriteBounds.center.z);
            var screenPoint = WorldCamera.WorldToScreenPoint(spriteTop);
            var canvasCamera = ParentCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : ParentCanvas.worldCamera;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    CanvasRectTransform, screenPoint, canvasCamera, out var canvasPoint) == false)
            {
                return false;
            }

            desiredPosition = canvasPoint + Vector2.up * AdditionalScreenOffset;
            return true;
        }

        public Rect GetLayoutRect(Vector2 anchoredPosition)
        {
            CachedRectTransform ??= (RectTransform)transform;
            var size = CachedRectTransform.rect.size;
            var pivot = CachedRectTransform.pivot;
            return new Rect(
                anchoredPosition.x - size.x * pivot.x,
                anchoredPosition.y - size.y * pivot.y,
                size.x,
                size.y);
        }

        public void SetLayoutPosition(Vector2 anchoredPosition)
        {
            CachedRectTransform ??= (RectTransform)transform;
            CachedRectTransform.anchoredPosition = anchoredPosition;
        }
    }
}
