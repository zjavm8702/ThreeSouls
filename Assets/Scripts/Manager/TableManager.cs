using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ThreeSouls.Data.Generated;
using ThreeSouls.Enumeration.AddressableKey;
using ThreeSouls.Interface;
using ThreeSouls.Manager;
using UnityEngine;

public class TableManager : MonoBehaviour, IManager
{
    public static TableManager Get()
    {
        return GameManager.Instance.Get<TableManager>();
    }

    public void Initialize()
    {
        
    }

    public void OnSceneChanged()
    {
        
    }

    public void Release()
    {

    }

    public IEnumerator AssetsPreLoad()
    {
        yield return AddressableManager.Get().AddressableLoadAssetAsync<ScriptableObject>(ScriptableObjectDatas.CharacterTableData.ToAddress(), false);
        yield return AddressableManager.Get().AddressableLoadAssetAsync<ScriptableObject>(ScriptableObjectDatas.StringTableData.ToAddress(), false);
        yield return AddressableManager.Get().AddressableLoadAssetAsync<ScriptableObject>(ScriptableObjectDatas.ThreeSoulsRuleData.ToAddress(), false);
        yield return AddressableManager.Get().AddressableLoadAssetAsync<ScriptableObject>(ScriptableObjectDatas.GroundTableData.ToAddress(), false);
    }


    public GroundTableDataRow GetGroundTableData(int stage)
    {
        return GetTable<GroundTableDataTable>(ScriptableObjectDatas.GroundTableData.ToAddress()).Items.FirstOrDefault(x => x.Stage == stage);
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

    private T GetTable<T>(string key) where T : Object
    {
        return AddressableManager.Get().LoadAsset<T>(key);
    }
}
