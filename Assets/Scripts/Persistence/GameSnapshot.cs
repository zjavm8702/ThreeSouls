using System;
using System.Collections.Generic;
using System.Linq;
using ThreeSouls.Data;
using ThreeSouls.Data.Generated;
using ThreeSouls.Growth;
using ThreeSouls.Manager;
using ThreeSouls.Runes;
using ThreeSouls.UI;

namespace ThreeSouls.Persistence
{
    [Serializable] public sealed class SavedItem
    {
        public string Id, Key;
        public float Attack, Health, Speed;
        public int Level, Invested;
        public bool Locked;
        public static SavedItem From(EquipmentInstance item) => new SavedItem { Id=item.InstanceId, Key=item.ItemKey, Attack=item.AttackDmg,
            Health=item.MaxHP, Speed=item.AttackSpeedBonus, Level=item.Growth.Level, Invested=item.Growth.InvestedStones, Locked=item.Growth.Locked };
        internal EquipmentInstance Build() => new EquipmentInstance(Id,Key,Attack,Health,Speed,
            new EquipmentGrowthData(Level,Invested,Locked,EquipmentUpgradeRules.GetMaxLevel()));
    }
    [Serializable] public sealed class SavedHero { public string Key; public int Level, Experience; public int[] Ranks; public string[] Equipment; }
    [Serializable] public sealed class SavedRune { public string Key, Socket; public int Rank; }
    [Serializable] public sealed class SavedSupply { public ConsumableType Type; public int Count; }
    [Serializable] public sealed class SavedSettings
    {
        public float Master=1,Music=1,Effects=1;
        public bool Muted, Damage=true, Loot=true;
        public static SavedSettings Capture()=>new SavedSettings {Master=GameSettings.MasterVolume,Music=GameSettings.MusicVolume,Effects=GameSettings.EffectsVolume,
            Muted=GameSettings.Muted,Damage=GameSettings.ShowDamage,Loot=GameSettings.ShowLoot};
        internal void Restore() { GameSettings.SetMaster(Master);GameSettings.SetMusic(Music);GameSettings.SetEffects(Effects);
            GameSettings.SetMuted(Muted);GameSettings.SetDamage(Damage);GameSettings.SetLoot(Loot); }
    }
    [Serializable] public sealed class GameSnapshot
    {
        public const int CurrentVersion=1;
        public int Version=CurrentVersion;
        public string ProfileId=Guid.NewGuid().ToString("N"), OwnerId="";
        public long Revision, SavedUtc;
        public long CloudBaseRevision;
        public int Gold, Chests, EnhancementStones, GrowthStones;
        public int ClearedProgress;
        public int[] Clears;
        public SavedItem[] Items;
        public SavedHero[] Heroes;
        public SavedRune[] Runes;
        public int QuestCompleted;
        public bool QuestAccepted, QuestObjectiveComplete;
        public int SmallPotions, PartyPotions;
        public SavedSupply[] Supplies;
        public AutoHealThreshold HealThreshold;
        public SavedSettings Settings=new SavedSettings();
        // Retain the legacy guide flag for version-1 save compatibility; the user removed its UI.
        public bool GuideDismissed, WasInDungeon;
        public string DeploymentId="";
        public string[] RewardReceipts=Array.Empty<string>();
        public string AdDay="";
        public int ResultAds, SupplyAds;
        public bool ShopInitialized;
        public SavedItem[] Shop=Array.Empty<SavedItem>();

        public static GameSnapshot Capture(UserManager user,GameSnapshot previous=null)
        {
            var data=user.UData;
            var keys=TableManager.Get().GetThreeSoulsRuleDataTable().BaseCharacterID;
            return new GameSnapshot { ProfileId=previous?.ProfileId ?? Guid.NewGuid().ToString("N"), OwnerId=previous?.OwnerId ?? "",
                Revision=previous?.Revision ?? 0,SavedUtc=previous?.SavedUtc ?? 0, Gold=data.Gold,Chests=data.ChestCount,EnhancementStones=data.EnhancementStones,GrowthStones=data.GrowthStones,
                CloudBaseRevision=previous?.CloudBaseRevision ?? 0,
                ClearedProgress=data.Campaign.ClearedProgress,Clears=Enumerable.Range(0,20).Select(i=>data.Campaign.GetClears(i/5+1,i%5+1)).ToArray(),
                Items=data.Inventory.GetItems().Select(SavedItem.From).ToArray(),
                Heroes=keys.Select(key=>{var h=data.HeroGrowth.FindHero(key);return new SavedHero {Key=key,Level=h.Level,Experience=h.Experience,
                    Ranks=Enumerable.Range(0,6).Select(i=>h.GetRank((HeroStat)i)).ToArray(),Equipment=new[]{EquipSlot.Weapon,EquipSlot.Armor,EquipSlot.Accessory}.Select(s=>data.Equipment.GetEquippedId(key,s)).ToArray()};}).ToArray(),
                Runes=LobbyTables.Get<RuneTableDataTable>().Items.Where(r=>data.Runes.GetRank(r.Key)>0).Select(r=>new SavedRune {Key=r.Key,Rank=data.Runes.GetRank(r.Key),Socket=data.Runes.GetEquipped(RuneRules.GetSocket(r))==r.Key?RuneRules.GetSocket(r):null}).ToArray(),
                QuestCompleted=data.Quests.CompletedCount,QuestAccepted=data.Quests.Accepted,QuestObjectiveComplete=data.Quests.ObjectiveComplete,
                SmallPotions=data.Consumables.GetOwned(ConsumableType.SmallHealingPotion),PartyPotions=data.Consumables.GetOwned(ConsumableType.PartyHealingPotion),
                Supplies=Enumerable.Range(0,2).Select(i=>new SavedSupply {Type=data.Supplies.GetSlot(i).Type,Count=data.Supplies.GetSlot(i).Count}).ToArray(),HealThreshold=data.Supplies.Threshold,
                Settings=SavedSettings.Capture(),GuideDismissed=previous?.GuideDismissed ?? false,
                RewardReceipts=previous?.RewardReceipts ?? Array.Empty<string>(),AdDay=previous?.AdDay ?? "",ResultAds=previous?.ResultAds ?? 0,SupplyAds=previous?.SupplyAds ?? 0,
                DeploymentId=previous?.DeploymentId ?? "",
                ShopInitialized=EquipmentShopManager.Get().HasPersistentStock,Shop=EquipmentShopManager.Get().GetPersistentStock().Select(SavedItem.From).ToArray() };
        }
        private static void Require(bool valid,string message) { if(!valid)throw new InvalidOperationException("저장 기록 오류: "+message); }
        public void Validate()
        {
            Require(Version==CurrentVersion,"지원하지 않는 버전");Require(Guid.TryParse(ProfileId,out _),"프로필 식별자");
            Require(Revision>=0 && Gold>=0 && Chests>=0 && EnhancementStones>=0 && GrowthStones>=0,"재화");
            Require(Clears!=null && Clears.Length==20 && Clears.All(x=>x>=0) && ClearedProgress>=0 && ClearedProgress<=20,"캠페인");
            Require(Enumerable.Range(0,ClearedProgress).All(i=>Clears[i]>0) && Enumerable.Range(ClearedProgress,20-ClearedProgress).All(i=>Clears[i]==0),"순차 진행");
            Require(Items!=null && Items.Length<=LobbyTables.GetRules().InventoryCapacity && Items.All(x=>x!=null),"인벤토리");
            var items=new Dictionary<string,SavedItem>(StringComparer.Ordinal);
            foreach(var item in Items) { ValidateItem(item);Require(items.TryAdd(item.Id,item),"장비 중복"); }
            var keys=TableManager.Get().GetThreeSoulsRuleDataTable().BaseCharacterID;
            Require(Heroes!=null && Heroes.Length==keys.Length && Heroes.All(h=>h!=null) && Heroes.Select(h=>h.Key).Distinct().Count()==keys.Length,"영웅");
            var equipped=new HashSet<string>(StringComparer.Ordinal);
            foreach(var h in Heroes)
            {
                Require(keys.Contains(h.Key) && h.Level>=1 && h.Level<=HeroGrowthRules.GetRule().MaxLevel,"영웅 레벨");
                var maximum=HeroGrowthRules.GetExperienceRequired(h.Level);
                Require(h.Experience>=0 && (maximum==0?h.Experience==0:h.Experience<maximum),"경험치");
                Require(h.Ranks!=null && h.Ranks.Length==6 && Enumerable.Range(0,6).All(i=>h.Ranks[i]>=0 && h.Ranks[i]<=HeroGrowthRules.GetRankLimit((HeroStat)i,h.Level)),"능력치 강화");
                Require(h.Equipment!=null && h.Equipment.Length==3,"장착 슬롯");
                var slots=new[]{EquipSlot.Weapon,EquipSlot.Armor,EquipSlot.Accessory};
                for(int i=0;i<3;i++) if(!string.IsNullOrEmpty(h.Equipment[i]))
                {
                    Require(items.TryGetValue(h.Equipment[i],out var item) && equipped.Add(h.Equipment[i]),"장착 참조");
                    var row=TableManager.Get().GetEquipmentTableData(item.Key);
                    Require(row.EquipSlot==slots[i] && EquipmentRules.CanEquip(row,TableManager.Get().GetCharacterTableDataTable(h.Key)),"영웅 장착 호환");
                }
            }
            Require(Runes!=null && Runes.Length<=LobbyTables.Get<RuneTableDataTable>().Items.Length,"룬 목록");
            var runeKeys=new HashSet<string>();var sockets=new HashSet<string>();
            foreach(var rune in Runes)
            {
                Require(rune!=null && runeKeys.Add(rune.Key),"룬 중복");var row=RuneRules.GetDefinition(rune.Key);
                Require(row!=null && row.Enabled && rune.Rank>=1 && rune.Rank<=RuneRules.GetSanctuaryRule().MaximumRuneRank,"룬 단계");
                if(!string.IsNullOrEmpty(rune.Socket))Require(rune.Socket==RuneRules.GetSocket(row) && sockets.Add(rune.Socket),"룬 장착");
            }
            var quests=LobbyTables.Get<StoryQuestTableDataTable>().Items.Length;
            Require(QuestCompleted>=0 && QuestCompleted<=quests && (!QuestObjectiveComplete || QuestAccepted) && (QuestCompleted<quests || !QuestAccepted && !QuestObjectiveComplete),"의뢰");
            var potionMax=LobbyTables.GetSupplyRules().MaxOwnedPerType;
            Require(SmallPotions>=0 && SmallPotions<=potionMax && PartyPotions>=0 && PartyPotions<=potionMax,"물약");
            Require(Supplies!=null && Supplies.Length==2 && Supplies.All(s=>s!=null) && Enum.IsDefined(typeof(AutoHealThreshold),HealThreshold),"공급품 설정");
            foreach(var s in Supplies)Require(s.Count>=0 && s.Count<=SupplyLoadout.GetMaximumPerSlot() && (s.Count==0?s.Type==ConsumableType.None:ConsumableCatalog.IsPotion(s.Type)),"공급품 슬롯");
            Require(Supplies.Where(s=>s.Type==ConsumableType.SmallHealingPotion).Sum(s=>s.Count)<=SmallPotions && Supplies.Where(s=>s.Type==ConsumableType.PartyHealingPotion).Sum(s=>s.Count)<=PartyPotions,"공급품 재고");
            Require(Settings!=null && new[]{Settings.Master,Settings.Music,Settings.Effects}.All(x=>!float.IsNaN(x) && x>=0 && x<=1),"음량");
            Require(RewardReceipts!=null && RewardReceipts.Length<=2048 && RewardReceipts.All(x=>!string.IsNullOrWhiteSpace(x)) && RewardReceipts.Distinct().Count()==RewardReceipts.Length && ResultAds>=0 && SupplyAds>=0,"보상 기록");
            Require(Shop!=null && Shop.Length<=1000 && Shop.All(x=>x!=null),"상점");var stockIds=new HashSet<string>();
            foreach(var item in Shop){ValidateItem(item);Require(stockIds.Add(item.Id) && !items.ContainsKey(item.Id),"상점 중복");}
        }
        private static void ValidateItem(SavedItem item)
        { Require(item!=null,"장비 데이터");Require(!string.IsNullOrWhiteSpace(item.Id) && item.Id.Length<=100 && TableManager.Get().GetEquipmentTableData(item.Key)!=null,"장비 키");item.Build(); }
        internal string GameplayFingerprint()
        {
            var copy=Newtonsoft.Json.JsonConvert.DeserializeObject<GameSnapshot>(Newtonsoft.Json.JsonConvert.SerializeObject(this));
            copy.Revision=0;copy.SavedUtc=0;copy.CloudBaseRevision=0;copy.WasInDungeon=false;copy.DeploymentId="";
            return Newtonsoft.Json.JsonConvert.SerializeObject(copy);
        }
        internal string StorageFingerprint()
        {
            var copy=Newtonsoft.Json.JsonConvert.DeserializeObject<GameSnapshot>(Newtonsoft.Json.JsonConvert.SerializeObject(this));
            copy.Revision=0;copy.SavedUtc=0;return Newtonsoft.Json.JsonConvert.SerializeObject(copy);
        }
        public void Restore(UserManager user)
        {
            Validate();user.UData.RestoreSnapshot(this);EquipmentShopManager.Get().RestorePersistentStock(ShopInitialized,Shop);Settings.Restore();
        }
    }
}
