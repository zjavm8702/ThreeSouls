using UnityEngine;

namespace ThreeSouls.UI
{
    /// <summary>
    /// 화면 회전과 노치 안전 영역을 로비의 화면 경계에 적용합니다.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class UI_LobbySafeArea : MonoBehaviour
    {
        private RectTransform Rect;
        private UnityEngine.Rect PreviousArea;
        private Vector2Int PreviousSize;

        private void Awake()
        {
            Rect = GetComponent<RectTransform>();
            Apply();
        }

        private void LateUpdate()
        {
            if (PreviousArea != Screen.safeArea || PreviousSize.x != Screen.width || PreviousSize.y != Screen.height)
            {
                Apply();
            }
        }

        private void Apply()
        {
            PreviousArea = Screen.safeArea;
            PreviousSize = new Vector2Int(Screen.width, Screen.height);
            Rect.anchorMin = new Vector2(PreviousArea.xMin / Screen.width, PreviousArea.yMin / Screen.height);
            Rect.anchorMax = new Vector2(PreviousArea.xMax / Screen.width, PreviousArea.yMax / Screen.height);
            Rect.offsetMin = Vector2.zero;
            Rect.offsetMax = Vector2.zero;
        }
    }
}
