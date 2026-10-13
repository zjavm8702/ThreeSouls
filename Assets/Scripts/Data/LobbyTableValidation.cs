using System;
using System.Linq;
using ThreeSouls.Data.Generated;

namespace ThreeSouls.Data
{
    /// <summary>
    /// 로비 테이블의 값, 참조 및 현재 구현이 지원하는 정책을 초기화 경계에서 검사합니다.
    /// </summary>
    public static class LobbyTableValidation
    {
        public static void Validate()
        {
            EquipmentShopCatalog.Validate();
            ThreeSouls.Growth.HeroGrowthRules.Validate();
            ThreeSouls.Runes.RuneRules.Validate();
            ThreeSouls.Dungeon.DungeonSelectionRules.Validate();
            CampaignBalance.Validate();
            var rule = LobbyTables.GetRules();
            Require(rule.InventoryCapacity > 0 && rule.EquipmentMaxLevel > 0, "인벤토리/강화 상한");
            Require(rule.InitialEquipmentLevel >= 0 && rule.InitialEquipmentLevel <= rule.EquipmentMaxLevel &&
                rule.InitialInvestedStones >= 0, "신규 장비 성장 값");
            Require(rule.BlockLockedDiscard == true && rule.BlockEquippedDiscard == true &&
                rule.BlockLockedDismantle == true && rule.BlockEquippedDismantle == true && rule.SaveImplemented == false,
                "잠금/장착 삭제 보호와 저장 미구현 정책");
            var upgrades = LobbyTables.Get<EquipmentUpgradeTableDataTable>().Items;
            Require(upgrades.Length == rule.EquipmentMaxLevel, "단계별 강화 비용 수");
            for (var level = 0; level < rule.EquipmentMaxLevel; level++)
            {
                var row = upgrades.Single(x => x.FromLevel == level);
                Require(row.ToLevel == level + 1 && row.GoldCost >= 0 && row.StoneCost > 0 && row.SuccessRate == 1,
                    "성공 보장 강화 비용: " + level);
            }
            var growth = LobbyTables.Get<EquipmentGrowthRuleTableDataTable>().Items;
            Require(growth.Length == 3 && growth.Select(x => x.EquipSlot).Distinct().Count() == 3, "슬롯별 강화 보정");
            for (var i = 0; i < growth.Length; i++)
            {
                Require(IsFinitePositive(growth[i].AttackRatePerLevel) == true && IsFinitePositive(growth[i].HPRatePerLevel) == true &&
                    IsFinitePositive(growth[i].SpeedBonusPerLevel) == true, "강화 보정 수치");
            }
            var dismantle = LobbyTables.Get<EquipmentDismantleTableDataTable>().Items;
            Require(dismantle.Length == Enum.GetValues(typeof(ThreeSouls.Data.Generated.ItemRarity)).Length && dismantle.Select(x => x.Rarity).Distinct().Count() == dismantle.Length, "등급별 분해 정의");
            for (var i = 0; i < dismantle.Length; i++)
            {
                var row = dismantle[i];
                Require(row.BaseStoneReward >= 0 && row.InvestedStoneReturnRate >= 0 && row.InvestedStoneReturnRate <= 1 &&
                    row.RoundDown == true && row.GoldRefund == 0, "분해 보상/버림/골드 미환급");
            }
            var supply = LobbyTables.GetSupplyRules();
            Require(supply.SlotCount == SupplyLoadout.SlotCount && supply.MaximumPerSlot > 0 && supply.MaximumPerSlot <= 99 &&
                supply.LimitToOwned == true && supply.ConsumeOnAssign == false && supply.UseEquipmentCapacity == false &&
                supply.CooldownImplemented == true && IsFinitePositive(supply.PlannedCooldownSeconds) == true,
                "공급품 2슬롯/재고 제한/편성 미소비/전투 공통 쿨타임");
            Require(supply.MinPurchase > 0 && supply.MaxPurchase >= supply.MinPurchase && supply.MaxOwnedPerType > 0 &&
                supply.BuyAmounts.Length == 2 && supply.BuyAmounts.All(x => x >= supply.MinPurchase && x <= supply.MaxPurchase) == true,
                "구매 버튼 두 수량과 요청 범위/보유 한도");
            var potions = LobbyTables.Get<ConsumableTableDataTable>().Items;
            Require(potions.Length == 2 && potions.Select(x => x.ConsumableType).Distinct().Count() == 2 &&
                potions.Any(x => x.ConsumableType == ConsumableType.None) == false, "두 물약 종류");
            for (var i = 0; i < potions.Length; i++)
            {
                var row = potions[i];
                Require(row.GoldPrice > 0 && row.HealMaxHPRatio > 0 && row.HealMaxHPRatio <= 1 && row.CombatHealImplemented == true &&
                    string.IsNullOrWhiteSpace(row.IconPath) == false && string.IsNullOrWhiteSpace(row.IconSpriteName) == false,
                    "물약 가격/효과/아이콘/전투 회복");
                Require(row.TargetScope == (row.ConsumableType == ConsumableType.SmallHealingPotion ? "SingleHero" : "LivingParty"),
                    "단일/생존 부대 회복 대상");
                Require(string.IsNullOrWhiteSpace(LobbyTables.GetText(row.NameStrKey)) == false &&
                    string.IsNullOrWhiteSpace(LobbyTables.GetText(row.EffectStrKey)) == false, "물약 문자열");
            }
            var thresholds = LobbyTables.Get<AutoHealTableDataTable>().Items;
            Require(thresholds.Length == 4 && thresholds.Select(x => x.Threshold).Distinct().Count() == 4 &&
                thresholds.Single(x => x.IsDefault).Threshold == supply.DefaultAutoHeal, "자동 회복 옵션/기본값");
            for (var i = 0; i < thresholds.Length; i++)
            {
                Require(thresholds[i].HPRatio >= 0 && thresholds[i].HPRatio <= 1 && thresholds[i].AutoUseImplemented == true,
                    "자동 사용 체력 기준");
                LobbyTables.GetText(thresholds[i].StrKey);
            }
            var village = LobbyTables.GetVillageRules();
            var heroes = LobbyTables.Get<LobbyHeroTableDataTable>().Items;
            Require(village.HeroKeys.Length == 3 && village.HeroKeys.Distinct().Count() == 3 && heroes.Length == 3 &&
                village.MoveSpeed > 0 && village.Spacing > 0 && village.BodyRadius > 0 &&
                IsFinitePositive(village.MoveSpeed) == true && IsFinitePositive(village.Spacing) == true &&
                IsFinitePositive(village.BodyRadius) == true, "세 영웅 대열/이동 수치");
            for (var i = 0; i < village.HeroKeys.Length; i++)
            {
                var hero = heroes.Single(x => x.HeroKey == village.HeroKeys[i]);
                LobbyTables.GetText(hero.NameStrKey);
                Require(LobbyTables.GetCharacter(hero.HeroKey) != null, "영웅 캐릭터 참조");
            }
            Require(village.MoveInputTabs.Length > 0 && village.MoveInputTabs.All(x => Enum.TryParse<ThreeSouls.UI.LobbyTab>(x, out _)) == true,
                "이동 허용 탭");
            var start = LobbyTables.Get<LobbyTestStartTableDataTable>().Items.Single(x => x.Key == "TestVillage");
            Require(start.StartingGold >= 0 && start.StartingEnhancementStones >= 0 &&
                start.StartingSmallPotions >= 0 && start.StartingPartyPotions >= 0 &&
                start.StartingSmallPotions <= supply.MaxOwnedPerType && start.StartingPartyPotions <= supply.MaxOwnedPerType,
                "테스트 지급 값");
            var equipment = LobbyTables.Get<LobbyStartingEquipmentTableDataTable>().Items.Where(x => x.PresetKey == start.Key).ToArray();
            Require(equipment.Length > 0 && equipment.Sum(x => (long)x.Count) <= rule.InventoryCapacity, "시작 장비 용량");
            for (var i = 0; i < equipment.Length; i++)
            {
                var row = equipment[i];
                var definition = LobbyTables.GetEquipment(row.ItemKey);
                Require(row.Count > 0 && definition != null, "시작 장비 참조/수량");
                Require(string.IsNullOrEmpty(row.EquipToHeroKey) == true ||
                    (village.HeroKeys.Contains(row.EquipToHeroKey) == true &&
                    EquipmentRules.CanEquip(definition, LobbyTables.GetCharacter(row.EquipToHeroKey)) == true),
                    "시작 장착 가능 영웅");
            }
            var options = LobbyTables.Get<LobbyOptionTableDataTable>().Items;
            Require(options.Select(x => x.Category + "/" + x.OptionKey).Distinct().Count() == options.Length, "옵션 중복 키");
            for (var i = 0; i < options.Length; i++)
            {
                LobbyTables.GetText(options[i].StrKey);
            }
        }

        private static bool IsFinitePositive(float value)
        {
            return float.IsNaN(value) == false && float.IsInfinity(value) == false && value >= 0;
        }

        private static void Require(bool valid, string field)
        {
            if (valid == false)
            {
                throw new InvalidOperationException("로비 테이블 값 또는 지원 정책을 확인하세요: " + field);
            }
        }
    }
}

