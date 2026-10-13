using System.Collections.Generic;
using System.Linq;
using ThreeSouls.Enumeration;
using ThreeSouls.Global;
using ThreeSouls.Interface;
using ThreeSouls.Utility;
using UnityEngine;

namespace ThreeSouls.Combat
{
    /// <summary>
    /// 직접 이동, 장애물 경로 요청 예산과 도달 불가 타깃의 재평가를 관리합니다.
    /// </summary>
    internal sealed class CombatNavigationHandler
    {
        private readonly CombatGroundHandler GroundHandler;
        private readonly CombatSpawnHandler SpawnHandler;
        private readonly AStarPathfinder PathFinder;
        private readonly Dictionary<ICombatStateProvider, Dictionary<GameObject, (Vector2Int Cell, float RetryTime)>> ExcludedTargets = new();
        private int PathRequestFrame = -1;
        private int PathRequestCount;

        internal CombatNavigationHandler(CombatGroundHandler ground, CombatSpawnHandler spawns)
        {
            GroundHandler = ground;
            SpawnHandler = spawns;
            PathFinder = new AStarPathfinder(ground);
        }

        internal void ClearExcludedTargets()
        {
            ExcludedTargets.Clear();
        }

        internal void RemoveEntity(ICombatStateProvider entity)
        {
            ExcludedTargets.Remove(entity);
        }

        internal void Release()
        {
            ExcludedTargets.Clear();
            PathRequestFrame = -1;
            PathRequestCount = 0;
        }

        private bool TryReservePathRequest()
        {
            if (PathRequestFrame != Time.frameCount)
            {
                PathRequestFrame = Time.frameCount;
                PathRequestCount = 0;
            }

            if (PathRequestCount >= Constants.MOVE_PATH_FRAME_BUDGET)
            {
                return false;
            }

            PathRequestCount++;

            return true;
        }

        public bool CanMoveDirectly(ICombatStateProvider entity, Vector2 destination)
        {
            return GroundHandler.CanMove(entity.GetWorldPosition(), destination, entity.GetMoveRadius(), entity.GetMoveOffset());
        }

        public MovePathResult GetMovePath(ICombatStateProvider entity, out Vector2[] path)
        {
            path = null;

            if (IsTargetAlive(entity.EnemyTarget) == false)
            {
                return MovePathResult.NotFound;
            }

            if (TryReservePathRequest() == false)
            {
                return MovePathResult.Pending;
            }

            var startPosition = entity.GetWorldPosition();
            var targetPosition = GetTargetPosition(entity.EnemyTarget);
            var startCell = GroundHandler.WorldToCell(startPosition);
            var goalCell = GroundHandler.WorldToCell(targetPosition);
            var attackDistance = entity.CharacterData.CharDistanceToTarget + Constants.ATTACK_RANGE_TOLERANCE;
            var radius = entity.GetMoveRadius();
            var offset = entity.GetMoveOffset();
            var startCenter = (Vector2)GroundHandler.CellToWorldCenter(startCell);
            var viaStartCenter = GroundHandler.CanMove(startPosition, startCenter, radius, offset);
            var nodes = PathFinder.FindPath(
                GroundHandler.GetNodeData(startCell),
                GroundHandler.GetNodeData(goalCell),
                cell =>
            {
                var position = (Vector2)GroundHandler.CellToWorldCenter(cell);

                return Vector2.SqrMagnitude(position - targetPosition) <= attackDistance * attackDistance &&
                    GroundHandler.CanMove(position, targetPosition, radius, offset) == true;
            },
                (from, to) => GroundHandler.CanMove(
                from == startCell && viaStartCenter == false ? startPosition : (Vector2)GroundHandler.CellToWorldCenter(from),
                GroundHandler.CellToWorldCenter(to),
                radius,
                offset));

            if (nodes == null)
            {
                return MovePathResult.NotFound;
            }

            var firstIndex = viaStartCenter == true ? 0 : 1;
            path = new Vector2[nodes.Count - firstIndex];

            for (var i = firstIndex; i < nodes.Count; i++)
            {
                path[i - firstIndex] = GroundHandler.CellToWorldCenter(nodes[i].CellPosition);
            }

            return MovePathResult.Found;
        }

        public bool MoveTowards(ICombatStateProvider entity, Vector2 destination, float moveSpeed)
        {
            var position = entity.GetWorldPosition();
            var nextPosition = Vector2.MoveTowards(position, destination, moveSpeed * Time.fixedDeltaTime);

            if (CanMoveDirectly(entity, nextPosition) == false)
            {
                entity.StopMove();

                return false;
            }

            entity.SetDirection(nextPosition - position);
            entity.MovePosition(nextPosition);

            return true;
        }

        public bool IsTargetExcluded(ICombatStateProvider entity, GameObject target)
        {
            return target != null &&
                ExcludedTargets.TryGetValue(entity, out var targets) == true &&
                targets.TryGetValue(target, out var excluded) == true &&
                excluded.Cell == GroundHandler.WorldToCell(GetTargetPosition(target)) &&
                Time.time < excluded.RetryTime;
        }

        public void ExcludeTarget(ICombatStateProvider entity, GameObject target)
        {
            if (target == null)
            {
                return;
            }

            if (ExcludedTargets.TryGetValue(entity, out var targets) == false)
            {
                targets = new Dictionary<GameObject, (Vector2Int Cell, float RetryTime)>();
                ExcludedTargets.Add(entity, targets);
            }

            targets[target] = (GroundHandler.WorldToCell(GetTargetPosition(target)), Time.time + 1f);
            ThreeSoulsLog.Warning($"Move path not found : {entity.CharacterData.CharacterKey} -> {target.name}");
        }

        public Vector2 GetTargetPosition(GameObject target)
        {
            return SpawnHandler.GetCharacterInstance(target).GetWorldPosition();
        }

        public bool IsTargetAlive(GameObject target)
        {
            return SpawnHandler.IsAlive(target);
        }

        public bool IsTargetInAttackRange(ICombatStateProvider attacker, GameObject target)
        {
            if (IsTargetAlive(target) == false || attacker is ThreeSouls.Character.BaseEntity entity && entity.CharHealth.IsAlive == false)
            {
                return false;
            }

            var distance = Vector2.Distance(attacker.GetWorldPosition(), GetTargetPosition(target));

            return distance <= attacker.CharacterData.CharDistanceToTarget + Constants.ATTACK_RANGE_TOLERANCE &&
                CanMoveDirectly(attacker, GetTargetPosition(target)) == true;
        }

        public MovePathResult GetDestinationPath(ICombatStateProvider entity, Vector2 destination, out Vector2[] path)
        {
            path = null;

            if (TryReservePathRequest() == false)
            {
                return MovePathResult.Pending;
            }

            var start = GroundHandler.WorldToCell(entity.GetWorldPosition());
            var goal = GroundHandler.WorldToCell(destination);
            var startCenter = (Vector2)GroundHandler.CellToWorldCenter(start);
            var viaStartCenter = GroundHandler.CanMove(entity.GetWorldPosition(), startCenter, entity.GetMoveRadius(), entity.GetMoveOffset());
            var nodes = PathFinder.FindPath(
                GroundHandler.GetNodeData(start),
                GroundHandler.GetNodeData(goal),
                null,
                (from, to) => GroundHandler.CanMove(
                    from == start && viaStartCenter == false ? entity.GetWorldPosition() : (Vector2)GroundHandler.CellToWorldCenter(from),
                    GroundHandler.CellToWorldCenter(to),
                    entity.GetMoveRadius(),
                    entity.GetMoveOffset()));

            if (nodes == null)
            {
                return MovePathResult.NotFound;
            }

            var points = nodes.Skip(viaStartCenter == true ? 0 : 1)
                .Select(node => (Vector2)GroundHandler.CellToWorldCenter(node.CellPosition))
                .ToList();
            var lastPosition = points.Count > 0 ? points[points.Count - 1] : entity.GetWorldPosition();

            if (GroundHandler.CanMove(lastPosition, destination, entity.GetMoveRadius(), entity.GetMoveOffset()) == false)
            {
                return MovePathResult.NotFound;
            }

            if (Vector2.Distance(lastPosition, destination) > 0.001f)
            {
                points.Add(destination);
            }

            path = points.ToArray();

            return MovePathResult.Found;
        }
    }
}
