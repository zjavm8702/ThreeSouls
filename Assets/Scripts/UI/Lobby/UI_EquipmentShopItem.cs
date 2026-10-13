using System;
using ThreeSouls.Data;
using UnityEngine;

namespace ThreeSouls.UI
{
    /// <summary>
    /// 상점 장비 한 칸의 아이콘, 가격과 선택 상태를 표시합니다.
    /// </summary>
    public sealed class UI_EquipmentShopItem : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.Button SelectButton;
        [SerializeField] private UnityEngine.UI.Image Icon;
        [SerializeField] private UnityEngine.UI.Image RarityLine;
        [SerializeField] private GameObject Selection;
        [SerializeField] private UnityEngine.UI.Text ItemName;
        [SerializeField] private UnityEngine.UI.Text Price;
        private string InstanceId;
        private Action<string> OnSelect;

        private void Awake()
        {
            SelectButton.onClick.AddListener(Select);
        }

        public void Show(EquipmentInstance item, string title, string price, bool selected, Action<string> onSelect)
        {
            InstanceId = item.InstanceId;
            OnSelect = onSelect;
            Icon.sprite = LobbyPresentationData.GetItemIcon(item.ItemKey);
            ItemName.text = title;
            Price.text = price;
            RarityLine.color = LobbyPresentationData.GetRarityColor(LobbyTables.GetEquipment(item.ItemKey).Rarity);
            Selection.SetActive(selected);
        }

        private void Select()
        {
            OnSelect(InstanceId);
        }

        private void OnDestroy()
        {
            SelectButton.onClick.RemoveListener(Select);
            OnSelect = null;
        }
    }
}
