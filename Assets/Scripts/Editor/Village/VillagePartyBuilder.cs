using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ThreeSouls.UI;
using ThreeSouls.Village;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine.U2D.Animation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace ThreeSouls.EditorTools
{
    /// <summary>
    /// 열린 테스트 마을의 세 주민을 기사 선두 부대와 입력 및 기존 추적 카메라에 연결합니다.
    /// </summary>
    public static class VillagePartyBuilder
    {
        [MenuItem("Three Souls/Village/Connect Three Hero Party")]
        public static void Build()
        {
            var scene = SceneManager.GetActiveScene();

            if (EditorApplication.isPlayingOrWillChangePlaymode == true || scene.path != VillageLobbyUIBuilder.ScenePath)
            {
                throw new InvalidOperationException("테스트 마을을 Edit 모드로 열어 연결하세요.");
            }

            var transforms = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
            var names = new[] { "Knight", "Archer", "Wizard" };
            var actors = new VillagePartyMember[3];

            for (var i = 0; i < names.Length; i++)
            {
                var actor = transforms.Single(item => item.name.StartsWith(names[i] + " ", StringComparison.Ordinal));
                Remove<VillageAmbientMotion>(actor.gameObject);
                Remove<VillageAnimationPhase>(actor.gameObject);
                ConfigureActor(actor.gameObject, i);
                actor.position = new Vector3(-2, -3 - i * 1.15f, 0);
                actor.localScale = Vector3.one;
                actors[i] = GetOrAdd<VillagePartyMember>(actor.gameObject);
            }

            var partyObject = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "VillageParty");

            if (partyObject == null)
            {
                partyObject = new GameObject("VillageParty");
            }

            var grid = GetOrAdd<VillageNavigationGrid>(partyObject);
            var party = GetOrAdd<VillagePartyController>(partyObject);
            var input = GetOrAdd<VillageMoveInput>(partyObject);
            var maps = transforms.Select(item => item.GetComponent<Tilemap>()).Where(item => item != null).ToArray();
            var serialized = new SerializedObject(grid);
            var floors = serialized.FindProperty("GroundMaps");
            floors.arraySize = 2;
            floors.GetArrayElementAtIndex(0).objectReferenceValue = maps.Single(map => map.name == "Grass");
            floors.GetArrayElementAtIndex(1).objectReferenceValue = maps.Single(map => map.name == "Paths and Plaza");
            serialized.FindProperty("BridgeMap").objectReferenceValue = maps.Single(map => map.name == "Cliffs and Bridge");
            serialized.FindProperty("BridgeTile").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TileBase>("Assets/Tiles/Planks.asset");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            serialized = new SerializedObject(party);
            serialized.FindProperty("Navigation").objectReferenceValue = grid;
            var members = serialized.FindProperty("Members");
            members.arraySize = actors.Length;

            for (var i = 0; i < actors.Length; i++)
            {
                members.GetArrayElementAtIndex(i).objectReferenceValue = actors[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            var tabs = transforms.Select(item => item.GetComponent<UI_LobbyTabs>()).Single(item => item != null);
            serialized = new SerializedObject(input);
            serialized.FindProperty("WorldCamera").objectReferenceValue = transforms.Select(item => item.GetComponent<Camera>()).Single(item => item != null);
            serialized.FindProperty("Party").objectReferenceValue = party;
            serialized.FindProperty("Tabs").objectReferenceValue = tabs;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            tabs.SelectTab(ThreeSouls.Data.LobbyTables.GetRules().InitialTab);
            VillageLobbyUIBuilder.ConfigureVillageGuide(tabs);
            var welcome = transforms.Single(item => item.name == "Eyebrow").GetComponent<UnityEngine.UI.Text>();
            welcome.text = "마을 바닥을 눌러 세 영웅을 이동하세요";
            var blocks = new[] { "Header", "Welcome", "Navigation" };

            for (var i = 0; i < blocks.Length; i++)
            {
                var image = transforms.Single(item => item.name == blocks[i]).GetComponent<UnityEngine.UI.Image>();
                image.raycastTarget = true;
                PrefabUtility.RecordPrefabInstancePropertyModifications(image);
            }

            var camera = transforms.Select(item => item.GetComponent<CinemachineCamera>()).Single(item => item != null);
            camera.Target = new CameraTarget { TrackingTarget = actors[0].transform };
            var driver = scene.GetRootGameObjects().Select(root => root.GetComponent<T_Camera>()).Single(item => item != null);
            serialized = new SerializedObject(driver);
            var targets = serialized.FindProperty("FollowTargets");
            targets.arraySize = 1;
            targets.GetArrayElementAtIndex(0).objectReferenceValue = actors[0].gameObject;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var cameraBounds = transforms.Single(item => item.name == "VillageCameraBounds").GetComponent<PolygonCollider2D>();
            cameraBounds.SetPath(0, new[]
            {
                new Vector2(-42, -32), new Vector2(-42, 32),
                new Vector2(42, 32), new Vector2(42, -32)
            });
            SplitBridgeBoundary(transforms);
            PrefabUtility.RecordPrefabInstancePropertyModifications(tabs);
            PrefabUtility.RecordPrefabInstancePropertyModifications(welcome);
            EditorSceneManager.MarkSceneDirty(scene);
            VillageEquipmentShopBuilder.Connect();
            Debug.Log("Village party connected: Knight -> Archer -> Wizard; existing camera follows Knight.");
        }

        private static void SplitBridgeBoundary(Transform[] transforms)
        {
            var boundary = transforms.FirstOrDefault(item => item.name == "Water Boundary - South");

            if (boundary == null)
            {
                return;
            }

            var old = boundary.GetComponent<BoxCollider2D>();

            if (old != null)
            {
                UnityEngine.Object.DestroyImmediate(old);
            }

            var names = new[] { "Left Bank", "Right Bank" };

            for (var i = 0; i < names.Length; i++)
            {
                var bank = boundary.Find(names[i]);

                if (bank == null)
                {
                    bank = new GameObject(names[i]).transform;
                    bank.SetParent(boundary, false);
                }

                bank.localPosition = new Vector3(i == 0 ? -12 : 12, 0, 0);
                GetOrAdd<BoxCollider2D>(bank.gameObject).size = new Vector2(20, 2);
            }
        }

        internal static void ConfigureActor(GameObject actor, int index)
        {
            var hero = LobbyPresentationData.GetHero(index);
            var animator = actor.GetComponent<Animator>();
            animator.runtimeAnimatorController = LobbyResourceCatalog.GetAnimator(hero.VillageAnimatorPath);
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var body = actor.transform.Find("Body");

            if (body == null)
            {
                body = new GameObject("Body").transform;
                body.SetParent(actor.transform, false);
            }

            var renderer = GetOrAdd<SpriteRenderer>(body.gameObject);
            var previous = actor.GetComponent<SpriteRenderer>();

            if (previous != null)
            {
                EditorUtility.CopySerialized(previous, renderer);
                UnityEngine.Object.DestroyImmediate(previous);
            }

            var libraryName = Path.GetFileNameWithoutExtension(hero.PortraitImagePath);
            var library = GetOrAdd<SpriteLibrary>(body.gameObject);
            library.spriteLibraryAsset = AssetDatabase.LoadAssetAtPath<SpriteLibraryAsset>(
                "Assets/SpriteLibrarys/Character/" + libraryName + ".spriteLib")
                ?? throw new InvalidOperationException("마을 영웅 SpriteLibrary가 필요합니다: " + libraryName);
            var resolver = GetOrAdd<SpriteResolver>(body.gameObject);
            resolver.SetCategoryAndLabel("Down_Idle", "Idle_Down_0");
            resolver.ResolveSpriteToSpriteRenderer();
            renderer.flipX = false;
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            var component = target.GetComponent<T>();

            return component == null ? target.AddComponent<T>() : component;
        }

        private static void Remove<T>(GameObject target) where T : Component
        {
            var component = target.GetComponent<T>();

            if (component != null)
            {
                UnityEngine.Object.DestroyImmediate(component);
            }
        }
    }
}
