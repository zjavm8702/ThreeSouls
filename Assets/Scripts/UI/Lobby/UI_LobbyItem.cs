using ThreeSouls.Data;
using ThreeSouls.Data.Generated;
using UnityEngine;
using UnityEngine.UI;

namespace ThreeSouls.UI
{
    /// <summary>
    /// 하나의 장비 칸을 표시하고 선택 요청을 인벤토리 화면에 전달합니다.
    /// </summary>
    public sealed class UI_LobbyItem : MonoBehaviour
    {
        [SerializeField] private Button SelectButton;
        [SerializeField] private Image Icon;
        [SerializeField] private Image RarityLine;
        [SerializeField] private GameObject Selection;
        [SerializeField] private Text ItemName;
        [SerializeField] private Text Owner;
        private string InstanceId;
        private UI_LobbyInventory Inventory;

        private void Awake()
        {
            SelectButton.onClick.AddListener(Select);
        }

        public void Show(UI_LobbyInventory inventory, EquipmentInstance item,
            EquipmentTableDataRow row,
            string title, string owner, bool selected)
        {
            Inventory = inventory;
            InstanceId = item?.InstanceId;
            SelectButton.interactable = item != null;
            Icon.enabled = item != null;
            Icon.sprite = item == null ? null : LobbyPresentationData.GetItemIcon(item.ItemKey);
            ItemName.text = title;
            Owner.text = (owner == null ? string.Empty : "장착 중")
                + (item != null && item.Growth.Locked == true ? "  잠금" : string.Empty);
            RarityLine.color = row == null ? new Color32(48, 62, 71, 255)
                : LobbyPresentationData.GetRarityColor(row.Rarity);
            Selection.SetActive(selected);
        }

        private void Select()
        {
            Inventory.SelectItem(InstanceId);
        }

        private void OnDestroy()
        {
            SelectButton.onClick.RemoveListener(Select);
        }
    }
}
