using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ThreeSouls.Village;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace ThreeSouls.EditorTools
{
    /// <summary>
    /// 별도 2D 물리 씬에서 실제 마을 바닥과 부대의 우회·추종·표현 계약을 확인합니다.
    /// </summary>
    public static class VillagePartyVerification
    {
        private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [MenuItem("Three Souls/Village/Verify Hero Party (Edit Only)")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode == true)
            {
                throw new InvalidOperationException("Edit 검사만 실행할 수 있습니다.");
            }

            var original = SceneManager.GetActiveScene();
            var dirty = original.isDirty;
            var roots = original.GetRootGameObjects();
            var source = roots.Single(root => root.name == "VillageParty");
            var scene = EditorSceneManager.NewPreviewScene();
            var checks = new List<string>();

            void Check(bool result, string name)
            {
                if (result == false)
                {
                    throw new InvalidOperationException("Village party check failed: " + name);
                }

                checks.Add(name);
            }

            try
            {
                Check(scene.GetPhysicsScene2D().Equals(original.GetPhysicsScene2D()) == false,
                    "Preview owns a separate Physics2D scene");
                var sourceGrid = roots.SelectMany(root => root.GetComponentsInChildren<Grid>(true)).Single();
                var terrain = UnityEngine.Object.Instantiate(sourceGrid.gameObject);
                SceneManager.MoveGameObjectToScene(terrain, scene);
                var colliders = roots.SelectMany(root => root.GetComponentsInChildren<BoxCollider2D>(true))
                    .Where(collider => collider.isTrigger == false && collider.GetComponentInParent<Grid>() == null).ToArray();

                for (var i = 0; i < colliders.Length; i++)
                {
                    var copy = new GameObject("Obstacle " + i);
                    SceneManager.MoveGameObjectToScene(copy, scene);
                    copy.transform.position = colliders[i].transform.position;
                    copy.transform.rotation = colliders[i].transform.rotation;
                    copy.transform.localScale = colliders[i].transform.lossyScale;
                    EditorUtility.CopySerialized(colliders[i], copy.AddComponent<BoxCollider2D>());
                }

                var fixture = new GameObject("PartyFixture");
                SceneManager.MoveGameObjectToScene(fixture, scene);
                var navigation = fixture.AddComponent<VillageNavigationGrid>();
                EditorUtility.CopySerialized(source.GetComponent<VillageNavigationGrid>(), navigation);
                var sourceData = new SerializedObject(source.GetComponent<VillagePartyController>());
                var members = new VillagePartyMember[3];

                for (var i = 0; i < members.Length; i++)
                {
                    var member = (VillagePartyMember)sourceData.FindProperty("Members").GetArrayElementAtIndex(i).objectReferenceValue;
                    var copy = UnityEngine.Object.Instantiate(member.gameObject);
                    SceneManager.MoveGameObjectToScene(copy, scene);
                    members[i] = copy.GetComponent<VillagePartyMember>();
                    Check(copy.GetComponent<VillageAmbientMotion>() == null && copy.GetComponent<VillageAnimationPhase>() == null,
                        "Independent ambient motion removed " + i);
                }

                var navData = new SerializedObject(navigation);
                var maps = terrain.GetComponentsInChildren<Tilemap>(true);
                var floorData = navData.FindProperty("GroundMaps");
                floorData.GetArrayElementAtIndex(0).objectReferenceValue = maps.Single(map => map.name == "Grass");
                floorData.GetArrayElementAtIndex(1).objectReferenceValue = maps.Single(map => map.name == "Paths and Plaza");
                navData.FindProperty("BridgeMap").objectReferenceValue = maps.Single(map => map.name == "Cliffs and Bridge");
                navData.ApplyModifiedPropertiesWithoutUndo();
                var party = fixture.AddComponent<VillagePartyController>();
                EditorUtility.CopySerialized(source.GetComponent<VillagePartyController>(), party);
                var partyData = new SerializedObject(party);
                partyData.FindProperty("Navigation").objectReferenceValue = navigation;

                for (var i = 0; i < members.Length; i++)
                {
                    partyData.FindProperty("Members").GetArrayElementAtIndex(i).objectReferenceValue = members[i];
                }

                partyData.ApplyModifiedPropertiesWithoutUndo();
                party.Initialize();
                Check(navigation.TryFindRoute(members[0].GetWorldPosition(), new Vector2(-20, -6), out var detour) == true && detour.Count > 2,
                    "Market obstruction produces an A* detour");

                for (var i = 1; i < detour.Count; i++)
                {
                    Check(navigation.CanTraverse(detour[i - 1], detour[i]), "Body-safe detour segment " + i);
                }

                Check(party.TryMoveTo(new Vector2(22, -4)) == false, "Water destination rejected");
                Check(party.TryMoveTo(new Vector2(-13, -5)) == false, "Inside-building destination rejected");
                Check(party.TryMoveTo(new Vector2(100, 100)) == false, "Outside-floor destination rejected");
                var destinations = new[] { new Vector2(-20, -6), new Vector2(-20, 4),
                    new Vector2(12, 4), new Vector2(12, -10), new Vector2(0, -22), new Vector2(-2, -3) };
                var spacing = ThreeSouls.Data.LobbyTables.GetVillageRules().Spacing;

                for (var routeIndex = 0; routeIndex < destinations.Length; routeIndex++)
                {
                    Check(party.TryMoveTo(destinations[routeIndex]) == true, "Destination accepted " + routeIndex);
                    var previous = new Vector2[3];

                    for (var tick = 0; tick < 1600; tick++)
                    {
                        for (var i = 0; i < members.Length; i++)
                        {
                            previous[i] = members[i].GetWorldPosition();
                        }

                        Call(party, "Advance", 0.02f);

                        for (var i = 0; i < members.Length; i++)
                        {
                            if (navigation.CanTraverse(previous[i], members[i].GetWorldPosition()) == false)
                            {
                                throw new InvalidOperationException("Unsafe member movement: route=" + routeIndex + ", tick=" + tick + ", member=" + i);
                            }

                            if (i > 0 && Vector2.Distance(members[i].GetWorldPosition(), (Vector2)Call(party, "SampleTrail", spacing * i)) > 0.002f)
                            {
                                throw new InvalidOperationException("Follower left leader history.");
                            }
                        }

                        if (Read(party, "Route") == null)
                        {
                            break;
                        }
                    }

                    Check(Vector2.Distance(members[0].GetWorldPosition(), destinations[routeIndex]) < 0.02f,
                        "Leader reaches route destination " + routeIndex);
                    Check(((List<Vector2>)Read(party, "Trail")).Count < 130, "Movement history remains bounded " + routeIndex);

                    for (var i = 0; i < members.Length; i++)
                    {
                        Check(members[i].GetComponent<Animator>().GetBool("Walking") == false, "Member idles on arrival " + routeIndex + "/" + i);
                    }
                }

                Check(party.TryMoveTo(new Vector2(10, 0)) == true, "New movement starts");
                Call(party, "Advance", 0.2f);
                Check(party.TryMoveTo(new Vector2(-5, -10)) == true, "Moving destination can be replaced");
                Call(party, "OnDisable");
                Check(Read(party, "Route") == null && members.All(member => member.GetComponent<Animator>().GetBool("Walking") == false),
                    "Disabling party cancels route and walking");
                var heroNames = new[] { "Knight", "Archer", "Wizard" };
                var states = new[] { "Right_Idle", "Down_Idle", "Up_Idle", "Right_Walking", "Down_Walking", "Up_Walking" };

                for (var i = 0; i < members.Length; i++)
                {
                    for (var j = 0; j < states.Length; j++)
                    {
                        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Character/" + states[j] + ".anim");
                        clip.SampleAnimation(members[i].gameObject, 0);
                        members[i].GetComponentInChildren<UnityEngine.U2D.Animation.SpriteResolver>().ResolveSpriteToSpriteRenderer();
                        var first = members[i].transform.Find("Body").GetComponent<SpriteRenderer>().sprite;
                        var binding = AnimationUtility.GetCurveBindings(clip)[0];
                        var nextFrame = AnimationUtility.GetEditorCurve(clip, binding).keys[1].time;
                        clip.SampleAnimation(members[i].gameObject, nextFrame + 0.001f);
                        members[i].GetComponentInChildren<UnityEngine.U2D.Animation.SpriteResolver>().ResolveSpriteToSpriteRenderer();
                        Check(first != members[i].transform.Find("Body").GetComponent<SpriteRenderer>().sprite,
                            "Directional clip frame changes " + heroNames[i] + "/" + states[j]);
                    }
                }

                var camera = roots.SelectMany(root => root.GetComponentsInChildren<CinemachineCamera>(true)).Single();
                var knight = (VillagePartyMember)sourceData.FindProperty("Members").GetArrayElementAtIndex(0).objectReferenceValue;
                Check(camera.Target.TrackingTarget == knight.transform, "Cinemachine tracks the Knight");
                var driver = roots.Select(root => root.GetComponent<T_Camera>()).Single(item => item != null);
                Check(new SerializedObject(driver).FindProperty("FollowTargets").GetArrayElementAtIndex(0).objectReferenceValue == knight.gameObject,
                    "Existing intro also returns to Knight");
                Check(original.isDirty == dirty && EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android,
                    "Original scene dirty state and Android target retained");
                var animatorOwner = new GameObject("ShutdownAnimatorFixture");
                SceneManager.MoveGameObjectToScene(animatorOwner, scene);
                var retiringAnimator = animatorOwner.AddComponent<Animator>();
                typeof(VillagePartyMember).GetField("Animator", System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic).SetValue(members[0], retiringAnimator);
                UnityEngine.Object.DestroyImmediate(animatorOwner);
                typeof(VillagePartyController).GetMethod("OnDisable", System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic).Invoke(party, null);
                Check(true, "Party shutdown tolerates a member Animator already destroyed");
                Directory.CreateDirectory("Docs/Previews/VillageParty");
                File.WriteAllText("Docs/Previews/VillageParty/EditChecks.txt",
                    "Edit mode; separate Physics2D scene; manually advanced party and animation clips\nPassed=" + checks.Count + "\n" + string.Join("\n", checks));
                Debug.Log("Village party Edit checks passed: " + checks.Count);
            }
            finally
            {
                SceneManager.SetActiveScene(original);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static object Read(object target, string name)
        {
            return target.GetType().GetField(name, Private).GetValue(target);
        }

        private static object Call(object target, string name, params object[] arguments)
        {
            return target.GetType().GetMethod(name, Private).Invoke(target, arguments);
        }
    }
}
