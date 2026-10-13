using System;
using ThreeSouls.Combat;
using ThreeSouls.Data;

namespace ThreeSouls.Manager
{
    /// <summary>
    /// 출전 공급품을 예약하고 실제 사용 확정 및 미사용 반환을 처리합니다.
    /// </summary>
    public sealed class CombatSupplyManager
    {
        private readonly ConsumableInventory Inventory;
        private readonly SupplyLoadout Loadout;
        private CombatSupplyData Data;
        private bool Reserved;

        internal CombatSupplyManager(ConsumableInventory inventory, SupplyLoadout loadout)
        {
            Inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            Loadout = loadout ?? throw new ArgumentNullException(nameof(loadout));
        }

        public AutoHealThreshold GetThreshold()
        {
            return Data.Threshold;
        }

        public SupplySlotData GetSlot(int index)
        {
            return Data.GetSlot(index);
        }

        internal bool TryReserve(out string error)
        {
            if (Reserved == true)
            {
                throw new InvalidOperationException("출전 물약은 한 번만 예약합니다.");
            }
            var types = new[] { ConsumableType.SmallHealingPotion, ConsumableType.PartyHealingPotion };
            for (var i = 0; i < types.Length; i++)
            {
                if (Loadout.GetAssigned(types[i]) > Inventory.GetOwned(types[i]))
                {
                    error = "편성한 물약의 보유 수량이 부족합니다.";
                    return false;
                }
            }
            Data = new CombatSupplyData(Loadout);
            for (var i = 0; i < SupplyLoadout.SlotCount; i++)
            {
                var slot = Data.GetSlot(i);
                if (slot.Count > 0)
                {
                    Inventory.Remove(slot.Type, slot.Count);
                }
            }
            Reserved = true;
            error = string.Empty;
            return true;
        }

        internal void Consume(int index)
        {
            if (Reserved == false)
            {
                throw new InvalidOperationException("예약된 공급품만 사용할 수 있습니다.");
            }
            Data.Consume(index);
        }

        internal void ReturnUnused()
        {
            if (Reserved == false)
            {
                return;
            }
            for (var i = 0; i < SupplyLoadout.SlotCount; i++)
            {
                var slot = Data.GetSlot(i);
                if (slot.Count > 0)
                {
                    Inventory.Add(slot.Type, slot.Count);
                }
            }
            // 다음 출전 준비안은 실제로 남은 재고 안에서만 유지합니다.
            var types = new[] { ConsumableType.SmallHealingPotion, ConsumableType.PartyHealingPotion };
            for (var i = 0; i < types.Length; i++)
            {
                var available = Inventory.GetOwned(types[i]);
                for (var index = 0; index < SupplyLoadout.SlotCount; index++)
                {
                    var slot = Loadout.GetSlot(index);
                    if (slot.Type == types[i])
                    {
                        var count = Math.Min(available, slot.Count);
                        Loadout.SetSlot(index, slot.Type, count);
                        available -= count;
                    }
                }
            }
            Reserved = false;
        }
    }
}
