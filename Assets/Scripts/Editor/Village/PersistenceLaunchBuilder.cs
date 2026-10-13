using System;
using System.Linq;
using ThreeSouls.UI;
using ThreeSouls.Persistence;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace ThreeSouls.EditorTools
{
    public static class PersistenceLaunchBuilder
    {
        private const string SavePath="Assets/Prefabs/UI/UI_SaveAccount.prefab";
        private static TMP_FontAsset Font;
        private static Sprite ButtonArt,PanelArt;
        private static readonly Color Paper=new Color32(242,235,211,255),Gold=new Color32(239,191,102,255);
        private static void Style(){Font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Village/Production/StoryUI.asset");ButtonArt=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/UIIMage/WebUI/MetalButton.png");PanelArt=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/UIIMage/WebUI/MetalPanel.png");}
        [MenuItem("Tools/Three Souls/UI/Build Save Account")]
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Edit mode only");Style();
            var config=AssetDatabase.LoadAssetAtPath<GameServicesConfig>("Assets/Resources/GameServicesConfig.asset");
            if(config==null){config=ScriptableObject.CreateInstance<GameServicesConfig>();AssetDatabase.CreateAsset(config,"Assets/Resources/GameServicesConfig.asset");}
            var root=Rect("UI_SaveAccount",null);Stretch(root);var view=root.gameObject.AddComponent<UI_SaveAccount>();
            var canvas=root.gameObject.AddComponent<Canvas>();canvas.overrideSorting=true;canvas.sortingOrder=120;root.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            var dim=Image("Dim",root,new Color(0,0,0,.8f));Stretch(dim.rectTransform);
            var contents=Rect("Contents",root);Stretch(contents);var group=contents.gameObject.AddComponent<CanvasGroup>();
            var layout=Rect("Layout",contents);layout.sizeDelta=new Vector2(1000,920);layout.gameObject.AddComponent<UI_SettingsLayout>();
            var frame=Image("Frame",layout,new Color(.58f,.70f,.79f),PanelArt);frame.rectTransform.sizeDelta=new Vector2(1000,920);
            Text("Title",frame.transform,"저장 · 계정",44,Gold,new Vector2(0,395),new Vector2(800,70));
            var close=Button("Close",frame.transform,"닫기",new Vector2(395,395),new Vector2(140,64));
            var local=Text("LocalStatus",frame.transform,"현재 기기에 자동 저장됩니다.",27,Paper,new Vector2(0,305),new Vector2(900,90));
            var cloud=Text("CloudStatus",frame.transform,"Google Play 연결 준비 중",25,Paper,new Vector2(0,210),new Vector2(900,90));
            var save=Button("Save",frame.transform,"지금 저장",new Vector2(-220,110),new Vector2(350,64));
            var backup=Button("Backup",frame.transform,"백업 갱신",new Vector2(220,110),new Vector2(350,64));
            var restore=Button("Restore",frame.transform,"로컬 백업 복구",new Vector2(-220,20),new Vector2(350,64));
            var connect=Button("Connect",frame.transform,"Google Play 연결",new Vector2(220,20),new Vector2(350,64));
            var upload=Button("Upload",frame.transform,"클라우드 백업",new Vector2(-220,-70),new Vector2(350,64));
            var download=Button("Download",frame.transform,"클라우드 복구",new Vector2(220,-70),new Vector2(350,64));
            var newgame=Button("NewGame",frame.transform,"진행 초기화",new Vector2(0,-160),new Vector2(350,64));
            Text("Help",frame.transform,"구매·강화·의뢰·클리어 후 자동 저장됩니다.\n던전 도중 종료하면 출전 전 상태로 마을에 복귀합니다.\n재설치·기기 변경 복구에는 계정 연결이 필요합니다.",25,Paper,new Vector2(0,-305),new Vector2(900,180));
            var confirmation=Image("Confirmation",contents,new Color(0,0,0,.9f));Stretch(confirmation.rectTransform);
            var box=Image("Box",confirmation.transform,new Color(.58f,.70f,.79f),PanelArt);box.rectTransform.sizeDelta=new Vector2(820,390);
            var message=Text("Message",box.transform,"확인",28,Paper,new Vector2(0,55),new Vector2(740,220));
            var confirm=Button("Confirm",box.transform,"확인",new Vector2(185,-115),new Vector2(250,64));
            var cancel=Button("Cancel",box.transform,"취소",new Vector2(-185,-115),new Vector2(250,64));
            confirmation.gameObject.SetActive(false);
            Bind(view,"Root",contents.gameObject);Bind(view,"Contents",contents.gameObject);Bind(view,"Dim",dim.gameObject);
            Bind(view,"LocalStatus",local);Bind(view,"CloudStatus",cloud);Bind(view,"Save",save);Bind(view,"Backup",backup);Bind(view,"Restore",restore);
            Bind(view,"Connect",connect);Bind(view,"Upload",upload);Bind(view,"Download",download);Bind(view,"NewGame",newgame);Bind(view,"Close",close);
            Bind(view,"ConfirmPanel",confirmation.gameObject);Bind(view,"ConfirmText",message);Bind(view,"Confirm",confirm);Bind(view,"Cancel",cancel);
            var motion=contents.gameObject.AddComponent<UIPanelMotion>();Bind(motion,"Content",frame.rectTransform);Bind(motion,"Group",group);
            PrefabUtility.SaveAsPrefabAsset(root.gameObject,SavePath);UnityEngine.Object.DestroyImmediate(root.gameObject);
            ConnectSettings();ConnectAds();AssetDatabase.SaveAssets();
        }
        public static void ConnectSettings()
        {
            if(!System.IO.File.Exists(SavePath))return;Style();
            const string path="Assets/Prefabs/UI/UI_Settings.prefab";var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view=root.GetComponent<UI_Settings>();var frame=root.GetComponentsInChildren<RectTransform>(true).Single(x=>x.name=="SettingsPanel");
                var defaults=(RectTransform)frame.Find("Defaults");defaults.anchoredPosition=new Vector2(-220,-410);
                var existing=frame.Find("Storage");var storage=existing!=null?existing.GetComponent<UnityEngine.UI.Button>():Button("Storage",frame,"저장 · 계정",new Vector2(220,-410),new Vector2(300,64));
                Bind(view,"Storage",storage);Bind(view,"StoragePrefab",AssetDatabase.LoadAssetAtPath<GameObject>(SavePath).GetComponent<UI_SaveAccount>());
                frame.Find("PauseNotice").GetComponent<TMP_Text>().text="설정 중에는 게임이 일시정지됩니다.\n설정과 진행은 자동 저장됩니다.";
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        [MenuItem("Tools/Three Souls/UI/Connect Rewarded Ads")]
        public static void ConnectAds()
        {
            Style();const string resultPath="Assets/Prefabs/UI/UI_Popup_Result.prefab";
            var root=PrefabUtility.LoadPrefabContents(resultPath);
            try
            {
                var contents=root.transform.Find("Root/Contents");
                var notice=(UnityEngine.UI.Text)new SerializedObject(root.GetComponent<UI_Popup_Result>()).FindProperty("RewardNotice").objectReferenceValue;
                notice.rectTransform.anchoredPosition=new Vector2(0,-350);notice.rectTransform.sizeDelta=new Vector2(800,86);
                Offer(contents,"RewardedGold",ThreeSouls.Ads.RewardPlacement.DungeonGold,new Vector2(0,-475),new Vector2(740,130));
                PrefabUtility.SaveAsPrefabAsset(root,resultPath);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            const string lobbyPath="Assets/Prefabs/UI/Lobby/UI_Lobby.prefab";root=PrefabUtility.LoadPrefabContents(lobbyPath);
            try
            {
                var tabs=root.GetComponentInChildren<UI_LobbyTabs>(true);var s=new SerializedObject(tabs);var page=(GameObject)s.FindProperty("Pages").GetArrayElementAtIndex(3).objectReferenceValue;
                if(page.transform.Find("RewardedPotion")==null)foreach(RectTransform child in page.transform){child.anchorMin=new Vector2(child.anchorMin.x,.16f+.84f*child.anchorMin.y);child.anchorMax=new Vector2(child.anchorMax.x,.16f+.84f*child.anchorMax.y);}
                var offer=Offer(page.transform,"RewardedPotion",ThreeSouls.Ads.RewardPlacement.VillagePotion,Vector2.zero,Vector2.zero);
                Anchor(offer,.05f,.015f,.96f,.14f);PrefabUtility.SaveAsPrefabAsset(root,lobbyPath);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            AssetDatabase.SaveAssets();
        }
        private static RectTransform Offer(Transform parent,string name,ThreeSouls.Ads.RewardPlacement placement,Vector2 pos,Vector2 size)
        {
            var existing=parent.Find(name);if(existing!=null)return (RectTransform)existing;
            var root=Rect(name,parent);root.anchoredPosition=pos;root.sizeDelta=size;var view=root.gameObject.AddComponent<ThreeSouls.Ads.UI_RewardedOffer>();
            var card=Rect("Card",root);Stretch(card);
            var button=Button("Watch",card,"선택 광고 보상",Vector2.zero,Vector2.zero);Anchor((RectTransform)button.transform,.02f,.40f,.98f,.96f);
            var label=button.GetComponentInChildren<TMP_Text>();label.fontSize=27;
            var notice=Text("Notice",card,"광고 준비 중",23,Paper,Vector2.zero,Vector2.zero);Anchor(notice.rectTransform,.02f,.00f,.98f,.38f);
            Bind(view,"Watch",button);Bind(view,"Label",label);Bind(view,"Notice",notice);Bind(view,"Card",card.gameObject);
            var so=new SerializedObject(view);so.FindProperty("Placement").enumValueIndex=(int)placement;so.ApplyModifiedPropertiesWithoutUndo();return root;
        }
        [MenuItem("Tools/Three Souls/Services/Apply Google Play Identifiers")]
        public static void ConfigureGoogle()
        {
            var c=GameServicesConfig.Load();if(c==null || !c.GoogleConfigured)throw new InvalidOperationException("GameServicesConfig에 실제 Google 게임 ID와 웹 클라이언트 ID를 입력하세요.");
            GooglePlayGames.Editor.GPGSAndroidSetupUI.PerformSetup(c.GoogleWebClientId,c.GoogleGameId,null);
        }
        private static RectTransform Rect(string name,Transform parent){var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);return r;}
        private static void Stretch(RectTransform r){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;}
        private static void Anchor(RectTransform r,float a,float b,float c,float d){r.anchorMin=new Vector2(a,b);r.anchorMax=new Vector2(c,d);r.offsetMin=r.offsetMax=Vector2.zero;}
        private static UnityEngine.UI.Image Image(string name,Transform parent,Color color,Sprite sprite=null){var r=Rect(name,parent);var i=r.gameObject.AddComponent<UnityEngine.UI.Image>();i.color=color;i.sprite=sprite;if(sprite!=null)i.type=UnityEngine.UI.Image.Type.Sliced;return i;}
        private static TMP_Text Text(string name,Transform parent,string text,int size,Color color,Vector2 position,Vector2 dimensions){var r=Rect(name,parent);r.anchoredPosition=position;r.sizeDelta=dimensions;var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=Font;t.text=text;t.fontSize=size;t.color=color;t.alignment=TextAlignmentOptions.Center;t.raycastTarget=false;return t;}
        private static UnityEngine.UI.Button Button(string name,Transform parent,string title,Vector2 position,Vector2 size){var image=Image(name,parent,new Color(.7f,.83f,.94f),ButtonArt);image.rectTransform.anchoredPosition=position;image.rectTransform.sizeDelta=size;var b=image.gameObject.AddComponent<UnityEngine.UI.Button>();b.targetGraphic=image;image.gameObject.AddComponent<UIButtonFeedback>();var t=Text("Label",image.transform,title,27,new Color32(23,32,43,255),Vector2.zero,size);Stretch(t.rectTransform);return b;}
        private static void Bind(UnityEngine.Object target,string name,UnityEngine.Object value){var s=new SerializedObject(target);s.FindProperty(name).objectReferenceValue=value;s.ApplyModifiedPropertiesWithoutUndo();}
    }
}
