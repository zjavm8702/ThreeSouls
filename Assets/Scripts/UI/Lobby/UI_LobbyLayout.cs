using System;
using UnityEngine;

namespace ThreeSouls.UI
{
    /// <summary>
    /// 기존 Canvas 설정을 유지하면서 로비의 기준 크기를 부모의 안전 영역에 맞춥니다.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public sealed class UI_LobbyLayout : MonoBehaviour
    {
        private RectTransform Rect;
        private RectTransform Parent;
        private Vector2 PreviousSize;
        private readonly Vector2 ReferenceSize = new Vector2(1080, 1920);

        private void OnEnable()
        {
            Rect = GetComponent<RectTransform>();
            Parent = transform.parent as RectTransform;

            if (Parent == null)
            {
                throw new InvalidOperationException("로비 레이아웃에는 RectTransform 부모가 필요합니다.");
            }

            Apply();
        }

        private void LateUpdate()
        {
            if (Parent.rect.size != PreviousSize)
            {
                Apply();
            }
        }

        private void OnRectTransformDimensionsChange()
        {
            if (Parent != null && Rect != null && Parent.rect.size != PreviousSize)
            {
                Apply();
            }
        }

        /// <summary>
        /// 부모 크기가 확정된 직후 편집 도구에서도 같은 배율을 적용합니다.
        /// </summary>
        public void Apply()
        {
            PreviousSize = Parent.rect.size;
            Rect.anchorMin = new Vector2(0.5f, 0.5f);
            Rect.anchorMax = new Vector2(0.5f, 0.5f);
            Rect.pivot = new Vector2(0.5f, 0.5f);
            Rect.sizeDelta = ReferenceSize;
            Rect.anchoredPosition = Vector2.zero;
            var scale = Mathf.Min(PreviousSize.x / ReferenceSize.x, PreviousSize.y / ReferenceSize.y);
            Rect.localScale = new Vector3(scale, scale, 1);
        }
    }
}
