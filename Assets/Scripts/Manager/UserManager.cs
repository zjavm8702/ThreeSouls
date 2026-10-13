using ThreeSouls.Data;

namespace ThreeSouls.Manager
{
    /// <summary>
    /// 사용자 데이터 소유권과 재화 변경을 관리하며 분야별 Manager를 연결합니다.
    /// </summary>
    public class UserManager : BaseManager
    {
        public delegate void OnCombatRewardChangedDelegate(int gold, int chestCount);
        public delegate void OnEnhancementMaterialsChangedDelegate(int stones);
        public delegate void OnGrowthMaterialsChangedDelegate(int stones);
        private OnGrowthMaterialsChangedDelegate onGrowthMaterialsChangedEventHandler;
        public event OnGrowthMaterialsChangedDelegate OnGrowthMaterialsChangedEventHandler
        {
            add
            {
                onGrowthMaterialsChangedEventHandler -= value;
                onGrowthMaterialsChangedEventHandler += value;
            }
            remove
            {
                onGrowthMaterialsChangedEventHandler -= value;
            }
        }
        private OnEnhancementMaterialsChangedDelegate onEnhancementMaterialsChangedEventHandler;
        public event OnEnhancementMaterialsChangedDelegate OnEnhancementMaterialsChangedEventHandler
        {
            add
            {
                onEnhancementMaterialsChangedEventHandler -= value;
                onEnhancementMaterialsChangedEventHandler += value;
            }
            remove
            {
                onEnhancementMaterialsChangedEventHandler -= value;
            }
        }

        private OnCombatRewardChangedDelegate onCombatRewardChangedEventHandler;
        public event OnCombatRewardChangedDelegate OnCombatRewardChangedEventHandler
        {
            add
            {
                onCombatRewardChangedEventHandler -= value;
                onCombatRewardChangedEventHandler += value;
            }
            remove
            {
                onCombatRewardChangedEventHandler -= value;
            }
        }

        public static UserManager Get() => GameManager.Instance.Get<UserManager>();
        public UserData UData { get; } = new();
        private bool Initialized;
        private HeroGrowthManager HeroGrowth;
        private RuneManager Runes;
        private QuestManager Quests;

        public QuestManager GetQuestManager()
        {
            return Quests ?? throw new System.InvalidOperationException("UserManager를 먼저 초기화해야 합니다.");
        }
        private DungeonPreparationManager DungeonPreparation;
        private bool InitialSessionPrepared;

        public void PrepareInitialSession()
        {
            if (InitialSessionPrepared == true)
            {
                return;
            }

            UserSessionInitializer.Prepare(this);
            ThreeSouls.Persistence.PersistentSession.StartSession(this);
            InitialSessionPrepared = true;
        }

        public DungeonPreparationManager GetDungeonPreparationManager()
        {
            return DungeonPreparation ?? throw new System.InvalidOperationException("UserManager를 먼저 초기화해야 합니다.");
        }

        public RuneManager GetRuneManager()
        {
            return Runes ?? throw new System.InvalidOperationException("UserManager를 먼저 초기화해야 합니다.");
        }

        public HeroGrowthManager GetHeroGrowthManager()
        {
            return HeroGrowth ?? throw new System.InvalidOperationException("UserManager를 먼저 초기화해야 합니다.");
        }

        public void ApplyTableRules()
        {
            UData.Inventory.ConfigureCapacity(LobbyTables.GetRules().InventoryCapacity);
            HeroGrowth.ApplyTableRules();
            Runes.ApplyTableRules();
            Quests.ApplyTableRules();
            DungeonPreparation.ApplyTableRules();
        }

        public void AddGrowthStones(int amount)
        {
            UData.CreditGrowthStones(amount);
            if (amount > 0)
            {
                onGrowthMaterialsChangedEventHandler?.Invoke(UData.GrowthStones);
            }
        }

        internal void PayHeroGrowthCost(int gold, int stones)
        {
            UData.PayHeroGrowthCost(gold, stones);
        }

        internal void CreditGrowthStones(int stones)
        {
            UData.CreditGrowthStones(stones);
        }

        internal void NotifyHeroGrowthCurrencies()
        {
            NotifyGoldChanged();
            onGrowthMaterialsChangedEventHandler?.Invoke(UData.GrowthStones);
        }

        public void AddEnhancementStones(int amount)
        {
            UData.AddEnhancementStones(amount);

            if (amount > 0)
            {
                onEnhancementMaterialsChangedEventHandler?.Invoke(UData.EnhancementStones);
            }
        }

        internal void PayEnhancementCost(int gold, int stones)
        {
            UData.PayEnhancementCost(gold, stones);
        }

        internal void PayShopCost(int gold)
        {
            UData.SpendGold(gold);
        }

        internal void CreditShopGold(int gold)
        {
            UData.CreditShopGold(gold);
        }

        internal void NotifyGoldChanged()
        {
            onCombatRewardChangedEventHandler?.Invoke(UData.Gold, UData.ChestCount);
        }

        internal void CreditDismantleStones(int stones)
        {
            UData.AddEnhancementStones(stones);
        }

        internal void NotifyEnhancementCurrencyChanged(bool goldChanged)
        {
            if (goldChanged == true)
            {
                onCombatRewardChangedEventHandler?.Invoke(UData.Gold, UData.ChestCount);
            }

            onEnhancementMaterialsChangedEventHandler?.Invoke(UData.EnhancementStones);
        }

        public void AddCombatReward(int gold, int chestCount)
        {
            var previousGold = UData.Gold;
            var previousChestCount = UData.ChestCount;
            UData.AddCombatReward(gold, chestCount);

            if (previousGold != UData.Gold || previousChestCount != UData.ChestCount)
            {
                onCombatRewardChangedEventHandler?.Invoke(UData.Gold, UData.ChestCount);
            }
        }

        public override void Initialize()
        {
            if (Initialized == true)
            {
                return;
            }

            var equipment = EquipmentManager.Get();
            HeroGrowth = new HeroGrowthManager(this, UData.HeroGrowth);
            Runes = new RuneManager(this, UData.Runes);
            Quests = new QuestManager(this, UData.Quests);
            DungeonPreparation = new DungeonPreparationManager(UData.DungeonPreparation, Runes);
            equipment.Configure(UData.Inventory, UData.Equipment);
            InventoryManager.Get().Configure(UData.Inventory, equipment);
            EquipmentGrowthManager.Get().Configure(this, UData.Inventory, equipment, TableManager.Get());
            Initialized = true;
        }

        public override void Release()
        {
            onCombatRewardChangedEventHandler = null;
            onEnhancementMaterialsChangedEventHandler = null;
            onGrowthMaterialsChangedEventHandler = null;
            HeroGrowth?.Release();
            DungeonPreparation?.Release();
            Quests?.Release();
            Runes?.Release();
            Initialized = false;
        }

        public override void OnSceneChanged()
        {
            HeroGrowth?.EndLobbySession();
            DungeonPreparation?.EndLobbySession();
            Runes?.EndLobbySession();
            Quests?.EndLobbySession();
        }
    }
}
