using System;
using ThreeSouls.Data;
using UnityEngine;
using UnityEngine.UI;

namespace ThreeSouls.UI
{
    /// <summary>
    /// 프리팹의 표시 요소에 로비 테이블 문자열과 이미지를 최초 적용합니다.
    /// </summary>
    public sealed class UI_LobbyTablePresentation : MonoBehaviour
    {
        [SerializeField] private Text[] HeroNames;
        [SerializeField] private Image[] HeroPortraits;
        [SerializeField] private Text[] PotionNames;
        [SerializeField] private Text[] PotionPrices;
        [SerializeField] private Text[] PotionEffects;
        [SerializeField] private Image[] PotionIcons;
        [SerializeField] private Text[] BuyFirstLabels;
        [SerializeField] private Text[] BuySecondLabels;
        [SerializeField] private Text[] TypeLabels;
        [SerializeField] private Text[] ThresholdLabels;
        [SerializeField] private Text[] FilterLabels;
        [SerializeField] private Text[] TabLabels;

        private void Awake()
        {
            ApplyTables();
        }

        public void ApplyTables()
        {
            if (HeroNames.Length != 6 || HeroPortraits.Length != 6 || PotionNames.Length != 2 ||
                PotionPrices.Length != 2 || PotionEffects.Length != 2 || PotionIcons.Length != 2 ||
                BuyFirstLabels.Length != 2 || BuySecondLabels.Length != 2 || TypeLabels.Length != 2 ||
                ThresholdLabels.Length != 4 || FilterLabels.Length != 5 || TabLabels.Length != 4)
            {
                throw new InvalidOperationException("로비 테이블 표시 요소 연결을 확인하세요.");
            }
            for (var i = 0; i < HeroNames.Length; i++)
            {
                var hero = LobbyPresentationData.GetHero(i % 3);
                HeroNames[i].text = LobbyTables.GetText(hero.NameStrKey);
                HeroPortraits[i].sprite = LobbyResourceCatalog.GetSprite(hero.PortraitImagePath, hero.PortraitSpriteName);
            }
            var types = new[] { ConsumableType.SmallHealingPotion, ConsumableType.PartyHealingPotion };
            var supply = LobbyTables.GetSupplyRules();
            for (var i = 0; i < types.Length; i++)
            {
                var row = ConsumableCatalog.GetDefinition(types[i]);
                PotionNames[i].text = LobbyTables.GetText(row.NameStrKey);
                PotionPrices[i].text = $"{row.GoldPrice:N0} 골드 / 개";
                PotionEffects[i].text = LobbyTables.GetText(row.EffectStrKey);
                PotionIcons[i].sprite = LobbyResourceCatalog.GetSprite(row.IconPath, row.IconSpriteName);
                BuyFirstLabels[i].text = $"{supply.BuyAmounts[0]}개 구매";
                BuySecondLabels[i].text = $"{supply.BuyAmounts[1]}개 구매";
                TypeLabels[i].text = PotionNames[i].text + " 편성";
            }
            var thresholds = new[] { AutoHealThreshold.Disabled, AutoHealThreshold.At20Percent,
                AutoHealThreshold.At30Percent, AutoHealThreshold.At40Percent };
            for (var i = 0; i < thresholds.Length; i++)
            {
                ThresholdLabels[i].text = ConsumableCatalog.GetThresholdLabel(thresholds[i]);
            }
            var filters = new[] { "All", "Weapon", "Armor", "Accessory", "Equipped" };
            for (var i = 0; i < filters.Length; i++)
            {
                FilterLabels[i].text = LobbyTables.GetText(LobbyTables.GetOption("Filter", filters[i]).StrKey);
            }
            for (var i = 0; i < TabLabels.Length; i++)
            {
                TabLabels[i].text = LobbyTables.GetText(LobbyTables.GetOption("Tab", ((LobbyTab)i).ToString()).StrKey);
            }
        }
    }
}
