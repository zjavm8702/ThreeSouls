using System;
using System.Linq;
using ThreeSouls.Data.Generated;
using ThreeSouls.UI;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace ThreeSouls.EditorTools
{
    public static class RuneUiPresentationBuilder
    {
        public static void Apply(GameObject root)
        {
            var definitions = AssetDatabase.LoadAssetAtPath<RuneTableDataTable>("Assets/ScriptableObjectDatas/RuneTableData.asset").Items;
            var skills = AssetDatabase.LoadAssetAtPath<SkillTableDataTable>("Assets/ScriptableObjectDatas/SkillTableData.asset").Items;
            foreach (var view in root.GetComponentsInChildren<UI_RuneStones>(true))
            {
                var preparation = view.GetComponentInParent<UI_DungeonPreparation>() != null;
                var so = new SerializedObject(view);
                var icons = so.FindProperty("DefinitionIcons");
                icons.arraySize = definitions.Length;
                for (var i = 0; i < definitions.Length; i++)
                {
                    var row = definitions[i];
                    var path = row.Category == ThreeSouls.Runes.RuneCategory.Skill
                        ? "Assets/Images/UIIcon/Skills/" + skills.Single(s => s.Key == row.SkillKey).SkillIconKey + ".png"
                        : "Assets/Images/UIIcon/Icon_" + (row.Effect switch
                        { ThreeSouls.Runes.RuneEffect.EquipmentDrop => "Sword", ThreeSouls.Runes.RuneEffect.Gold => "Gold", ThreeSouls.Runes.RuneEffect.Gems => "Ring", _ => "Armor" }) + ".png";
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    if (sprite == null) throw new InvalidOperationException("Missing rune icon: " + path);
                    icons.GetArrayElementAtIndex(i).objectReferenceValue = sprite;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                var parent = view.transform;
                Rect(parent.Find("Loadout"), .04f, .689f, .96f, .754f);
                Rect(parent.Find("DetailCard"), .04f, .147f, .96f, .341f);
                Rect(parent.Find("Equip"), .04f, .067f, .40f, .139f);
                Rect(parent.Find("Purchase"), .42f, .067f, .96f, .139f);
                Rect(parent.Find("Message"), .04f, .006f, .96f, .06f);
                var buttons = so.FindProperty("RuneButtons");
                for (var i = 0; i < buttons.arraySize; i++)
                {
                    var button = (UnityEngine.UI.Button)buttons.GetArrayElementAtIndex(i).objectReferenceValue;
                    var x = i % 2 == 0 ? .04f : .51f;
                    var y = .604f - i / 2 * .085f;
                    Rect(button.transform, x, y, x + .45f, y + .079f);
                    var label = (TMP_Text)so.FindProperty("RuneLabels").GetArrayElementAtIndex(i).objectReferenceValue;
                    Rect(label.transform, .27f, .06f, .98f, .94f);
                    label.enableAutoSizing = false; label.fontSize = preparation ? 28 : 34;
                    var image = (UnityEngine.UI.Image)so.FindProperty("RuneImages").GetArrayElementAtIndex(i).objectReferenceValue;
                    Rect(image.transform, .025f, .10f, .25f, .90f);
                    image.preserveAspect = true; image.color = Color.white; image.raycastTarget = false;
                }
                foreach (var name in new[] { "Sanctuary", "Skills", "Hero0", "Hero1", "Hero2", "Close", "Equip", "Purchase" })
                {
                    var label = parent.Find(name).GetComponentInChildren<TMP_Text>(true);
                    label.enableAutoSizing = false; label.fontSize = name == "Purchase" ? 28 : 34;
                }
                ((TMP_Text)so.FindProperty("Wallet").objectReferenceValue).fontSize = 30;
                ((TMP_Text)so.FindProperty("Message").objectReferenceValue).fontSize = 24;
                var loadout = (TMP_Text)so.FindProperty("Loadout").objectReferenceValue;
                loadout.enableAutoSizing = false; loadout.fontSize = 26;
                var detail = (TMP_Text)so.FindProperty("Detail").objectReferenceValue;
                detail.enableAutoSizing = false; detail.fontSize = 28;
                var card = parent.Find("DetailCard");
                var viewport = card.Find("Viewport") as RectTransform;
                if (viewport == null)
                {
                    viewport = new GameObject("Viewport", typeof(RectTransform), typeof(UnityEngine.UI.RectMask2D)).GetComponent<RectTransform>();
                    viewport.SetParent(card, false);
                }
                Rect(viewport, .035f, .035f, .965f, .965f);
                detail.transform.SetParent(viewport, false);
                var content = detail.rectTransform;
                content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
                content.pivot = new Vector2(.5f, 1); content.anchoredPosition = Vector2.zero; content.sizeDelta = Vector2.zero;
                detail.alignment = TextAlignmentOptions.TopLeft;
                var fit = detail.GetComponent<UnityEngine.UI.ContentSizeFitter>() ?? detail.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
                fit.horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.Unconstrained;
                fit.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
                var scroll = card.GetComponent<UnityEngine.UI.ScrollRect>() ?? card.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
                scroll.content = content; scroll.viewport = viewport; scroll.horizontal = false; scroll.vertical = true;
                scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 32;
                card.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
            }
            foreach (var shop in root.GetComponentsInChildren<UI_RuneShop>(true))
            {
                var management = shop.GetComponentInChildren<UI_RuneStones>(true);
                if (management == null) continue;
                var source = new SerializedObject(management).FindProperty("DefinitionIcons");
                var so = new SerializedObject(shop);
                var target = so.FindProperty("DefinitionIcons"); target.arraySize = source.arraySize;
                for (var i = 0; i < source.arraySize; i++) target.GetArrayElementAtIndex(i).objectReferenceValue = source.GetArrayElementAtIndex(i).objectReferenceValue;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void Rect(Transform target, float left, float bottom, float right, float top)
        {
            var rect = (RectTransform)target;
            rect.anchorMin = new Vector2(left, bottom); rect.anchorMax = new Vector2(right, top);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
