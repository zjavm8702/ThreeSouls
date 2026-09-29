using System;
using System.Collections;
using System.Collections.Generic;
using ThreeSouls.Character;
using ThreeSouls.Enumeration;
using ThreeSouls.Interface;
using UnityEngine;

namespace ThreeSouls.Combat
{
    /// <summary>
    /// 웨이브 전환 시 영웅을 리더 뒤의 일렬 대형으로 이동시키고 도착 상태를 추적합니다.
    /// </summary>
    internal sealed class CombatFormationHandler
    {
        private readonly HashSet<BaseEntity> MovingHeroes = new();
        private readonly List<BaseEntity> OrderedHeroes = new();
        private readonly HashSet<Vector2Int> ReservedCells = new();

        public void Begin(IReadOnlyList<BaseEntity> heroes, BaseEntity leader,
            IReadOnlyList<BaseEntity> monsters,
            Func<Vector3, Vector2Int> worldToCell,
            Func<BaseEntity, Vector2Int, Vector2[]> createPath,
            Func<Vector2Int, bool> isWalkable)
        {
            Clear();
            OrderedHeroes.Add(leader);

            for (var i = 0; i < heroes.Count; i++)
            {
                if (heroes[i] != leader)
                {
                    OrderedHeroes.Add(heroes[i]);
                }
            }

            var targetMonster = FindClosestMonster(leader, monsters);
            var monsterCell = worldToCell(targetMonster.CurrentWorldPosition);
            var previewPath = createPath(leader, monsterCell);
            var travelVector = targetMonster.CurrentWorldPosition - leader.CurrentWorldPosition;

            if (previewPath != null && previewPath.Length > 0)
            {
                travelVector = previewPath[0] - leader.CurrentWorldPosition;
            }

            var travelDirection = GetCardinalDirection(travelVector);
            var leaderCell = worldToCell(leader.CurrentWorldPosition);
            var trailingDirection = -travelDirection;

            for (var i = 0; i < OrderedHeroes.Count; i++)
            {
                var hero = OrderedHeroes[i];
                var targetCell = FindFormationCell(
                    leaderCell,
                    trailingDirection,
                    i,
                    isWalkable);
                var path = createPath(hero, targetCell);

                hero.EnemyTarget = null;
                hero.CurrentDestinations = path ?? Array.Empty<Vector2>();
                MovingHeroes.Add(hero);
                hero.ChangeState(CombatState.MoveToDestination);
            }
        }

        public IEnumerator StartFollowing(float startDelay)
        {
            for (var i = 0; i < OrderedHeroes.Count; i++)
            {
                var hero = OrderedHeroes[i];

                if (hero.CharHealth.IsAlive == false)
                {
                    continue;
                }

                hero.ChangeState(CombatState.DetectTarget);

                if (i + 1 < OrderedHeroes.Count)
                {
                    yield return new WaitForSeconds(startDelay);
                }
            }
        }

        public bool TryMarkArrived(ICombatStateProvider entity, out bool allArrived)
        {
            allArrived = false;

            var hero = entity as BaseEntity;

            if (hero == null || MovingHeroes.Remove(hero) == false)
            {
                return false;
            }

            allArrived = MovingHeroes.Count == 0;
            return true;
        }

        public void Cancel()
        {
            MovingHeroes.Clear();
        }

        public void Clear()
        {
            MovingHeroes.Clear();
            OrderedHeroes.Clear();
            ReservedCells.Clear();
        }

        private Vector2Int FindFormationCell(
            Vector2Int leaderCell,
            Vector2Int trailingDirection,
            int order,
            Func<Vector2Int, bool> isWalkable)
        {
            var preferredCell = leaderCell + trailingDirection * order;

            if (TryReserve(preferredCell, isWalkable))
            {
                return preferredCell;
            }

            for (var offset = 1; offset <= Global.Constants.ENCOUNTER_CELL_SEARCH_RADIUS; offset++)
            {
                var candidate = preferredCell + trailingDirection * offset;

                if (TryReserve(candidate, isWalkable))
                {
                    return candidate;
                }
            }

            ReservedCells.Add(leaderCell);
            return leaderCell;
        }

        private bool TryReserve(Vector2Int cell, Func<Vector2Int, bool> isWalkable)
        {
            if (ReservedCells.Contains(cell) || isWalkable(cell) == false)
            {
                return false;
            }

            ReservedCells.Add(cell);
            return true;
        }

        private static BaseEntity FindClosestMonster(
            BaseEntity leader,
            IReadOnlyList<BaseEntity> monsters)
        {
            var closest = monsters[0];
            var closestDistance = Vector2.SqrMagnitude(
                closest.CurrentWorldPosition - leader.CurrentWorldPosition);

            for (var i = 1; i < monsters.Count; i++)
            {
                var distance = Vector2.SqrMagnitude(
                    monsters[i].CurrentWorldPosition - leader.CurrentWorldPosition);

                if (distance < closestDistance)
                {
                    closest = monsters[i];
                    closestDistance = distance;
                }
            }

            return closest;
        }

        private static Vector2Int GetCardinalDirection(Vector2 direction)
        {
            if (Mathf.Abs(direction.x) >= Mathf.Abs(direction.y))
            {
                return direction.x >= 0 ? Vector2Int.right : Vector2Int.left;
            }

            return direction.y >= 0 ? Vector2Int.up : Vector2Int.down;
        }
    }
}
