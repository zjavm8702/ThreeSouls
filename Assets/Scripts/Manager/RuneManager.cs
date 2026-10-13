using System;
using ThreeSouls.Data;
using ThreeSouls.Data.Generated;
using ThreeSouls.Runes;

namespace ThreeSouls.Manager
{
    /// <summary>
    /// 룬 획득·제작·강화·장착 요청을 검증하고 전투 중 변경을 차단합니다.
    /// </summary>
    public sealed class RuneManager
    {
        public delegate void OnRunesChangedDelegate();
        private OnRunesChangedDelegate onRunesChangedEventHandler;
        public event OnRunesChangedDelegate OnRunesChangedEventHandler
        {
            add
            {
                onRunesChangedEventHandler -= value;
                onRunesChangedEventHandler += value;
            }
            remove
            {
                onRunesChangedEventHandler -= value;
            }
        }

        private readonly UserManager User;
        private readonly RuneCollection Collection;
        private bool Ready;
        private bool LobbySession;
        private bool CombatSession;
        private RuneQuote Pending;
        private RuneSaleQuote PendingSale;

        internal RuneManager(UserManager user, RuneCollection collection)
        {
            User = user ?? throw new ArgumentNullException(nameof(user));
            Collection = collection ?? throw new ArgumentNullException(nameof(collection));
        }

        internal void ApplyTableRules()
        {
            RuneRules.Validate();
            var rules = LobbyTables.Get<RuneShopRuleTableDataTable>().Items;
            if (rules.Length != 1 || rules[0].SaleGoldRatio <= 0 || rules[0].SaleGoldRatio >= 1 ||
                float.IsNaN(rules[0].SaleGoldRatio) == true || rules[0].InteractionRadius <= 0)
            {
                throw new InvalidOperationException("룬 상점 환급률/접근 반경을 확인하세요.");
            }
            Ready = true;
        }

        public void BeginLobbySession()
        {
            RequireReady();
            if (CombatSession == true)
            {
                throw new InvalidOperationException("전투 종료 후 룬 로비를 시작하세요.");
            }

            LobbySession = true;
        }

        public void EndLobbySession()
        {
            LobbySession = false;
            Pending = null;
            PendingSale = null;
        }

        public void GrantTestRunes()
        {
            RequireReady();
            if (LobbySession == false || CombatSession == true)
            {
                throw new InvalidOperationException("테스트 룬은 마을 준비에서만 지급합니다.");
            }

            if (Collection.HasTestGrant() == true)
            {
                return;
            }

            var rows = LobbyTables.Get<RuneTableDataTable>().Items;
            for (var i = 0; i < rows.Length; i++)
            {
                if (rows[i].TestGrant == true && rows[i].Enabled == true && Collection.GetRank(rows[i].Key) == 0)
                {
                    Collection.Add(rows[i].Key);
                }
            }

            Collection.CompleteTestGrant();
            onRunesChangedEventHandler?.Invoke();
        }

        public int GetRank(string key)
        {
            RequireReady();
            return Collection.GetRank(key);
        }

        public bool IsEquipped(string key)
        {
            var row = RuneRules.GetDefinition(key);
            return row != null && Collection.GetEquipped(RuneRules.GetSocket(row)) == key;
        }

        public string GetEquipped(string socket)
        {
            RequireReady();
            return Collection.GetEquipped(socket);
        }

        public bool TryEquip(string key, out string error)
        {
            if (CanChange(key, out var row, out error) == false)
            {
                return false;
            }

            if (Collection.GetRank(key) == 0)
            {
                error = "먼저 룬석을 제작하세요.";
                return false;
            }

            if (IsEquipped(key) == true)
            {
                error = "이미 장착한 룬석입니다.";
                return false;
            }

            Collection.Equip(RuneRules.GetSocket(row), key);
            ThreeSouls.Audio.GameAudio.Play(ThreeSouls.Audio.SoundCue.Equip);
            Pending = null;
            error = string.Empty;
            onRunesChangedEventHandler?.Invoke();
            return true;
        }

        public bool TryUnequip(string key, out string error)
        {
            if (CanChange(key, out var row, out error) == false)
            {
                return false;
            }

            if (IsEquipped(key) == false)
            {
                error = "장착한 룬석을 선택하세요.";
                return false;
            }

            Collection.Unequip(RuneRules.GetSocket(row));
            ThreeSouls.Audio.GameAudio.Play(ThreeSouls.Audio.SoundCue.Equip);
            Pending = null;
            error = string.Empty;
            onRunesChangedEventHandler?.Invoke();
            return true;
        }

        public bool TryQuote(string key, RunePurchase purchase, out RuneQuote quote, out string error)
        {
            quote = null;
            Pending = null;
            PendingSale = null;
            if (!TryGetPurchaseCost(key, purchase, out var row, out var rank, out var gold, out var stones, out error))
                return false;

            quote = new RuneQuote(key, purchase, rank, gold, stones, row.GainPerRank, Collection.GetRevision());
            Pending = quote;
            return true;
        }

        public bool CanPurchase(string key, RunePurchase purchase, out string error)
        {
            return TryGetPurchaseCost(key, purchase, out _, out _, out _, out _, out error);
        }

        private bool TryGetPurchaseCost(string key, RunePurchase purchase, out RuneTableDataRow row,
            out int rank, out int gold, out int stones, out string error)
        {
            rank = gold = stones = 0;
            if (CanChange(key, out row, out error) == false)
            {
                return false;
            }

            rank = Collection.GetRank(key);
            if (Enum.IsDefined(typeof(RunePurchase), purchase) == false ||
                purchase == RunePurchase.Craft && rank != 0 ||
                purchase == RunePurchase.Upgrade && (rank == 0 || rank >= RuneRules.GetSanctuaryRule().MaximumRuneRank))
            {
                error = rank == 0 ? "먼저 제작하세요." : "이미 보유했거나 최대 단계입니다.";
                return false;
            }

            if (purchase == RunePurchase.Craft && User.UData.Campaign.ClearedProgress < CampaignBalance.RuneUnlock(key))
            {
                error = CampaignBalance.UnlockLabel(CampaignBalance.RuneUnlock(key));
                return false;
            }
            var cost = purchase == RunePurchase.Upgrade ? RuneRules.GetUpgrade(rank + 1) : null;
            gold = cost == null ? row.CraftGold : cost.Gold;
            stones = cost == null ? row.CraftStones : cost.Stones;
            if (User.UData.Gold < gold || User.UData.EnhancementStones < stones)
            {
                error = $"비용 {gold:N0}골드 · 강화석 {stones}개\n부족: 골드 {Math.Max(0, gold - User.UData.Gold):N0} · 강화석 {Math.Max(0, stones - User.UData.EnhancementStones)}";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public bool TryPurchase(RuneQuote quote, out string error)
        {
            RequireReady();
            if (quote == null || ReferenceEquals(quote, Pending) == false ||
                quote.Revision != Collection.GetRevision())
            {
                error = "확인 내용이 변경됐습니다. 다시 확인하세요.";
                return false;
            }

            var previous = quote;
            if (TryQuote(quote.Key, quote.Purchase, out var current, out error) == false)
            {
                return false;
            }

            Pending = null;
            if (previous.Rank != current.Rank || previous.Gold != current.Gold || previous.Stones != current.Stones ||
                previous.GainPerRank != current.GainPerRank)
            {
                error = "단계·가격·효과가 변경됐습니다. 다시 확인하세요.";
                return false;
            }

            User.PayEnhancementCost(current.Gold, current.Stones);
            if (current.Purchase == RunePurchase.Craft)
            {
                Collection.Add(current.Key);
            }
            else
            {
                Collection.Upgrade(current.Key);
            }

            User.NotifyEnhancementCurrencyChanged(true);
            ThreeSouls.Audio.GameAudio.Play(ThreeSouls.Audio.SoundCue.Rune);
            onRunesChangedEventHandler?.Invoke();
            error = string.Empty;
            return true;
        }

        public void CancelQuote(RuneQuote quote)
        {
            if (ReferenceEquals(quote, Pending) == true)
            {
                Pending = null;
            }
        }

        public bool TryQuoteSale(string key, out RuneSaleQuote quote, out string error)
        {
            quote = null;
            PendingSale = null;
            if (CanChange(key, out var row, out error) == false)
            {
                return false;
            }

            var rank = Collection.GetRank(key);
            if (rank == 0 || IsEquipped(key) == true)
            {
                error = rank == 0 ? "보유한 룬을 선택하세요." : "장착 중인 룬입니다. 강화·장착에서 먼저 해제하세요.";
                return false;
            }

            long cost = row.CraftGold;
            for (var level = 2; level <= rank; level++)
            {
                cost += RuneRules.GetUpgrade(level).Gold;
            }

            var gold = (int)Math.Min(int.MaxValue, Math.Floor(cost * (decimal)LobbyTables.Get<RuneShopRuleTableDataTable>().Items[0].SaleGoldRatio));
            if (gold <= 0 || (long)User.UData.Gold + gold > int.MaxValue)
            {
                error = "판매 금액이 지갑 상한을 넘습니다.";
                return false;
            }

            Pending = null;
            quote = new RuneSaleQuote(key, rank, gold, Collection.GetRevision());
            PendingSale = quote;
            error = string.Empty;
            return true;
        }

        public bool TrySell(RuneSaleQuote quote, out string error)
        {
            if (quote == null || ReferenceEquals(quote, PendingSale) == false || quote.Revision != Collection.GetRevision())
            {
                error = "판매 확인이 변경됐습니다. 다시 선택하세요.";
                return false;
            }

            if (TryQuoteSale(quote.Key, out var current, out error) == false)
            {
                return false;
            }

            PendingSale = null;
            if (current.Rank != quote.Rank || current.Gold != quote.Gold)
            {
                error = "판매 가격이 변경됐습니다. 다시 확인하세요.";
                return false;
            }

            Collection.Remove(quote.Key);
            User.CreditShopGold(current.Gold);
            ThreeSouls.Audio.GameAudio.Play(ThreeSouls.Audio.SoundCue.Trade);
            User.NotifyGoldChanged();
            onRunesChangedEventHandler?.Invoke();
            error = string.Empty;
            return true;
        }

        public void CancelSaleQuote(RuneSaleQuote quote)
        {
            if (ReferenceEquals(quote, PendingSale) == true)
            {
                PendingSale = null;
            }
        }

        public RuneCombatSnapshot CreatePreview()
        {
            RequireReady();
            return new RuneCombatSnapshot(Collection);
        }

        internal RuneCombatSnapshot BeginCombatSession()
        {
            RequireReady();
            if (CombatSession == true)
            {
                throw new InvalidOperationException("룬 전투 스냅샷은 전투마다 한 번 준비하세요.");
            }

            var snapshot = CreatePreview();
            EndLobbySession();
            CombatSession = true;
            return snapshot;
        }

        internal string GrantCampaignRune(string key)
        {
            RequireReady();
            var row = RuneRules.GetDefinition(key);
            if (!CombatSession || row == null || !row.Enabled || User.UData.Campaign.ClearedProgress < CampaignBalance.RuneUnlock(key))
                throw new InvalidOperationException("전투 승리의 해금된 룬만 지급할 수 있습니다.");
            var duplicate = Collection.GetRank(key) > 0;
            if (duplicate) User.AddEnhancementStones(2); else Collection.Add(key);
            onRunesChangedEventHandler?.Invoke();
            return row.DisplayName + (duplicate ? " · 중복 → 강화석 +2" : " · 획득");
        }

        internal void EndCombatSession()
        {
            CombatSession = false;
        }

        internal void Release()
        {
            EndLobbySession();
            EndCombatSession();
            Ready = false;
            onRunesChangedEventHandler = null;
        }

        private bool CanChange(string key, out RuneTableDataRow row, out string error)
        {
            RequireReady();
            row = RuneRules.GetDefinition(key);
            if (LobbySession == false || CombatSession == true)
            {
                error = "룬석 변경은 마을에서만 가능합니다.";
                return false;
            }

            if (row == null || row.Enabled == false)
            {
                error = row?.Effect == RuneEffect.Gems ? "보석 보상 시스템 준비 중입니다. 재화를 소비하지 않습니다." : "사용 가능한 룬석을 선택하세요.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private void RequireReady()
        {
            if (Ready == false)
            {
                throw new InvalidOperationException("룬 테이블을 로드하고 규칙을 적용하세요.");
            }
        }
    }

    /// <summary>
    /// Manager가 발행한 한 번의 확인 요청에 필요한 가격과 이전 단계를 보관합니다.
    /// </summary>
    public sealed class RuneQuote
    {
        public string Key { get; }
        public RunePurchase Purchase { get; }
        public int Rank { get; }
        public int Gold { get; }
        public int Stones { get; }
        internal float GainPerRank { get; }
        internal int Revision { get; }

        internal RuneQuote(string key, RunePurchase purchase, int rank, int gold, int stones, float gain, int revision)
        {
            Key = key;
            Purchase = purchase;
            Rank = rank;
            Gold = gold;
            Stones = stones;
            GainPerRank = gain;
            Revision = revision;
        }
    }

    /// <summary>
    /// 판매 확인에 필요한 룬 단계, 환급액과 변경 번호를 보관합니다.
    /// </summary>
    public sealed class RuneSaleQuote
    {
        public string Key { get; }
        public int Rank { get; }
        public int Gold { get; }
        internal int Revision { get; }

        internal RuneSaleQuote(string key, int rank, int gold, int revision)
        {
            Key = key;
            Rank = rank;
            Gold = gold;
            Revision = revision;
        }
    }
}
