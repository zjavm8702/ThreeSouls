using System;
using ThreeSouls.UI;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace ThreeSouls.EditorTools
{
    public static class HealthBarPresentationBuilder
    {
        [MenuItem("Tools/Three Souls/UI/Style Hero and Monster Health Bars")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit mode only");
            const string path="Assets/Prefabs/UI/UI_Item_HealthBar.prefab";
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var component=root.GetComponent<UI_Item_HealthBar>();
                var so=new SerializedObject(component);
                var fill=(UnityEngine.UI.Image)so.FindProperty("ProgressHealthBarImage").objectReferenceValue;
                var bar=root.transform.Find("BarArea") as RectTransform;
                if (bar==null)
                {
                    bar=new GameObject("BarArea",typeof(RectTransform),typeof(UnityEngine.UI.Image)).GetComponent<RectTransform>();
                    bar.SetParent(root.transform,false);
                }
                bar.anchorMin=Vector2.zero;bar.anchorMax=Vector2.one;bar.offsetMin=bar.offsetMax=Vector2.zero;
                var background=bar.GetComponent<UnityEngine.UI.Image>();background.color=new Color32(42,18,26,255);background.raycastTarget=false;
                var outline=bar.GetComponent<UnityEngine.UI.Outline>() ?? bar.gameObject.AddComponent<UnityEngine.UI.Outline>();
                outline.effectColor=new Color32(90,31,41,255);outline.effectDistance=new Vector2(1,-1);
                fill.transform.SetParent(bar,false);fill.rectTransform.anchorMin=Vector2.zero;fill.rectTransform.anchorMax=Vector2.one;
                fill.rectTransform.offsetMin=fill.rectTransform.offsetMax=Vector2.zero;fill.color=new Color32(235,83,87,255);fill.raycastTarget=false;
                var old=root.GetComponent<UnityEngine.UI.Image>();if(old!=null)old.enabled=false;
                var label=root.transform.Find("HeroName");
                if(label==null){label=new GameObject("HeroName",typeof(RectTransform),typeof(TextMeshProUGUI)).transform;label.SetParent(root.transform,false);}
                var text=label.GetComponent<TextMeshProUGUI>();text.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Village/Production/StoryUI.asset");
                text.fontSize=18;text.color=new Color32(181,242,222,255);text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;
                text.rectTransform.anchorMin=new Vector2(0,.47f);text.rectTransform.anchorMax=Vector2.one;text.rectTransform.offsetMin=text.rectTransform.offsetMax=Vector2.zero;
                text.text="";label.gameObject.SetActive(false);
                root.GetComponent<RectTransform>().sizeDelta=new Vector2(90,12);
                foreach(var graphic in root.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))graphic.raycastTarget=false;
                so.FindProperty("BarBackground").objectReferenceValue=background;
                so.FindProperty("BarOutline").objectReferenceValue=outline;
                so.FindProperty("HeroName").objectReferenceValue=text;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }
    }
}
