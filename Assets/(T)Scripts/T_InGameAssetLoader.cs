using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ThreeSouls.Combat;
using ThreeSouls.Data;
using ThreeSouls.Data.Generated;
using ThreeSouls.Enumeration.AddressableKey;
using ThreeSouls.Manager;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.U2D.Animation;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 에디터의 원본 에셋을 주소 키로 찾아 완료 핸들로 등록합니다. 번들 로드는 실행하지 않습니다.
/// </summary>
internal sealed class T_InGameAssetLoader
{
    private readonly Dictionary<string, AssetData> Assets;

    internal T_InGameAssetLoader(AddressableManager manager)
    {
        var cache = typeof(AddressableManager).GetField("LoadedAssets", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(typeof(AddressableManager).Name, "LoadedAssets");
        Assets = (Dictionary<string, AssetData>)cache.GetValue(manager);
        var types = LobbyTables.GetTableTypes();
        for (var i = 0; i < types.Length; i++)
        {
            Load<ScriptableObject>(LobbyTables.GetAddress(types[i]));
        }
    }

    /// <summary>
    /// 동일 주소는 한 번 등록하며 경로가 바뀐 원본은 이름과 타입이 유일할 때만 다시 찾습니다.
    /// </summary>
    internal T Load<T>(string address) where T : UnityEngine.Object
    {
        if (Assets.TryGetValue(address, out var cached) == true)
        {
            return cached.AssetHandle.Result as T
                ?? throw new InvalidOperationException($"테스트 에셋 타입 불일치 : {address}, {typeof(T).Name}");
        }

#if UNITY_EDITOR
        var asset = FindAsset<T>(address);
        var handle = Addressables.ResourceManager.CreateCompletedOperation(asset, string.Empty);
        Assets.Add(address, new AssetData(address, handle, null, false));
        return asset;
#else
        throw new InvalidOperationException("T_InGameData는 에디터의 직접 Play 테스트 전용입니다.");
#endif
    }

#if UNITY_EDITOR
    private static T FindAsset<T>(string address) where T : UnityEngine.Object
    {
        var bracket = address.IndexOf('[');
        if (bracket >= 0 && address.EndsWith("]", StringComparison.Ordinal) == true)
        {
            var path = address.Substring(0, bracket);
            var name = address.Substring(bracket + 1, address.Length - bracket - 2);
            var subAssets = AssetDatabase.LoadAllAssetsAtPath(path);
            for (var i = 0; i < subAssets.Length; i++)
            {
                if (subAssets[i] is T typed && typed.name == name)
                {
                    return typed;
                }
            }
            throw new InvalidOperationException("테스트 Sprite 누락: " + address);
        }
        var asset = AssetDatabase.LoadAssetAtPath<T>(address);

        if (asset != null)
        {
            return asset;
        }

        var assetName = Path.GetFileNameWithoutExtension(address);
        var guids = AssetDatabase.FindAssets($"{assetName} t:{typeof(T).Name}", new[] { "Assets" });

        for (var i = 0; i < guids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[i]);

            if (Path.GetFileNameWithoutExtension(path) != assetName)
            {
                continue;
            }

            var candidate = AssetDatabase.LoadAssetAtPath<T>(path);

            if (candidate == null)
            {
                continue;
            }

            if (asset != null)
            {
                throw new InvalidOperationException($"동일 이름의 테스트 에셋이 여러 개입니다. 주소 키를 갱신하세요 : {address}");
            }

            asset = candidate;
        }

        return asset != null ? asset
            : throw new InvalidOperationException($"테스트 원본 에셋을 찾을 수 없습니다 : {address}, {typeof(T).Name}");
    }
#endif

    /// <summary>
    /// 선택한 스테이지의 테이블, 전투 원본, 스킬과 필요한 캐릭터 라이브러리를 준비합니다.
    /// </summary>
    internal void Prepare(int region, int stage)
    {
        var grounds = Load<GroundTableDataTable>(ScriptableObjectDatas.GroundTableData.ToAddress());
        var rules = Load<ThreeSoulsRuleDataTable>(ScriptableObjectDatas.ThreeSoulsRuleData.ToAddress());
        var characters = Load<CharacterTableDataTable>(ScriptableObjectDatas.CharacterTableData.ToAddress());
        var holyPlaces = Load<HolyPlaceTableDataTable>(ScriptableObjectDatas.HolyPlaceTableData.ToAddress());
        var skills = Load<SkillTableDataTable>(ScriptableObjectDatas.SkillTableData.ToAddress());
        Load<StringTableDataTable>(ScriptableObjectDatas.StringTableData.ToAddress());
        Load<DropTableDataTable>(ScriptableObjectDatas.DropTableData.ToAddress());
        Load<EquipmentTableDataTable>(ScriptableObjectDatas.EquipmentTableData.ToAddress());
        Load<CameraPresentationData>(ScriptableObjectDatas.CameraPresentationData.ToAddress());

        var ground = grounds.Items.FirstOrDefault(row => row.RegionId == region && row.Stage == stage)
            ?? throw new InvalidOperationException($"테스트 스테이지를 찾을 수 없습니다 : {region}, {stage}");

        if (rules.Items.Length == 0)
        {
            throw new InvalidOperationException("테스트 Rule 테이블이 비어 있습니다.");
        }

        var rule = rules.Items[0];

        if (rule.MaxCharacterCombat <= 0 || rule.BaseCharacterID.Length < rule.MaxCharacterCombat)
        {
            throw new InvalidOperationException("테스트 영웅의 ID와 인원 설정을 확인하세요.");
        }

        Load<GameObject>(Prefabs.Hero.ToAddress());
        Load<GameObject>(Prefabs.Monster.ToAddress());
        Load<GameObject>(ground.MapName.ToAddress());
        Load<GameObject>(Prefabs.Projectile.ToAddress());
        Load<GameObject>(Prefabs.UI_Item_HealthBar.ToAddress());
        Load<GameObject>(Prefabs.UI_Item_CombatValue.ToAddress());
        Load<GameObject>(Prefabs.UI_Popup_Result.ToAddress());
        Load<Sprite>(Images.Icon_Gold.ToAddress());
        Load<Sprite>(Images.Icon_Reward.ToAddress());

        for (var i = 0; i < skills.Items.Length; i++)
        {
            var skill = skills.Items[i];
            Load<Sprite>(CombatSkillAssetLoader.GetIconAddress(skill));
            Load<AnimationClip>(CombatSkillAssetLoader.GetAttackClipAddress(skill));
            Load<GameObject>(CombatSkillAssetLoader.GetEffectPrefabAddress(skill));
        }

        var keys = new List<string>();

        for (var i = 0; i < rule.MaxCharacterCombat; i++)
        {
            keys.Add(rule.BaseCharacterID[i]);
        }

        keys.AddRange(ground.Monsters);

        for (var i = 0; i < ground.RoundKeys.Length; i++)
        {
            var roundKey = ground.RoundKeys[i];
            var round = holyPlaces.Items.FirstOrDefault(row => row.RoundKey == roundKey)
                ?? throw new InvalidOperationException($"테스트 성소 라운드 누락 : {roundKey}");
            keys.AddRange(round.Monsters);
        }

        for (var i = 0; i < keys.Count; i++)
        {
            var key = keys[i];
            var character = characters.Items.FirstOrDefault(row => row.StrKey == key)
                ?? throw new InvalidOperationException($"테스트 캐릭터 누락 : {key}");
            Load<SpriteLibraryAsset>(CombatSpawnHandler.GetSpriteLibraryAddress(character.ResourceName));
        }

        Load<SpriteLibraryAsset>(SpriteLibrarys.ArcherArrow.ToAddress());
        Load<SpriteLibraryAsset>(SpriteLibrarys.WizardMagicBean.ToAddress());
    }
}
