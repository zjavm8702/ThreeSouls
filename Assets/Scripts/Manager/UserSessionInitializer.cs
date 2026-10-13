using System;
using ThreeSouls.Data;
using ThreeSouls.Data.Generated;
using ThreeSouls.Growth;

namespace ThreeSouls.Manager
{
    /// <summary>
    /// 저장 연결 전의 시작 지급 테이블을 한 사용자 세션에 적용합니다.
    /// </summary>
    internal static class UserSessionInitializer
    {
        internal static void Prepare(UserManager user)
        {
            var equipment = EquipmentManager.Get();
            var inventory = InventoryManager.Get();
            var rows = LobbyTables.Get<LobbyStartingEquipmentTableDataTable>().Items;
            var start = LobbyTables.Get<LobbyTestStartTableDataTable>().Items[0];
            for (var i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                if (row.PresetKey != "TestVillage")
                {
                    continue;
                }
                for (var copy = 0; copy < row.Count; copy++)
                {
                    if (equipment.TryCreateEquipment(row.ItemKey, out var item, out _) == false ||
                        inventory.TryAddEquipment(item) == false)
                    {
                        throw new InvalidOperationException($"시작 장비 지급 실패: {row.ItemKey}");
                    }
                    if (copy == 0 && string.IsNullOrEmpty(row.EquipToHeroKey) == false &&
                        equipment.TryEquip(row.EquipToHeroKey, item.InstanceId, out var error) == false)
                    {
                        throw new InvalidOperationException(error);
                    }
                }
            }
            user.AddCombatReward(start.StartingGold, 0);
            user.AddEnhancementStones(start.StartingEnhancementStones);
            user.AddGrowthStones(HeroGrowthRules.GetRule().TestStartingGrowthStones);
            var runes = user.GetRuneManager();
            runes.BeginLobbySession();
            runes.GrantTestRunes();
            runes.EndLobbySession();
            var supplies = LobbySupplyManager.Get();
            supplies.Configure(user);
            supplies.GrantStartingPotions(ConsumableType.SmallHealingPotion, start.StartingSmallPotions);
            supplies.GrantStartingPotions(ConsumableType.PartyHealingPotion, start.StartingPartyPotions);
        }
    }
}
