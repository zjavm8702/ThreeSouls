using System;
using System.Collections.Generic;
using System.Linq;
using ThreeSouls.Character;
using ThreeSouls.Enumeration;
using ThreeSouls.Interface;
using UnityEngine;

namespace ThreeSouls.Combat
{
    /// <summary>
    /// 생존 부대의 리더, 대열 경로와 캐릭터별 목적지를 관리합니다.
    /// </summary>
    internal sealed class CombatFormationHandler
    {
        public delegate void OnLeaderChangeDelegate(BaseEntity leader);

        private OnLeaderChangeDelegate onLeaderChangeEventHandler = null;
        public event OnLeaderChangeDelegate OnLeaderChangeEventHandler
        {
            add
            {
                onLeaderChangeEventHandler -= value;
                onLeaderChangeEventHandler += value;
            }
            remove
            {
                onLeaderChangeEventHandler -= value;
            }
        }

        private static readonly Vector2Int[] FormationDirections =
        {
            Vector2Int.up,
            Vector2Int.down,
            Vector2Int.left,
            Vector2Int.right
        };

        private readonly CombatSpawnHandler SpawnHandler;
        private readonly CombatGroundHandler GroundHandler;
        private readonly AStarPathfinder PathFinder;
        private readonly Dictionary<ICombatStateProvider, Vector2> HeroDestinations = new();
        private readonly List<BaseEntity> FormationHeroes = new();
        private IReadOnlyList<Vector2Int> SpawnCells;
        private float NavigationRadius;
        private List<Vector2> FormationRoute = new();
        private float FormationRouteLength;
        private float FormationProgress;
        private const float FormationArrivalDistance = 0.08f;
        
        internal BaseEntity Leader { get; private set; }

        internal CombatFormationHandler(CombatSpawnHandler spawns, CombatGroundHandler ground)
        {
            SpawnHandler = spawns;
            GroundHandler = ground;
            PathFinder = new AStarPathfinder(ground);
        }

        internal void Initialize(IReadOnlyList<Vector2Int> cells, float radius)
        {
            Clear();
            SpawnCells = cells;
            NavigationRadius = radius;
            SetLeader(SelectLeader(SpawnHandler.GetAliveHeroes()));
        }

        internal void SetLeader(BaseEntity leader)
        {
            if (Leader == leader)
            {
                return;
            }

            Leader = leader;
            onLeaderChangeEventHandler?.Invoke(leader);
        }

        internal bool NeedsRebuild()
        {
            return Leader == null || Leader.CharHealth.IsAlive == false ||
                FormationHeroes.Count > 0 && FormationHeroes.Count != SpawnHandler.GetAliveHeroes().Count;
        }

        internal bool IsTravelCompleted()
        {
            return FormationProgress >= FormationRouteLength;
        }

        internal void BeginRally(Vector3 center, Vector3 nextHolyPlace, string roundKey)
        {
            BuildFormationRoute(center, nextHolyPlace, roundKey);
            FormationProgress = (FormationHeroes.Count - 1) * GetFormationSpacing();
            UpdateFormationDestinations();
            MoveHeroesToFormation();
        }

        internal void BeginArrival(Vector3 center, Vector3 nextHolyPlace, string roundKey)
        {
            BuildFormationRoute(center, nextHolyPlace, roundKey);
            FormationProgress = FormationRouteLength;
            UpdateFormationDestinations();
            MoveHeroesToFormation();
        }

        internal bool IsAwayFromDestinations(Vector2 position, float squaredDistance)
        {
            using var destinations = HeroDestinations.Values.GetEnumerator();

            while (destinations.MoveNext() == true)
            {
                if (Vector2.SqrMagnitude(destinations.Current - position) <= squaredDistance)
                {
                    return false;
                }
            }

            return true;
        }

        internal void ClearDestinations()
        {
            HeroDestinations.Clear();
        }

        internal void ClearMembers()
        {
            FormationHeroes.Clear();
        }

        internal void Clear()
        {
            HeroDestinations.Clear();
            FormationHeroes.Clear();
            FormationRoute.Clear();
            FormationRouteLength = 0;
            FormationProgress = 0;
        }

        internal void Release()
        {
            Clear();
            Leader = null;
            SpawnCells = null;
        }

        internal List<Vector2Int> GetFormationCells(Vector3 position, int count)
        {
            var center = GroundHandler.WorldToCell(position);
            var cells = new List<Vector2Int>();

            for (var distance = 1; distance <= 16 && cells.Count < count; distance++)
            {
                for (var directionIndex = 0; directionIndex < FormationDirections.Length; directionIndex++)
                {
                    var direction = FormationDirections[directionIndex];
                    var cell = center + direction * distance;

                    if (IsConnectedCell(cell) == true)
                    {
                        cells.Add(cell);
                    }
                }
            }

            if (cells.Count < count)
            {
                throw new InvalidOperationException($"성소 주변 정렬 공간 부족 : {position}");
            }

            return cells;
        }

        private void BuildFormationRoute(Vector3 center, Vector3 nextHolyPlace, string roundKey)
        {
            FormationHeroes.Clear();
            FormationHeroes.AddRange(SpawnHandler.GetAliveHeroes()
                .OrderBy(hero => hero == Leader ? -1 : GetLeaderRolePriority(hero.CharacterData.CharacterRole)));
           
            var span = (FormationHeroes.Count - 1) * GetFormationSpacing();
            var goalCell = GetFormationCells(center, FormationHeroes.Count)
                .OrderBy(cell => Vector2.SqrMagnitude((Vector2)GroundHandler.CellToWorldCenter(cell) - (Vector2)nextHolyPlace))
                .First();
           
            var goal = (Vector2)GroundHandler.CellToWorldCenter(goalCell);
            // 현재 부대 가까이에 대열 전체를 놓을 수 있는 출발 경로를 선택한다.
          
            var rallyCenter = GetFormationCenter();
            var orderedCells = SpawnCells.OrderBy(cell => Vector2.SqrMagnitude((Vector2)GroundHandler.CellToWorldCenter(cell) - rallyCenter))
                .ToArray();

            for (var cellIndex = 0; cellIndex < orderedCells.Length; cellIndex++)
            {
                var startCell = orderedCells[cellIndex];
                var start = (Vector2)GroundHandler.CellToWorldCenter(startCell);

                if (Vector2.Distance(start, goal) < span + 0.1f)
                {
                    continue;
                }

                var nodes = PathFinder.FindPath(
                    GroundHandler.GetNodeData(startCell),
                    GroundHandler.GetNodeData(goalCell),
                    null,
                    (from, to) => GroundHandler.CanMove(
                        GroundHandler.CellToWorldCenter(from),
                        GroundHandler.CellToWorldCenter(to),
                        NavigationRadius,
                        Vector2.zero));

                if (nodes == null)
                {
                    continue;
                }

                FormationRoute = nodes.Select(node => (Vector2)GroundHandler.CellToWorldCenter(node.CellPosition)).ToList();
                FormationRouteLength = 0;

                for (var i = 1; i < FormationRoute.Count; i++)
                {
                    FormationRouteLength += Vector2.Distance(FormationRoute[i - 1], FormationRoute[i]);
                }

                if (FormationRouteLength >= span)
                {
                    return;
                }
            }

            throw new InvalidOperationException($"탱커 선두 대열의 이동 경로를 찾을 수 없습니다 : {roundKey}");
        }

        private Vector2 GetFormationPosition(float distance)
        {
            distance = Mathf.Clamp(distance, 0, FormationRouteLength);

            for (var i = 1; i < FormationRoute.Count; i++)
            {
                var length = Vector2.Distance(FormationRoute[i - 1], FormationRoute[i]);

                if (distance <= length)
                {
                    return Vector2.Lerp(FormationRoute[i - 1], FormationRoute[i], distance / length);
                }

                distance -= length;
            }

            return FormationRoute[FormationRoute.Count - 1];
        }

        private void UpdateFormationDestinations()
        {
            HeroDestinations.Clear();

            for (var i = 0; i < FormationHeroes.Count; i++)
            {
                HeroDestinations.Add(FormationHeroes[i], GetFormationPosition(FormationProgress - i * GetFormationSpacing()));
            }
        }

        private bool IsConnectedCell(Vector2Int cell)
        {
            return SpawnCells.Contains(cell);
        }

        private float GetFormationSpacing()
        {
            return Mathf.Max(0.6f, NavigationRadius * 2f + 0.2f);
        }

        private Vector2 GetFormationCenter()
        {
            var center = Vector2.zero;

            for (var i = 0; i < FormationHeroes.Count; i++)
            {
                center += FormationHeroes[i].GetWorldPosition();
            }

            return center / FormationHeroes.Count;
        }

        internal void UpdateTravel()
        {
            var speed = float.MaxValue;

            for (var i = 0; i < FormationHeroes.Count; i++)
            {
                var hero = FormationHeroes[i];

                if (Vector2.Distance(hero.GetWorldPosition(), HeroDestinations[hero]) > 0.35f)
                {
                    return;
                }

                speed = Mathf.Min(speed, hero.CharacterData.CharMoveSpeed);
            }

            FormationProgress = Mathf.Min(FormationRouteLength, FormationProgress + speed * Time.deltaTime);
            UpdateFormationDestinations();
        }

        private void MoveHeroesToFormation()
        {
            var heroes = SpawnHandler.GetAliveHeroes();

            for (var i = 0; i < heroes.Count; i++)
            {
                heroes[i].SetEnemyTarget(null);
                heroes[i].ChangeState(CombatState.MoveToDestination);
            }
        }

        internal void WaitHeroes()
        {
            var heroes = SpawnHandler.GetAliveHeroes();

            for (var i = 0; i < heroes.Count; i++)
            {
                heroes[i].ChangeState(CombatState.Wait);
            }
        }

        internal bool AreHeroesAtDestinations()
        {
            var heroes = SpawnHandler.GetAliveHeroes();

            for (var i = 0; i < heroes.Count; i++)
            {
                if (HeroDestinations.TryGetValue(heroes[i], out var destination) == false ||
                    Vector2.Distance(heroes[i].GetWorldPosition(), destination) > FormationArrivalDistance)
                {
                    return false;
                }
            }

            return true;
        }

        internal bool TryGetDestination(ICombatStateProvider entity, out Vector2 destination)
        {
            return HeroDestinations.TryGetValue(entity, out destination);
        }

        internal static BaseEntity SelectLeader(IReadOnlyList<BaseEntity> heroes)
        {
            return heroes.OrderBy(hero => GetLeaderRolePriority(hero.CharacterData.CharacterRole))
                .ThenBy(hero => hero.CharacterData.CharDistanceToTarget)
                .FirstOrDefault();
        }

        private static int GetLeaderRolePriority(CharacterRole role)
        {
            switch (role)
            {
                case CharacterRole.Tank:
                    return 0;
                case CharacterRole.MeleeDealer:
                    return 1;
                case CharacterRole.RangedDealer:
                    return 2;
                default:
                    return int.MaxValue;
            }
        }
    }
}
