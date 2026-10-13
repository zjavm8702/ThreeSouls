using System;
using System.Collections;
using UnityEditor;
using UnityEngine;
namespace ThreeSouls.EditorTools
{
    public static class DungeonSelectionPlayVerification
    {
        private static IEnumerator Routine;
        private static int LastFrame;
        private static double Deadline;
        public static void Start()
        {
            if (EditorApplication.isPlaying == false) throw new InvalidOperationException("Village Play required");
            EditorApplication.update -= Advance;
            Routine = RunSteps(); LastFrame = -1; Deadline = EditorApplication.timeSinceStartup + 45;
            EditorApplication.update += Advance;
        }
        private static void Advance()
        {
            try
            {
                if (EditorApplication.isPlaying == false || EditorApplication.timeSinceStartup > Deadline) throw new Exception("Dungeon Play verification timed out");
                if (Time.frameCount == LastFrame) return;
                LastFrame = Time.frameCount;
                if (Routine.MoveNext() == false)
                {
                    EditorApplication.update -= Advance;
                    Routine = null;
                    System.IO.File.WriteAllText("Docs/Previews/DungeonSelection/PlayStatus.txt", "PASS");
                    Debug.Log("Dungeon map Play verification passed");
                }
            }
            catch (Exception error)
            {
                EditorApplication.update -= Advance;
                Routine = null;
                System.IO.File.WriteAllText("Docs/Previews/DungeonSelection/PlayStatus.txt", "FAIL: " + error);
                Debug.LogException(error);
            }
        }
        private static IEnumerator RunSteps()
        {
var checks = new System.Collections.Generic.List<string>();
void Check(bool value, string name) { if(value==false)throw new System.Exception(name);checks.Add(name); }
var tabs=UnityEngine.Object.FindAnyObjectByType<ThreeSouls.UI.UI_LobbyTabs>();
var view=UnityEngine.Object.FindAnyObjectByType<ThreeSouls.UI.UI_DungeonSelection>(UnityEngine.FindObjectsInactive.Include);
var popup=view.GetComponentInChildren<ThreeSouls.UI.UI_DungeonPreparation>(true);
var user=ThreeSouls.Manager.UserManager.Get();
var data=user.UData.DungeonPreparation;
var maps=ThreeSouls.Data.LobbyTables.Get<ThreeSouls.Data.Generated.DungeonMapTableDataTable>().Items;
popup.Close();view.Close();tabs.transform.Find("Navigation/OpenDungeonMap").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
yield return null;
Check(view.gameObject.activeInHierarchy && data.SelectedStageKey==null,"Real entry button opens total map with clean selection");
var map=view.GetComponentInChildren<UnityEngine.UI.Image>();
Check(map.sprite.name=="TotalMap" && map.rectTransform.rect.height>0 && float.IsNaN(map.rectTransform.rect.height)==false,"Total map renders with finite geometry");
Check(System.Math.Abs(map.rectTransform.rect.width/map.rectTransform.rect.height-1672f/941f)<.0001f,"Original map aspect ratio preserved");
void Raycast(UnityEngine.UI.Button button,string name)
{
 UnityEngine.Canvas.ForceUpdateCanvases();
 var rect=(UnityEngine.RectTransform)button.transform;
 var center=UnityEngine.RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
 var pointer=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){position=center};
 var hits=new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
 UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointer,hits);
 Check(hits.Count>0 && hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>()==button,name);
}
for(var i=0;i<maps.Length;i++)
{
 var world=view.transform.Find("MapFrame/Map/Dungeon"+i).GetComponent<UnityEngine.UI.Button>();
 Raycast(world,"World point raycast "+maps[i].Key);world.onClick.Invoke();yield return null;
 Check(map.sprite.name==maps[i].RegionNumber.ToString(),"Correct image "+maps[i].Key);
 var stages=ThreeSouls.Dungeon.DungeonSelectionRules.GetStages(maps[i].Key);
 for(var j=0;j<stages.Count;j++)
 {
  var button=view.transform.Find("MapFrame/Map/Stage"+j).GetComponent<UnityEngine.UI.Button>();
  Raycast(button,"Stage point raycast "+stages[j].Key);button.onClick.Invoke();yield return null;
  Check(popup.gameObject.activeInHierarchy && data.SelectedStageKey==stages[j].Key,"Stage point opens correct popup "+stages[j].Key);
  popup.transform.Find("Cancel").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();yield return null;
  Check(popup.gameObject.activeSelf==false && data.SelectedStageKey==null && data.Departure==null,"Popup cancel clears draft "+stages[j].Key);
 }
 view.transform.Find("Back").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();yield return null;
 Check(map.sprite.name=="TotalMap","Back returns to world map "+maps[i].Key);
}
var count=0;
ThreeSouls.Manager.DungeonPreparationManager.OnPreparationChangedDelegate listener=()=>count++;
user.GetDungeonPreparationManager().OnPreparationChangedEventHandler+=listener;
view.SetUp(user.GetDungeonPreparationManager(),user.GetRuneManager(),user,UnityEngine.Object.FindAnyObjectByType<ThreeSouls.Village.VillageShopInteraction>());
view.SetUp(user.GetDungeonPreparationManager(),user.GetRuneManager(),user,UnityEngine.Object.FindAnyObjectByType<ThreeSouls.Village.VillageShopInteraction>());
view.transform.Find("List2").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
var before=count;view.transform.Find("List4").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
Check(data.SelectedStageKey=="Forest_5" && count==before+1,"Repeated setup keeps one list selection callback");
user.GetDungeonPreparationManager().OnPreparationChangedEventHandler-=listener;
Check(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path=="Assets/(T)Scene/(T)Scene_Village.unity","Map flow stays in Village");
System.IO.File.WriteAllLines("Docs/Previews/DungeonSelection/PlayChecks.txt",checks);
yield break;

        }
    }
}
