using System.Linq;
using ThreeSouls.Persistence;
using ThreeSouls.Data.Generated;

namespace ThreeSouls.Data
{
    public sealed partial class UserData
    {
        internal void RestoreSnapshot(GameSnapshot s)
        {
            Gold=s.Gold;ChestCount=s.Chests;EnhancementStones=s.EnhancementStones;GrowthStones=s.GrowthStones;
            Inventory.Restore(s.Items);Equipment.Restore(s.Heroes);HeroGrowth.Restore(s.Heroes);Runes.Restore(s.Runes);
            Campaign.Restore(s.ClearedProgress,s.Clears);Quests.Restore(s.QuestCompleted,s.QuestAccepted,s.QuestObjectiveComplete);
            Consumables.Restore(s.SmallPotions,s.PartyPotions);
            for(int i=0;i<2;i++)Supplies.SetSlot(i,s.Supplies[i].Type,s.Supplies[i].Count);Supplies.SetThreshold(s.HealThreshold);
            DungeonPreparation.Clear();
        }
    }
    public sealed partial class Inventory { internal void Restore(SavedItem[] items) { var restored=items.Select(x=>x.Build()).ToArray();Items.Clear();Items.AddRange(restored); } }
    public sealed partial class HeroEquipmentCollection
    {
        internal void Restore(SavedHero[] heroes)
        {
            Heroes.Clear();var slots=new[]{EquipSlot.Weapon,EquipSlot.Armor,EquipSlot.Accessory};
            foreach(var h in heroes){var data=new HeroEquipmentData(h.Key);for(int i=0;i<3;i++)data.Set(slots[i],h.Equipment[i]);Heroes.Add(data);}
        }
    }
    public sealed partial class CampaignProgress { internal void Restore(int progress,int[] clears){ClearedProgress=progress;System.Array.Copy(clears,Clears,20);} }
    public sealed partial class ConsumableInventory { internal void Restore(int small,int party){SmallPotions=small;PartyPotions=party;} }
}
namespace ThreeSouls.Growth
{
    public sealed partial class HeroGrowthData { internal void RestoreRanks(int[] ranks)=>System.Array.Copy(ranks,Ranks,6); }
    public sealed partial class HeroGrowthCollection
    {
        internal void Restore(SavedHero[] rows)
        { Heroes.Clear();foreach(var row in rows){var h=new HeroGrowthData(row.Key,row.Level,row.Experience,HeroGrowthRules.GetRule().MaxLevel);h.RestoreRanks(row.Ranks);Heroes.Add(row.Key,h);} }
    }
}
namespace ThreeSouls.Runes
{
    public sealed partial class RuneCollection
    { internal void Restore(SavedRune[] rows){Ranks.Clear();Sockets.Clear();foreach(var r in rows){Ranks.Add(r.Key,r.Rank);if(!string.IsNullOrEmpty(r.Socket))Sockets.Add(r.Socket,r.Key);}TestGranted=true;Revision++;} }
}
namespace ThreeSouls.Quests
{
    public sealed partial class QuestJournal { internal void Restore(int completed,bool accepted,bool objective){CompletedCount=completed;Accepted=accepted;ObjectiveComplete=objective;} }
}
namespace ThreeSouls.Manager
{
    public sealed partial class EquipmentShopManager
    {
        internal bool HasPersistentStock=>Stock!=null;
        internal ThreeSouls.Data.EquipmentInstance[] GetPersistentStock()=>Stock?.GetOffers().Select(o=>o.Item).ToArray() ?? System.Array.Empty<ThreeSouls.Data.EquipmentInstance>();
        internal void RestorePersistentStock(bool initialized,SavedItem[] rows)
        {
            Stock=null;if(!initialized)return;Stock=new ThreeSouls.Data.EquipmentShopStock();
            var products=ThreeSouls.Data.EquipmentShopCatalog.GetProducts();
            foreach(var r in rows){var product=products.FirstOrDefault(x=>x.ItemKey==r.Key);if(product!=null && product.StockCount>0)Stock.Add(new ThreeSouls.Data.EquipmentShopOffer(r.Build(),product.BuyGold));}
        }
    }
}
