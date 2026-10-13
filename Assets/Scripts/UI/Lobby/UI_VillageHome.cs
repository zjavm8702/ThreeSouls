using UnityEngine;

namespace ThreeSouls.UI
{
    /// <summary>
    /// 타일 없는 정식 마을의 안내 카드와 출전·상점 버튼 배치를 표시합니다.
    /// </summary>
    public sealed class UI_VillageHome : MonoBehaviour
    {
        [SerializeField] private RectTransform DungeonButton;
        [SerializeField] private RectTransform ShopButton;

        public void Show()
        {
            gameObject.SetActive(true);
            if (DungeonButton.parent.name != "Navigation") Position(DungeonButton, 0.15f, 0.23f);
            Position(ShopButton, 0.245f, 0.325f);
        }

        private static void Position(RectTransform rect, float minimum, float maximum)
        {
            rect.anchorMin = new Vector2(0.15f, minimum);
            rect.anchorMax = new Vector2(0.85f, maximum);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
