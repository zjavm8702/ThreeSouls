using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ThreeSouls.Combat;
using ThreeSouls.Data;
using ThreeSouls.Data.Generated;
using ThreeSouls.Enumeration;
using ThreeSouls.Enumeration.AddressableKey;
using ThreeSouls.Growth;
using ThreeSouls.Manager;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;

namespace ThreeSouls.EditorTools
{
    /// <summary>
    /// 격리된 사용자 데이터에서 룬 거래, 장착, 전투 스냅샷과 성소·스킬 계산을 검사합니다.
    /// </summary>
    public static class RuneStonesVerification
    {
        [MenuItem("Three Souls/Village/Verify Rune Stones (Edit Only)")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode == true)
            {
                throw new InvalidOperationException("Edit 모드에서 검사하세요.");
            }

            var checks = new List<string>();
            void Check(bool success, string name)
            {
                if (success == false)
                {
                    throw new InvalidOperationException("Rune check failed: " + name);
                }

                checks.Add(name);
            }

            var original = SceneManager.GetActiveScene();
            var dirty = original.isDirty;
            var preview = EditorSceneManager.NewPreviewScene();
            var fixture = new GameObject("RuneFixture");
            fixture.SetActive(false);
            SceneManager.MoveGameObjectToScene(fixture, preview);
            var singleton = typeof(GameManager).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
            var previousSingleton = singleton.GetValue(null);
            var handles = new List<AsyncOperationHandle>();
            try
            {
                var game = fixture.AddComponent<GameManager>();
                var user = fixture.AddComponent<UserManager>();
                var equipment = fixture.AddComponent<EquipmentManager>();
                var inventory = fixture.AddComponent<InventoryManager>();
                var equipmentGrowth = fixture.AddComponent<EquipmentGrowthManager>();
                var tables = fixture.AddComponent<TableManager>();
                var assets = fixture.AddComponent<AddressableManager>();
                var managers = (IDictionary)typeof(GameManager).GetField("ManagerContainer", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game);
                var components = new BaseManager[] { user, equipment, inventory, equipmentGrowth, tables, assets };
                for (var i = 0; i < components.Length; i++)
                {
                    managers.Add(components[i].GetType(), components[i]);
                }

                singleton.SetValue(null, game);
                var cache = typeof(VillageLobbyVerification).GetMethod("Cache", BindingFlags.Static | BindingFlags.NonPublic);
                cache.Invoke(null, new object[] { assets, handles, ScriptableObjectDatas.CharacterTableData.ToAddress(), ScriptableObjectDatas.CharacterTableData.ToAddress() });
                cache.Invoke(null, new object[] { assets, handles, ScriptableObjectDatas.StringTableData.ToAddress(), ScriptableObjectDatas.StringTableData.ToAddress() });
                cache.Invoke(null, new object[] { assets, handles, ScriptableObjectDatas.EquipmentTableData.ToAddress(), ScriptableObjectDatas.EquipmentTableData.ToAddress() });
                user.Initialize();
                user.ApplyTableRules();
                var runes = user.GetRuneManager();
                Check(LobbyTables.Get<RuneTableDataTable>().Items.Length == 16, "Four shrine plus twelve skill definitions");
                Check(runes.TryEquip("HeavySlash_Power", out _) == false, "Changes outside lobby rejected");
                runes.BeginLobbySession();
                Check(runes.TryEquip("unknown", out _) == false, "Unknown rune rejected");
                Check(runes.TryEquip("HeavySlash_Power", out _) == false, "Unowned rune rejected");
                Check(runes.TryQuote("HeavySlash_Power", ThreeSouls.Runes.RunePurchase.Upgrade, out _, out _) == false, "Unowned upgrade rejected");
                Check(runes.TryQuote("HeavySlash_Power", ThreeSouls.Runes.RunePurchase.Craft, out _, out _) == false, "Insufficient currency rejected");
                Check(runes.TryQuote("Shrine_Gems", ThreeSouls.Runes.RunePurchase.Craft, out _, out _) == false, "Future gem crafting blocked without charge");
                user.AddCombatReward(100000, 0);
                user.AddEnhancementStones(1000);
                Check(runes.TryQuote("HeavySlash_Power", ThreeSouls.Runes.RunePurchase.Craft, out var quote, out _), "Craft quote prepared");
                var notified = 0;
                ThreeSouls.Manager.RuneManager.OnRunesChangedDelegate observer = () =>
                {
                    Check(runes.GetRank("HeavySlash_Power") == 1 && user.UData.Gold == 99000 && user.UData.EnhancementStones == 996,
                        "Observer sees all craft state committed");
                    notified++;
                };
                runes.OnRunesChangedEventHandler += observer;
                runes.OnRunesChangedEventHandler += observer;
                Check(runes.TryPurchase(quote, out _), "Craft commits costs and rank");
                Check(notified == 1, "Duplicate callback subscription deduplicated");
                runes.OnRunesChangedEventHandler -= observer;
                Check(runes.TryPurchase(quote, out _) == false && user.UData.Gold == 99000, "Repeated confirmation cannot double charge");
                Check(runes.TryQuote("HeavySlash_Power", ThreeSouls.Runes.RunePurchase.Craft, out _, out _) == false, "Duplicate crafting rejected");
                runes.GrantTestRunes();
                var rows = LobbyTables.Get<RuneTableDataTable>().Items;
                for (var i = 0; i < rows.Length; i++)
                {
                    Check(runes.GetRank(rows[i].Key) == (rows[i].Enabled == true ? 1 : 0), "Test grant " + rows[i].Key);
                }
                runes.GrantTestRunes();
                Check(runes.GetRank("HeavySlash_Power") == 1, "Test grant is session independent and once only");
                Check(runes.TryEquip("Shrine_Gems", out _) == false, "Future gem equipping blocked");
                Check(runes.TryEquip("HeavySlash_Power", out _), "Skill rune equipped");
                Check(runes.TryEquip("HeavySlash_Power", out _) == false, "Repeated equip rejected");
                Check(runes.TryEquip("HeavySlash_Haste", out _), "Alternative replaces same skill socket");
                Check(runes.IsEquipped("HeavySlash_Power") == false && runes.IsEquipped("HeavySlash_Haste") == true, "One rune per skill without consuming old rune");
                Check(runes.TryEquip("PrecisionShot_Power", out _) && runes.IsEquipped("HeavySlash_Haste"), "Another skill socket independent");
                Check(runes.TryQuote("HeavySlash_Haste", ThreeSouls.Runes.RunePurchase.Upgrade, out quote, out _), "Upgrade quote ready");
                Check(runes.TryUnequip("HeavySlash_Haste", out _), "Free rune removal");
                var goldBefore = user.UData.Gold;
                Check(runes.TryPurchase(quote, out _) == false && user.UData.Gold == goldBefore, "Loadout changes invalidate paid confirmation");
                Check(runes.TryUnequip("HeavySlash_Haste", out _) == false, "Empty socket removal rejected");
                Check(runes.TryQuote("HeavySlash_Haste", ThreeSouls.Runes.RunePurchase.Upgrade, out quote, out _), "Requote upgrade");
                var cost = ThreeSouls.Runes.RuneRules.GetUpgrade(2);
                var oldCost = cost.Gold;
                try
                {
                    cost.Gold++;
                    Check(runes.TryPurchase(quote, out _) == false && user.UData.Gold == goldBefore, "Changed price rejected without charge");
                }
                finally
                {
                    cost.Gold = oldCost;
                }
                Check(runes.TryEquip("HeavySlash_Haste", out _), "Haste reequipped");
                for (var rank = 2; rank <= 5; rank++)
                {
                    Check(runes.TryQuote("HeavySlash_Haste", ThreeSouls.Runes.RunePurchase.Upgrade, out quote, out _), "Affordable rank " + rank);
                    Check(runes.TryPurchase(quote, out _) && runes.GetRank("HeavySlash_Haste") == rank, "Rank committed " + rank);
                }
                Check(runes.TryQuote("HeavySlash_Haste", ThreeSouls.Runes.RunePurchase.Upgrade, out _, out _) == false, "Rank cap enforced");
                var snapshot = runes.CreatePreview();
                Check(Math.Abs(snapshot.GetSkill("HeavySlash").CooldownMultiplier - .8f) < .0001f, "Haste rank five applies twenty percent cooldown reduction");
                Check(Math.Abs(snapshot.GetSkill("PrecisionShot").DamageMultiplier - 1.08f) < .0001f, "Power applies only matching skill");
                Check(snapshot.GetSkill("FireBolt").DamageMultiplier == 1, "Other hero skills unchanged");
                Check(Math.Abs(snapshot.EquipmentDropMultiplier - 1.1f) < .0001f && Math.Abs(snapshot.GoldMultiplier - 1.1f) < .0001f,
                    "Unequipped sanctuary keeps base buffs");
                Check(runes.TryEquip("Shrine_Loot", out _) && runes.TryEquip("Shrine_Gold", out _) && runes.TryEquip("Shrine_Ward", out _),
                    "Three enabled sanctuary sockets independent");
                Check(runes.TryEquip("Whirlwind_Area", out _), "Area rune equipped");
                var originalSkill = LobbyTables.Get<SkillTableDataTable>().Items[1];
                var originalRadius = originalSkill.Radius;
                var runePreview = runes.CreatePreview();
                var effective = ThreeSouls.Runes.RuneSkillRules.CreateSkills(LobbyTables.Get<SkillTableDataTable>().Items, runePreview);
                Check(Math.Abs(effective[1].Radius - originalRadius * 1.06f) < .0001f, "Area radius applied to effective skills");
                Check(originalSkill.Radius == originalRadius && ReferenceEquals(originalSkill, effective[1]) == false, "Original skill row preserved");
                Check(Math.Abs(runePreview.EquipmentDropMultiplier - 1.14f) < .0001f && Math.Abs(runePreview.GoldMultiplier - 1.16f) < .0001f,
                    "Shrine buff adds rune to base bonus");
                Check(Math.Abs(runePreview.DebuffRatio - .1f) < .0001f, "Ward reduces debuff by percentage points");
                var before = user.UData.Gold;
                var begin = typeof(RuneManager).GetMethod("BeginCombatSession", BindingFlags.Instance | BindingFlags.NonPublic);
                var combatSnapshot = (ThreeSouls.Runes.RuneCombatSnapshot)begin.Invoke(runes, null);
                Check(runes.TryEquip("FireBolt_Power", out _) == false, "Combat blocks equip");
                Check(runes.TryUnequip("Shrine_Gold", out _) == false, "Combat blocks removal");
                Check(runes.TryQuote("Shrine_Loot", ThreeSouls.Runes.RunePurchase.Upgrade, out _, out _) == false && user.UData.Gold == before,
                    "Combat blocks currency changes");
                var rule = ThreeSouls.Runes.RuneRules.GetSanctuaryRule();
                var baseBonus = rule.GoldBonus;
                try
                {
                    rule.GoldBonus = .5f;
                    Check(Math.Abs(combatSnapshot.GoldMultiplier - 1.16f) < .0001f, "Snapshot freezes table values");
                }
                finally
                {
                    rule.GoldBonus = baseBonus;
                }
                var end = typeof(RuneManager).GetMethod("EndCombatSession", BindingFlags.Instance | BindingFlags.NonPublic);
                end.Invoke(runes, null);
                Check(runes.TryEquip("FireBolt_Power", out _) == false, "Completion does not open lobby until explicit entry");
                runes.BeginLobbySession();
                Check(runes.TryEquip("FireBolt_Power", out _), "Next lobby session unlocks changes");
                Check(runes.TryQuote("Shrine_Loot", ThreeSouls.Runes.RunePurchase.Upgrade, out quote, out _), "Cancel fixture quote created");
                var cancelGold = user.UData.Gold;
                runes.CancelQuote(quote);
                Check(runes.TryPurchase(quote, out _) == false && user.UData.Gold == cancelGold, "Canceled quote cannot commit later");
                Check(combatSnapshot.GetSkill("FireBolt").DamageMultiplier == 1, "Snapshot independent of next lobby changes");
                Check(ThreeSouls.Runes.RuneCombatSnapshot.CalculateGold(10, 1.16f) == 11, "Gold rounds down per monster");
                Check(ThreeSouls.Runes.RuneCombatSnapshot.CalculateGold(int.MaxValue, 2) == int.MaxValue, "Gold overflow saturates");
                Check(original == SceneManager.GetActiveScene() && dirty == original.isDirty, "Open scene unchanged");
                Directory.CreateDirectory("Docs/Previews/RuneStones");
                File.WriteAllText("Docs/Previews/RuneStones/EditChecks.txt", "Isolated Edit mode\nPassed=" + checks.Count + "\n" + string.Join("\n", checks));
                Debug.Log("Rune Edit checks passed: " + checks.Count);
            }
            finally
            {
                singleton.SetValue(null, previousSingleton);
                EditorSceneManager.ClosePreviewScene(preview);
                for (var i = 0; i < handles.Count; i++)
                {
                    if (handles[i].IsValid() == true)
                    {
                        UnityEngine.AddressableAssets.Addressables.Release(handles[i]);
                    }
                }
            }
        }
    }
}
