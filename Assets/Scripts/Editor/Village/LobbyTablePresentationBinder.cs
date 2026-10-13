using System;
using System.Collections.Generic;
using ThreeSouls.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ThreeSouls.EditorTools
{
    /// <summary>
    /// 기존 로비 배치의 표시 요소를 테이블 표시 담당자에 연결합니다.
    /// </summary>
    public static class LobbyTablePresentationBinder
    {
        public static void Bind(GameObject root)
        {
            var view = root.GetComponent<UI_LobbyTablePresentation>() ?? root.AddComponent<UI_LobbyTablePresentation>();
            var inventory = root.GetComponentInChildren<UI_LobbyInventory>(true).transform;
            var characters = root.GetComponentInChildren<UI_LobbyCharacters>(true).transform;
            var supplies = root.GetComponentInChildren<UI_LobbySupplies>(true).transform;
            var names = new List<UnityEngine.Object>();
            var portraits = new List<UnityEngine.Object>();
            for (var page = 0; page < 2; page++)
            {
                var parent = page == 0 ? inventory : characters;
                for (var i = 0; i < 3; i++)
                {
                    var hero = parent.Find("Hero" + i);
                    names.Add(page == 0 ? hero.GetComponentInChildren<Text>() : hero.Find("Name").GetComponent<Text>());
                    portraits.Add(hero.Find("Portrait").GetComponent<Image>());
                }
            }
            Set(view, "HeroNames", names.ToArray());
            Set(view, "HeroPortraits", portraits.ToArray());
            var fields = new[] { "PotionNames", "PotionPrices", "PotionEffects", "PotionIcons", "BuyFirstLabels", "BuySecondLabels", "TypeLabels" };
            var children = new[] { "Name", "Price", "Effect", "Icon", "BuyOne", "BuyFive", "" };
            for (var field = 0; field < fields.Length; field++)
            {
                var values = new UnityEngine.Object[2];
                for (var i = 0; i < values.Length; i++)
                {
                    var child = field == 6 ? supplies.Find("Type" + i) : supplies.Find("Potion" + i + "/" + children[field]);
                    values[i] = field == 3 ? child.GetComponent<Image>() : child.GetComponentInChildren<Text>();
                }
                Set(view, fields[field], values);
            }
            var thresholds = new UnityEngine.Object[4];
            for (var i = 0; i < thresholds.Length; i++)
            {
                thresholds[i] = supplies.Find("Threshold" + i).GetComponentInChildren<Text>();
            }
            Set(view, "ThresholdLabels", thresholds);
            var filters = new UnityEngine.Object[5];
            for (var i = 0; i < filters.Length; i++)
            {
                filters[i] = inventory.Find("Filter" + i).GetComponentInChildren<Text>();
            }
            Set(view, "FilterLabels", filters);
            var tabs = root.GetComponentInChildren<UI_LobbyTabs>(true);
            var buttons = new SerializedObject(tabs).FindProperty("TabButtons");
            var labels = new UnityEngine.Object[buttons.arraySize];
            for (var i = 0; i < labels.Length; i++)
            {
                labels[i] = ((Button)buttons.GetArrayElementAtIndex(i).objectReferenceValue).GetComponentInChildren<Text>();
            }
            Set(view, "TabLabels", labels);
            view.ApplyTables();
        }

        private static void Set(UI_LobbyTablePresentation target, string field, UnityEngine.Object[] values)
        {
            var data = new SerializedObject(target);
            var property = data.FindProperty(field);
            property.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++)
            {
                if (values[i] == null)
                {
                    throw new InvalidOperationException("로비 표시 연결 누락: " + field);
                }
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
