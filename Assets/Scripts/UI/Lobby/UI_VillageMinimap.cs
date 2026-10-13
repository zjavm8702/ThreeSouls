using ThreeSouls.Village;
using UnityEngine;

namespace ThreeSouls.UI
{
    /// <summary>
    /// 고정 마을 지형 위에 부대의 현재 월드 위치를 표시합니다.
    /// </summary>
    public sealed class UI_VillageMinimap : MonoBehaviour
    {
        [SerializeField] private Rect WorldBounds;
        [SerializeField] private RectTransform LeaderMarker;
        private VillagePartyController Party;

        public void Connect(VillagePartyController party)
        {
            Party = party;
        }

        private void LateUpdate()
        {
            if (Party == null || LeaderMarker == null)
            {
                return;
            }

            var position = Party.GetLeaderPosition();
            var normalized = new Vector2(
                Mathf.Clamp01((position.x - WorldBounds.xMin) / WorldBounds.width),
                Mathf.Clamp01((position.y - WorldBounds.yMin) / WorldBounds.height));
            LeaderMarker.anchorMin = normalized;
            LeaderMarker.anchorMax = normalized;
            LeaderMarker.anchoredPosition = Vector2.zero;
        }
    }
}
