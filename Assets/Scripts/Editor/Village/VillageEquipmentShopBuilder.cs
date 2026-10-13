using System;
using System.Linq;
using ThreeSouls.Data;
using ThreeSouls.UI;
using ThreeSouls.Village;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ThreeSouls.EditorTools
{
    /// <summary>
    /// 기존 Canvas와 상점 건물을 유지하며 별도 장비 상점 프리팹과 입구 상호작용을 연결합니다.
    /// </summary>
    public static class VillageEquipmentShopBuilder
    {
        public const string PrefabPath = "Assets/Prefabs/UI/Lobby/UI_EquipmentShop.prefab";
        private const string ItemPath = "Assets/Prefabs/UI/Lobby/UI_EquipmentShopItem.prefab";
        private static readonly Color Ink = new Color32(19, 28, 35, 255);
        private static readonly Color Card = new Color32(32, 44, 53, 255);
        private static readonly Color Gold = new Color32(226, 183, 105, 255);
        private static readonly Color White = new Color32(237, 236, 218, 255);
        private static readonly Color Muted = new Color32(151, 169, 176, 255);
        private static Font Font;

        [MenuItem("Three Souls/Village/Connect Equipment Shop")]
        public static void Connect()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode == true || scene.path != VillageLobbyUIBuilder.ScenePath)
            {
                throw new InvalidOperationException("테스트 Village를 Edit 모드에서 열어 연결하세요.");
            }

            EquipmentShopCatalog.Validate();
            var transforms = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
            var tabs = transforms.Select(item => item.GetComponent<UI_LobbyTabs>()).Single(item => item != null);
            var layout = transforms.Select(item => item.GetComponent<UI_LobbyLayout>()).Single(item => item != null);
            var shop = layout.GetComponentInChildren<UI_EquipmentShop>(true);
            if (shop == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) ?? BuildPrefab();
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, layout.transform);
                Undo.RegisterCreatedObjectUndo(instance, "Add Equipment Shop");
                shop = instance.GetComponent<UI_EquipmentShop>();
            }

            Bind(shop, "Tabs", tabs);
            shop.gameObject.SetActive(false);
            var market = transforms.Single(item => item.name == "Market");
            var entrance = transforms.Single(item => item.name == "Entrance - Market");
            var sign = transforms.Single(item => item.name == "Market Sign");
            var target = GetOrAdd<VillageShopTarget>(market.gameObject);
            var hit = market.Find("Equipment Shop Hit Area");
            if (hit == null)
            {
                hit = new GameObject("Equipment Shop Hit Area").transform;
                hit.SetParent(market, false);
                Undo.RegisterCreatedObjectUndo(hit.gameObject, "Shop Building Hit Area");
            }

            hit.localPosition = new Vector3(0, 2.75f, 0);
            var buildingArea = GetOrAdd<BoxCollider2D>(hit.gameObject);
            buildingArea.isTrigger = true;
            buildingArea.size = new Vector2(10, 7.5f);
            var signArea = GetOrAdd<BoxCollider2D>(sign.gameObject);
            signArea.isTrigger = true;
            signArea.size = Vector2.one * 1.3f;
            Bind(target, "Entrance", entrance);
            var serialized = new SerializedObject(target);
            var areas = serialized.FindProperty("HitAreas");
            areas.arraySize = 2;
            areas.GetArrayElementAtIndex(0).objectReferenceValue = buildingArea;
            areas.GetArrayElementAtIndex(1).objectReferenceValue = signArea;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var party = transforms.Select(item => item.GetComponent<VillagePartyController>()).Single(item => item != null);
            var interaction = GetOrAdd<VillageShopInteraction>(party.gameObject);
            Bind(interaction, "Party", party);
            Bind(interaction, "Target", target);
            Bind(interaction, "Tabs", tabs);
            Bind(interaction, "Shop", shop);
            var status = transforms.Single(item => item.name == "Eyebrow").GetComponent<UnityEngine.UI.Text>();
            status.text = "바닥: 이동  ·  왼쪽 주황 지붕 상점/간판: 장비 구매·판매";
            PrefabUtility.RecordPrefabInstancePropertyModifications(status);
            Bind(interaction, "Status", status);
            Bind(party.GetComponent<VillageMoveInput>(), "ShopInteraction", interaction);
            var test = transforms.Select(item => item.GetComponent<T_VillageData>()).Single(item => item != null);
            Bind(test, "Shop", shop);
            Physics2D.SyncTransforms();
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("Village equipment shop connected. Scene remains unsaved.");
        }

        private static GameObject BuildPrefab()
        {
            Font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Mulmaru.ttf")
                ?? throw new InvalidOperationException("기존 Mulmaru UI 폰트가 필요합니다.");
            var item = BuildItem();
            var root = Box("UI_EquipmentShop", null, 0, 0, 1, 1, new Color(0, 0, 0, 0.72f), true);
            var shop = root.gameObject.AddComponent<UI_EquipmentShop>();
            var panel = Box("ShopPanel", root, 0.02f, 0.12f, 0.98f, 0.9f, Ink);
            var frame = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/UIIMage/ResultPanel.png");
            var panelImage = panel.GetComponent<UnityEngine.UI.Image>();
            panelImage.sprite = frame;
            panelImage.type = UnityEngine.UI.Image.Type.Sliced;
            Bind(shop, "Title", Label("Title", panel, 0.04f, 0.93f, 0.72f, 0.985f, "장비 상점", 44, Gold));
            Bind(shop, "CloseButton", Button("Close", panel, 0.77f, 0.932f, 0.96f, 0.98f, "마을로", out _));
            Bind(shop, "Wallet", Label("Wallet", panel, 0.04f, 0.879f, 0.96f, 0.925f, "", 28, White));
            var buy = Button("BuyTab", panel, 0.04f, 0.81f, 0.487f, 0.868f, "장비 구매", out _);
            var sell = Button("SellTab", panel, 0.513f, 0.81f, 0.96f, 0.868f, "보유 장비 판매", out _);
            Bind(shop, "BuyTab", buy);
            Bind(shop, "SellTab", sell);
            Bind(shop, "BuyIndicator", Box("Indicator", buy.transform, 0, 0, 1, 0.06f, Gold).GetComponent<UnityEngine.UI.Image>());
            Bind(shop, "SellIndicator", Box("Indicator", sell.transform, 0, 0, 1, 0.06f, Muted).GetComponent<UnityEngine.UI.Image>());
            Bind(shop, "Count", Label("Count", panel, 0.04f, 0.757f, 0.96f, 0.805f, "", 22, Muted));
            var scrollRoot = Box("Items", panel, 0.04f, 0.412f, 0.96f, 0.75f, Card);
            var scroll = scrollRoot.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            var viewport = Box("Viewport", scrollRoot, 0, 0, 1, 1, Color.white, true);
            viewport.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic = false;
            var content = Rect("Content", viewport, 0, 1, 1, 1);
            content.pivot = new Vector2(0.5f, 1);
            var grid = content.gameObject.AddComponent<UnityEngine.UI.GridLayoutGroup>();
            grid.cellSize = new Vector2(215, 200);
            grid.spacing = new Vector2(12, 12);
            grid.padding = new RectOffset(10, 10, 10, 10);
            grid.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;
            var fitter = content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
            fitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            Bind(shop, "Content", content);
            Bind(shop, "Scroll", scroll);
            Bind(shop, "ItemPrefab", item);
            var empty = Label("Empty", scrollRoot, 0.03f, 0.3f, 0.97f, 0.7f, "", 30, White);
            empty.alignment = TextAnchor.MiddleCenter;
            Bind(shop, "Empty", empty);
            var detail = Box("Detail", panel, 0.04f, 0.153f, 0.96f, 0.395f, Card);
            var icon = Rect("Icon", detail, 0.025f, 0.56f, 0.15f, 0.92f).gameObject.AddComponent<UnityEngine.UI.Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            Bind(shop, "DetailIcon", icon);
            Bind(shop, "DetailTitle", Label("Name", detail, 0.18f, 0.735f, 0.975f, 0.965f, "", 34, Gold));
            Bind(shop, "DetailType", Label("Type", detail, 0.18f, 0.555f, 0.975f, 0.735f, "", 23, Muted));
            Bind(shop, "DetailStats", Label("Stats", detail, 0.04f, 0.305f, 0.96f, 0.555f, "", 26, White));
            Bind(shop, "DetailPrice", Label("Price", detail, 0.04f, 0.025f, 0.96f, 0.305f, "", 25, Gold));
            Bind(shop, "TradeButton", Button("Trade", panel, 0.04f, 0.082f, 0.96f, 0.14f, "장비 구매", out var tradeLabel));
            Bind(shop, "TradeLabel", tradeLabel);
            Bind(shop, "Message", Label("Message", panel, 0.04f, 0.011f, 0.96f, 0.073f, "", 23, Gold));
            var confirm = Box("SaleConfirmation", root, 0, 0, 1, 1, new Color(0, 0, 0, 0.84f), true);
            var confirmPanel = Box("Panel", confirm, 0.07f, 0.34f, 0.93f, 0.66f, Ink);
            Bind(shop, "ConfirmationTitle", Label("Title", confirmPanel, 0.06f, 0.31f, 0.94f, 0.94f, "", 32, White));
            Bind(shop, "ConfirmButton", Button("Confirm", confirmPanel, 0.06f, 0.085f, 0.47f, 0.235f, "판매 확정", out _));
            Bind(shop, "CancelButton", Button("Cancel", confirmPanel, 0.53f, 0.085f, 0.94f, 0.235f, "취소", out _));
            Bind(shop, "Confirmation", confirm.gameObject);
            confirm.gameObject.SetActive(false);
            root.gameObject.SetActive(false);
            UiPolishBuilder.Apply(root.gameObject);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject, PrefabPath);
            UnityEngine.Object.DestroyImmediate(root.gameObject);
            return prefab;
        }

        private static UI_EquipmentShopItem BuildItem()
        {
            var root = Box("UI_EquipmentShopItem", null, 0, 0, 1, 1, Ink, true);
            root.sizeDelta = new Vector2(215, 200);
            var item = root.gameObject.AddComponent<UI_EquipmentShopItem>();
            var button = root.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = root.GetComponent<UnityEngine.UI.Image>();
            Bind(item, "SelectButton", button);
            var selection = Box("Selection", root, 0, 0, 1, 1, Gold);
            Box("Inset", selection, 0.02f, 0.02f, 0.98f, 0.98f, Ink);
            Bind(item, "Selection", selection.gameObject);
            var icon = Rect("Icon", root, 0.25f, 0.37f, 0.75f, 0.91f).gameObject.AddComponent<UnityEngine.UI.Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            Bind(item, "Icon", icon);
            var title = Label("Name", root, 0.035f, 0.13f, 0.965f, 0.365f, "", 21, White);
            title.alignment = TextAnchor.MiddleCenter;
            Bind(item, "ItemName", title);
            var price = Label("Price", root, 0.03f, 0.025f, 0.97f, 0.13f, "", 19, Gold);
            price.alignment = TextAnchor.MiddleCenter;
            Bind(item, "Price", price);
            Bind(item, "RarityLine", Box("Rarity", root, 0.05f, 0.93f, 0.95f, 0.96f, Gold).GetComponent<UnityEngine.UI.Image>());
            UiPolishBuilder.Apply(root.gameObject);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject, ItemPath).GetComponent<UI_EquipmentShopItem>();
            UnityEngine.Object.DestroyImmediate(root.gameObject);
            return prefab;
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            var component = target.GetComponent<T>();
            if (component == null)
            {
                component = Undo.AddComponent<T>(target);
            }

            return component;
        }

        private static RectTransform Rect(string name, Transform parent, float x0, float y0, float x1, float y1)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(x0, y0);
            rect.anchorMax = new Vector2(x1, y1);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static RectTransform Box(string name, Transform parent, float x0, float y0, float x1, float y1, Color color,
            bool raycast = false)
        {
            var rect = Rect(name, parent, x0, y0, x1, y1);
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = color;
            image.raycastTarget = raycast;
            return rect;
        }

        private static UnityEngine.UI.Text Label(string name, Transform parent, float x0, float y0, float x1, float y1,
            string text, int size, Color color)
        {
            var rect = Rect(name, parent, x0, y0, x1, y1);
            var label = rect.gameObject.AddComponent<UnityEngine.UI.Text>();
            label.font = Font;
            label.fontSize = size;
            label.text = text;
            label.color = color;
            label.alignment = TextAnchor.MiddleLeft;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            return label;
        }

        private static UnityEngine.UI.Button Button(string name, Transform parent, float x0, float y0, float x1, float y1,
            string title, out UnityEngine.UI.Text label)
        {
            var rect = Box(name, parent, x0, y0, x1, y1, Card, true);
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = rect.GetComponent<UnityEngine.UI.Image>();
            var navigation = button.navigation;
            navigation.mode = UnityEngine.UI.Navigation.Mode.None;
            button.navigation = navigation;
            label = Label("Label", rect, 0.035f, 0.02f, 0.965f, 0.98f, title, 28, White);
            label.alignment = TextAnchor.MiddleCenter;
            return button;
        }

        private static void Bind(UnityEngine.Object target, string name, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(name).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            if (PrefabUtility.IsPartOfPrefabInstance(target) == true)
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            }
        }
    }
}
