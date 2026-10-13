using System;
using ThreeSouls.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ThreeSouls.Editor
{
    public static class CombatHUDPrefabGenerator
    {
        private const string PrefabPath = "Assets/Prefabs/UI/UI_HUD_Top.prefab";

        [MenuItem("Tools/Three Souls/UI/Create Combat HUD")]
        public static void CreateCombatHUD()
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Mulmaru.ttf");

            if (font == null)
            {
                throw new InvalidOperationException("HUD font was not found: Assets/Fonts/Mulmaru.ttf");
            }

            var root = CreateUIObject("UI_HUD_Top", null);
            var rootRect = (RectTransform)root.transform;
            rootRect.anchorMin = new Vector2(0f, 1f);
            rootRect.anchorMax = Vector2.one;
            rootRect.pivot = new Vector2(0.5f, 1f);
            rootRect.anchoredPosition = Vector2.zero;
            rootRect.sizeDelta = new Vector2(0f, 112f);

            root.AddComponent<CanvasGroup>();
            var background = root.AddComponent<Image>();
            background.color = new Color(0.035f, 0.045f, 0.075f, 0.92f);
            background.raycastTarget = false;

            var hud = root.AddComponent<UI_HUD_Top>();
            var timeText = CreateTimePanel(rootRect, font);
            var goldCard = CreateRewardCard(rootRect, "Gold", "G", new Color(1f, 0.72f, 0.08f), -390f, font);
            var chestCard = CreateRewardCard(rootRect, "Chest", "BOX", new Color(0.76f, 0.38f, 0.13f), -120f, font);

            var hudObject = new SerializedObject(hud);
            hudObject.FindProperty("TimeText").objectReferenceValue = timeText;
            hudObject.FindProperty("GoldText").objectReferenceValue = goldCard.Value;
            hudObject.FindProperty("ChestText").objectReferenceValue = chestCard.Value;
            hudObject.FindProperty("GoldTarget").objectReferenceValue = goldCard.Target;
            hudObject.ApplyModifiedPropertiesWithoutUndo();

            SetLayerRecursively(root, LayerMask.NameToLayer("UI"));
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            UnityEngine.Object.DestroyImmediate(root);

            if (prefab == null || prefab.TryGetComponent<UI_HUD_Top>(out var prefabHUD) == false)
            {
                throw new InvalidOperationException($"Failed to create HUD prefab: {PrefabPath}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Combat HUD created: {PrefabPath}");
        }

        private static Text CreateTimePanel(RectTransform parent, Font font)
        {
            var panel = CreatePanel("Time", parent, new Vector2(0.5f, 1f), new Vector2(300f, 76f), new Vector2(0f, -18f));
            CreateText("Label", panel, font, "BATTLE TIME", 18, new Color(0.65f, 0.72f, 0.82f),
                new Vector2(0f, 0.56f), Vector2.one);
            return CreateText("Value", panel, font, "00:00", 34, Color.white,
                Vector2.zero, new Vector2(1f, 0.68f));
        }

        private static RewardCard CreateRewardCard(
            RectTransform parent,
            string name,
            string iconLabel,
            Color accent,
            float x,
            Font font)
        {
            var panel = CreatePanel(name, parent, Vector2.one, new Vector2(240f, 72f), new Vector2(x, -20f));
            panel.pivot = Vector2.one;

            var iconObject = CreateUIObject("Icon", panel);
            var iconRect = (RectTransform)iconObject.transform;
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = new Vector2(12f, 0f);
            iconRect.sizeDelta = new Vector2(52f, 52f);
            var iconImage = iconObject.AddComponent<Image>();
            iconImage.color = accent;
            iconImage.raycastTarget = false;
            var iconText = CreateText("Symbol", iconRect, font, iconLabel, iconLabel.Length > 1 ? 15 : 28, Color.white,
                Vector2.zero, Vector2.one);
            iconText.fontStyle = FontStyle.Bold;

            var value = CreateText("Value", panel, font, "0", 30, Color.white,
                new Vector2(0.3f, 0f), new Vector2(0.95f, 1f));
            value.alignment = TextAnchor.MiddleRight;
            return new RewardCard(iconRect, value);
        }

        private static RectTransform CreatePanel(
            string name,
            RectTransform parent,
            Vector2 anchor,
            Vector2 size,
            Vector2 position)
        {
            var panelObject = CreateUIObject(name, parent);
            var panel = (RectTransform)panelObject.transform;
            panel.anchorMin = anchor;
            panel.anchorMax = anchor;
            panel.pivot = new Vector2(0.5f, 1f);
            panel.anchoredPosition = position;
            panel.sizeDelta = size;
            var image = panelObject.AddComponent<Image>();
            image.color = new Color(0.12f, 0.15f, 0.22f, 0.96f);
            image.raycastTarget = false;
            return panel;
        }

        private static Text CreateText(
            string name,
            RectTransform parent,
            Font font,
            string value,
            int fontSize,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax)
        {
            var textObject = CreateUIObject(name, parent);
            var rect = (RectTransform)textObject.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = new Vector2(8f, 4f);
            rect.offsetMax = new Vector2(-8f, -4f);
            var text = textObject.AddComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        private static GameObject CreateUIObject(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            return gameObject;
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;

            foreach (Transform child in root.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        private readonly struct RewardCard
        {
            public RectTransform Target { get; }
            public Text Value { get; }

            public RewardCard(RectTransform target, Text value)
            {
                Target = target;
                Value = value;
            }
        }
    }
}
