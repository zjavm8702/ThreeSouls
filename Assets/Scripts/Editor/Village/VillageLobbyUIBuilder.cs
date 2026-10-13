using System;
using System.IO;
using ThreeSouls.Data;
using ThreeSouls.UI;
using ThreeSouls.Village;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem.UI;

namespace ThreeSouls.EditorTools
{
    /// <summary>
    /// 테스트 마을에 편집 가능한 로비 UI 프리팹과 테스트 데이터 연결을 제작합니다.
    /// </summary>
    public static class VillageLobbyUIBuilder
    {
        public const string ScenePath = "Assets/(T)Scene/(T)Scene_Village.unity";
        public const string PrefabPath = "Assets/Prefabs/UI/Lobby/UI_Lobby.prefab";
        private static readonly Color Ink = new Color32(19, 28, 35, 250);
        private static readonly Color Card = new Color32(32, 44, 53, 255);
        private static readonly Color Gold = new Color32(226, 183, 105, 255);
        private static readonly Color White = new Color32(237, 236, 218, 255);
        private static readonly Color Muted = new Color32(151, 169, 176, 255);
        private static Font Font;
        private static Sprite Frame;

        [MenuItem("Three Souls/Village/Rebuild Lobby UI on Existing Canvas")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode == true || EditorApplication.isCompiling == true)
            {
                throw new InvalidOperationException("Edit 모드에서 컴파일 완료 후 실행하세요.");
            }

            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (scene.IsValid() == false || scene.isLoaded == false)
            {
                throw new InvalidOperationException("옮겨둔 CanvasGroup이 있는 테스트 마을 씬을 먼저 여세요.");
            }

            var roots = scene.GetRootGameObjects();
            Transform uiCanvas = null;
            GameObject previousUI = null;
            T_VillageData test = null;

            for (var i = 0; i < roots.Length; i++)
            {
                if (roots[i].name == "CanvasGroup" && roots[i].CompareTag("CanvasGroup") == true)
                {
                    uiCanvas = roots[i].transform.Find("UICanvas");
                }

                var tabs = roots[i].GetComponentInChildren<UI_LobbyTabs>(true);

                if (tabs != null)
                {
                    previousUI = PrefabUtility.GetOutermostPrefabInstanceRoot(tabs.gameObject);

                    if (previousUI == null || PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(tabs.gameObject) != PrefabPath)
                    {
                        throw new InvalidOperationException("이 제작 도구가 만든 로비 프리팹만 교체할 수 있습니다.");
                    }
                }

                if (roots[i].GetComponent<T_VillageData>() != null)
                {
                    test = roots[i].GetComponent<T_VillageData>();
                }
            }

            if (uiCanvas == null || uiCanvas.GetComponent<UnityEngine.Canvas>() == null)
            {
                throw new InvalidOperationException("옮겨둔 CanvasGroup/UICanvas가 필요합니다.");
            }

            var previous = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(scene);

            try
            {
                PrepareAssets();
                var cell = BuildItem();
                var root = BuildLobby(cell);
                LobbyTablePresentationBinder.Bind(root);
                UiPolishBuilder.Apply(root);
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                UnityEngine.Object.DestroyImmediate(root);
                if (previousUI != null)
                {
                    Undo.DestroyObjectImmediate(previousUI);
                }

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, uiCanvas);
                Undo.RegisterCreatedObjectUndo(instance, "Connect Lobby UI");

                if (test == null)
                {
                    test = new GameObject("T_VillageData").AddComponent<T_VillageData>();
                }

                Bind(test, "Tabs", instance.GetComponentInChildren<UI_LobbyTabs>(true));
                Bind(test, "Inventory", instance.GetComponentInChildren<UI_LobbyInventory>(true));
                Bind(test, "Characters", instance.GetComponentInChildren<UI_LobbyCharacters>(true));
                Bind(test, "Forge", instance.GetComponentInChildren<UI_LobbyForge>(true));
                Bind(test, "Supplies", instance.GetComponentInChildren<UI_LobbySupplies>(true));
                var tabs = instance.GetComponentInChildren<UI_LobbyTabs>(true);

                for (var i = 0; i < roots.Length; i++)
                {
                    var input = roots[i].GetComponent<VillageMoveInput>();

                    if (input != null)
                    {
                        Bind(input, "Tabs", tabs);
                        tabs.SelectTab(ThreeSouls.Data.LobbyTables.GetRules().InitialTab);
                    }
                }

                if (FindEventSystem(scene) == false)
                {
                    new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                }

                VillageEquipmentShopBuilder.Connect();

                EditorSceneManager.MarkSceneDirty(scene);
                Debug.Log("Village lobby UI connected to existing UICanvas. Scene changes remain unsaved: " + ScenePath);
            }
            finally
            {
                SceneManager.SetActiveScene(previous);
            }
        }

        private static bool FindEventSystem(Scene scene)
        {
            var roots = scene.GetRootGameObjects();

            for (var i = 0; i < roots.Length; i++)
            {
                if (roots[i].GetComponentInChildren<EventSystem>(true) != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static void PrepareAssets()
        {
            Directory.CreateDirectory("Assets/Prefabs/UI/Lobby");
            AssetDatabase.Refresh();
            Font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Mulmaru.ttf")
                ?? throw new InvalidOperationException("Mulmaru UI 폰트가 필요합니다.");
            Frame = LobbyResourceCatalog.GetSprite("Assets/Images/UIIMage/ResultPanel.png", "ResultPanel");
        }

        private static UI_LobbyItem BuildItem()
        {
            var rect = Rect("UI_LobbyItem", null, 0, 0, 1, 1);
            rect.sizeDelta = new Vector2(162, 176);
            var button = rect.gameObject.AddComponent<Button>();
            var image = rect.gameObject.AddComponent<Image>();
            image.color = Card;
            button.targetGraphic = image;
            var selected = Box("Selected", rect, 0, 0, 1, 1, Gold);
            Box("Inset", selected, 0.018f, 0.018f, 0.982f, 0.982f, Card);
            var line = Box("Rarity", rect, 0.06f, 0.03f, 0.94f, 0.055f, Muted).GetComponent<Image>();
            var icon = Picture("Icon", rect, 0.22f, 0.36f, 0.78f, 0.82f, null);
            var title = Label("Name", rect, 0.035f, 0.065f, 0.965f, 0.36f, "", 18, White);
            title.alignment = TextAnchor.MiddleCenter;
            var owner = Label("Owner", rect, 0.07f, 0.82f, 0.93f, 0.97f, "", 18, Gold);
            owner.alignment = TextAnchor.MiddleRight;
            var component = rect.gameObject.AddComponent<UI_LobbyItem>();
            Bind(component, "SelectButton", button);
            Bind(component, "Icon", icon);
            Bind(component, "RarityLine", line);
            Bind(component, "Selection", selected.gameObject);
            Bind(component, "ItemName", title);
            Bind(component, "Owner", owner);
            selected.gameObject.SetActive(false);
            UiPolishBuilder.Apply(rect.gameObject);
            var prefab = PrefabUtility.SaveAsPrefabAsset(rect.gameObject, "Assets/Prefabs/UI/Lobby/UI_LobbyItem.prefab");
            UnityEngine.Object.DestroyImmediate(rect.gameObject);

            return prefab.GetComponent<UI_LobbyItem>();
        }

        private static GameObject BuildLobby(UI_LobbyItem cell)
        {
            var safe = Rect("UI_Lobby", null, 0, 0, 1, 1);
            safe.gameObject.AddComponent<UI_LobbySafeArea>();
            var shell = Rect("Lobby", safe, 0, 0, 1, 1);
            shell.gameObject.AddComponent<UI_LobbyLayout>();
            var tabs = shell.gameObject.AddComponent<UI_LobbyTabs>();
            var header = Box("Header", shell, 0.035f, 0.89f, 0.965f, 0.985f, Ink);
            header.GetComponent<Image>().raycastTarget = true;
            Label("Brand", header, 0.04f, 0.53f, 0.60f, 0.87f, "THREE SOULS", 36, Gold);
            Label("Location", header, 0.04f, 0.12f, 0.65f, 0.45f, "성소 마을  /  테스트 로비", 24, Muted);
            Picture("GoldIcon", header, 0.72f, 0.35f, 0.78f, 0.70f,
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/UIIcon/Icon_Gold.png"));
            var gold = Label("Gold", header, 0.80f, 0.25f, 0.97f, 0.78f, "12,500", 30, White);
            gold.alignment = TextAnchor.MiddleRight;
            Bind(tabs, "Gold", gold);
            var village = Rect("Village", shell, 0.035f, 0.13f, 0.965f, 0.87f);
            var welcome = Box("Welcome", village, 0, 0.005f, 1, 0.08f, Ink);
            welcome.GetComponent<Image>().raycastTarget = true;
            Label("Eyebrow", welcome, 0.04f, 0.08f, 0.62f, 0.92f, "마을 바닥을 눌러 세 영웅을 이동하세요", 22, Gold).alignment = TextAnchor.MiddleLeft;
            welcome.gameObject.SetActive(false);
            var inventory = BuildInventory(shell, cell);
            var characters = BuildCharacters(shell);
            var supplies = BuildSupplies(shell);
            var bar = Box("Navigation", shell, 0.035f, 0.025f, 0.965f, 0.11f, Ink);
            bar.GetComponent<Image>().raycastTarget = true;
            var buttons = new UnityEngine.Object[4];
            var indicators = new UnityEngine.Object[4];
            var tabNames = new[] { "마을", "인벤토리", "캐릭터", "준비" };
            var hints = new[] { "VILLAGE", "INVENTORY", "CHARACTERS", "SUPPLIES" };

            for (var i = 0; i < buttons.Length; i++)
            {
                var button = Button("Tab" + i, bar, i / 5f + 0.01f, 0.08f, (i + 1) / 5f - 0.01f, 0.94f,
                    tabNames[i], out var title);
                title.rectTransform.anchorMin = new Vector2(0.05f, 0.35f);
                title.rectTransform.anchorMax = new Vector2(0.95f, 0.92f);
                title.fontSize = 28;
                Label("Hint", button.transform, 0.05f, 0.02f, 0.95f, 0.34f, hints[i], 18, Muted).alignment = TextAnchor.MiddleCenter;
                buttons[i] = button;
                indicators[i] = Box("Indicator", button.transform, 0.05f, 0.96f, 0.95f, 1, i == 1 ? Gold : Card).GetComponent<Image>();
            }

            BindArray(tabs, "TabButtons", buttons);
            BindArray(tabs, "Indicators", indicators);
            BindArray(tabs, "Pages", new UnityEngine.Object[] { village.gameObject, inventory.gameObject, characters.gameObject, supplies.gameObject });
            DungeonSelectionBuilder.Configure(tabs);
            ThreeSouls.Editor.GameCycleSetup.ConfigureLobby(tabs);
            village.gameObject.SetActive(false);
            characters.gameObject.SetActive(false);
            supplies.gameObject.SetActive(false);

            return safe.gameObject;
        }

        private static UI_LobbySupplies BuildSupplies(Transform shell)
        {
            var page = Box("Supplies", shell, 0.035f, 0.13f, 0.965f, 0.87f, new Color32(19, 28, 35, 255));
            page.GetComponent<Image>().raycastTarget = true;
            var view = page.gameObject.AddComponent<UI_LobbySupplies>();
            Label("Title", page, 0.045f, 0.925f, 0.955f, 0.975f, "출정 준비", 46, White);
            Label("Subtitle", page, 0.045f, 0.885f, 0.955f, 0.925f, "회복 물약과 자동 회복 조건을 준비하세요", 25, Muted);
            Bind(view, "Wallet", Label("Wallet", page, 0.045f, 0.842f, 0.955f, 0.882f, "", 27, Gold));
            var types = new[] { ConsumableType.SmallHealingPotion, ConsumableType.PartyHealingPotion };
            var spriteNames = new[] { "Potions_8", "Potions_2" };
            var owned = new UnityEngine.Object[2];
            var one = new UnityEngine.Object[2];
            var five = new UnityEngine.Object[2];
            var typeButtons = new UnityEngine.Object[2];
            var sprites = AssetDatabase.LoadAllAssetsAtPath("Assets/Images/UIIcon/Potions.png");

            for (var i = 0; i < types.Length; i++)
            {
                Sprite icon = null;

                for (var j = 0; j < sprites.Length; j++)
                {
                    if (sprites[j] is Sprite sprite && sprite.name == spriteNames[i])
                    {
                        icon = sprite;
                        break;
                    }
                }

                if (icon == null)
                {
                    throw new InvalidOperationException("물약 이미지가 필요합니다: " + spriteNames[i]);
                }

                var x = 0.045f + i * 0.465f;
                var card = Box("Potion" + i, page, x, 0.600f, x + 0.445f, 0.830f, Card);
                Picture("Icon", card, 0.025f, 0.60f, 0.20f, 0.94f, icon);
                Label("Name", card, 0.22f, 0.73f, 0.98f, 0.94f, ConsumableCatalog.GetName(types[i]), 29, White);
                Label("Price", card, 0.22f, 0.57f, 0.98f, 0.74f, $"{ConsumableCatalog.GetPrice(types[i])} 골드 / 개", 24, Gold);
                Label("Effect", card, 0.055f, 0.35f, 0.945f, 0.57f, ConsumableCatalog.GetEffect(types[i]), 22, Muted);
                owned[i] = Label("Owned", card, 0.055f, 0.24f, 0.945f, 0.36f, "", 23, White);
                one[i] = Button("BuyOne", card, 0.055f, 0.035f, 0.48f, 0.22f, "1개 구매", out _);
                five[i] = Button("BuyFive", card, 0.52f, 0.035f, 0.945f, 0.22f, "5개 구매", out _);
                typeButtons[i] = Button("Type" + i, page, x, 0.342f, x + 0.445f, 0.392f,
                    ConsumableCatalog.GetName(types[i]) + " 편성", out var label);
                label.fontSize = 25;
            }

            BindArray(view, "Owned", owned);
            BindArray(view, "BuyOne", one);
            BindArray(view, "BuyFive", five);
            BindArray(view, "TypeButtons", typeButtons);
            Label("SlotsTitle", page, 0.045f, 0.550f, 0.955f, 0.592f, "공급품 편성  ·  슬롯을 선택하세요", 29, Gold);
            var slots = new UnityEngine.Object[2];
            var labels = new UnityEngine.Object[2];
            var indicators = new UnityEngine.Object[2];

            for (var i = 0; i < slots.Length; i++)
            {
                var x = 0.045f + i * 0.465f;
                var slot = Button("Slot" + i, page, x, 0.435f, x + 0.445f, 0.548f, "", out var label);
                label.fontSize = 27;
                slots[i] = slot;
                labels[i] = label;
                indicators[i] = Box("Indicator", slot.transform, 0.03f, 0.94f, 0.97f, 0.99f, Card).GetComponent<Image>();
            }

            BindArray(view, "SlotButtons", slots);
            BindArray(view, "SlotLabels", labels);
            BindArray(view, "SlotIndicators", indicators);
            Bind(view, "Selected", Label("Selected", page, 0.045f, 0.398f, 0.955f, 0.430f, "", 24, Muted));
            Bind(view, "Decrease", Button("Decrease", page, 0.045f, 0.279f, 0.265f, 0.330f, "−  1개", out _));
            Bind(view, "Increase", Button("Increase", page, 0.280f, 0.279f, 0.500f, 0.330f, "+  1개", out _));
            Bind(view, "RemoveSlot", Button("RemoveSlot", page, 0.515f, 0.279f, 0.955f, 0.330f, "선택 슬롯 비우기", out _));
            Label("AutoHealTitle", page, 0.045f, 0.230f, 0.955f, 0.273f, "자동 회복 기준", 29, Gold);
            var thresholds = new[] { AutoHealThreshold.Disabled, AutoHealThreshold.At20Percent, AutoHealThreshold.At30Percent, AutoHealThreshold.At40Percent };
            var thresholdButtons = new UnityEngine.Object[4];
            var thresholdIndicators = new UnityEngine.Object[4];

            for (var i = 0; i < thresholds.Length; i++)
            {
                var x = 0.045f + i * 0.2325f;
                var button = Button("Threshold" + i, page, x, 0.173f, x + 0.2125f, 0.224f,
                    ConsumableCatalog.GetThresholdLabel(thresholds[i]), out var label);
                label.fontSize = 22;
                thresholdButtons[i] = button;
                thresholdIndicators[i] = Box("Indicator", button.transform, 0.03f, 0.94f, 0.97f, 0.99f, Card).GetComponent<Image>();
            }

            BindArray(view, "ThresholdButtons", thresholdButtons);
            BindArray(view, "ThresholdIndicators", thresholdIndicators);
            Bind(view, "Summary", Label("Summary", page, 0.045f, 0.080f, 0.66f, 0.167f, "", 26, White));
            Bind(view, "ClearSlots", Button("ClearSlots", page, 0.69f, 0.099f, 0.955f, 0.153f, "편성 초기화", out var clearLabel));
            clearLabel.fontSize = 24;
            Bind(view, "Message", Label("Message", page, 0.045f, 0.012f, 0.955f, 0.077f,
                "물약 종류를 고르고 + / −로 수량을 조절하세요.", 23, Muted));

            return view;
        }

        internal static void ConfigureVillageGuide(UI_LobbyTabs tabs)
        {
            var guide = (RectTransform)tabs.transform.Find("Village/Welcome");
            guide.gameObject.SetActive(false);
            SetGuideRect(guide, new Vector2(0, 0.005f), new Vector2(1, 0.08f));
            var hint = guide.Find("Eyebrow").GetComponent<Text>();
            SetGuideRect(hint.rectTransform, new Vector2(0.04f, 0.08f), new Vector2(0.62f, 0.92f));
            hint.text = "마을 바닥을 눌러 세 영웅을 이동하세요";
            hint.fontSize = 22;
            hint.alignment = TextAnchor.MiddleLeft;
            PrefabUtility.RecordPrefabInstancePropertyModifications(hint);
            var title = guide.Find("Title");

            if (title != null)
            {
                title.gameObject.SetActive(false);
                PrefabUtility.RecordPrefabInstancePropertyModifications(title.gameObject);
            }

            var shortcut = guide.Find("OpenInventory");
            if (shortcut != null) UnityEngine.Object.DestroyImmediate(shortcut.gameObject);
        }

        private static void SetGuideRect(RectTransform rect, Vector2 minimum, Vector2 maximum)
        {
            rect.anchorMin = minimum;
            rect.anchorMax = maximum;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
        }

        private static UI_LobbyInventory BuildInventory(Transform shell, UI_LobbyItem cell)
        {
            var page = Box("Inventory", shell, 0.035f, 0.13f, 0.965f, 0.87f, Ink);
            page.GetComponent<Image>().sprite = Frame;
            page.GetComponent<Image>().type = Image.Type.Sliced;
            var view = page.gameObject.AddComponent<UI_LobbyInventory>();
            Bind(view, "ItemPrefab", cell);
            Label("Title", page, 0.045f, 0.915f, 0.65f, 0.975f, "인벤토리", 46, White);
            var count = Label("Count", page, 0.52f, 0.925f, 0.955f, 0.965f, "24 / 80   ·   표시 24", 22, Muted);
            count.alignment = TextAnchor.MiddleRight;
            Bind(view, "Count", count);
            var heroes = new UnityEngine.Object[3];
            var heroIndicators = new UnityEngine.Object[3];

            for (var i = 0; i < heroes.Length; i++)
            {
                var x = 0.045f + i * 0.31f;
                var hero = Button("Hero" + i, page, x, 0.805f, x + 0.29f, 0.902f,
                    LobbyPresentationData.GetHeroName(i), out var title);
                title.rectTransform.anchorMin = new Vector2(0.39f, 0.10f);
                Picture("Portrait", hero.transform, 0.025f, 0.04f, 0.42f, 0.96f, LobbyPresentationData.GetHeroPortrait(i));
                heroes[i] = hero;
                heroIndicators[i] = Box("Indicator", hero.transform, 0.05f, 0.02f, 0.95f, 0.045f, i == 0 ? Gold : Card).GetComponent<Image>();
            }

            BindArray(view, "HeroButtons", heroes);
            BindArray(view, "HeroIndicators", heroIndicators);
            var filters = new UnityEngine.Object[5];
            var filterIndicators = new UnityEngine.Object[5];
            var names = new[] { "전체", "무기", "방어구", "장신구", "장착 중" };

            for (var i = 0; i < filters.Length; i++)
            {
                var x = 0.045f + i * 0.144f;
                var filter = Button("Filter" + i, page, x, 0.744f, x + 0.134f, 0.789f, names[i], out var title);
                title.fontSize = 23;
                filters[i] = filter;
                filterIndicators[i] = Box("Indicator", filter.transform, 0.05f, 0, 0.95f, 0.045f, i == 0 ? Gold : Card).GetComponent<Image>();
            }

            BindArray(view, "FilterButtons", filters);
            BindArray(view, "FilterIndicators", filterIndicators);
            var order = Button("Order", page, 0.78f, 0.744f, 0.955f, 0.789f, "등급순 ↓", out var orderTitle);
            orderTitle.fontSize = 22;
            Bind(view, "OrderButton", order);
            Bind(view, "OrderLabel", orderTitle);
            var gridArea = Box("Scroll", page, 0.045f, 0.335f, 0.955f, 0.727f, new Color32(14, 22, 29, 255));
            var scroll = gridArea.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            var viewport = Rect("Viewport", gridArea, 0, 0, 1, 1);
            viewport.gameObject.AddComponent<Image>().color = Color.clear;
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = Rect("Content", viewport, 0, 1, 1, 1);
            content.pivot = new Vector2(0.5f, 1);
            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(162, 176);
            grid.spacing = new Vector2(12, 12);
            grid.padding = new RectOffset(12, 12, 12, 12);
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 5;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;
            Bind(view, "Content", content);
            Bind(view, "Scroll", scroll);
            Bind(view, "Empty", Label("Empty", gridArea, 0.05f, 0.45f, 0.95f, 0.58f, "", 26, Muted));
            var detail = Box("Detail", page, 0.045f, 0.037f, 0.955f, 0.317f, Card);
            Bind(view, "DetailIcon", Picture("Icon", detail, 0.035f, 0.57f, 0.16f, 0.92f, null));
            Bind(view, "DetailTitle", Label("Name", detail, 0.19f, 0.77f, 0.965f, 0.95f, "장비를 선택하세요", 34, Gold));
            Bind(view, "DetailType", Label("Type", detail, 0.19f, 0.58f, 0.965f, 0.78f, "아이콘을 누르면 상세 정보를 확인할 수 있습니다.", 21, Muted));
            Bind(view, "DetailStats", Label("Stats", detail, 0.04f, 0.365f, 0.965f, 0.59f, "", 26, White));
            Bind(view, "Comparison", Label("Comparison", detail, 0.04f, 0.25f, 0.965f, 0.38f, "", 21, Gold));
            var equip = Button("Equip", detail, 0.04f, 0.035f, 0.36f, 0.235f, "장착", out var equipTitle);
            equip.GetComponent<Image>().color = new Color32(86, 76, 49, 255);
            Bind(view, "EquipButton", equip);
            Bind(view, "EquipLabel", equipTitle);
            Bind(view, "DiscardButton", Button("Discard", detail, 0.68f, 0.035f, 0.96f, 0.235f, "버리기", out _));
            Bind(view, "Message", Label("Message", page, 0.045f, 0.006f, 0.955f, 0.036f, "선택한 영웅의 장비와 비교합니다.", 21, Muted));
            var dialog = Box("DiscardDialog", page, 0, 0, 1, 1, new Color(0, 0, 0, 0.8f));
            dialog.GetComponent<Image>().raycastTarget = true;
            var confirm = Box("ConfirmCard", dialog, 0.075f, 0.34f, 0.925f, 0.61f, Ink);
            Bind(view, "DiscardTitle", Label("Title", confirm, 0.06f, 0.48f, 0.94f, 0.92f, "이 장비를 버릴까요?", 32, White));
            Bind(view, "ConfirmDiscard", Button("Confirm", confirm, 0.06f, 0.10f, 0.48f, 0.35f, "버리기", out _));
            Bind(view, "CancelDiscard", Button("Cancel", confirm, 0.52f, 0.10f, 0.94f, 0.35f, "취소", out _));
            Bind(view, "DiscardDialog", dialog.gameObject);
            dialog.gameObject.SetActive(false);
            var forgeOpen = Button("OpenForge", detail, 0.38f, 0.035f, 0.66f, 0.235f, "강화 / 분해", out var forgeLabel);
            forgeLabel.fontSize = 25;
            BuildForge(page, forgeOpen);

            return view;
        }

        private static void BuildForge(RectTransform page, Button openButton)
        {
            var forge = page.gameObject.AddComponent<UI_LobbyForge>();
            var panel = Box("ForgePanel", page, 0, 0, 1, 1, new Color32(19, 28, 35, 255));
            panel.GetComponent<Image>().raycastTarget = true;
            Bind(forge, "Panel", panel.gameObject);
            Bind(forge, "OpenButton", openButton);
            Label("Heading", panel, 0.045f, 0.915f, 0.7f, 0.975f, "대장간", 46, Gold);
            Bind(forge, "CloseButton", Button("Close", panel, 0.73f, 0.925f, 0.955f, 0.975f, "돌아가기", out var closeLabel));
            closeLabel.fontSize = 24;
            Bind(forge, "Wallet", Label("Wallet", panel, 0.045f, 0.855f, 0.955f, 0.905f, "", 28, White));
            var card = Box("Preview", panel, 0.045f, 0.37f, 0.955f, 0.835f, Card);
            Bind(forge, "Title", Label("Item", card, 0.05f, 0.86f, 0.95f, 0.97f, "", 36, Gold));
            Bind(forge, "Stats", Label("Stats", card, 0.05f, 0.06f, 0.95f, 0.85f, "", 28, White));
            Bind(forge, "Cost", Label("Cost", panel, 0.05f, 0.21f, 0.95f, 0.355f, "", 25, Muted));
            var upgrade = Button("Upgrade", panel, 0.045f, 0.11f, 0.405f, 0.18f, "강화 +1", out _);
            upgrade.GetComponent<Image>().color = new Color32(86, 76, 49, 255);
            Bind(forge, "UpgradeButton", upgrade);
            var lockButton = Button("Lock", panel, 0.425f, 0.11f, 0.675f, 0.18f, "장비 잠금", out var lockLabel);
            lockLabel.fontSize = 26;
            Bind(forge, "LockButton", lockButton);
            Bind(forge, "LockLabel", lockLabel);
            Bind(forge, "DismantleButton", Button("Dismantle", panel, 0.695f, 0.11f, 0.955f, 0.18f, "분해", out _));
            Bind(forge, "Message", Label("Message", panel, 0.05f, 0.025f, 0.95f, 0.095f, "", 23, Gold));
            var confirm = Box("DismantleConfirmation", panel, 0, 0, 1, 1, new Color32(0, 0, 0, 225));
            confirm.GetComponent<Image>().raycastTarget = true;
            var confirmCard = Box("Card", confirm, 0.075f, 0.30f, 0.925f, 0.70f, Card);
            var title = Label("Title", confirmCard, 0.06f, 0.37f, 0.94f, 0.94f, "", 31, White);
            title.alignment = TextAnchor.MiddleCenter;
            Bind(forge, "ConfirmationTitle", title);
            Bind(forge, "Confirmation", confirm.gameObject);
            Bind(forge, "ConfirmButton", Button("Confirm", confirmCard, 0.06f, 0.09f, 0.47f, 0.27f, "분해 확정", out _));
            Bind(forge, "CancelButton", Button("Cancel", confirmCard, 0.53f, 0.09f, 0.94f, 0.27f, "취소", out _));
            confirm.gameObject.SetActive(false);
            panel.gameObject.SetActive(false);
        }

        private static UI_LobbyCharacters BuildCharacters(Transform shell)
        {
            var page = Box("Characters", shell, 0.035f, 0.13f, 0.965f, 0.87f, Ink);
            page.GetComponent<Image>().sprite = Frame;
            page.GetComponent<Image>().type = Image.Type.Sliced;
            var view = page.gameObject.AddComponent<UI_LobbyCharacters>();
            Label("Title", page, 0.045f, 0.915f, 0.955f, 0.975f, "캐릭터", 46, White);
            Label("Subtitle", page, 0.045f, 0.87f, 0.955f, 0.915f, "장비를 바꾸면 능력치도 함께 갱신됩니다.", 25, Muted);
            var stats = new UnityEngine.Object[3];
            var equipment = new UnityEngine.Object[3];
            var roles = new[] { "선두를 지키는 탱커", "정확한 원거리 사수", "강력한 마법 시전자" };

            for (var i = 0; i < stats.Length; i++)
            {
                var y = 0.59f - i * 0.28f;
                var card = Box("Hero" + i, page, 0.045f, y, 0.955f, y + 0.255f, Card);
                Picture("Portrait", card, 0.015f, 0.15f, 0.235f, 0.95f, LobbyPresentationData.GetHeroPortrait(i));
                Label("Name", card, 0.25f, 0.76f, 0.96f, 0.97f, LobbyPresentationData.GetHeroName(i), 35, Gold);
                Label("Role", card, 0.25f, 0.61f, 0.96f, 0.78f, roles[i], 22, Muted);
                stats[i] = Label("Stats", card, 0.25f, 0.08f, 0.65f, 0.63f, "", 23, White);
                equipment[i] = Label("Equipment", card, 0.66f, 0.08f, 0.985f, 0.63f, "", 18, Muted);
            }

            BindArray(view, "Stats", stats);
            BindArray(view, "EquipmentLabels", equipment);
            Label("Footer", page, 0.045f, 0.001f, 0.955f, 0.028f, "장비 관리는 인벤토리에서 진행하세요.", 20, Muted);
            HeroGrowthBuilder.ConfigureCharacters(view);
            RuneStonesBuilder.ConfigureCharacters(view);

            return view;
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

        private static RectTransform Box(string name, Transform parent, float x0, float y0, float x1, float y1, Color color)
        {
            var rect = Rect(name, parent, x0, y0, x1, y1);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;

            return rect;
        }

        private static Text Label(string name, Transform parent, float x0, float y0, float x1, float y1,
            string text, int size, Color color)
        {
            var rect = Rect(name, parent, x0, y0, x1, y1);
            var label = rect.gameObject.AddComponent<Text>();
            label.font = Font;
            label.fontSize = size;
            label.text = text;
            label.color = color;
            label.alignment = TextAnchor.MiddleLeft;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;

            return label;
        }

        private static Image Picture(string name, Transform parent, float x0, float y0, float x1, float y1, Sprite sprite)
        {
            var rect = Rect(name, parent, x0, y0, x1, y1);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;

            return image;
        }

        private static Button Button(string name, Transform parent, float x0, float y0, float x1, float y1,
            string title, out Text label)
        {
            var rect = Box(name, parent, x0, y0, x1, y1, Card);
            var image = rect.GetComponent<Image>();
            image.raycastTarget = true;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color32(235, 216, 170, 255);
            colors.pressedColor = new Color32(192, 170, 119, 255);
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.7f);
            button.colors = colors;
            var navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
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
        }

        private static void BindArray(UnityEngine.Object target, string name, UnityEngine.Object[] values)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(name);
            property.arraySize = values.Length;

            for (var i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
