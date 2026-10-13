using System;
using ThreeSouls.Data;

namespace ThreeSouls.Combat
{
    /// <summary>
    /// 한 출전의 공급품 종류·남은 수량·자동 회복 기준을 보관합니다.
    /// </summary>
    internal sealed class CombatSupplyData
    {
        private readonly SupplySlotData[] Slots;
        internal AutoHealThreshold Threshold { get; }

        internal CombatSupplyData(SupplyLoadout loadout)
        {
            Slots = new SupplySlotData[SupplyLoadout.SlotCount];
            for (var i = 0; i < Slots.Length; i++)
            {
                Slots[i] = loadout.GetSlot(i);
            }
            Threshold = loadout.Threshold;
        }

        internal SupplySlotData GetSlot(int index)
        {
            return Slots[index];
        }

        internal void Consume(int index)
        {
            var slot = Slots[index];
            if (slot.Count <= 0)
            {
                throw new InvalidOperationException("남은 공급품을 먼저 확인하세요.");
            }
            Slots[index] = new SupplySlotData(slot.Type, slot.Count - 1);
        }
    }
}
