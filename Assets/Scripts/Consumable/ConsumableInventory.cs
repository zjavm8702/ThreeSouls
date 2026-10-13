using System;

namespace ThreeSouls.Data
{
    /// <summary>
    /// 장비 인벤토리와 별개로 보유 물약 수량의 일관성을 보호합니다.
    /// </summary>
    public sealed partial class ConsumableInventory
    {
        private int SmallPotions;
        private int PartyPotions;

        public int GetOwned(ConsumableType type)
        {
            return type switch
            {
                ConsumableType.SmallHealingPotion => SmallPotions,
                ConsumableType.PartyHealingPotion => PartyPotions,
                _ => throw new ArgumentOutOfRangeException(nameof(type))
            };
        }

        internal bool CanAdd(ConsumableType type, int count)
        {
            return ConsumableCatalog.IsPotion(type) == true && count > 0 &&
                (long)GetOwned(type) + count <= LobbyTables.GetSupplyRules().MaxOwnedPerType;
        }

        internal void Add(ConsumableType type, int count)
        {
            if (CanAdd(type, count) == false)
            {
                throw new InvalidOperationException("보유 물약 증가량을 먼저 검증해야 합니다.");
            }

            if (type == ConsumableType.SmallHealingPotion)
            {
                SmallPotions += count;
            }
            else
            {
                PartyPotions += count;
            }
        }

        internal void Remove(ConsumableType type, int count)
        {
            if (count <= 0 || count > GetOwned(type))
            {
                throw new InvalidOperationException("물약 차감량을 먼저 검증해야 합니다.");
            }
            if (type == ConsumableType.SmallHealingPotion)
            {
                SmallPotions -= count;
            }
            else
            {
                PartyPotions -= count;
            }
        }
    }
}
