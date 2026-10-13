using System;
using System.Collections.Generic;
using System.Linq;
using ThreeSouls.Data.Generated;
using ThreeSouls.Character;
using ThreeSouls.Data;
using ThreeSouls.Enumeration;
using ThreeSouls.Equipment;
using ThreeSouls.Interface;
using ThreeSouls.Manager;
using ThreeSouls.Utility;
using UnityEngine;

namespace ThreeSouls.Combat
{
    /// <summary>
    /// 던전 보상을 임시 집계하고 승리 결과에서만 골드와 수집 아이템을 지급합니다.
    /// </summary>
    internal sealed class CombatRewardHandler : IEventListener<Event_EquipmentCollected>
    {
        public int CallbackOrder => 0;

        private readonly CombatRewardSummary Summary = new();
        private readonly List<EquipmentWorldDrop> WorldDrops = new();
        private int StageNumber;
        private CampaignStageTableDataRow Campaign;
        private CampaignEquipmentLootTableDataRow[] LootPool;
        private float SpecialItemDropMultiplier;
        private float StartedAt;
        private bool Started;
        private float CompletedElapsed;
        internal float GetElapsedSeconds()=>Completed?CompletedElapsed:Started?Mathf.Max(0,Time.time-StartedAt):0;
        private InventoryManager InventoryRewards;
        private float LuckDropMultiplier;
        private string[] HeroKeys = Array.Empty<string>();
        private bool Completed;
        private ThreeSouls.Runes.RuneCombatSnapshot Runes;

        internal void Initialize(int stageNumber, float specialItemDropMultiplier,
            ThreeSouls.Runes.RuneCombatSnapshot runes = null, int regionNumber = 1)
        {
            Release();
            Runes = runes;
            StageNumber = stageNumber;
            Campaign = CampaignBalance.GetStage(regionNumber, stageNumber);
            LootPool = CampaignBalance.GetLootPool(Campaign.Progress);
            SpecialItemDropMultiplier = specialItemDropMultiplier;
            StartedAt = 0f;
            InventoryRewards = InventoryManager.Get();
            HeroKeys = (string[])TableManager.Get().GetThreeSoulsRuleDataTable().BaseCharacterID.Clone();
            LuckDropMultiplier = UserManager.Get().GetHeroGrowthManager().GetPartyDropMultiplier(HeroKeys);
            InventoryRewards.BeginCombatRewards();
            EventUtility.AddListener<Event_EquipmentCollected>(this);
        }

        internal void BeginCombat()
        {
            StartedAt = Time.time;
            Started = true;
        }

        internal CombatResultData Complete(int regionNumber, int stageNumber, CombatOutcome outcome)
        {
            if (Completed == true)
            {
                throw new InvalidOperationException("전투 보상은 한 번만 정산해야 합니다.");
            }

            CompletedElapsed=GetElapsedSeconds();
            Completed = true;
            for (var i = 0; i < WorldDrops.Count; i++)
            {
                var drop = WorldDrops[i];

                if (drop == null)
                {
                    continue;
                }

                if (outcome == CombatOutcome.Victory)
                {
                    drop.TryCollect();
                }

                drop.enabled = false;
            }

            EventUtility.RemoveListener<Event_EquipmentCollected>(this);

            InventoryRewards.CompleteCombatRewards(outcome);
            var grantedGold = 0;
            var stones = 0;
            var runeReward = string.Empty;
            var growthReward = default(ThreeSouls.Growth.HeroGrowthReward);

            if (outcome == CombatOutcome.Victory)
            {
                var user = UserManager.Get();
                var previousGold = user.UData.Gold;
                user.AddCombatReward(Summary.GetPendingGold(), 0);
                grantedGold = user.UData.Gold - previousGold;
                growthReward = user.GetHeroGrowthManager().GrantVictoryRewards(HeroKeys, Campaign.Progress, Campaign);
                user.UData.Campaign.RecordVictory(regionNumber, stageNumber);
                stones = Math.Min(Campaign.EnhancementStones, int.MaxValue - user.UData.EnhancementStones);
                user.AddEnhancementStones(stones);
                if (UnityEngine.Random.value < Campaign.RuneChance)
                {
                    var pool = LobbyTables.Get<CampaignRuneLootTableDataTable>().Items.Where(x => x.RequiredProgress <= user.UData.Campaign.ClearedProgress).ToArray();
                    var roll = UnityEngine.Random.value * pool.Sum(x => x.Weight);
                    foreach (var row in pool)
                    {
                        roll -= row.Weight;
                        if (roll <= 0) { runeReward = user.GetRuneManager().GrantCampaignRune(row.Key); break; }
                    }
                }
                user.GetQuestManager().RecordVictory(regionNumber, stageNumber);
            }
            else
            {
                Summary.Clear();
            }

            return Summary.CreateResult(regionNumber, stageNumber, CompletedElapsed, grantedGold,
                growthReward.Experience, growthReward.Stones, stones, runeReward);
        }

        internal void Release()
        {
            EventUtility.RemoveListener<Event_EquipmentCollected>(this);
            InventoryRewards?.CancelCombatRewards();
            InventoryRewards = null;
            Summary.Clear();
            HeroKeys = Array.Empty<string>();
            LuckDropMultiplier = 1;
            Completed = false;
            Started = false;
            CompletedElapsed = 0;
            Runes = null;

            for (var i = 0; i < WorldDrops.Count; i++)
            {
                if (WorldDrops[i] != null)
                {
                    UnityEngine.Object.Destroy(WorldDrops[i].gameObject);
                }
            }

            WorldDrops.Clear();
        }

        public void OnEvent<TSender>(TSender sender, Event_EquipmentCollected eventData) where TSender : class
        {
            Summary.AddItem(eventData.Item.ItemKey);
        }

        internal int GrantMonsterReward(Monster monster)
        {
            if (monster.TryMarkRewardGranted() == false)
            {
                return 0;
            }

            var rows = TableManager.Get().GetDropTableDataTable(monster.CharacterData.CharacterKey);

            if (rows.Count == 0)
            {
                ThreeSoulsLog.Warning("Drop table not found: " + monster.CharacterData.CharacterKey);

                return 0;
            }

            var gold = 0;
            var rewardItems = CombatDropResolver.Resolve(
                rows,
                StageNumber,
                () => UnityEngine.Random.value,
                msg => ThreeSoulsLog.Warning(msg),
                (monster.IsSpecialReward == true ? SpecialItemDropMultiplier * (Runes?.EquipmentDropMultiplier ?? 1) : 1f) * LuckDropMultiplier);

            for (var rewardIndex = 0; rewardIndex < rewardItems.Count; rewardIndex++)
            {
                var reward = rewardItems[rewardIndex];

                if (reward.Type == RewardType.Gold)
                {
                    gold = (int)Math.Min(int.MaxValue, (long)gold + reward.Amount);
                    continue;
                }

                var equipment = EquipmentManager.Get();

                for (int i = 0; i < reward.Amount; i++)
                {
                    if (equipment.TryCreateEquipment(reward.Key, out var item, out var rarity) == false)
                    {
                        ThreeSoulsLog.Warning("Equipment not found/invalid: " + reward.Key);
                        break;
                    }

                    var drop = EquipmentWorldDrop.Create(monster.transform.position, item, rarity);
                    WorldDrops.Add(drop);
                }
            }

            var chance = Campaign.EquipmentChance * LuckDropMultiplier * (monster.IsSpecialReward ? SpecialItemDropMultiplier * (Runes?.EquipmentDropMultiplier ?? 1) : 1);
            if (UnityEngine.Random.value < Mathf.Clamp01(chance))
            {
                var key = CampaignBalance.RollEquipment(LootPool, Campaign.Progress, UnityEngine.Random.value);
                if (EquipmentManager.Get().TryCreateEquipment(key, out var item, out var rarity))
                    WorldDrops.Add(EquipmentWorldDrop.Create(monster.transform.position, item, rarity));
            }
            gold = (int)Math.Min(int.MaxValue, Math.Floor(gold * (double)Campaign.GoldMultiplier));
            if (gold > 0)
            {
                if (monster.IsSpecialReward == true && Runes != null)
                {
                    gold = ThreeSouls.Runes.RuneCombatSnapshot.CalculateGold(gold, Runes.GoldMultiplier);
                }

                Summary.AddGold(gold);
            }

            return gold;
        }
    }
}
