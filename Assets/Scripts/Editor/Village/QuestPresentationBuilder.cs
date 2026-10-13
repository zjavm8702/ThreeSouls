using System;
using System.Linq;
using ThreeSouls.Village;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ThreeSouls.EditorTools
{
    /// <summary>Reapply the rune merchant appearance and licensed quest status art.</summary>
    public static class QuestPresentationBuilder
    {
        public const string ArtFolder = "Assets/Images/UIIcon/Quests/";

        public static Sprite Art(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(ArtFolder + name + ".png");

        public static void ApplyWorld(Scene scene)
        {
            var targets = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<VillageNpcTarget>(true));
            foreach (var target in targets)
            {
                if (target.GetNpcKey() == "RuneShop") ApplyMerchant(target);
                if (!target.GetNpcKey().StartsWith("Quest_")) continue;
                var data = new SerializedObject(target);
                data.FindProperty("AvailableIcon").objectReferenceValue = Art("QuestAvailable");
                data.FindProperty("ActiveIcon").objectReferenceValue = Art("QuestActive");
                data.FindProperty("CompleteIcon").objectReferenceValue = Art("QuestComplete");
                var marker = data.FindProperty("QuestMarker").objectReferenceValue as SpriteRenderer;
                data.ApplyModifiedPropertiesWithoutUndo();
                if (marker != null)
                {
                    marker.sprite = Art("QuestAvailable");
                    marker.color = new Color32(255, 210, 85, 255);
                    marker.transform.localScale = Vector3.one * (.65f / Mathf.Max(marker.sprite.bounds.size.x, marker.sprite.bounds.size.y));
                    // Status markers must remain readable in shadowed parts of the village.
                    marker.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
                }
            }
        }

        private static void ApplyMerchant(VillageNpcTarget target)
        {
            const string folder = "Assets/Village/Production/NPCAnimations/";
            const string clipPath = folder + "NPC_Rune_Quest4_Idle.anim";
            const string controllerPath = folder + "NPC_Rune_Quest4.controller";
            var sprites = AssetDatabase.LoadAllAssetsAtPath("Assets/Images/Character/NPC/NPC_Quest_4.png").OfType<Sprite>()
                .OrderByDescending(x => x.rect.y).ThenBy(x => x.rect.x).Take(2).ToArray();
            if (sprites.Length != 2) throw new InvalidOperationException("NPC_Quest_4 idle sprites missing");
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null) { clip = new AnimationClip { name = "NPC_Rune_Quest4_Idle", frameRate = 2 }; AssetDatabase.CreateAsset(clip, clipPath); }
            AnimationUtility.SetObjectReferenceCurve(clip, new EditorCurveBinding { path = "", type = typeof(SpriteRenderer), propertyName = "m_Sprite" },
                new[] { new ObjectReferenceKeyframe { time = 0, value = sprites[0] }, new ObjectReferenceKeyframe { time = .5f, value = sprites[1] }, new ObjectReferenceKeyframe { time = 1, value = sprites[0] } });
            var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = true; AnimationUtility.SetAnimationClipSettings(clip, settings);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                var state = controller.layers[0].stateMachine.AddState("Idle"); state.motion = clip;
                controller.layers[0].stateMachine.defaultState = state;
            }
            var body = target.transform.Find("Body");
            body.GetComponent<SpriteRenderer>().sprite = sprites[0];
            body.GetComponent<Animator>().runtimeAnimatorController = controller;
            EditorUtility.SetDirty(clip);
        }

        public static void ApplyStory(GameObject root)
        {
            foreach (var story in root.GetComponentsInChildren<ThreeSouls.UI.UI_StoryQuest>(true))
            {
                var data = new SerializedObject(story);
                data.FindProperty("JournalIcon").objectReferenceValue = Art("QuestJournal");
                data.ApplyModifiedPropertiesWithoutUndo();
                var tracker = story.GetComponentsInChildren<UnityEngine.UI.Image>(true).FirstOrDefault(x => x.name == "TrackerBook");
                if (tracker != null) { tracker.sprite = Art("QuestJournal"); tracker.color = new Color32(255, 210, 85, 255); tracker.preserveAspect = true; }
            }
        }
    }
}
