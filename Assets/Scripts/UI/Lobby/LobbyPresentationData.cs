using ThreeSouls.Enumeration.AddressableKey;
using System;
using System.Linq;
using ThreeSouls.Data;
using ThreeSouls.Data.Generated;
using UnityEngine;

namespace ThreeSouls.UI
{
    /// <summary>
    /// 로비 테이블의 영웅 표시, 장비 아이콘과 옵션 표시를 조회합니다.
    /// </summary>
    public static class LobbyPresentationData
    {
        public static LobbyHeroTableDataRow GetHero(int index)
        {
            var key = LobbyTables.GetVillageRules().HeroKeys[index];
            return LobbyTables.Get<LobbyHeroTableDataTable>().Items.Single(x => x.HeroKey == key);
        }

        public static string GetHeroKey(int index)
        {
            return GetHero(index).HeroKey;
        }

        public static string GetHeroName(int index)
        {
            return LobbyTables.GetText(GetHero(index).NameStrKey);
        }

        public static Sprite GetHeroPortrait(int index)
        {
            var hero = GetHero(index);
            return LobbyResourceCatalog.GetSprite(hero.PortraitImagePath, hero.PortraitSpriteName);
        }

        public static Sprite GetItemIcon(string key)
        {
#if UNITY_EDITOR
            if (Application.isPlaying == false)
            {
                var row = UnityEditor.AssetDatabase.LoadAssetAtPath<EquipmentTableDataTable>(ScriptableObjectDatas.EquipmentTableData.ToAddress())
                    .Items.Single(x => x.ItemKey == key);
                return LobbyResourceCatalog.GetSprite(row.IconKey);
            }
#endif
            return LobbyResourceCatalog.GetSprite(TableManager.Get().GetEquipmentTableData(key).IconKey);
        }

        public static Color GetRarityColor(ItemRarity rarity)
        {
            if (ColorUtility.TryParseHtmlString(LobbyTables.GetOption("Rarity", rarity.ToString()).ColorHex, out var color) == false)
            {
                throw new InvalidOperationException("등급 색상 테이블을 확인하세요: " + rarity);
            }
            return color;
        }

        public static string GetRarityName(ItemRarity rarity)
        {
            return LobbyTables.GetText(LobbyTables.GetOption("Rarity", rarity.ToString()).StrKey);
        }

        public static string GetSlotName(EquipSlot slot)
        {
            return LobbyTables.GetText(LobbyTables.GetOption("EquipSlot", slot.ToString()).StrKey);
        }
    }
}
