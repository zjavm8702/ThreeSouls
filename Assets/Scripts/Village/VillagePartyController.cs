using System;
using System.Collections.Generic;
using UnityEngine;

namespace ThreeSouls.Village
{
    /// <summary>
    /// 기사 선두의 경로와 이동 기록을 소유하고 후열이 같은 경로를 간격대로 따르게 합니다.
    /// </summary>
    public sealed class VillagePartyController : MonoBehaviour
    {
        [SerializeField] private VillageNavigationGrid Navigation;
        [SerializeField] private VillagePartyMember[] Members;
        private float MoveSpeed;
        private float Spacing;
        private readonly List<Vector2> Trail = new();
        private List<Vector2> Route;
        private int RouteIndex;

        private void Awake()
        {
            for (var i = 0; i < Members.Length; i++)
            {
                var hero = ThreeSouls.UI.LobbyPresentationData.GetHero(i);
                Members[i].GetComponent<Animator>().runtimeAnimatorController =
                    ThreeSouls.UI.LobbyResourceCatalog.GetAnimator(hero.VillageAnimatorPath);
            }
            Initialize();
        }

        public void Initialize()
        {
            var rule = ThreeSouls.Data.LobbyTables.GetVillageRules();
            MoveSpeed = rule.MoveSpeed;
            Spacing = rule.Spacing;
            if (Navigation == null || Members == null || Members.Length != 3 || MoveSpeed <= 0 || Spacing <= 0)
            {
                throw new InvalidOperationException("마을 부대의 이동 Grid와 기사/궁수/마법사를 순서대로 연결해야 합니다.");
            }

            Navigation.Initialize();
            Trail.Clear();

            for (var i = 0; i < Members.Length; i++)
            {
                if (Members[i] == null || Navigation.IsPositionWalkable(Members[i].GetWorldPosition()) == false)
                {
                    throw new InvalidOperationException("마을 영웅은 통행 가능한 시작 위치에 있어야 합니다.");
                }

                Members[i].Initialize();
                Trail.Add(Members[i].GetWorldPosition());

                if (i > 0 && Navigation.CanTraverse(Trail[i - 1], Trail[i]) == false)
                {
                    throw new InvalidOperationException("마을 부대의 시작 대열 사이에 장애물이 있습니다.");
                }
            }

            Route = null;
            RouteIndex = 0;
        }

        public bool TryMoveTo(Vector2 destination)
        {
            if (Navigation.TryFindRoute(Members[0].GetWorldPosition(), destination, out var route) == false)
            {
                return false;
            }

            Route = route;
            RouteIndex = 1;

            return true;
        }

        public Vector2 GetLeaderPosition()
        {
            return Members[0].GetWorldPosition();
        }

        public bool IsMoving()
        {
            return Route != null;
        }

        public void StopMovement()
        {
            Stop();
        }

        private void Update()
        {
            var before = Members[0].GetWorldPosition();
            Advance(Time.deltaTime);
            if (Vector2.Distance(before, Members[0].GetWorldPosition()) > .001f)
            {
                ThreeSouls.Audio.GameAudio.Step();
            }
        }

        private void Advance(float deltaTime)
        {
            if (Route == null)
            {
                return;
            }

            var budget = MoveSpeed * deltaTime;

            while (budget > 0.00001f && RouteIndex < Route.Count)
            {
                var start = Members[0].GetWorldPosition();
                var distance = Vector2.Distance(start, Route[RouteIndex]);

                if (distance < 0.0001f)
                {
                    RouteIndex++;
                    continue;
                }

                var step = Mathf.Min(budget, Mathf.Min(distance, 0.06f));
                var next = Vector2.MoveTowards(start, Route[RouteIndex], step);

                if (Navigation.CanTraverse(start, next) == false)
                {
                    Stop();

                    return;
                }

                Members[0].MoveTo(next);
                Trail.Insert(0, next);
                UpdateFollowers();
                TrimTrail();
                budget -= step;
            }

            if (RouteIndex >= Route.Count)
            {
                Stop();
            }
        }

        private void UpdateFollowers()
        {
            for (var i = 1; i < Members.Length; i++)
            {
                var destination = SampleTrail(i * Spacing);

                if (Navigation.CanTraverse(Members[i].GetWorldPosition(), destination) == false)
                {
                    throw new InvalidOperationException("마을 후열의 이동 기록이 통행 가능한 경로에서 벗어났습니다.");
                }

                Members[i].MoveTo(destination);
            }
        }

        private Vector2 SampleTrail(float distance)
        {
            for (var i = 1; i < Trail.Count; i++)
            {
                var length = Vector2.Distance(Trail[i - 1], Trail[i]);

                if (distance <= length)
                {
                    return Vector2.Lerp(Trail[i - 1], Trail[i], distance / length);
                }

                distance -= length;
            }

            return Trail[Trail.Count - 1];
        }

        private void TrimTrail()
        {
            var distance = 0f;

            for (var i = 1; i < Trail.Count; i++)
            {
                distance += Vector2.Distance(Trail[i - 1], Trail[i]);

                if (distance > Spacing * (Members.Length - 1) + 0.15f)
                {
                    Trail.RemoveRange(i + 1, Trail.Count - i - 1);
                    break;
                }
            }
        }

        private void Stop()
        {
            Route = null;

            for (var i = 0; i < Members.Length; i++)
            {
                Members[i].Stop();
            }
        }

        private void OnDisable()
        {
            Route = null;
            if (Trail.Count > 0)
            {
                for (var i = 0; i < Members.Length; i++)
                {
                    if (Members[i] != null)
                    {
                        Members[i].StopOnDisable();
                    }
                }
            }
        }
    }
}
