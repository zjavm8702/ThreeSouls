using System;
using System.Linq;
using ThreeSouls.UI;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace ThreeSouls.EditorTools
{
    public static class GoldSettingsBuilder
    {
        private const string Path = "Assets/Prefabs/UI/UI_Settings.prefab";
        private static readonly Color Ink = new Color32(23, 32, 43, 255);
        private static readonly Color Paper = new Color32(242, 235, 211, 255);
        private static readonly Color Gold = new Color32(239, 191, 102, 255);
        private static TMP_FontAsset Font;
        private static Sprite Panel;
        private static Sprite Button;

        [MenuItem("Tools/Three Souls/UI/Build Settings")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit mode only");
            Font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Village/Production/StoryUI.asset");
            Panel = Art("MetalPanel"); Button = Art("MetalButton");
            var root = Rect("UI_Settings", null);
            Stretch(root);
            var canvas = root.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true; canvas.sortingOrder = 100;
            root.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            var popup = root.gameObject.AddComponent<UI_Settings>();
            var dim = Image("Dim", root, new Color(0, 0, 0, .78f)); Stretch(dim.rectTransform);
            var contents = Rect("Contents", root); Stretch(contents);
            contents.gameObject.AddComponent<CanvasGroup>();
            var layout = Rect("Layout", contents);
            layout.sizeDelta = new Vector2(1000,920);
            layout.gameObject.AddComponent<UI_SettingsLayout>();
            var frame = Image("SettingsPanel", layout, new Color(.68f, .79f, .88f), Panel);
            frame.rectTransform.sizeDelta = new Vector2(1000, 920);
            Text("Title", frame.transform, "설정", 48, Gold, new Vector2(0, 390), new Vector2(900, 70));
            Text("Subtitle", frame.transform, "소리와 전투 표시를 취향에 맞게 조절하세요", 25, Paper, new Vector2(0, 335), new Vector2(900, 48));
            var close = MakeButton("Close", frame.transform, "닫기", new Vector2(380, 390), new Vector2(140, 64));
            Text("SoundSection", frame.transform, "소리", 28, Gold, new Vector2(-390, 270), new Vector2(100, 48));
            var master = MakeSlider("MasterVolume", frame.transform, "전체 음량", 205, out var masterText);
            var music = MakeSlider("MusicVolume", frame.transform, "배경음", 130, out var musicText);
            var effects = MakeSlider("EffectsVolume", frame.transform, "효과음", 55, out var effectsText);
            var mute = MakeToggle("Mute", frame.transform, "전체 음소거", -35);
            Text("DisplaySection", frame.transform, "전투 표시", 28, Gold, new Vector2(-350, -115), new Vector2(180, 48));
            var damage = MakeToggle("Damage", frame.transform, "피해 숫자 표시", -185);
            var loot = MakeToggle("Loot", frame.transform, "골드·장비 이름표 표시", -260);
            Text("PauseNotice", frame.transform, "설정 중에는 게임이 일시정지됩니다\n설정은 마을과 전투에 함께 적용되며, 게임 종료 시 초기화됩니다", 23, new Color(.77f,.83f,.86f), new Vector2(0,-335), new Vector2(900,80));
            var reset = MakeButton("Defaults", frame.transform, "기본값 복원", new Vector2(0,-410), new Vector2(300,64));
            Bind(popup, "Root", contents.gameObject); Bind(popup, "Contents", contents.gameObject); Bind(popup, "Dim", dim.gameObject);
            Bind(popup, "Master", master); Bind(popup, "Music", music); Bind(popup, "Effects", effects);
            Bind(popup, "Mute", mute); Bind(popup, "Damage", damage); Bind(popup, "Loot", loot);
            Bind(popup, "Close", close); Bind(popup, "Defaults", reset);
            var so = new SerializedObject(popup); var values=so.FindProperty("Percentages"); values.arraySize=3;
            values.GetArrayElementAtIndex(0).objectReferenceValue=masterText;
            values.GetArrayElementAtIndex(1).objectReferenceValue=musicText;
            values.GetArrayElementAtIndex(2).objectReferenceValue=effectsText;
            so.ApplyModifiedPropertiesWithoutUndo();
            var motion = contents.gameObject.AddComponent<UIPanelMotion>();
            Bind(motion, "Content", frame.rectTransform); Bind(motion, "Group", contents.GetComponent<CanvasGroup>());
            PrefabUtility.SaveAsPrefabAsset(root.gameObject, Path);
            UnityEngine.Object.DestroyImmediate(root.gameObject);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Path).GetComponent<UI_Settings>();
            Connect("Assets/Prefabs/UI/UI_HUD_Top.prefab", prefab, false);
            Connect("Assets/Prefabs/UI/Lobby/UI_Lobby.prefab", prefab, true);
            AssetDatabase.SaveAssets();
            PersistenceLaunchBuilder.ConnectSettings();
        }

        private static void Connect(string path, UI_Settings prefab, bool village)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var host = village ? root.GetComponentsInChildren<RectTransform>(true).Single(x=>x.name=="Header") : (RectTransform)root.transform;
                var existing = host.Find("SettingsButton");
                var button = existing != null ? existing.GetComponent<UnityEngine.UI.Button>() : MakeButton("SettingsButton", host, "설정", Vector2.zero, new Vector2(130,64));
                var rect = (RectTransform)button.transform;
                if (village)
                {
                    rect.anchorMin=new Vector2(.85f,.28f); rect.anchorMax=new Vector2(.99f,.73f); rect.offsetMin=rect.offsetMax=Vector2.zero;
                    var gold = (RectTransform)host.Find("Gold"); gold.anchorMin=new Vector2(.67f,.25f);gold.anchorMax=new Vector2(.83f,.78f);
                    var icon = (RectTransform)host.Find("GoldIcon");icon.anchorMin=new Vector2(.60f,.35f);icon.anchorMax=new Vector2(.66f,.70f);
                    var brand=(RectTransform)host.Find("Brand");brand.anchorMax=new Vector2(.55f,.87f);
                }
                else
                {
                    rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(1,1);rect.anchoredPosition=new Vector2(-24,-24);
                    var top = (RectTransform)root.transform;
                    SetHudCard((RectTransform)top.Find("Gold"), .035f, .29f);
                    SetHudCard((RectTransform)top.Find("Time"), .36f, .64f);
                    SetHudCard((RectTransform)top.Find("Chest"), .72f, .84f);
                }
                var launcher=root.GetComponent<UI_SettingsLauncher>() ?? root.AddComponent<UI_SettingsLauncher>();
                Bind(launcher,"OpenButton",button);Bind(launcher,"Prefab",prefab);
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent,false);rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);return rect;
        }
        private static void SetHudCard(RectTransform rect, float left, float right)
        {
            rect.anchorMin = new Vector2(left,1); rect.anchorMax = new Vector2(right,1);
            rect.pivot = new Vector2(.5f,1); rect.anchoredPosition = new Vector2(0,-20);
            rect.sizeDelta = new Vector2(0,72);
        }
        private static void Stretch(RectTransform rect)
        { rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero; }
        private static UnityEngine.UI.Image Image(string name,Transform parent,Color color,Sprite sprite=null)
        {
            var rect=Rect(name,parent);var view=rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            view.color=color;view.sprite=sprite;if(sprite!=null)view.type=UnityEngine.UI.Image.Type.Sliced;return view;
        }
        private static TMP_Text Text(string name,Transform parent,string value,int size,Color color,Vector2 pos,Vector2 dimensions)
        {
            var rect=Rect(name,parent);rect.anchoredPosition=pos;rect.sizeDelta=dimensions;
            var text=rect.gameObject.AddComponent<TextMeshProUGUI>();text.font=Font;text.text=value;text.fontSize=size;
            text.color=color;text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;return text;
        }
        private static UnityEngine.UI.Button MakeButton(string name,Transform parent,string title,Vector2 pos,Vector2 size)
        {
            var image=Image(name,parent,new Color(.73f,.84f,.95f),Button);image.rectTransform.anchoredPosition=pos;image.rectTransform.sizeDelta=size;
            var button=image.gameObject.AddComponent<UnityEngine.UI.Button>();button.targetGraphic=image;
            image.gameObject.AddComponent<UIButtonFeedback>();
            var label=Text("Label",image.transform,title,28,Ink,Vector2.zero,size);Stretch(label.rectTransform);return button;
        }
        private static UnityEngine.UI.Slider MakeSlider(string name,Transform parent,string title,float y,out TMP_Text percentage)
        {
            Text(name+"Label",parent,title,28,Paper,new Vector2(-330,y),new Vector2(220,56));
            percentage=Text(name+"Percentage",parent,"100%",26,Gold,new Vector2(390,y),new Vector2(100,56));
            var root=Rect(name,parent);root.sizeDelta=new Vector2(480,50);root.anchoredPosition=new Vector2(50,y);
            var background=Image("Track",root,Ink);background.rectTransform.sizeDelta=new Vector2(480,20);
            var fillArea=Rect("FillArea",root);Stretch(fillArea);fillArea.offsetMin=new Vector2(12,15);fillArea.offsetMax=new Vector2(-12,-15);
            var fill=Image("Fill",fillArea,Gold);Stretch(fill.rectTransform);fill.raycastTarget=false;
            var handleArea=Rect("HandleArea",root);Stretch(handleArea);handleArea.offsetMin=new Vector2(12,0);handleArea.offsetMax=new Vector2(-12,0);
            var handle=Image("Handle",handleArea,Paper,Button);handle.rectTransform.sizeDelta=new Vector2(38,50);
            var slider=root.gameObject.AddComponent<UnityEngine.UI.Slider>();slider.targetGraphic=handle;slider.fillRect=fill.rectTransform;slider.handleRect=handle.rectTransform;
            slider.minValue=0;slider.maxValue=1;slider.value=1;return slider;
        }
        private static UnityEngine.UI.Toggle MakeToggle(string name,Transform parent,string title,float y)
        {
            var row=Image(name,parent,new Color(.14f,.20f,.26f));row.rectTransform.sizeDelta=new Vector2(880,60);row.rectTransform.anchoredPosition=new Vector2(0,y);
            Text("Label",row.transform,title,28,Paper,new Vector2(-130,0),new Vector2(600,56));
            var box=Image("Box",row.transform,Paper,Button);box.rectTransform.sizeDelta=new Vector2(52,52);box.rectTransform.anchoredPosition=new Vector2(390,0);
            var check=Text("Check",box.transform,"ON",20,Ink,Vector2.zero,new Vector2(50,50));
            var toggle=row.gameObject.AddComponent<UnityEngine.UI.Toggle>();toggle.targetGraphic=box;toggle.graphic=check;toggle.isOn=true;return toggle;
        }
        private static Sprite Art(string name)=>AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/UIIMage/WebUI/"+name+".png");
        private static void Bind(UnityEngine.Object target,string name,UnityEngine.Object value)
        { var so=new SerializedObject(target);so.FindProperty(name).objectReferenceValue=value;so.ApplyModifiedPropertiesWithoutUndo(); }
    }
}
