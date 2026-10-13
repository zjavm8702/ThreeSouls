using System;

namespace ThreeSouls.Data
{
    public enum ConsumableType
    {
        None,
        SmallHealingPotion,
        PartyHealingPotion
    }

    public enum AutoHealThreshold
    {
        Disabled,
        At20Percent,
        At30Percent,
        At40Percent
    }


    /// <summary>
    /// 물약 가격과 표시 문자열 및 자동 회복 옵션을 테이블에서 조회합니다.
    /// </summary>
    public static class ConsumableCatalog
    {
        public static bool IsPotion(ConsumableType type)
        {
            var rows = LobbyTables.Get<ThreeSouls.Data.Generated.ConsumableTableDataTable>().Items;
            for (var i = 0; i < rows.Length; i++)
            {
                if (rows[i].ConsumableType == type)
                {
                    return true;
                }
            }
            return false;
        }

        public static ThreeSouls.Data.Generated.ConsumableTableDataRow GetDefinition(ConsumableType type)
        {
            var rows = LobbyTables.Get<ThreeSouls.Data.Generated.ConsumableTableDataTable>().Items;
            for (var i = 0; i < rows.Length; i++)
            {
                if (rows[i].ConsumableType == type)
                {
                    return rows[i];
                }
            }
            throw new ArgumentOutOfRangeException(nameof(type));
        }

        public static int GetPrice(ConsumableType type)
        {
            return GetDefinition(type).GoldPrice;
        }

        public static string GetName(ConsumableType type)
        {
            return type == ConsumableType.None ? "비어 있음" : LobbyTables.GetText(GetDefinition(type).NameStrKey);
        }

        public static string GetEffect(ConsumableType type)
        {
            return LobbyTables.GetText(GetDefinition(type).EffectStrKey);
        }

        public static string GetThresholdLabel(AutoHealThreshold threshold)
        {
            var rows = LobbyTables.Get<ThreeSouls.Data.Generated.AutoHealTableDataTable>().Items;
            for (var i = 0; i < rows.Length; i++)
            {
                if (rows[i].Threshold == threshold)
                {
                    return LobbyTables.GetText(rows[i].StrKey);
                }
            }
            throw new ArgumentOutOfRangeException(nameof(threshold));
        }
    }
}
