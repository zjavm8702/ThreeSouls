using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ThreeSouls.Data.Generated;
using ThreeSouls.Enumeration.AddressableKey;
using ThreeSouls.Interface;
using ThreeSouls.Manager;
using UnityEngine;

public class TableManager : BaseManager, IManager
{
    public bool EquipmentReady { get; private set; }

    public static TableManager Get()
    {
        return GameManager.Instance.Get<TableManager>();
    }

    public override void Initialize()
    {

    }

    public override void OnSceneChanged()
    {

    }

    public override void Release()
    {

    }

    public IEnumerator AssetsPreLoad()
    {
        yield return AddressableManager.Get().PreLoadBatch<ScriptableObject>(new[]
        {
            ScriptableObjectDatas.CharacterTableData.ToAddress(),
            ScriptableObjectDatas.StringTableData.ToAddress(),
            ScriptableObjectDatas.ThreeSoulsRuleData.ToAddress(),
            ScriptableObjectDatas.GroundTableData.ToAddress(),
            ScriptableObjectDatas.DropTableData.ToAddress(),
            ScriptableObjectDatas.EquipmentTableData.ToAddress(),
            ScriptableObjectDatas.HolyPlaceTableData.ToAddress(),
            ScriptableObjectDatas.SkillTableData.ToAddress(),
            ScriptableObjectDatas.CameraPresentationData.ToAddress()
        }, false);
        yield return ThreeSouls.Data.LobbyTables.PreLoad(AddressableManager.Get());
        ThreeSouls.Data.LobbyTableValidation.Validate();
        UserManager.Get().ApplyTableRules();
        EquipmentReady = true;
    }


    public HolyPlaceTableDataRow GetHolyPlaceTableData(string roundKey)
    {
        var table = GetTable<HolyPlaceTableDataTable>(ScriptableObjectDatas.HolyPlaceTableData.ToAddress());
        if (table == null)
        {
            throw new System.InvalidOperationException("HolyPlaceTableData was not loaded.");
        }
        return table.Items.FirstOrDefault(row => row.RoundKey == roundKey);
    }

    public GroundTableDataRow GetGroundTableData(int region, int stage)
    {
        return GetTable<GroundTableDataTable>(ScriptableObjectDatas.GroundTableData.ToAddress()).Items.FirstOrDefault(x => x.Stage == stage && x.RegionId == region);
    }

    public ThreeSoulsRuleDataRow GetThreeSoulsRuleDataTable()
    {
        return GetTable<ThreeSoulsRuleDataTable>(ScriptableObjectDatas.ThreeSoulsRuleData.ToAddress()).Items[0];
    }

    public List<StringTableDataRow> GetStringTableDataTable()
    {
        return GetTable<StringTableDataTable>(ScriptableObjectDatas.StringTableData.ToAddress()).Items.ToList();
    }

    public List<CharacterTableDataRow> GetCharacterTableDataTable()
    {
        return GetTable<CharacterTableDataTable>(ScriptableObjectDatas.CharacterTableData.ToAddress()).Items.ToList();
    }

    public CharacterTableDataRow GetCharacterTableDataTable(string strKey)
    {
        return GetTable<CharacterTableDataTable>(ScriptableObjectDatas.CharacterTableData.ToAddress()).Items.FirstOrDefault(x => x.StrKey.Equals(strKey));
    }

    public List<DropTableDataRow> GetDropTableDataTable(string monsterKey)
    {
        return GetTable<DropTableDataTable>(ScriptableObjectDatas.DropTableData.ToAddress())
            .Items.Where(x => string.Equals(x.MonsterKey, monsterKey, System.StringComparison.Ordinal))
            .ToList();
    }

    public EquipmentTableDataRow GetEquipmentTableData(string itemKey)
    {
        return GetTable<EquipmentTableDataTable>(ScriptableObjectDatas.EquipmentTableData.ToAddress()).Items.FirstOrDefault(x => x.ItemKey == itemKey);
    }

    public string GetEquipmentName(string itemKey)
    {
        var definition = GetEquipmentTableData(itemKey);
        var name = definition == null ? null : GetStringTableDataTable().FirstOrDefault(x => x.StrKey == definition.StrKey);
        return string.IsNullOrWhiteSpace(name?.KrValue) ? itemKey : name.KrValue;
    }

    /// <summary>
    /// 전투 스킬 테이블의 행을 읽기 전용 목록으로 조회합니다.
    /// </summary>
    public IReadOnlyList<SkillTableDataRow> GetSkillTableData()
    {
        var table = GetTable<SkillTableDataTable>(ScriptableObjectDatas.SkillTableData.ToAddress());

        if (table == null)
        {
            throw new System.InvalidOperationException("SkillTableData was not loaded.");
        }

        return System.Array.AsReadOnly(table.Items);
    }

    private T GetTable<T>(string key) where T : Object
    {
        return AddressableManager.Get().LoadAsset<T>(key);
    }
}
