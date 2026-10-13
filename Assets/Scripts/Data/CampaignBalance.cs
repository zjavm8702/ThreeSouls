using System;
using System.Linq;
using ThreeSouls.Data.Generated;
using UnityEngine;

namespace ThreeSouls.Data
{
    /// <summary>스테이지별 난이도·장비 풀·룬 해금. 원본 캐릭터 행은 수정하지 않습니다.</summary>
    public static class CampaignBalance
    {
        public static void Validate()
        {
            var stages = LobbyTables.Get<CampaignStageTableDataTable>().Items;
            if (stages.Length != 20) throw new InvalidOperationException("캠페인 20단계가 필요합니다.");
            for (var p = 1; p <= 20; p++)
            {
                var row = stages.Single(x => x.Progress == p);
                if (CampaignProgress.Index(row.Region, row.Stage) != p || row.RequiredHeroLevel < 1 ||
                    row.RequiredHeroLevel > ThreeSouls.Growth.HeroGrowthRules.GetRule().MaxLevel ||
                    !Positive(row.EnemyHP) || !Positive(row.EnemyAttack) || !Positive(row.GoldMultiplier) ||
                    !Probability(row.EquipmentChance) || !Probability(row.RuneChance) ||
                    row.Experience <= 0 || row.GrowthStones < 0 || row.EnhancementStones < 0 ||
                    row.RecommendedUpgrade < 0 || row.RecommendedUpgrade > LobbyTables.GetRules().EquipmentMaxLevel ||
                    p > 1 && row.RequiredHeroLevel < stages.Single(x => x.Progress == p - 1).RequiredHeroLevel)
                    throw new InvalidOperationException("캠페인 수치 오류: " + p);
            }
            var loot = LobbyTables.Get<CampaignEquipmentLootTableDataTable>().Items;
            if (loot.Select(x => x.ItemKey).Distinct().Count() != loot.Length ||
                loot.Any(x => x.Tier < 1 || x.Tier > 20 || !Positive(x.Weight) || LobbyTables.GetEquipment(x.ItemKey) == null) ||
                Enumerable.Range(1,20).Any(p => !loot.Any(x => x.Tier == p)))
                throw new InvalidOperationException("장비 드롭 풀의 키·티어·가중치 오류");
            var runes = LobbyTables.Get<CampaignRuneLootTableDataTable>().Items;
            if (runes.Select(x => x.Key).Distinct().Count() != runes.Length || runes.Length == 0 ||
                runes.Any(x => !Positive(x.Weight) || x.RequiredProgress < 0 || x.RequiredProgress > 20 ||
                    ThreeSouls.Runes.RuneRules.GetDefinition(x.Key)?.Enabled != true) || !runes.Any(x => x.RequiredProgress == 0))
                throw new InvalidOperationException("룬 드롭 풀의 해금·가중치 오류");
        }
        private static bool Positive(float x) => !float.IsNaN(x) && !float.IsInfinity(x) && x > 0;
        private static bool Probability(float x) => !float.IsNaN(x) && x >= 0 && x <= 1;
        public static CampaignStageTableDataRow GetStage(int region, int stage) =>
            LobbyTables.Get<CampaignStageTableDataTable>().Items.Single(x => x.Progress == CampaignProgress.Index(region, stage));
        public static int RuneUnlock(string key) =>
            LobbyTables.Get<CampaignRuneLootTableDataTable>().Items.FirstOrDefault(x => x.Key == key)?.RequiredProgress ?? 0;
        public static string UnlockLabel(int progress) => progress <= 0 ? "해금" : $"{(progress - 1) / 5 + 1}-{(progress - 1) % 5 + 1} 승리 후 해금";
        public static bool CanEnter(ThreeSouls.Manager.UserManager user, int region, int stage, out string error)
        {
            if (!user.UData.Campaign.CanEnter(region, stage)) { error = "선행 스테이지를 먼저 승리하세요."; return false; }
            var required = GetStage(region, stage).RequiredHeroLevel;
            if (LobbyTables.GetVillageRules().HeroKeys.Any(key => user.GetHeroGrowthManager().GetHero(key).Level < required))
            { error = $"부대 Lv.{required} 필요 · 이전 스테이지에서 장비와 경험치를 모으세요."; return false; }
            error = string.Empty;
            return true;
        }
        public static CampaignEquipmentLootTableDataRow[] GetLootPool(int progress) =>
            LobbyTables.Get<CampaignEquipmentLootTableDataTable>().Items.Where(x => x.Tier <= progress && x.Tier >= Math.Max(1, progress - 2)).ToArray();
        public static string RollEquipment(CampaignEquipmentLootTableDataRow[] pool, int progress, float roll)
        {
            if (pool == null || pool.Length == 0 || float.IsNaN(roll) || roll < 0 || roll > 1) throw new ArgumentException("장비 풀/추첨 값 오류");
            // 최신 20%, 직전 50%, 이전 30%. 초기 구간은 존재하는 티어끼리 정규화.
            float Weight(CampaignEquipmentLootTableDataRow row)
            {
                var tierWeight = row.Tier == progress ? .2f : row.Tier == progress - 1 ? .5f : .3f;
                var tierTotal = pool.Where(x => x.Tier == row.Tier).Sum(x => x.Weight);
                return tierWeight * row.Weight / tierTotal;
            }
            var remaining = Mathf.Min(roll, .999999f) * pool.Sum(Weight);
            foreach (var row in pool) { remaining -= Weight(row); if (remaining <= 0) return row.ItemKey; }
            return pool[pool.Length - 1].ItemKey;
        }
        public static CharacterTableDataRow ScaleMonster(CharacterTableDataRow source, int region, int stage)
        {
            var rule = GetStage(region, stage);
            return new CharacterTableDataRow {
                Index=source.Index, StrKey=source.StrKey, MoveSpeed=source.MoveSpeed, IsRunningPlay=source.IsRunningPlay,
                RunningSpeed=source.RunningSpeed, AttackType=source.AttackType, CharacterType=source.CharacterType,
                MaxHP=source.MaxHP*rule.EnemyHP, AttackDmg=source.AttackDmg*rule.EnemyAttack,
                DistanceToTarget=source.DistanceToTarget, CharacterRole=source.CharacterRole, AttackRange=source.AttackRange,
                AttackSpeedMultiplier=source.AttackSpeedMultiplier, DetectDelay=source.DetectDelay,
                ResourceName=source.ResourceName, TargetSearchRadius=source.TargetSearchRadius
            };
        }
    }
}
