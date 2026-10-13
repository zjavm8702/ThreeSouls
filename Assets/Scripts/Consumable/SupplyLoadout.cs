using System;

namespace ThreeSouls.Data
{
    /// <summary>
    /// 한 출정 슬롯의 종류와 편성 수량을 값으로 전달합니다.
    /// </summary>
    public readonly struct SupplySlotData
    {
        public ConsumableType Type { get; }
        public int Count { get; }

        internal SupplySlotData(ConsumableType type, int count)
        {
            if (count < 0 || count > SupplyLoadout.GetMaximumPerSlot() ||
                (type != ConsumableType.None && ConsumableCatalog.IsPotion(type) == false) ||
                (count > 0 && ConsumableCatalog.IsPotion(type) == false))
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            Type = count == 0 ? ConsumableType.None : type;
            Count = count;
        }
    }

    /// <summary>
    /// 보유 수량과 분리된 두 슬롯의 출정 준비안 및 자동 회복 설정을 보관합니다.
    /// </summary>
    public sealed class SupplyLoadout
    {
        public const int SlotCount = 2;
        public static int GetMaximumPerSlot()
        {
            return LobbyTables.GetSupplyRules().MaximumPerSlot;
        }
        private readonly SupplySlotData[] Slots = new SupplySlotData[SlotCount];
        public AutoHealThreshold Threshold { get; private set; }

        public SupplySlotData GetSlot(int index)
        {
            return Slots[index];
        }

        public int GetAssigned(ConsumableType type)
        {
            var count = 0;

            for (var i = 0; i < Slots.Length; i++)
            {
                if (Slots[i].Type == type)
                {
                    count += Slots[i].Count;
                }
            }

            return count;
        }

        public int GetTotalAssigned()
        {
            return Slots[0].Count + Slots[1].Count;
        }

        internal void SetSlot(int index, ConsumableType type, int count)
        {
            Slots[index] = new SupplySlotData(type, count);
        }

        internal void SetThreshold(AutoHealThreshold threshold)
        {
            if (Enum.IsDefined(typeof(AutoHealThreshold), threshold) == false)
            {
                throw new ArgumentOutOfRangeException(nameof(threshold));
            }

            Threshold = threshold;
        }

        internal void Clear()
        {
            for (var i = 0; i < Slots.Length; i++)
            {
                Slots[i] = default;
            }
        }
    }
}
