using ThreeSouls.Enumeration.AddressableKey;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using ThreeSouls.Data;
using ThreeSouls.Combat;
using ThreeSouls.Data.Generated;
using ThreeSouls.EditorTools.Sheets;
using ThreeSouls.Enumeration;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace ThreeSouls.EditorTools
{
    public static class EquipmentPackageSetup
    {
        private static bool IsFinite(float value)
        {
            return float.IsNaN(value) == false && float.IsInfinity(value) == false;
        }

        private sealed class Pending { public string Name; public SheetTableCodec.Table Schema; public Type Type; public Array Rows; }
        private static readonly string[] Names={"CharacterTableData","DropTableData","EquipmentTableData","StringTableData"};
        [MenuItem("Tools/Three Souls/Equipment/Build Tables and Register Asset")]
        public static void Install()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Stop Play and wait for compilation before building tables.");
            var addressables=AddressableAssetSettingsDefaultObject.Settings;
            if(addressables==null || addressables.DefaultGroup==null) throw new InvalidOperationException("Addressables settings are missing.");
            string priorGuid=AssetDatabase.AssetPathToGUID(ScriptableObjectDatas.EquipmentTableData.ToAddress());
            if(addressables.groups.Where(x=>x!=null).Any(group=>group.entries.Any(x=>x.address==ScriptableObjectDatas.EquipmentTableData.ToAddress() && x.guid!=priorGuid)))
                throw new InvalidOperationException("Duplicate Addressables address: "+ScriptableObjectDatas.EquipmentTableData.ToAddress());
            var pending=new List<Pending>();
            foreach(string name in Names)
            {
                string csv="Assets/Generated/SheetData/Csv/"+name+".csv";
                string unique=name=="CharacterTableData" || name=="StringTableData" ? "StrKey" : name=="EquipmentTableData" ? "ItemKey" : "Index";
                var schema=SheetTableCodec.Read(name,File.ReadAllText(csv,Encoding.UTF8),unique);
                string code="Assets/Generated/SheetData/Code/"+name+"Table.cs";
                if(File.ReadAllText(code,Encoding.UTF8)!=SheetTableCodec.GenerateCode(schema)) throw new InvalidOperationException("CSV/schema mismatch: "+name);
                Type type=typeof(EquipmentTableDataRow).Assembly.GetType(SheetTableCodec.GeneratedNamespace+"."+name+"Table",true);
                Type rowType=type.GetField("Items").FieldType.GetElementType();
                var rows=Array.CreateInstance(rowType,schema.Rows.Count);
                for(int i=0;i<schema.Rows.Count;i++) {
                    var row=Activator.CreateInstance(rowType);
                    for(int j=0;j<schema.Columns.Length;j++) rowType.GetField(schema.Columns[j].Name).SetValue(row,schema.Rows[i][j]);
                    rows.SetValue(row,i);
                }
                string path="Assets/ScriptableObjectDatas/"+name+".asset";
                var existing=AssetDatabase.LoadMainAssetAtPath(path);
                if((existing!=null && existing.GetType()!=type) || (existing==null && File.Exists(path))) throw new InvalidOperationException("Existing asset type mismatch: "+path);
                pending.Add(new Pending { Name=name,Schema=schema,Type=type,Rows=rows });
            }
            var characters=pending.First(x=>x.Name=="CharacterTableData").Rows.Cast<CharacterTableDataRow>().ToArray();
            var definitions=pending.First(x=>x.Name=="EquipmentTableData").Rows.Cast<EquipmentTableDataRow>().ToArray();
            var drops=pending.First(x=>x.Name=="DropTableData").Rows.Cast<DropTableDataRow>().ToArray();
            foreach(var character in characters)
                if(character.MaxHP<=0 || !IsFinite(character.MaxHP) || character.AttackSpeedMultiplier<=0 || !IsFinite(character.AttackSpeedMultiplier)
                    || character.TargetSearchRadius<=0 || !IsFinite(character.TargetSearchRadius))
                    throw new InvalidOperationException("Invalid character stats: "+character.StrKey);
            foreach(var item in definitions) {
                if(!EquipmentRules.IsValidDefinition(item)) throw new InvalidOperationException("Invalid equipment: "+item.ItemKey);
                foreach(string key in item.AllowedCharacterKeys ?? Array.Empty<string>())
                    if(!characters.Any(x=>x.StrKey==key && x.CharacterType==CharacterType.Hero)) throw new InvalidOperationException("Invalid allowed hero: "+key);
            }
            foreach(var drop in drops) {
                if(!characters.Any(x=>x.StrKey==drop.MonsterKey && x.CharacterType==CharacterType.Monster)) throw new InvalidOperationException("Unknown drop monster: "+drop.MonsterKey);
                if(drop.RewardType==RewardType.Item && !definitions.Any(x=>x.ItemKey==drop.RewardKey)) throw new InvalidOperationException("Unknown reward item: "+drop.RewardKey);
            }
            var invalidDrops=new List<string>();
            CombatDropResolver.Resolve(drops,1,()=>0.5,msg=>invalidDrops.Add(msg));
            if(invalidDrops.Count>0) throw new InvalidOperationException(string.Join("\n",invalidDrops));
            string backup="CodexBackups/EquipmentSetup_"+DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            foreach(string directory in new[] {"Assets/ScriptableObjectDatas","Assets/AddressableAssetsData"})
                if(Directory.Exists(directory)) foreach(string source in Directory.GetFiles(directory,"*",SearchOption.AllDirectories)) {
                    string dest=Path.Combine(backup,source); Directory.CreateDirectory(Path.GetDirectoryName(dest)); File.Copy(source,dest,false);
                }
            string settingsFile="UserSettings/ThreeSoulsSheetImporter.asset";
            if(File.Exists(settingsFile)) { string dest=Path.Combine(backup,settingsFile); Directory.CreateDirectory(Path.GetDirectoryName(dest)); File.Copy(settingsFile,dest,false); }
            Directory.CreateDirectory("Assets/ScriptableObjectDatas"); Directory.CreateDirectory("Assets/Generated/SheetData/Data");
            foreach(var item in pending)
            {
                string path="Assets/ScriptableObjectDatas/"+item.Name+".asset";
                var asset=AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if(asset==null) { asset=ScriptableObject.CreateInstance(item.Type); AssetDatabase.CreateAsset(asset,path); }
                Undo.RecordObject(asset,"Build equipment tables"); item.Type.GetField("Items").SetValue(asset,item.Rows); EditorUtility.SetDirty(asset);
                Type payload=typeof(EquipmentTableDataRow).Assembly.GetType(SheetTableCodec.GeneratedNamespace+"."+item.Name+"Payload",true);
                var json=Activator.CreateInstance(payload); payload.GetField("Items").SetValue(json,item.Rows);
                File.WriteAllText("Assets/Generated/SheetData/Data/"+item.Name+".json",JsonUtility.ToJson(json,true),new UTF8Encoding(false));
                var source=SheetImporterSettings.instance.Sources.FirstOrDefault(x=>x.Name==item.Name);
                if(source==null) { source=new SheetSource {Name=item.Name}; SheetImporterSettings.instance.Sources.Add(source); }
                source.Enabled=true; source.LocalCsv=Path.GetFullPath("Assets/Generated/SheetData/Csv/"+item.Name+".csv");
                source.UniqueKey=item.Name=="CharacterTableData" || item.Name=="StringTableData" ? "StrKey" : item.Name=="EquipmentTableData"?"ItemKey":"Index";
            }
            string equipmentGuid=AssetDatabase.AssetPathToGUID(ScriptableObjectDatas.EquipmentTableData.ToAddress());
            foreach(var group in addressables.groups.Where(x=>x!=null))
                if(group.entries.Any(x=>x.address==ScriptableObjectDatas.EquipmentTableData.ToAddress() && x.guid!=equipmentGuid))
                    throw new InvalidOperationException("Duplicate Addressables address: "+ScriptableObjectDatas.EquipmentTableData.ToAddress());
            var destination=addressables.FindGroup("ScriptableObjectDatas")
                ?? throw new InvalidOperationException("ScriptableObjectDatas group is missing.");
            var entry=addressables.CreateOrMoveEntry(equipmentGuid,destination);
            entry.address=ScriptableObjectDatas.EquipmentTableData.ToAddress();
            addressables.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified,entry,true);
            SheetImporterSettings.instance.Persist(); AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("장비 테이블 적용","테이블 4개를 생성하고 장비 테이블 주소를 등록했습니다.\nAddressables 번들을 다시 빌드한 뒤 테스트하세요.\n백업: "+backup,"확인");
        }
    }
}
