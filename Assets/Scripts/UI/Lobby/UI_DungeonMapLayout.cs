using System;
using UnityEngine;

namespace ThreeSouls.UI
{
    /// <summary>
    /// 부모 배치가 확정된 뒤 원본 지도의 비율과 선택점 좌표계를 유지합니다.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public sealed class UI_DungeonMapLayout : MonoBehaviour
    {
        private RectTransform Rect;
        private RectTransform Parent;
        private Vector2 PreviousSize;
        private UnityEngine.UI.Image Image;
        private Sprite PreviousTexture;

        private void OnEnable()
        {
            Rect = GetComponent<RectTransform>();
            Image = GetComponent<UnityEngine.UI.Image>();
            Parent = transform.parent as RectTransform
                ?? throw new InvalidOperationException("지도 배치에는 RectTransform 부모가 필요합니다.");
            Apply();
        }

        private void LateUpdate()
        {
            if (Image != null && (Parent.rect.size != PreviousSize || Image.sprite != PreviousTexture))
            {
                Apply();
            }
        }

        private void Apply()
        {
            if (Image == null) return;
            PreviousSize = Parent.rect.size;
            if (PreviousSize.x <= 0 || PreviousSize.y <= 0)
            {
                return;
            }

            PreviousTexture = Image.sprite;
            var ratio = PreviousTexture == null ? 16f / 9f : PreviousTexture.rect.width / PreviousTexture.rect.height;
            var width = Mathf.Min(PreviousSize.x, PreviousSize.y * ratio);
            Rect.anchorMin = new Vector2(.5f, .5f);
            Rect.anchorMax = Rect.anchorMin;
            Rect.pivot = new Vector2(.5f, .5f);
            Rect.sizeDelta = new Vector2(width, width / ratio);
            Rect.anchoredPosition = Vector2.zero;
        }
    }
}
