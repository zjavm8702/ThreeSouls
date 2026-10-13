using System;

namespace ThreeSouls.Data
{
    /// <summary>
    /// 사용자 재화와 인벤토리 및 장착 데이터의 소유권을 보관합니다.
    /// </summary>
    public sealed partial class UserData
    {
        public CampaignProgress Campaign { get; } = new();
        public int Gold { get; private set; }
        public int ChestCount { get; private set; }
        public int EnhancementStones { get; private set; }
        public int GrowthStones { get; private set; }
        public ThreeSouls.Growth.HeroGrowthCollection HeroGrowth { get; } = new();
        public ThreeSouls.Runes.RuneCollection Runes { get; } = new();
        public ThreeSouls.Quests.QuestJournal Quests { get; } = new();
        public ThreeSouls.Dungeon.DungeonPreparationData DungeonPreparation { get; } = new();
        public StageData MaxStageData { get; set; } = new();
        public Inventory Inventory { get; } = new();
        public HeroEquipmentCollection Equipment { get; } = new();
        public ConsumableInventory Consumables { get; } = new();
        public SupplyLoadout Supplies { get; } = new();

        internal void SpendGold(int amount)
        {
            if (amount < 0 || Gold < amount)
            {
                throw new InvalidOperationException("보유 골드와 비용을 검증한 뒤 차감해야 합니다.");
            }

            Gold -= amount;
        }

        internal void PayHeroGrowthCost(int gold, int stones)
        {
            if (gold < 0 || stones < 0 || Gold < gold || GrowthStones < stones)
            {
                throw new InvalidOperationException("영웅 성장 비용을 검증한 뒤 차감해야 합니다.");
            }

            Gold -= gold;
            GrowthStones -= stones;
        }

        internal void CreditGrowthStones(int amount)
        {
            if (amount < 0 || (long)GrowthStones + amount > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }

            GrowthStones += amount;
        }

        internal void CreditShopGold(int amount)
        {
            if (amount <= 0 || (long)Gold + amount > int.MaxValue)
            {
                throw new InvalidOperationException("판매 금액과 지갑 상한을 검증한 뒤 지급해야 합니다.");
            }

            Gold += amount;
        }

        internal void AddCombatReward(int gold, int chestCount)
        {
            Gold = (int)Math.Min(int.MaxValue, (long)Gold + Math.Max(0, gold));
            ChestCount = (int)Math.Min(int.MaxValue, (long)ChestCount + Math.Max(0, chestCount));
        }

        internal void AddEnhancementStones(int amount)
        {
            if (amount < 0 || (long)EnhancementStones + amount > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }

            EnhancementStones += amount;
        }

        internal void PayEnhancementCost(int gold, int stones)
        {
            if (gold < 0 || stones < 0 || Gold < gold || EnhancementStones < stones)
            {
                throw new InvalidOperationException("강화 비용을 검증한 뒤 차감해야 합니다.");
            }

            Gold -= gold;
            EnhancementStones -= stones;
        }
    }
}
