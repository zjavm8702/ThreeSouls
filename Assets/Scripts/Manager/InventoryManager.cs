using System;
using ThreeSouls.Data;
using ThreeSouls.Enumeration;

namespace ThreeSouls.Manager
{
    /// <summary>
    /// 아이템 획득과 삭제 요청을 검증하며 장착 중 삭제 금지를 연결합니다.
    /// </summary>
    public sealed class InventoryManager : BaseManager
    {
        private Inventory Inventory;
        private EquipmentManager Equipment;
        private Inventory PendingCombatItems;

        public static InventoryManager Get() => GameManager.Instance.Get<InventoryManager>();

        public override void Initialize()
        {
        }

        public override void OnSceneChanged()
        {
        }

        internal void Configure(Inventory inventory, EquipmentManager equipment)
        {
            Inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            Equipment = equipment ?? throw new ArgumentNullException(nameof(equipment));
        }

        public bool TryAddEquipment(EquipmentInstance item)
        {
            if (CanAddEquipment(item) == false)
            {
                return false;
            }

            return Inventory.TryAdd(item);
        }

        internal bool CanAddEquipment(EquipmentInstance item)
        {
            RequireConfigured();
            return Equipment.IsValidEquipment(item) == true &&
                Inventory.GetCount() + (PendingCombatItems?.GetCount() ?? 0) < Inventory.Capacity &&
                Inventory.FindEquipment(item.InstanceId) == null &&
                PendingCombatItems?.FindEquipment(item.InstanceId) == null;
        }

        internal bool TryAddShopEquipment(EquipmentInstance item)
        {
            return CanAddEquipment(item) == true && Inventory.TryAdd(item, false) == true;
        }

        internal bool TryRemoveShopEquipment(string instanceId)
        {
            RequireConfigured();
            var item = Inventory.FindEquipment(instanceId);
            return item != null && item.Growth.Locked == false && Equipment.GetOwner(instanceId) == null &&
                Inventory.TryRemove(instanceId, false) == true;
        }

        internal void NotifyShopInventoryChanged()
        {
            Inventory.NotifyChanged();
        }

        /// <summary>
        /// 이번 던전의 아이템을 보유 인벤토리와 분리해 준비합니다.
        /// </summary>
        internal void BeginCombatRewards()
        {
            RequireConfigured();

            if (PendingCombatItems != null)
            {
                throw new InvalidOperationException("이전 던전의 아이템 보상을 먼저 확정하거나 취소해야 합니다.");
            }

            PendingCombatItems = new Inventory(Inventory.Capacity);
        }

        internal bool TryCollectCombatEquipment(EquipmentInstance item)
        {
            RequireConfigured();

            if (PendingCombatItems == null || Equipment.IsValidEquipment(item) == false ||
                Inventory.GetCount() + PendingCombatItems.GetCount() >= Inventory.Capacity ||
                Inventory.FindEquipment(item.InstanceId) != null)
            {
                return false;
            }

            return PendingCombatItems.TryAdd(item);
        }

        /// <summary>
        /// 승리한 던전의 임시 아이템만 보유 인벤토리에 추가하며 패배 보상은 폐기합니다.
        /// </summary>
        internal void CompleteCombatRewards(CombatOutcome outcome)
        {
            RequireConfigured();

            if (PendingCombatItems == null)
            {
                throw new InvalidOperationException("확정할 던전 아이템 보상이 없습니다.");
            }

            if (outcome != CombatOutcome.Victory && outcome != CombatOutcome.Defeat)
            {
                throw new ArgumentOutOfRangeException(nameof(outcome));
            }

            var items = outcome == CombatOutcome.Victory ? PendingCombatItems.GetItems() : Array.Empty<EquipmentInstance>();
            PendingCombatItems = null;
            Inventory.AddBatch(items);
        }

        internal void CancelCombatRewards()
        {
            PendingCombatItems = null;
        }

        public bool TryDiscard(string instanceId)
        {
            RequireConfigured();

            var item = Inventory.FindEquipment(instanceId);

            if (item == null || item.Growth.Locked == true || Equipment.GetOwner(instanceId) != null)
            {
                return false;
            }

            return Inventory.TryRemove(instanceId);
        }

        public override void Release()
        {
            CancelCombatRewards();
            Inventory = null;
            Equipment = null;
        }

        private void RequireConfigured()
        {
            if (Inventory == null || Equipment == null)
            {
                throw new InvalidOperationException("InventoryManager에 사용자 데이터를 먼저 연결해야 합니다.");
            }
        }
    }
}
