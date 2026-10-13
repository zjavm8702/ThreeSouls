using System;
using System.Collections;
using System.Linq;
using ThreeSouls.Enumeration.AddressableKey;
using ThreeSouls.Manager;
using UnityEngine;
using UnityEngine.U2D.Animation;

namespace ThreeSouls.Combat
{
    /// <summary>
    /// 스테이지 전투에서 사용하는 원본 에셋을 미리 로드합니다.
    /// </summary>
    internal sealed class CombatAssetLoader
    {
        internal IEnumerator PreLoad(int regionNumber, int stageNumber)
        {
            yield return AddressableManager.Get().AddressableLoadAssetAsync<GameObject>(Prefabs.Hero.ToAddress(), false);
            yield return AddressableManager.Get().AddressableLoadAssetAsync<GameObject>(Prefabs.Monster.ToAddress(), false);
            var ground = TableManager.Get().GetGroundTableData(regionNumber, stageNumber);
            yield return AddressableManager.Get().AddressableLoadAssetAsync<GameObject>(ground.MapName.ToAddress(), true);
            yield return AddressableManager.Get().AddressableLoadAssetAsync<GameObject>(Prefabs.Projectile.ToAddress(), false);
            yield return AddressableManager.Get().AddressableLoadAssetAsync<GameObject>(Prefabs.UI_Item_HealthBar.ToAddress(), false);
            yield return AddressableManager.Get().AddressableLoadAssetAsync<GameObject>(Prefabs.UI_Item_CombatValue.ToAddress(), false);
            yield return AddressableManager.Get().AddressableLoadAssetAsync<GameObject>(Prefabs.UI_Popup_Result.ToAddress(), false);
            yield return AddressableManager.Get().AddressableLoadAssetAsync<Sprite>(Images.Icon_Gold.ToAddress(), false);
            yield return AddressableManager.Get().AddressableLoadAssetAsync<Sprite>(Images.Icon_Reward.ToAddress(), false);
            yield return AddressableManager.Get().AddressableLoadAssetAsync<ScriptableObject>(ScriptableObjectDatas.CameraPresentationData.ToAddress(), false);
            yield return CombatSkillAssetLoader.PreLoad(TableManager.Get().GetSkillTableData(), AddressableManager.Get());
            yield return AddressableManager.Get().AddressableLoadAssetAsync<SpriteLibraryAsset>(SpriteLibrarys.ArcherArrow.ToAddress(), false);
            yield return AddressableManager.Get().AddressableLoadAssetAsync<SpriteLibraryAsset>(SpriteLibrarys.WizardMagicBean.ToAddress(), false);
            var heroes = TableManager.Get().GetThreeSoulsRuleDataTable().BaseCharacterID;
            for (var i = 0; i < heroes.Length; i++)
            {
                var hero = TableManager.Get().GetCharacterTableDataTable(heroes[i]);
                var address = CombatSpawnHandler.GetSpriteLibraryAddress(hero.ResourceName);
                yield return AddressableManager.Get().AddressableLoadAssetAsync<SpriteLibraryAsset>(address, false);
            }
            var monsterKeys = ground.Monsters.Concat(ground.RoundKeys.SelectMany(roundKey => TableManager.Get().GetHolyPlaceTableData(roundKey).Monsters))
                .Distinct()
                .ToArray();

            for (var monsterIndex = 0; monsterIndex < monsterKeys.Length; monsterIndex++)
            {
                var character = TableManager.Get().GetCharacterTableDataTable(monsterKeys[monsterIndex]);
                var address = CombatSpawnHandler.GetSpriteLibraryAddress(character.ResourceName);
                yield return AddressableManager.Get().AddressableLoadAssetAsync<SpriteLibraryAsset>(address, true);
            }
        }
    }
}
