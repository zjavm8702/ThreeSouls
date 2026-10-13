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
    /// 격리된 사용자 데이터에서 성장 거래, 레벨 경계와 치명타 및 행운 계산을 검사합니다.
    /// </summary>
    public static class HeroGrowthVerification
    {
        [MenuItem("Three Souls/Village/Verify Hero Growth (Edit Only)")]
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
                    throw new InvalidOperationException("Hero growth check failed: " + name);
                }

                checks.Add(name);
            }

            var original = SceneManager.GetActiveScene();
            var dirty = original.isDirty;
            var preview = EditorSceneManager.NewPreviewScene();
            var fixture = new GameObject("HeroGrowthFixture");
            fixture.SetActive(false);
            SceneManager.MoveGameObjectToScene(fixture, preview);
            var singleton = typeof(GameManager).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
            var previousSingleton = singleton.GetValue(null);
            var handles = new List<AsyncOperationHandle>();
            var definition = HeroGrowthRules.GetStat(HeroStat.Attack);
            var previousCost = definition.BaseGoldCost;
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
                var growth = user.GetHeroGrowthManager();
                var knight = growth.GetHero("Key_night");
                var archer = growth.GetHero("Key_archer");
                var mage = growth.GetHero("Key_wizard");
                Check(knight.Level == 1 && archer.Level == 1 && mage.Level == 1, "Three fixed heroes start at level one");
                Check(knight.GetRank(HeroStat.Attack) == 0 && knight.Experience == 0, "Initial growth has no purchased ranks");
                Check(growth.TryQuoteUpgrade("Key_night", HeroStat.Attack, out _, out _) == false, "No upgrades outside a lobby session");
                growth.BeginLobbySession();
                Check(growth.TryQuoteUpgrade("unknown", HeroStat.Attack, out _, out _) == false, "Unknown hero rejected");
                Check(growth.TryQuoteUpgrade("Key_night", (HeroStat)99, out _, out _) == false, "Unknown stat rejected");
                Check(growth.TryQuoteUpgrade("Key_night", HeroStat.AttackSpeed, out _, out var locked) == false && locked.Contains("3"), "Speed locked until level three");
                Check(growth.TryQuoteUpgrade("Key_night", HeroStat.Attack, out _, out _) == false, "Missing currencies reject upgrade");
                user.AddCombatReward(20000, 0);
                user.AddGrowthStones(100);
                var notifications = 0;
                void Observe()
                {
                    Check(knight.GetRank(HeroStat.Attack) == 1 && user.UData.Gold == 19900 && user.UData.GrowthStones == 99,
                        "Observer reads committed currencies and rank");
                    notifications++;
                }

                UserManager.OnCombatRewardChangedDelegate goldObserver = (gold, chests) => Observe();
                UserManager.OnGrowthMaterialsChangedDelegate stoneObserver = stones => Observe();
                ThreeSouls.Interface.IHeroGrowthSource.OnHeroGrowthChangedDelegate heroObserver = key => Observe();
                user.OnCombatRewardChangedEventHandler += goldObserver;
                user.OnGrowthMaterialsChangedEventHandler += stoneObserver;
                growth.OnHeroGrowthChangedEventHandler += heroObserver;
                growth.OnHeroGrowthChangedEventHandler += heroObserver;
                Check(growth.TryQuoteUpgrade("Key_night", HeroStat.Attack, out var quote, out _), "Affordable attack quote");
                Check(growth.TryUpgrade(quote, out _), "Attack upgrade succeeds");
                Check(notifications == 3, "One callback per owned notification with duplicate subscription protection");
                user.OnCombatRewardChangedEventHandler -= goldObserver;
                user.OnGrowthMaterialsChangedEventHandler -= stoneObserver;
                growth.OnHeroGrowthChangedEventHandler -= heroObserver;
                Check(growth.TryUpgrade(quote, out _) == false && knight.GetRank(HeroStat.Attack) == 1 && user.UData.Gold == 19900,
                    "Repeated confirmation cannot charge twice");
                var basis = tables.GetCharacterTableDataTable("Key_night");
                Check(Math.Abs(growth.GetBonuses(basis).Attack - basis.AttackDmg * 0.03f) < 0.0001f, "Attack gain uses base character value");
                Check(growth.GetBonuses(basis).MaxHealth == 0 && archer.GetRank(HeroStat.Attack) == 0, "Other stats and heroes preserved");
                growth.TryQuoteUpgrade("Key_night", HeroStat.Attack, out var stale, out _);
                definition.BaseGoldCost++;
                Check(growth.TryUpgrade(stale, out _) == false && user.UData.Gold == 19900 && user.UData.GrowthStones == 99,
                    "Changed confirmation price causes no mutations");
                definition.BaseGoldCost = previousCost;
                Check(HeroGrowthRules.GetRankLimit(HeroStat.Attack, 1) == 5, "Level one attack limit is five");
                for (var i = 1; i < 5; i++)
                {
                    growth.TryQuoteUpgrade("Key_night", HeroStat.Attack, out quote, out _);
                    Check(growth.TryUpgrade(quote, out _), "Fill initial rank limit " + i);
                }

                var goldBefore = user.UData.Gold;
                Check(growth.TryQuoteUpgrade("Key_night", HeroStat.Attack, out _, out _) == false && user.UData.Gold == goldBefore,
                    "Current level cap rejects another purchase");
                growth.GrantExperience("Key_night", 99);
                Check(knight.Level == 1 && knight.Experience == 99, "Experience before exact level boundary");
                growth.GrantExperience("Key_night", 1);
                Check(knight.Level == 2 && knight.Experience == 0, "Exact experience boundary levels up");
                Check(knight.GetRank(HeroStat.Attack) == 5 && HeroGrowthRules.GetRankLimit(HeroStat.Attack, 2) == 7,
                    "Level grants capacity and keeps purchased stats unchanged");
                Check(growth.TryQuoteUpgrade("Key_night", HeroStat.Attack, out quote, out _) && quote.Stones == 2,
                    "Level opens extra ranks and rank five increases material cost");
                Check(growth.TryUpgrade(quote, out _), "Purchase above previous level cap");
                growth.GrantExperience("Key_night", 140);
                Check(knight.Level == 3 && growth.TryQuoteUpgrade("Key_night", HeroStat.AttackSpeed, out _, out _), "Level three unlocks speed");
                growth.GrantExperience("Key_night", 3200);
                Check(knight.Level >= 10 && growth.TryQuoteUpgrade("Key_night", HeroStat.Luck, out _, out _), "Multi-level experience unlocks luck");
                Check(growth.GetBonuses(basis).CriticalChance == HeroGrowthRules.GetRule().BaseCriticalChance,
                    "Leveling does not automatically buy critical ranks");
                for (var i = 0; i < 3; i++)
                {
                    growth.TryQuoteUpgrade("Key_night", HeroStat.Luck, out quote, out _);
                    Check(growth.TryUpgrade(quote, out _), "Luck purchase " + i);
                }

                var multiplier = growth.GetPartyDropMultiplier(new[] { "Key_night", "Key_archer", "Key_wizard" });
                Check(Math.Abs(multiplier - 1.005f) < 0.00001f, "Party uses average luck instead of last-hit ownership");
                var dropRows = new[]
                {
                    new DropTableDataRow { RewardType = RewardType.Item, RewardKey = "Item_Sword_01", DropChance = 0.2f, RollCount = 1, MinAmount = 1, MaxAmount = 1 },
                    new DropTableDataRow { RewardType = RewardType.Gold, RewardKey = "Key_gold", DropChance = 0.2f, RollCount = 1, MinAmount = 1, MaxAmount = 1 }
                };
                var normalDrops = CombatDropResolver.Resolve(dropRows, 1, () => 0.2005);
                var luckyDrops = CombatDropResolver.Resolve(dropRows, 1, () => 0.2005, null, multiplier);
                Check(normalDrops.Count == 0 && luckyDrops.Count == 1 && luckyDrops[0].Type == RewardType.Item,
                    "Luck changes item probability while gold remains unchanged");
                Check(CombatDropResolver.Resolve(dropRows, 1, () => 0.9, null, 100).Count == 1,
                    "Item probability is capped at one");
                Check(CombatCriticalRules.Calculate(10, 0.2f, 1.5f, 0.199f) == 15, "Critical hit multiplies explicit damage");
                Check(CombatCriticalRules.Calculate(10, 0.2f, 1.5f, 0.2f) == 10, "Critical boundary remains strict");
                Check(CombatCriticalRules.Calculate(10, 0, 1.5f, 0) == 10, "Zero critical chance never crits");
                Check(CombatCriticalRules.Calculate(10, 1, 2, 1) == 20, "Guaranteed critical handles inclusive RNG endpoint");
                var materialBefore = user.UData.GrowthStones;
                var negativeRejected = false;
                try
                {
                    growth.GrantExperience("Key_night", -1);
                }
                catch (ArgumentOutOfRangeException)
                {
                    negativeRejected = true;
                }

                Check(negativeRejected == true && user.UData.GrowthStones == materialBefore, "Negative experience rejected without side effects");
                var reward = typeof(HeroGrowthManager).GetMethod("GrantVictoryRewards", BindingFlags.Instance | BindingFlags.NonPublic);
                var badRewardRejected = false;
                try
                {
                    reward.Invoke(growth, new object[] { new[] { "Key_archer", "Key_archer" }, 1 });
                }
                catch (TargetInvocationException exception)
                {
                    badRewardRejected = exception.InnerException is ArgumentException;
                }

                Check(badRewardRejected == true && archer.Experience == 0 && user.UData.GrowthStones == materialBefore,
                    "Duplicate participant reward rejected before mutation");
                reward.Invoke(growth, new object[] { new[] { "Key_night", "Key_archer", "Key_wizard" }, 1 });
                Check(archer.Experience == 40 && mage.Experience == 40 && user.UData.GrowthStones == materialBefore + 2,
                    "Victory grants all heroes experience and separate growth stones");
                growth.GrantExperience("Key_night", int.MaxValue);
                Check(knight.Level == 30 && knight.Experience == 0, "Large experience safely reaches maximum level");
                growth.GrantExperience("Key_night", int.MaxValue);
                Check(knight.Level == 30 && knight.Experience == 0, "Experience at maximum cannot overflow");
                growth.EndLobbySession();
                Check(growth.TryUpgrade(quote, out _) == false, "Session end blocks stale upgrades");
                Check(original == SceneManager.GetActiveScene() && dirty == original.isDirty &&
                    EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android, "Active scene and Android preserved");
                Directory.CreateDirectory("Docs/Previews/HeroGrowth");
                File.WriteAllText("Docs/Previews/HeroGrowth/EditChecks.txt", "Isolated Edit mode\nPassed=" + checks.Count + "\n" + string.Join("\n", checks));
                Debug.Log("Hero growth Edit checks passed: " + checks.Count);
            }
            finally
            {
                definition.BaseGoldCost = previousCost;
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
