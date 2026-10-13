using TMPro;
using UnityEngine;

namespace ThreeSouls.Village
{
    /// <summary>
    /// 주민 선택 영역, 접근 지점과 의뢰 표시를 보관합니다.
    /// </summary>
    public sealed class VillageNpcTarget : MonoBehaviour
    {
        [SerializeField] private string NpcKey;
        [SerializeField] private Collider2D HitArea;
        [SerializeField] private TMP_Text RoleLabel;
        [SerializeField] private SpriteRenderer QuestMarker;
        [SerializeField] private Sprite AvailableIcon;
        [SerializeField] private Sprite ActiveIcon;
        [SerializeField] private Sprite CompleteIcon;

        public string GetNpcKey() => NpcKey;
        public Vector2 GetApproach() => (Vector2)transform.position + Vector2.down * 0.65f;
        public bool ContainsPoint(Vector2 point) => HitArea.OverlapPoint(point);

        public void ShowQuestState(string currentGiver, bool accepted, bool complete)
        {
            var questNpc = NpcKey.StartsWith("Quest_");
            if (RoleLabel != null)
            {
                RoleLabel.gameObject.SetActive(!questNpc);
                if (!questNpc) RoleLabel.text = "룬 상점";
            }
            if (QuestMarker != null)
            {
                QuestMarker.sprite = complete ? CompleteIcon : accepted ? ActiveIcon : AvailableIcon;
                QuestMarker.color = complete ? new Color32(112, 236, 153, 255) : accepted ? new Color32(108, 218, 255, 255) : new Color32(255, 210, 85, 255);
                QuestMarker.enabled = questNpc && NpcKey == currentGiver && QuestMarker.sprite != null;
            }
        }
    }
}
