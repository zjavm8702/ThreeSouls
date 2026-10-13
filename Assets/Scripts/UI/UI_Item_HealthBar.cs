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
        [SerializeField] private UnityEngine.UI.Image BarBackground;
        [SerializeField] private UnityEngine.UI.Outline BarOutline;
        [SerializeField] private TMPro.TMP_Text HeroName;
        public static Color HeroColor => new Color32(85, 220, 185, 255);
        private bool IsHero;

        private BaseEntity Target = null;
        private RectTransform CachedRectTransform = null;
        private RectTransform ProgressRectTransform = null;
        private RectTransform CanvasRectTransform = null;
        private Canvas ParentCanvas = null;
        private Camera WorldCamera = null;

        public void Bind(BaseEntity entity)
        {
            Clear();

            gameObject.SetActive(false);
            
            Target = entity;
            IsHero = entity is Hero;
            ApplyStyle();
            Target.CharHealth.OnHealthChangeEventHandler += OnHealthChangeEventHandler;
            Target.CharHealth.GetHealth(out var maximum, out var current);
            SetProgress(maximum > 0 ? current / maximum : 0);
            gameObject.SetActive(IsHero && Target.CharHealth.IsAlive);
        }

        private void ApplyStyle()
        {
            CachedRectTransform ??= (RectTransform)transform;
            CachedRectTransform.sizeDelta = IsHero ? new Vector2(132, 36) : new Vector2(90, 12);
            ProgressHealthBarImage.color = IsHero ? HeroColor : new Color32(235, 83, 87, 255);
            ProgressHealthBarImage.raycastTarget = false;
            if (BarBackground != null)
            {
                BarBackground.color = IsHero ? new Color32(12, 40, 42, 255) : new Color32(42, 18, 26, 255);
                var rect = BarBackground.rectTransform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = new Vector2(1, IsHero ? .42f : 1);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
            }
            if (BarOutline != null) BarOutline.effectColor = IsHero ? new Color32(181, 242, 222, 255) : new Color32(90, 31, 41, 255);
            if (HeroName != null)
            {
                HeroName.gameObject.SetActive(IsHero);
                HeroName.text = !IsHero || Target == null ? string.Empty : Target.CharacterData.CharacterKey switch
                { "Key_night" => "나이트", "Key_archer" => "아처", "Key_wizard" => "위자드", _ => "영웅" };
            }
        }

        private void OnHealthChangeEventHandler(
            bool takeDamage,
            float maxHealth,
            float currentHealth,
            float damage,
            BaseEntity sourceEntity)
        {
            SetProgress(maxHealth > 0 ? currentHealth / maxHealth : 0);

            if (currentHealth <= 0)
            {
                gameObject.SetActive(false);
                return;
            }

            if (takeDamage == true)
            {
                gameObject.SetActive(true);
            }
        }

        public override void Clear()
        {
            if (Target != null)
            {
                Target.CharHealth.OnHealthChangeEventHandler -= OnHealthChangeEventHandler;
                Target = null;
            }

            WorldCamera = null;
            ParentCanvas = null;
            CanvasRectTransform = null;
            IsHero = false;
            ApplyStyle();
            SetProgress(1f);
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

            if (Target == null)
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
          
            var spriteBounds = Target.GetSpriteBounds();
            var spriteTop = new Vector3(spriteBounds.center.x, spriteBounds.max.y, spriteBounds.center.z);
            var screenPoint = WorldCamera.WorldToScreenPoint(spriteTop);
            var canvasCamera = ParentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : ParentCanvas.worldCamera;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(CanvasRectTransform, screenPoint, canvasCamera, out var canvasPoint) == false)
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

            return new Rect(anchoredPosition.x - size.x * pivot.x, anchoredPosition.y - size.y * pivot.y, size.x, size.y);
        }

        public void SetLayoutPosition(Vector2 anchoredPosition)
        {
            CachedRectTransform ??= (RectTransform)transform;
            CachedRectTransform.anchoredPosition = anchoredPosition;
        }
    }
}
