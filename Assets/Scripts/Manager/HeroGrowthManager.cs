using System;
using ThreeSouls.Data;
using ThreeSouls.Data.Generated;
using ThreeSouls.Growth;
using ThreeSouls.Interface;

namespace ThreeSouls.Manager
{
    /// <summary>
    /// 사용자 소유의 영웅 성장에 대한 해금, 비용 검증과 확정 및 알림을 담당합니다.
    /// </summary>
    public sealed class HeroGrowthManager : IHeroGrowthSource
    {
        private readonly UserManager User;
        private readonly HeroGrowthCollection Heroes;
        private bool Ready;
        private bool LobbySession;
        private IHeroGrowthSource.OnHeroGrowthChangedDelegate onHeroGrowthChangedEventHandler;
        public event IHeroGrowthSource.OnHeroGrowthChangedDelegate OnHeroGrowthChangedEventHandler
        {
            add
            {
                onHeroGrowthChangedEventHandler -= value;
                onHeroGrowthChangedEventHandler += value;
            }
            remove
            {
                onHeroGrowthChangedEventHandler -= value;
            }
        }

        internal HeroGrowthManager(UserManager user, HeroGrowthCollection heroes)
        {
            User = user ?? throw new ArgumentNullException(nameof(user));
            Heroes = heroes ?? throw new ArgumentNullException(nameof(heroes));
        }

        internal void ApplyTableRules()
        {
            HeroGrowthRules.Validate();
            var starts = LobbyTables.Get<HeroGrowthStartTableDataTable>().Items;
            for (var i = 0; i < starts.Length; i++)
            {
                var start = starts[i];
                if (Heroes.FindHero(start.HeroKey) == null)
                {
                    Heroes.Add(new HeroGrowthData(start.HeroKey, start.InitialLevel,
                        start.InitialExperience, HeroGrowthRules.GetRule().MaxLevel));
                }
            }

            Ready = true;
        }

        public void BeginLobbySession()
        {
            RequireReady();
            LobbySession = true;
        }

        public void EndLobbySession()
        {
            LobbySession = false;
        }

        public HeroGrowthData GetHero(string heroKey)
        {
            RequireReady();
            return Heroes.FindHero(heroKey)
                ?? throw new ArgumentException("등록된 고정 영웅이 아닙니다.", nameof(heroKey));
        }

        public HeroGrowthBonuses GetBonuses(CharacterTableDataRow hero)
        {
            return HeroGrowthRules.Calculate(hero, GetHero(hero.StrKey));
        }

        public bool TryQuoteUpgrade(string heroKey, HeroStat stat, out HeroUpgradeQuote quote, out string error)
        {
            RequireReady();
            quote = default;
            var hero = Heroes.FindHero(heroKey);
            if (hero == null || Enum.IsDefined(typeof(HeroStat), stat) == false)
            {
                error = "영웅과 능력치를 선택하세요.";
                return false;
            }

            quote = HeroGrowthRules.GetQuote(hero, stat);
            var definition = HeroGrowthRules.GetStat(stat);
            if (LobbySession == false)
            {
                error = "능력치 강화는 마을에서 진행하세요.";
                return false;
            }

            if (hero.Level < definition.UnlockLevel)
            {
                error = $"Lv.{definition.UnlockLevel}에 해금됩니다.";
                return false;
            }

            if (hero.GetRank(stat) >= HeroGrowthRules.GetRankLimit(stat, hero.Level))
            {
                error = hero.GetRank(stat) >= definition.MaximumRank ? "최대 강화 단계입니다." :
                    "현재 레벨의 강화 한도입니다. 레벨이 오르면 더 강화할 수 있습니다.";
                return false;
            }

            if (User.UData.Gold < quote.Gold || User.UData.GrowthStones < quote.Stones)
            {
                error = $"부족: 골드 {Math.Max(0, quote.Gold - User.UData.Gold):N0} / 성장석 {Math.Max(0, quote.Stones - User.UData.GrowthStones):N0}";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public bool TryUpgrade(HeroUpgradeQuote expected, out string error)
        {
            if (TryQuoteUpgrade(expected.HeroKey, expected.Stat, out var current, out error) == false)
            {
                return false;
            }

            if (current.Rank != expected.Rank || current.Gold != expected.Gold || current.Stones != expected.Stones)
            {
                error = "강화 단계나 비용이 변경되었습니다. 다시 확인하세요.";
                return false;
            }

            var hero = GetHero(expected.HeroKey);
            User.PayHeroGrowthCost(current.Gold, current.Stones);
            hero.IncreaseRank(expected.Stat, HeroGrowthRules.GetRankLimit(expected.Stat, hero.Level));
            ThreeSouls.Audio.GameAudio.Play(ThreeSouls.Audio.SoundCue.Upgrade);
            User.NotifyHeroGrowthCurrencies();
            onHeroGrowthChangedEventHandler?.Invoke(hero.HeroKey);
            return true;
        }

        /// <summary>
        /// 경험치는 레벨과 강화 가능 범위만 늘리며 능력치 강화 단계를 자동으로 올리지 않습니다.
        /// </summary>
        public void GrantExperience(string heroKey, int amount)
        {
            var hero = GetHero(heroKey);
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }

            if (amount == 0 || hero.Level == HeroGrowthRules.GetRule().MaxLevel)
            {
                return;
            }

            CalculateExperience(hero, amount, out var level, out var experience);
            hero.ApplyExperience(level, experience);
            onHeroGrowthChangedEventHandler?.Invoke(hero.HeroKey);
        }

        public float GetPartyDropMultiplier(string[] heroKeys)
        {
            if (heroKeys == null || heroKeys.Length == 0)
            {
                throw new ArgumentException("출전 영웅을 연결해야 합니다.", nameof(heroKeys));
            }

            var total = 0f;
            for (var i = 0; i < heroKeys.Length; i++)
            {
                var hero = GetHero(heroKeys[i]);
                total += hero.GetRank(HeroStat.Luck) * HeroGrowthRules.GetStat(HeroStat.Luck).GainPerRank;
            }

            return HeroGrowthRules.GetLuckDropMultiplier(total / heroKeys.Length);
        }

        internal HeroGrowthReward GrantVictoryRewards(string[] heroKeys, int stage, ThreeSouls.Data.Generated.CampaignStageTableDataRow campaign = null)
        {
            RequireReady();
            if (stage < 1 || heroKeys == null || heroKeys.Length == 0)
            {
                throw new ArgumentException("승리 보상의 스테이지와 영웅이 필요합니다.");
            }

            var rule = HeroGrowthRules.GetRule();
            var amount = campaign?.Experience ?? checked(rule.VictoryExperience + rule.ExperiencePerStage * (stage - 1));
            var stones = campaign?.GrowthStones ?? checked(rule.VictoryGrowthStones + (stage - 1) / rule.StagesPerBonusStone);
            var heroes = new HeroGrowthData[heroKeys.Length];
            var levels = new int[heroKeys.Length];
            var experience = new int[heroKeys.Length];
            for (var i = 0; i < heroKeys.Length; i++)
            {
                heroes[i] = GetHero(heroKeys[i]);
                for (var other = 0; other < i; other++)
                {
                    if (heroKeys[other] == heroKeys[i])
                    {
                        throw new ArgumentException("출전 영웅이 중복되었습니다.");
                    }
                }

                CalculateExperience(heroes[i], amount, out levels[i], out experience[i]);
            }

            if ((long)User.UData.GrowthStones + stones > int.MaxValue)
            {
                throw new InvalidOperationException("성장석 보상 상한을 초과했습니다.");
            }

            for (var i = 0; i < heroes.Length; i++)
            {
                heroes[i].ApplyExperience(levels[i], experience[i]);
            }

            User.CreditGrowthStones(stones);
            User.NotifyHeroGrowthCurrencies();
            for (var i = 0; i < heroes.Length; i++)
            {
                onHeroGrowthChangedEventHandler?.Invoke(heroes[i].HeroKey);
            }

            return new HeroGrowthReward(amount, stones);
        }

        internal void Release()
        {
            LobbySession = false;
            onHeroGrowthChangedEventHandler = null;
            Ready = false;
        }

        private static void CalculateExperience(HeroGrowthData hero, int amount, out int level, out int experience)
        {
            var remaining = (long)hero.Experience + amount;
            level = hero.Level;
            var maximum = HeroGrowthRules.GetRule().MaxLevel;
            while (level < maximum && remaining >= HeroGrowthRules.GetExperienceRequired(level))
            {
                remaining -= HeroGrowthRules.GetExperienceRequired(level);
                level++;
            }

            experience = level == maximum ? 0 : (int)remaining;
        }

        private void RequireReady()
        {
            if (Ready == false)
            {
                throw new InvalidOperationException("영웅 성장 테이블을 먼저 적용해야 합니다.");
            }
        }
    }
}
