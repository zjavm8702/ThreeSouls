using ThreeSouls.Data.Generated;
using UnityEngine;
using UnityEngine.UI;

namespace ThreeSouls.UI
{
    /// <summary>
    /// 결과창 장비 한 종류의 아이콘, 등급, 이름과 수량을 표시합니다.
    /// </summary>
    public sealed class UI_Item_ResultReward : MonoBehaviour
    {
        [SerializeField] private Image ItemIcon;
        [SerializeField] private Image RarityBorder;
        [SerializeField] private Text NameText;
        [SerializeField] private Text RarityText;
        [SerializeField] private Text CountText;
        [SerializeField] private CanvasGroup Group;
        [SerializeField] private RectTransform Presentation;

        internal void Bind(Sprite icon, string itemName, ItemRarity rarity, int count)
        {
            var color = LobbyPresentationData.GetRarityColor(rarity);

            ItemIcon.sprite = icon;
            NameText.text = itemName;
            NameText.color = color;
            RarityBorder.color = color;
            RarityText.color = color;
            RarityText.text = LobbyPresentationData.GetRarityName(rarity);
            CountText.text = $"×{count}";
            Group.alpha = 1f;
            Group.interactable = false;
            Group.blocksRaycasts = false;
            Presentation.localScale = Vector3.one;
        }

        internal CanvasGroup GetPresentationGroup()
        {
            return Group;
        }

        internal RectTransform GetPresentationRect()
        {
            return Presentation;
        }
    }
}
