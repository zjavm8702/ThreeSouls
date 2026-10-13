using System;
using System.Linq;
using ThreeSouls.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ThreeSouls.EditorTools
{
    /// <summary>기존 UI의 참조와 레이아웃을 유지하며 픽셀 아트와 짧은 연출을 적용합니다.</summary>
    public static class UiPolishBuilder
    {
        private static Sprite Panel;
        private static Sprite Button;
        private static Sprite Slot;
        private static Material Theme;
        private static readonly Color PanelTint = new Color32(159, 184, 207, 255);
        private static readonly Color CardTint = new Color32(177, 194, 218, 255);

        [MenuItem("Tools/Three Souls/UI/Polish Existing Prefabs")]
        public static void ApplyAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode == true) throw new InvalidOperationException("Edit mode only");
            var paths = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/UI", "Assets/Resources/Prefabs" })
                .Select(AssetDatabase.GUIDToAssetPath).Where(x => x.Contains("/UI_")).OrderBy(x => AssetDatabase.GetDependencies(x, true).Count(d => d.EndsWith(".prefab") && d.Contains("/UI_"))).ToArray();
            foreach (var path in paths)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    Apply(root);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("UI polish: " + paths.Length + " existing prefabs");
        }

        public static void Apply(GameObject root)
        {
            Panel = WebArt("MetalPanel");
            Button = WebArt("MetalButton");
            Theme = null;
            Slot = WebArt("SlotPanel");
            // Previously applied palette materials must also be removed on reapplication.
            foreach (var image in root.GetComponentsInChildren<UnityEngine.UI.Image>(true))
            {
                if (image.material != null && AssetDatabase.GetAssetPath(image.material).EndsWith("/UiPixelPalette.mat"))
                {
                    image.material = null;
                    Frame(image, image.GetComponent<UnityEngine.UI.Button>() != null ? Button : Panel, Color.white);
                }
            }
            foreach (var component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null) continue;
                var serialized = new SerializedObject(component);
                var property = serialized.GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference || property.objectReferenceValue is Sprite == false) continue;
                    var path = AssetDatabase.GetAssetPath(property.objectReferenceValue);
                    if (path.EndsWith("/QuestPortrait_Archive.png") == true)
                    {
                        property.objectReferenceValue = Art("QuestPortrait_Sera");
                    }
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            foreach (var graphic in root.GetComponentsInChildren<Graphic>(true))
            {
                if (graphic is TMP_Text || graphic is Text) graphic.raycastTarget = false;
            }
            foreach (var button in root.GetComponentsInChildren<UnityEngine.UI.Button>(true))
            {
                if (button.GetComponent<UIButtonFeedback>() == null) button.gameObject.AddComponent<UIButtonFeedback>();
                var image = button.targetGraphic as UnityEngine.UI.Image;
                if (image != null)
                {
                    var rect = image.rectTransform;
                    var square = rect.anchorMin == rect.anchorMax && rect.sizeDelta.x > 0 && Mathf.Abs(rect.sizeDelta.x - rect.sizeDelta.y) < 10;
                    Frame(image, square == true ? Slot : Button, CardTint);
                }
                var colors = button.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color32(255, 233, 193, 255);
                colors.pressedColor = new Color32(190, 167, 209, 255);
                colors.disabledColor = new Color32(124, 131, 146, 175);
                colors.fadeDuration = .08f;
                button.colors = colors;
            }
            foreach (var image in root.GetComponentsInChildren<UnityEngine.UI.Image>(true))
            {
                var name = image.name;
                if (image.GetComponent<UnityEngine.UI.Button>() != null || image.type == UnityEngine.UI.Image.Type.Filled) continue;
                if (name == "Dim" || name == "Viewport" || name == "Indicator" || name == "Selected" || name == "Selection" || name.Contains("Marker_") || name == "Leader") continue;
                if (image.color.a < .05f) continue;
                if (image.sprite != null && image.sprite != Panel && image.sprite != Slot && name != "UI_HUD_Top" && name != "GoldPanel" && name != "BG" && name != "Panel" && name != "VictoryBanner" && name != "ConfirmButton") continue;
                var dim = image.color.r < .05f && image.color.g < .05f && image.color.b < .05f;
                if (dim == true && image.raycastTarget == true) continue;
                if (name.Contains("Panel") || name.Contains("Card") || name.Contains("Detail") || name == "Header" || name == "Navigation" || name == "Welcome" || name == "Preview" || name == "Inventory" || name == "Characters" || name == "Supplies" || name == "SupplyBar" || name == "UI_HUD_Top" || name == "MapFrame" || name == "UIOnlyHome" || name == "SanctuaryStatus" || name == "UI_VillageMinimap")
                {
                    Frame(image, Panel, PanelTint);
                }
                else if (name == "Inset" || name == "Plate" || name == "BG")
                {
                    Frame(image, Slot, CardTint);
                }
                else if (name.StartsWith("UI_") == true && name.Contains("HUD") == false && name.Contains("Item_") == false && name.Contains("StoryQuest") == false && name.Contains("Minimap") == false)
                {
                    Frame(image, Panel, PanelTint);
                }
            }
            foreach (var view in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (view is UI_RuneShop || view is UI_RuneStones || view is UI_HeroGrowth || view is UI_EquipmentShop || view is UI_DungeonPreparation || view is UI_DungeonSelection)
                {
                    AddMotion(view.gameObject);
                    var rootImage = view.GetComponent<UnityEngine.UI.Image>();
                    if (rootImage != null && rootImage.color.a > .99f) Frame(rootImage, Panel, PanelTint);
                }
            }
            foreach (var rect in root.GetComponentsInChildren<RectTransform>(true).ToArray())
            {
                if (rect == null) continue;
                if (new[] { "Confirmation", "SaleConfirmation", "DismantleConfirmation", "DiscardDialog", "ForgePanel", "DialoguePanel", "PopupGroup", "ProgressGroup" }.Contains(rect.name)) AddMotion(rect.gameObject);
            }
            foreach (var selection in root.GetComponentsInChildren<UI_DungeonSelection>(true))
            {
                var data = new SerializedObject(selection);
                var maps = data.FindProperty("MapTextures");
                for (var i = 0; i < 5; i++) maps.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/Map/Previews/" + (i == 0 ? "NativeWorldMap" : "NativeDungeon_" + i) + ".png");
                var rows = AssetDatabase.LoadAssetAtPath<ThreeSouls.Data.Generated.DungeonMapTableDataTable>("Assets/ScriptableObjectDatas/DungeonMapTableData.asset").Items;
                var points = data.FindProperty("DungeonPoints");
                for (var i = 0; i < 4; i++)
                {
                    var button = (UnityEngine.UI.Button)points.GetArrayElementAtIndex(i).objectReferenceValue;
                    var rect = button.GetComponent<RectTransform>();
                    rect.anchorMin = rect.anchorMax = new Vector2(rows[i].WorldX, rows[i].WorldY);
                    rect.anchoredPosition = Vector2.zero;
                    button.GetComponentInChildren<TMP_Text>(true).text = rows[i].DisplayName;
                }
                var title = (TMP_Text)data.FindProperty("MapTitle").objectReferenceValue;
                title.color = new Color32(235, 191, 111, 255);
                var note = ((GameObject)data.FindProperty("WorldNote").objectReferenceValue).GetComponent<TMP_Text>();
                note.rectTransform.anchorMin = new Vector2(.03f, .43f);
                note.rectTransform.anchorMax = new Vector2(.97f, .57f);
                note.rectTransform.offsetMin = note.rectTransform.offsetMax = Vector2.zero;
                note.alignment = TextAlignmentOptions.Center;
                note.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Village/Production/StoryUI.asset");
                note.color = new Color32(178, 193, 198, 255);
                note.fontSize = 17;
                note.text = "지역별 지형 미리보기 · 지역을 골라 출전을 준비하세요";
                if (note.font.HasCharacters(note.text, out uint[] missingGlyphs, false, true) == false) throw new InvalidOperationException("Map note glyphs missing");
                EditorUtility.SetDirty(note.font);
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            var patch = root.GetComponent<UI_Popup_Patch>();
            if (patch != null)
            {
                var data = new SerializedObject(patch);
                AddMotion((GameObject)data.FindProperty("PopupGroup").objectReferenceValue);
                AddMotion((GameObject)data.FindProperty("ProgressGroup").objectReferenceValue);
            }
            DecorateShop(root);
            DecorateStory(root);
            QuestPresentationBuilder.ApplyStory(root);
            DecorateStagePreviews(root);
            DecorateLobby(root);
            DecorateHud(root);
            ApplyWebDetails(root);
            RuneUiPresentationBuilder.Apply(root);
        }

        private static void Frame(UnityEngine.UI.Image image, Sprite sprite, Color tint)
        {
            if (sprite == Panel)
            {
                var context = string.Join("/", image.GetComponentsInParent<Transform>(true).Select(x => x.name));
                var style = context.Contains("Story") || context.Contains("Dialogue") ? "StoryPanel"
                    : context.Contains("Rune") ? "RunePanel"
                    : context.Contains("Forge") || context.Contains("EquipmentShop") || context.Contains("Inventory") ? "WoodPanel"
                    : context.Contains("Dungeon") || context.Contains("Map") ? "MapPanel" : "MetalPanel";
                sprite = WebArt(style);
                tint = style == "WoodPanel" ? new Color(.40f, .43f, .48f)
                    : style == "RunePanel" ? new Color(.45f, .43f, .48f)
                    : style == "StoryPanel" ? new Color(.35f, .32f, .48f)
                    : style == "MapPanel" ? new Color(.32f, .42f, .43f) : new Color(.34f, .42f, .53f);
            }
            else if (sprite == Button || sprite == Slot)
            {
                var name = image.name;
                tint = name.Contains("Buy") || name.Contains("Start") || name.Contains("Accept") || name.Contains("Confirm") || name.Contains("Upgrade")
                    ? new Color(.40f, .65f, .44f)
                    : name.Contains("Sell") || name.Contains("Discard") || name.Contains("Cancel") ? new Color(.65f, .38f, .38f)
                    : sprite == Slot ? new Color(.40f, .48f, .60f) : new Color(.57f, .58f, .65f);
            }
            image.sprite = sprite;
            image.type = UnityEngine.UI.Image.Type.Sliced;
            image.material = Theme;
            image.pixelsPerUnitMultiplier = .5f;
            image.color = tint;
        }

        private static void AddMotion(GameObject target)
        {
            var wrapper = target.transform.Find("AnimatedContents");
            if (wrapper != null)
            {
                foreach (var child in wrapper.Cast<Transform>().ToArray()) child.SetParent(target.transform, false);
                UnityEngine.Object.DestroyImmediate(wrapper.gameObject);
            }
            var group = target.GetComponent<CanvasGroup>();
            if (group == null) group = target.AddComponent<CanvasGroup>();
            var motion = target.GetComponent<UIPanelMotion>();
            if (motion == null) motion = target.AddComponent<UIPanelMotion>();
            Bind(motion, "Content", target.transform);
            Bind(motion, "Group", group);
            var shield = Icon(target.transform, "InputShield", null, new Vector2(-.08f, -.08f), new Vector2(1.08f, 1.08f));
            shield.color = Color.clear;
            shield.raycastTarget = true;
            shield.transform.SetAsFirstSibling();
        }

        private static void DecorateShop(GameObject root)
        {
            foreach (var shop in root.GetComponentsInChildren<UI_RuneShop>(true))
            {
                var so = new SerializedObject(shop);
                var buttons = so.FindProperty("ProductButtons");
                var images = so.FindProperty("ProductImages");
                images.arraySize = buttons.arraySize;
                for (var i = 0; i < buttons.arraySize; i++)
                {
                    var button = (UnityEngine.UI.Button)buttons.GetArrayElementAtIndex(i).objectReferenceValue;
                    var icon = Icon(button.transform, "ProductArt", Load("Assets/Images/UIIcon/icon1.png"), new Vector2(.025f, .13f), new Vector2(.225f, .87f));
                    images.GetArrayElementAtIndex(i).objectReferenceValue = icon;
                    var text = button.GetComponentInChildren<TMP_Text>(true);
                    text.rectTransform.anchorMin = new Vector2(.25f, .10f);
                    text.rectTransform.anchorMax = new Vector2(.97f, .90f);
                    text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
                    text.fontSize = 22;
                    text.alignment = TextAlignmentOptions.MidlineLeft;
                }
                var runes = shop.GetComponentInChildren<UI_RuneStones>(true);
                var source = new SerializedObject(runes).FindProperty("DefinitionIcons");
                var target = so.FindProperty("DefinitionIcons");
                target.arraySize = source.arraySize;
                for (var i = 0; i < source.arraySize; i++) target.GetArrayElementAtIndex(i).objectReferenceValue = source.GetArrayElementAtIndex(i).objectReferenceValue;
                so.ApplyModifiedPropertiesWithoutUndo();
                BadgeLabel(shop.gameObject, "Title", "QuestBook", 44);
                BadgeLabel(shop.gameObject, "Wallet", "GrowthOre", 34);
            }
        }

        private static void DecorateStory(GameObject root)
        {
            foreach (var story in root.GetComponentsInChildren<UI_StoryQuest>(true))
            {
                var so = new SerializedObject(story);
                var speaker = (TMP_Text)so.FindProperty("Speaker").objectReferenceValue;
                var portrait = Icon(speaker.transform.parent, "SpeakerPortrait", Art("QuestBook"), new Vector2(.07f, .793f), new Vector2(.18f, .862f));
                speaker.rectTransform.anchorMin = new Vector2(.205f, .802f);
                Bind(story, "Portrait", portrait);
                Bind(story, "JournalIcon", Art("QuestBook"));
                var faces = new SerializedObject(story);
                var array = faces.FindProperty("NpcPortraits"); array.arraySize = 4;
                var names = new[] { "NPC_Quest_1", "NPC_Quest_2", "NPC_Quest_3", "NPC_Dungeon" };
                for (var i = 0; i < 4; i++) array.GetArrayElementAtIndex(i).objectReferenceValue =
                    AssetDatabase.LoadAllAssetsAtPath("Assets/Images/Character/NPC/" + names[i] + ".png").OfType<Sprite>().OrderBy(x => x.name).First();
                faces.ApplyModifiedPropertiesWithoutUndo();
                var tracker = (TMP_Text)so.FindProperty("Tracker").objectReferenceValue;
                Icon(tracker.transform.parent, "TrackerBook", Art("QuestScroll"), new Vector2(.025f, .60f), new Vector2(.15f, .94f));
                tracker.rectTransform.anchorMin = new Vector2(.18f, .10f);
                tracker.rectTransform.anchorMax = new Vector2(.95f, .94f);
                tracker.rectTransform.offsetMin = tracker.rectTransform.offsetMax = Vector2.zero;
                tracker.fontSize = 20;
            }
        }

        private static void DecorateLobby(GameObject root)
        {
            foreach (var button in root.GetComponentsInChildren<UnityEngine.UI.Button>(true))
            {
                var art = button.name switch
                {
                    "OpenDungeonMap" => "QuestScroll", "OpenEquipmentShop" => "EquipmentSword", "OpenForge" => "ForgeHammer", "OpenRunes" => "QuestBook",
                    "Tab0" => "QuestScroll", "Tab1" => "TreasureChest", "Tab2" => "EquipmentArmor", "Tab3" => "GrowthOre", _ => null
                };
                if (art == null) continue;
                Icon(button.transform, "ActionArt", Art(art), new Vector2(.04f, .20f), new Vector2(.20f, .80f));
                foreach (var text in button.GetComponentsInChildren<Graphic>(true).Where(x => x is Text || x is TMP_Text))
                {
                    var navigation = button.transform.parent.name == "Navigation";
                    text.rectTransform.anchorMin = new Vector2(.23f, navigation ? (text.name == "Hint" ? .08f : .40f) : .05f);
                    text.rectTransform.anchorMax = new Vector2(.97f, navigation ? (text.name == "Hint" ? .36f : .95f) : .95f);
                    text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
                }
            }
            foreach (var growth in root.GetComponentsInChildren<UI_HeroGrowth>(true)) BadgeLabel(growth.gameObject, "Title", "GrowthOre", 40);
            foreach (var runes in root.GetComponentsInChildren<UI_RuneStones>(true)) BadgeLabel(runes.gameObject, "Title", "QuestBook", 40);
            foreach (var selection in root.GetComponentsInChildren<UI_DungeonSelection>(true)) BadgeLabel(selection.gameObject, "Title", "QuestScroll", 40);
            foreach (var shop in root.GetComponentsInChildren<UI_EquipmentShop>(true)) BadgeLabel(shop.gameObject, "Title", "EquipmentSword", 40);
            foreach (var forge in root.GetComponentsInChildren<UI_LobbyForge>(true))
            {
                var panel = (GameObject)new SerializedObject(forge).FindProperty("Panel").objectReferenceValue;
                if (panel != null) BadgeLabel(panel, "Title", "ForgeHammer", 40);
            }
            var inventory = root.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(x => x.name == "Inventory");
            if (inventory != null)
            {
                var badge = inventory.Find("PolishBadge_Title");
                if (badge != null) badge.GetComponent<UnityEngine.UI.Image>().sprite = Art("TreasureChest");
            }
            foreach (var image in root.GetComponentsInChildren<UnityEngine.UI.Image>(true))
            {
                if (image.name == "UI_LobbyItem" || image.name == "UI_EquipmentShopItem") Frame(image, Slot, CardTint);
            }
        }

        private static void DecorateHud(GameObject root)
        {
            if (root.name == "UI_HUD_Supplies")
            {
                for (var i = 0; i < 2; i++)
                {
                    var button = root.GetComponentsInChildren<UnityEngine.UI.Button>(true).First(x => x.name == "Slot" + i);
                    Icon(button.transform, "PotionArt", AssetDatabase.LoadAllAssetsAtPath("Assets/Images/UIIcon/Potions.png").OfType<Sprite>().ElementAt(i), new Vector2(.04f, .15f), new Vector2(.23f, .85f));
                    var label = button.GetComponentInChildren<TMP_Text>(true);
                    if (label != null) label.rectTransform.anchorMin = new Vector2(.25f, .05f);
                }
            }
            if (root.name == "UI_Popup_Result")
            {
                var banner = root.GetComponentsInChildren<UnityEngine.UI.Image>(true).First(x => x.name == "VictoryBanner");
                Frame(banner, Load("Assets/Images/UIIMage/20251117darkDwellersHeaderE.png"), Color.white);
                var notice = root.GetComponentsInChildren<Text>(true).FirstOrDefault(x => x.name == "RewardNotice");
                if (notice != null) Icon(notice.transform.parent, "RewardChest", Art("TreasureChest"), new Vector2(.83f, .77f), new Vector2(.92f, .84f));
            }
        }

        private static void BadgeLabel(GameObject root, string labelName, string art, float size)
        {
            var text = root.GetComponentsInChildren<Graphic>(true).FirstOrDefault(x => x.name == labelName && (x is Text || x is TMP_Text));
            if (text == null) return;
            var rect = text.rectTransform;
            if (rect.parent.Find("PolishBadge_" + labelName) != null) return;
            var icon = Icon(rect.parent, "PolishBadge_" + labelName, Art(art), rect.anchorMin, rect.anchorMin);
            icon.rectTransform.anchorMin = new Vector2(rect.anchorMin.x, (rect.anchorMin.y + rect.anchorMax.y) * .5f);
            icon.rectTransform.anchorMax = icon.rectTransform.anchorMin;
            icon.rectTransform.pivot = new Vector2(0, .5f);
            icon.rectTransform.sizeDelta = new Vector2(size, size);
            rect.offsetMin += new Vector2(size + 10, 0);
        }

        private static UnityEngine.UI.Image Icon(Transform parent, string name, Sprite sprite, Vector2 min, Vector2 max)
        {
            var found = parent.Find(name);
            var image = found == null ? new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image)).GetComponent<UnityEngine.UI.Image>() : found.GetComponent<UnityEngine.UI.Image>();
            image.gameObject.layer = LayerMask.NameToLayer("UI");
            image.transform.SetParent(parent, false);
            image.rectTransform.anchorMin = min;
            image.rectTransform.anchorMax = max;
            image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero;
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        private static Sprite Art(string name)
        {
            foreach (var folder in new[] { "UIIcon/Actions", "UIIcon/Equipment", "UIIcon/Skills", "Character/NPC/Portraits", "UIIMage" })
            {
                var path = "Assets/Images/" + folder + "/" + name + ".png";
                if (AssetDatabase.LoadAssetAtPath<Sprite>(path) != null) return Load(path);
            }
            throw new InvalidOperationException("Missing UI art: " + name);
        }
        private static Sprite WebArt(string name) => Load("Assets/Images/UIIMage/WebUI/" + name + ".png");

        private static void ApplyWebDetails(GameObject root)
        {
            foreach (var image in root.GetComponentsInChildren<UnityEngine.UI.Image>(true))
            {
                if (image.name.StartsWith("PolishBadge_") && image.transform.parent.Find(image.name.Substring("PolishBadge_".Length)) is Transform label)
                    image.transform.SetSiblingIndex(label.GetSiblingIndex() + 1);
                if (image.name == "PotionArt") image.sprite = WebArt(image.transform.parent.name == "Slot0" ? "HealthPotion" : "SupplyPotion");
                if (image.name != "ActionArt" && image.name != "PolishBadge_Title" && image.name != "RewardChest") continue;
                var context = image.transform.parent.name;
                if (context == "Tab1" || image.name == "RewardChest") image.sprite = WebArt("InventoryChest");
                else if (context == "Tab2") image.sprite = WebArt("EquipmentShield");
                else if (context == "Tab3") image.sprite = WebArt("SupplyPotion");
                else if (image.GetComponentsInParent<Transform>(true).Any(x => x.name.Contains("Forge"))) image.sprite = WebArt("ForgeHammer");
            }
            foreach (var title in root.GetComponentsInChildren<TMP_Text>(true).Where(x => x.name == "Title"))
            {
                if (title.GetComponentsInParent<UI_StoryQuest>(true).Length == 0)
                {
                    var previous = title.transform.Find("WebTitleDivider");
                    if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
                    continue;
                }
                var divider = Icon(title.transform, "WebTitleDivider", WebArt("TitleDivider"), new Vector2(0, 0), new Vector2(.8f, 0));
                divider.rectTransform.pivot = new Vector2(.5f, 1);
                divider.rectTransform.sizeDelta = new Vector2(0, 8);
                divider.rectTransform.anchoredPosition = new Vector2(0, -3);
                divider.color = new Color32(208, 168, 103, 190);
                divider.preserveAspect = false;
            }
        }
        private static void DecorateStagePreviews(GameObject root)
        {
            var textures = Enumerable.Range(0, 20).Select(i => AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Images/Map/StagePreviews/Stage_" + (i / 5 + 1) + "_" + (i % 5 + 1) + ".png")).ToArray();
            if (textures.Any(x => x == null)) return;
            foreach (var map in root.GetComponentsInChildren<UI_DungeonSelection>(true))
            {
                var data = new SerializedObject(map);
                var images = data.FindProperty("StageTextures"); images.arraySize = 20;
                for (var i = 0; i < 20; i++) images.GetArrayElementAtIndex(i).objectReferenceValue = textures[i];
                var buttons = data.FindProperty("ListButtons");
                var previews = data.FindProperty("ListPreviews"); previews.arraySize = buttons.arraySize;
                for (var i = 0; i < buttons.arraySize; i++)
                {
                    var button = (UnityEngine.UI.Button)buttons.GetArrayElementAtIndex(i).objectReferenceValue;
                    var holder = button.transform.Find("StagePreviewFrame") as RectTransform;
                    if (holder == null)
                    {
                        holder = new GameObject("StagePreviewFrame", typeof(RectTransform)).GetComponent<RectTransform>();
                        holder.SetParent(button.transform, false);
                    }
                    holder.anchorMin = new Vector2(.025f, .10f); holder.anchorMax = new Vector2(.22f, .90f);
                    holder.offsetMin = holder.offsetMax = Vector2.zero;
                    var child = holder.Find("StagePreview");
                    if (child == null)
                    {
                        child = new GameObject("StagePreview", typeof(RectTransform), typeof(UnityEngine.UI.Image)).transform;
                        child.SetParent(holder, false);
                    }
                    var image = child.GetComponent<UnityEngine.UI.Image>(); image.raycastTarget = false;
                    image.sprite = textures[Math.Min(i, 3) * 5];
                    var aspect = child.GetComponent<UnityEngine.UI.AspectRatioFitter>();
                    if (aspect == null) aspect = child.gameObject.AddComponent<UnityEngine.UI.AspectRatioFitter>();
                    aspect.aspectMode = UnityEngine.UI.AspectRatioFitter.AspectMode.FitInParent; aspect.aspectRatio = 16f / 9f;
                    previews.GetArrayElementAtIndex(i).objectReferenceValue = image;
                    var label = button.GetComponentInChildren<TMP_Text>();
                    label.rectTransform.anchorMin = new Vector2(.245f, .04f); label.rectTransform.anchorMax = new Vector2(.98f, .96f);
                    label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero; label.fontSize = 24;
                }
                data.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static Sprite Load(string path) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault() ?? throw new InvalidOperationException("Missing sprite: " + path);
        private static void Bind(UnityEngine.Object view, string name, UnityEngine.Object value)
        {
            var so = new SerializedObject(view);
            so.FindProperty(name).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}





