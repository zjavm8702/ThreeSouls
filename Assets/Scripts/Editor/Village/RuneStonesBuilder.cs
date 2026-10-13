using System;
using System.Linq;
using ThreeSouls.Data;
using ThreeSouls.Data.Generated;
using ThreeSouls.Enumeration.AddressableKey;
using ThreeSouls.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ThreeSouls.EditorTools
{
    /// <summary>
    /// 기존 캐릭터 탭 안에 룬 프리팹을 연결하고 전투 HUD에 성소 효과 표시를 추가합니다.
    /// </summary>
    public static class RuneStonesBuilder
    {
        private const string PrefabPath = "Assets/Prefabs/UI/Lobby/UI_RuneStones.prefab";
        private const string FontPath = "Assets/Fonts/RuneUI.asset";
        private static readonly Color Ink = new Color32(19, 28, 35, 255);
        private static readonly Color Card = new Color32(32, 44, 53, 255);
        private static readonly Color Gold = new Color32(226, 183, 105, 255);
        private static readonly Color White = new Color32(237, 236, 218, 255);
        private static readonly Color Muted = new Color32(151, 169, 176, 255);
        private static TMP_FontAsset Font;

        [MenuItem("Three Souls/Village/Connect Rune Stones")]
        public static void Connect()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode == true || scene.path != VillageLobbyUIBuilder.ScenePath)
            {
                throw new InvalidOperationException("테스트 Village를 Edit 모드에서 열어 연결하세요.");
            }

            RuneRulesValidation();
            var root = PrefabUtility.LoadPrefabContents(VillageLobbyUIBuilder.PrefabPath);
            try
            {
                ConfigureCharacters(root.GetComponentInChildren<UI_LobbyCharacters>(true));
                UiPolishBuilder.Apply(root);
                PrefabUtility.SaveAsPrefabAsset(root, VillageLobbyUIBuilder.PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            var view = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<UI_LobbyCharacters>(true)).Single();
            ConfigureCharacters(view);
            ConnectCombatHUD();
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
        }

        private static void RuneRulesValidation()
        {
            ThreeSouls.Runes.RuneRules.Validate();
        }

        public static void ConfigureCharacters(UI_LobbyCharacters view)
        {
            if (view == null)
            {
                throw new InvalidOperationException("기존 캐릭터 탭이 필요합니다.");
            }

            PrepareFont();
            var panel = view.GetComponentInChildren<UI_RuneStones>(true);
            if (panel == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) ?? BuildPrefab();
                panel = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, view.transform)).GetComponent<UI_RuneStones>();
            }

            panel.gameObject.SetActive(false);
            Bind(view, "RunePanel", panel);
            if (view.transform.Find("OpenRunes") == null)
            {
                var button = Button("OpenRunes", view.transform, 0.74f, 0.91f, 0.96f, 0.955f, "룬석 관리");
                UnityEventTools.AddPersistentListener(button.onClick, view.OpenRunes);
            }

            var subtitle = view.transform.Find("Subtitle").GetComponent<UnityEngine.UI.Text>();
            subtitle.rectTransform.anchorMax = new Vector2(0.72f, subtitle.rectTransform.anchorMax.y);
            subtitle.fontSize = 22;
            Record(subtitle);
            Record(subtitle.rectTransform);
        }

        private static void PrepareFont()
        {
            Font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (Font != null)
            {
                return;
            }

            var source = AssetDatabase.LoadAssetAtPath<UnityEngine.Font>("Assets/Fonts/Mulmaru.ttf");
            Font = TMP_FontAsset.CreateFontAsset(source, 42, 6, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,
                1024, 1024, AtlasPopulationMode.Dynamic, true);
            Font.name = "RuneUI";
            Font.material.name = "RuneUI Material";
            AssetDatabase.CreateAsset(Font, FontPath);
            AssetDatabase.AddObjectToAsset(Font.material, Font);
            var textures = Font.atlasTextures;
            for (var i = 0; i < textures.Length; i++)
            {
                textures[i].name = "RuneUI Atlas " + i;
                AssetDatabase.AddObjectToAsset(textures[i], Font);
            }

            EditorUtility.SetDirty(Font);
            AssetDatabase.SaveAssets();
        }

        private static GameObject BuildPrefab()
        {
            var root = Box("UI_RuneStones", null, 0, 0, 1, 1, Ink, true);
            var view = root.gameObject.AddComponent<UI_RuneStones>();
            Label("Title", root, 0.04f, 0.939f, 0.75f, 0.985f, "룬석 · 부대의 힘을 새기다", 38, Gold);
            Bind(view, "CloseButton", Button("Close", root, 0.79f, 0.945f, 0.96f, 0.985f, "돌아가기"));
            Bind(view, "Wallet", Label("Wallet", root, 0.04f, 0.892f, 0.96f, 0.934f, "", 27, White));
            var categories = new UnityEngine.Object[]
            {
                Button("Sanctuary", root, 0.04f, 0.819f, 0.49f, 0.881f, "성소 룬석"),
                Button("Skills", root, 0.51f, 0.819f, 0.96f, 0.881f, "스킬 룬석")
            };
            BindArray(view, "CategoryButtons", categories);
            var heroes = new UnityEngine.Object[3];
            for (var i = 0; i < heroes.Length; i++)
            {
                var x = 0.04f + i * 0.313f;
                heroes[i] = Button("Hero" + i, root, x, 0.761f, x + 0.293f, 0.808f, LobbyPresentationData.GetHeroName(i));
            }

            BindArray(view, "HeroButtons", heroes);
            Bind(view, "Loadout", Label("Loadout", root, 0.04f, 0.674f, 0.96f, 0.752f, "", 25, Gold));
            var buttons = new UnityEngine.Object[8];
            var labels = new UnityEngine.Object[8];
            var images = new UnityEngine.Object[8];
            for (var i = 0; i < 8; i++)
            {
                var x = i % 2 == 0 ? 0.04f : 0.51f;
                var y = 0.620f - (i / 2) * 0.047f;
                var button = Button("Rune" + i, root, x, y, x + 0.45f, y + 0.044f, "");
                var label = button.GetComponentInChildren<TMP_Text>();
                label.rectTransform.anchorMin = new Vector2(0.25f, 0.06f);
                label.fontSize = 18;
                label.alignment = TextAlignmentOptions.Left;
                labels[i] = label;
                buttons[i] = button;
                var image = Box("Icon", button.transform, 0.035f, 0.12f, 0.22f, 0.88f, Color.white).GetComponent<UnityEngine.UI.Image>();
                image.preserveAspect = true;
                images[i] = image;
            }

            BindArray(view, "RuneButtons", buttons);
            BindArray(view, "RuneLabels", labels);
            BindArray(view, "RuneImages", images);
            var definitions = LobbyTables.Get<RuneTableDataTable>().Items;
            var icons = new UnityEngine.Object[definitions.Length];
            var skills = LobbyTables.Get<SkillTableDataTable>().Items;
            for (var i = 0; i < icons.Length; i++)
            {
                var row = definitions[i];
                if (row.Category == ThreeSouls.Runes.RuneCategory.Sanctuary)
                {
                    var name = row.Effect switch
                    {
                        ThreeSouls.Runes.RuneEffect.EquipmentDrop => "Sword",
                        ThreeSouls.Runes.RuneEffect.Gold => "Gold",
                        ThreeSouls.Runes.RuneEffect.Gems => "Ring",
                        _ => "Armor"
                    };
                    icons[i] = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Images/UIIcon/Icon_" + name + ".png");
                }
                else
                {
                    var skill = skills.Single(x => x.Key == row.SkillKey);
                    var address = GetSkillIconAddress(skill);
                    icons[i] = AssetDatabase.LoadAssetAtPath<Sprite>(address);
                }

                if (icons[i] == null)
                {
                    throw new InvalidOperationException("룬 표시 아이콘 누락: " + row.Key);
                }
            }

            BindArray(view, "DefinitionIcons", icons);
            for (var i = 0; i < buttons.Length; i++)
            {
                var visible = i < definitions.Length && definitions[i].Category == ThreeSouls.Runes.RuneCategory.Sanctuary;
                ((UnityEngine.UI.Button)buttons[i]).gameObject.SetActive(visible);
                ((UnityEngine.UI.Image)images[i]).sprite = visible ? (Sprite)icons[i] : null;
                ((UnityEngine.UI.Image)images[i]).enabled = visible;
            }
            var detail = Box("DetailCard", root, 0.04f, 0.191f, 0.96f, 0.459f, Card);
            Bind(view, "Detail", Label("Detail", detail, 0.035f, 0.035f, 0.965f, 0.965f, "", 23, White));
            Bind(view, "EquipButton", Button("Equip", root, 0.04f, 0.102f, 0.40f, 0.173f, "장착 / 교체"));
            Bind(view, "EquipLabel", root.Find("Equip").GetComponentInChildren<TMP_Text>());
            Bind(view, "PurchaseButton", Button("Purchase", root, 0.42f, 0.102f, 0.96f, 0.173f, "강화 +1"));
            Bind(view, "PurchaseLabel", root.Find("Purchase").GetComponentInChildren<TMP_Text>());
            Bind(view, "Message", Label("Message", root, 0.04f, 0.015f, 0.96f, 0.09f, "", 22, Muted));
            var confirmation = Box("Confirmation", root, 0, 0, 1, 1, new Color(0, 0, 0, 0.88f), true);
            var card = Box("Card", confirmation, 0.065f, 0.22f, 0.935f, 0.78f, Ink);
            Bind(view, "ConfirmationText", Label("Text", card, 0.055f, 0.25f, 0.945f, 0.94f, "", 29, White));
            Bind(view, "ConfirmButton", Button("Confirm", card, 0.055f, 0.07f, 0.47f, 0.20f, "확정"));
            Bind(view, "CancelButton", Button("Cancel", card, 0.53f, 0.07f, 0.945f, 0.20f, "취소"));
            Bind(view, "Confirmation", confirmation.gameObject);
            confirmation.gameObject.SetActive(false);
            root.gameObject.SetActive(false);
            UiPolishBuilder.Apply(root.gameObject);
            RuneUiPresentationBuilder.Apply(root.gameObject);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject, PrefabPath);
            UnityEngine.Object.DestroyImmediate(root.gameObject);
            return prefab;
        }

        private static string GetSkillIconAddress(SkillTableDataRow skill)
        {
            return Enum.Parse<Images>(skill.SkillIconKey).ToAddress();
        }

        private static void ConnectCombatHUD()
        {
            const string path = "Assets/Prefabs/UI/UI_HUD_Botttom.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var status = root.GetComponentInChildren<UI_HUD_Sanctuary>(true);
                if (status == null)
                {
                    var rect = Box("SanctuaryStatus", root.transform, 0.04f, 0, 0.96f, 0, Ink);
                    status = rect.gameObject.AddComponent<UI_HUD_Sanctuary>();
                    Bind(status, "Status", Label("Status", rect, 0.02f, 0.05f, 0.98f, 0.95f, "성소 효과", 23, Gold));
                }

                var statusRect = (RectTransform)status.transform;
                statusRect.anchorMin = new Vector2(0.04f, 0);
                statusRect.anchorMax = new Vector2(0.96f, 0);
                statusRect.pivot = new Vector2(0.5f, 0);
                statusRect.offsetMin = Vector2.zero;
                statusRect.offsetMax = Vector2.zero;
                statusRect.sizeDelta = new Vector2(0, 88);
                statusRect.anchoredPosition = new Vector2(0, 310);

                Bind(root.GetComponent<UI_HUD_Bottom>(), "SanctuaryStatus", status);
                UiPolishBuilder.Apply(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static RectTransform Box(string name, Transform parent, float x0, float y0, float x1, float y1, Color color, bool raycast = false)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = LayerMask.NameToLayer("UI");
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(x0, y0);
            rect.anchorMax = new Vector2(x1, y1);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = color;
            image.raycastTarget = raycast;
            return rect;
        }

        private static TMP_Text Label(string name, Transform parent, float x0, float y0, float x1, float y1, string text, int size, Color color)
        {
            var rect = Box(name, parent, x0, y0, x1, y1, Color.clear);
            UnityEngine.Object.DestroyImmediate(rect.GetComponent<UnityEngine.UI.Image>());
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = Font;
            label.fontSize = size;
            label.text = text;
            label.color = color;
            label.alignment = TextAlignmentOptions.TopLeft;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.Normal;
            return label;
        }

        private static UnityEngine.UI.Button Button(string name, Transform parent, float x0, float y0, float x1, float y1, string text)
        {
            var rect = Box(name, parent, x0, y0, x1, y1, Card, true);
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = rect.GetComponent<UnityEngine.UI.Image>();
            var navigation = button.navigation;
            navigation.mode = UnityEngine.UI.Navigation.Mode.None;
            button.navigation = navigation;
            var label = Label("Label", rect, 0.035f, 0.04f, 0.965f, 0.96f, text, 25, White);
            label.alignment = TextAlignmentOptions.Center;
            return button;
        }

        private static void Bind(UnityEngine.Object target, string name, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(name).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Record(target);
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
            Record(target);
        }

        private static void Record(UnityEngine.Object target)
        {
            if (PrefabUtility.IsPartOfPrefabInstance(target) == true)
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            }
        }
    }
}
