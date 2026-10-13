using System;
using System.Collections.Generic;
using System.Linq;
using ThreeSouls.Character;
using ThreeSouls.Data;
using ThreeSouls.Data.Generated;
using ThreeSouls.Enumeration;
using ThreeSouls.Global;
using ThreeSouls.Interface;
using ThreeSouls.Manager;
using ThreeSouls.Utility;
using UnityEngine;

namespace ThreeSouls.Combat
{
    /// <summary>
    /// 필드와 성소의 스폰 및 라운드 단계 전환을 관리합니다. 대열과 경로 처리는 각 담당자에 위임합니다.
    /// </summary>
    internal sealed class CombatRoundHandler
    {
        private readonly CombatSanctuaryHandler Sanctuary;
        private readonly CombatSpawnHandler SpawnHandler;
        private readonly CombatGroundHandler GroundHandler;
        private readonly CombatNavigationHandler NavigationHandler;
        private readonly CombatFormationHandler FormationHandler;
        private readonly ICombatWorld CombatWorld;
        private GroundTableDataRow StageGroundData;
        private ThreeSoulsRuleDataRow RoundRuleData;
        private HolyPlaceTableDataRow[] StageRounds = Array.Empty<HolyPlaceTableDataRow>();
        private Transform CharacterParent;
        private Vector3 HeroSpawnPoint;
        private Vector3[] HolyPlacePoints = Array.Empty<Vector3>();
        private List<Vector2Int> SpawnCells = new();
        private int CurrentRoundIndex = -1;
        private int RegionNumber;
        private int StageNumber;
        private float FormationReadyTime;
        private const float FormationWaitTime = 1f;

        internal CombatRoundPhase RoundPhase { get; private set; } = CombatRoundPhase.Preparing;

        internal CombatRoundHandler(CombatSpawnHandler spawns, CombatGroundHandler ground,
            CombatNavigationHandler navigation, CombatFormationHandler formation, ICombatWorld world,
            CombatSanctuaryHandler sanctuary)
        {
            SpawnHandler = spawns;
            GroundHandler = ground;
            NavigationHandler = navigation;
            FormationHandler = formation;
            CombatWorld = world;
            Sanctuary = sanctuary ?? throw new ArgumentNullException(nameof(sanctuary));
        }

        private int GetCurrentRoundNumber()
        {
            return CurrentRoundIndex + 1;
        }

        private string GetCurrentRoundKey()
        {
            return CurrentRoundIndex >= 0 ? StageGroundData.RoundKeys[CurrentRoundIndex] : null;
        }

        private int GetRemainingMonsterCount()
        {
            return SpawnHandler.GetAliveMonsters().Count;
        }

        internal void Release()
        {
            FormationHandler.Release();
            StageRounds = Array.Empty<HolyPlaceTableDataRow>();
            HolyPlacePoints = Array.Empty<Vector3>();
            SpawnCells.Clear();
            StageGroundData = null;
            RoundRuleData = null;
            CharacterParent = null;
            CurrentRoundIndex = -1;
            RoundPhase = CombatRoundPhase.Preparing;
        }

        internal void Initialize(GroundTableDataRow ground, ThreeSoulsRuleDataRow rule, Transform parent, Vector3 heroSpawnPoint, Vector3[] holyPlacePoints, int regionNumber, int stageNumber)
        {
            CharacterParent = parent;
            HeroSpawnPoint = heroSpawnPoint;
            HolyPlacePoints = holyPlacePoints;
            RegionNumber = regionNumber;
            StageNumber = stageNumber;
            StageGroundData = ground;
            RoundRuleData = rule;

            if (ground.RoundKeys == null ||
                ground.RoundKeys.Length == 0 ||
                ground.RoundKeys.Length != ground.Monsters.Length ||
                ground.RoundKeys.Length != ground.SpawnCounts.Length ||
                ground.RoundKeys.Length > HolyPlacePoints.Length)
            {
                throw new InvalidOperationException("GroundTable의 RoundKeys, 필드 몬스터, 수량 및 MonsterSpawn 지점 개수를 확인하세요.");
            }

            StageRounds = ground.RoundKeys.Select(key => TableManager.Get().GetHolyPlaceTableData(key) ?? throw new InvalidOperationException($"HolyPlaceTable 라운드 누락 : {key}"))
                .ToArray();

            for (var i = 0; i < StageRounds.Length; i++)
            {
                var round = StageRounds[i];

                if (round.RoundNumber != i + 1 ||
                    round.Monsters == null ||
                    round.SpawnCounts == null ||
                    round.Monsters.Length != round.SpawnCounts.Length ||
                    round.SpawnCounts.Any(count => count < 0) == true ||
                    ground.SpawnCounts[i] < 0)
                {
                    throw new InvalidOperationException($"HolyPlaceTable 라운드 순서 또는 몬스터 수량 오류 : {round.RoundKey}");
                }

                for (var keyIndex = 0; keyIndex <= round.Monsters.Length; keyIndex++)
                {
                    var key = keyIndex < round.Monsters.Length ? round.Monsters[keyIndex] : ground.Monsters[i];
                    var character = CampaignBalance.ScaleMonster(TableManager.Get().GetCharacterTableDataTable(key), RegionNumber, StageNumber);

                    if (character == null || character.CharacterType != CharacterType.Monster)
                    {
                        throw new InvalidOperationException($"라운드 몬스터 데이터 오류 : {key}");
                    }
                }
            }

            if (float.IsNaN(rule.SpecialItemDropMultiplier) == true ||
                float.IsInfinity(rule.SpecialItemDropMultiplier) == true ||
                rule.SpecialItemDropMultiplier < 0)
            {
                throw new InvalidOperationException("SpecialItemDropMultiplier must be finite and non-negative.");
            }

            var navigationRadius = SpawnHandler.GetAliveHeroes().Max(hero => hero.GetMoveRadius());
            SpawnCells = GroundHandler.GetReachableCells(HeroSpawnPoint, navigationRadius);
            FormationHandler.Initialize(SpawnCells, navigationRadius);

            if (SpawnCells.Count == 0)
            {
                throw new InvalidOperationException("영웅 스폰 지점에서 이동 가능한 구역이 없습니다.");
            }

            CurrentRoundIndex = -1;
            FormationHandler.ClearDestinations();
            RoundPhase = CombatRoundPhase.Preparing;

            // 모든 성소에 필요한 정렬 공간이 있는지 시작 전에 확인한다.
            for (var i = 0; i < StageRounds.Length; i++)
            {
                FormationHandler.GetFormationCells(HolyPlacePoints[i], SpawnHandler.GetAliveHeroes().Count);
            }
        }

        internal void StartRound(int index)
        {
            CurrentRoundIndex = index;
            NavigationHandler.ClearExcludedTargets();
            FormationHandler.SetLeader(FormationHandler.Leader != null && FormationHandler.Leader.CharHealth.IsAlive == true ? FormationHandler.Leader : CombatFormationHandler.SelectLeader(SpawnHandler.GetAliveHeroes()));
            FormationHandler.Clear();
            var round = StageRounds[index];
            var cells = GetFreeSpawnCells();
            Shuffle(cells);
            var total = StageGroundData.SpawnCounts[index];

            if (cells.Count < total)
            {
                throw new InvalidOperationException($"라운드 스폰 공간 부족 : {round.RoundKey}, {total}");
            }

            for (var i = 0; i < total; i++)
            {
                SpawnRoundMonster(StageGroundData.Monsters[index], cells[i], false);
            }

            RoundPhase = CombatRoundPhase.RoundCombat;
            SpawnHandler.RefreshEnemyTargets();
            ChangeAllCharactersToDetectState();
            ThreeSoulsLog.Info($"Ground combat started : {round.RoundKey}, {StageGroundData.Monsters[index]} x {total}");
        }

        private BaseEntity SpawnRoundMonster(string key, Vector2Int cell, bool special)
        {
            var character = CampaignBalance.ScaleMonster(TableManager.Get().GetCharacterTableDataTable(key), RegionNumber, StageNumber);
            var request = new CharacterSpawnRequest(
                CharacterType.Monster,
                character,
                RoundRuleData,
                character.ResourceName,
                CharacterParent,
                GroundHandler.CellToWorldCenter(cell),
                CombatWorld);
            var monster = SpawnHandler.SpawnCharacter(request);

            if (special == true)
            {
                ((Monster)monster).SetSpecialReward(true);
            }

            return monster;
        }

        private List<Vector2Int> GetFreeSpawnCells()
        {
            return SpawnCells.Where(cell =>
            {
                var position = (Vector2)GroundHandler.CellToWorldCenter(cell);

                return SpawnHandler.GetAliveHeroes()
                    .All(hero => Vector2.SqrMagnitude(hero.GetWorldPosition() - position) > 9f) == true &&
                    FormationHandler.IsAwayFromDestinations(position, 0.25f) == true;
            })
                .ToList();
        }

        internal void Update()
        {
            if (RoundPhase == CombatRoundPhase.Preparing ||
                RoundPhase == CombatRoundPhase.Completed ||
                RoundPhase == CombatRoundPhase.Defeated)
            {
                return;
            }

            if (SpawnHandler.GetAliveHeroes().Count == 0)
            {
                FinishStage(false);

                return;
            }

            if (FormationHandler.NeedsRebuild() == true)
            {
                FormationHandler.SetLeader(CombatFormationHandler.SelectLeader(SpawnHandler.GetAliveHeroes()));

                if (RoundPhase == CombatRoundPhase.RallyFormation || RoundPhase == CombatRoundPhase.TravelToHolyPlace)
                {
                    BeginRally();
                }
                else if (RoundPhase == CombatRoundPhase.HolyPlaceFormation ||
                    RoundPhase == CombatRoundPhase.DepartureFormation ||
                    RoundPhase == CombatRoundPhase.HolyPlaceWaiting)
                {
                    BeginFormation(RoundPhase == CombatRoundPhase.DepartureFormation ? RoundPhase : CombatRoundPhase.HolyPlaceFormation);
                }
                else
                {
                    FormationHandler.ClearMembers();
                }
            }

            switch (RoundPhase)
            {
                case CombatRoundPhase.RoundCombat:
                    if (GetRemainingMonsterCount() == 0)
                    {
                        BeginRally();
                    }

                    break;
                case CombatRoundPhase.RallyFormation:
                    if (FormationHandler.AreHeroesAtDestinations() == true)
                    {
                        RoundPhase = CombatRoundPhase.TravelToHolyPlace;
                    }

                    break;
                case CombatRoundPhase.TravelToHolyPlace:
                    FormationHandler.UpdateTravel();
                    if (FormationHandler.IsTravelCompleted() == true && GetRemainingMonsterCount() > 0)
                    {
                        RoundPhase = CombatRoundPhase.HolyPlaceCleanup;
                        FormationHandler.ClearDestinations();
                        NavigationHandler.ClearExcludedTargets();
                        ChangeAllCharactersToDetectState();
                    }
                    else if (FormationHandler.IsTravelCompleted() == true && FormationHandler.AreHeroesAtDestinations() == true)
                    {
                        BeginFormation(CombatRoundPhase.HolyPlaceFormation);
                    }

                    break;
                case CombatRoundPhase.HolyPlaceCleanup:
                    if (GetRemainingMonsterCount() == 0)
                    {
                        BeginFormation(CombatRoundPhase.HolyPlaceFormation);
                    }

                    break;
                case CombatRoundPhase.HolyPlaceFormation:
                    if (GetRemainingMonsterCount() > 0)
                    {
                        RoundPhase = CombatRoundPhase.HolyPlaceCleanup;
                        FormationHandler.ClearDestinations();
                        ChangeAllCharactersToDetectState();
                    }
                    else if (FormationHandler.AreHeroesAtDestinations() == true)
                    {
                        FormationHandler.WaitHeroes();
                        RoundPhase = CombatRoundPhase.HolyPlaceWaiting;
                        FormationReadyTime = Time.time + FormationWaitTime;
                    }

                    break;
                case CombatRoundPhase.HolyPlaceWaiting:
                    if (GetRemainingMonsterCount() > 0)
                    {
                        RoundPhase = CombatRoundPhase.HolyPlaceCleanup;
                        FormationHandler.ClearDestinations();
                        NavigationHandler.ClearExcludedTargets();
                        ChangeAllCharactersToDetectState();
                    }
                    else if (Time.time >= FormationReadyTime)
                    {
                        StartHolyPlaceCombat();
                    }

                    break;
                case CombatRoundPhase.HolyPlaceCombat:
                    if (GetRemainingMonsterCount() == 0)
                    {
                        Sanctuary.End();
                        BeginFormation(CombatRoundPhase.DepartureFormation);
                    }

                    break;
                case CombatRoundPhase.DepartureFormation:
                    if (FormationHandler.AreHeroesAtDestinations() == true)
                    {
                        if (FormationReadyTime < 0)
                        {
                            FormationHandler.WaitHeroes();
                            FormationReadyTime = Time.time + FormationWaitTime;
                        }
                        else if (Time.time >= FormationReadyTime)
                        {
                            if (CurrentRoundIndex + 1 < StageRounds.Length)
                            {
                                StartRound(CurrentRoundIndex + 1);
                            }
                            else
                            {
                                FinishStage(true);
                            }
                        }
                    }

                    break;
            }
        }

        private Vector3 GetNextHolyPlace()
        {
            return CurrentRoundIndex + 1 < StageRounds.Length ? HolyPlacePoints[CurrentRoundIndex + 1] : HolyPlacePoints[CurrentRoundIndex];
        }

        private void BeginFormation(CombatRoundPhase phase)
        {
            RoundPhase = phase;
            NavigationHandler.ClearExcludedTargets();
            FormationHandler.BeginArrival(HolyPlacePoints[CurrentRoundIndex], GetNextHolyPlace(), GetCurrentRoundKey());
            FormationReadyTime = -1;
            ThreeSoulsLog.Info($"Formation started : {GetCurrentRoundKey()}, {phase}");
        }

        private void BeginRally()
        {
            RoundPhase = CombatRoundPhase.RallyFormation;
            NavigationHandler.ClearExcludedTargets();
            FormationHandler.BeginRally(HolyPlacePoints[CurrentRoundIndex], GetNextHolyPlace(), GetCurrentRoundKey());
        }

        private void StartHolyPlaceCombat()
        {
            Sanctuary.Begin(GetCurrentRoundKey(), UnityEngine.Random.Range(0, 3));
            var center = (Vector2)HolyPlacePoints[CurrentRoundIndex];
            var cells = SpawnCells.Where(cell => SpawnHandler.GetAliveHeroes()
                .All(hero => Vector2.SqrMagnitude(hero.GetWorldPosition() - (Vector2)GroundHandler.CellToWorldCenter(cell)) > 0.36f))
                .OrderBy(cell => Vector2.SqrMagnitude((Vector2)GroundHandler.CellToWorldCenter(cell) - center))
                .ToList();
            var round = StageRounds[CurrentRoundIndex];
            var count = round.SpawnCounts.Sum();

            if (cells.Count < count)
            {
                throw new InvalidOperationException("성소 몬스터 스폰 공간 부족");
            }

            var cellIndex = 0;

            for (var i = 0; i < round.Monsters.Length; i++)
            {
                for (var j = 0; j < round.SpawnCounts[i]; j++)
                {
                    SpawnRoundMonster(round.Monsters[i], cells[cellIndex++], true);
                }
            }

            RoundPhase = CombatRoundPhase.HolyPlaceCombat;
            NavigationHandler.ClearExcludedTargets();
            SpawnHandler.RefreshEnemyTargets();
            ChangeAllCharactersToDetectState();
            ThreeSoulsLog.Info($"Holy place started : {GetCurrentRoundKey()}, Monsters {count}");
            EventUtility.TriggerEvent(this, new Event_CombatHolyPlaceStarted(GetCurrentRoundKey(), GetCurrentRoundNumber(), center));
        }

        private void FinishStage(bool victory)
        {
            Sanctuary.End();
            RoundPhase = victory == true ? CombatRoundPhase.Completed : CombatRoundPhase.Defeated;
            FormationHandler.Clear();
            ChangeAliveCharactersToWaitState();
            ThreeSoulsLog.Info($"Stage completed : {StageNumber}, Victory {victory}");
            EventUtility.TriggerEvent(
                this,
                new Event_CombatStageCompleted(RegionNumber, StageNumber, victory));
        }

        internal bool CanEngageTarget(ICombatStateProvider entity, GameObject target)
        {
            if (CombatWorld.IsTargetAlive(target) == false)
            {
                return false;
            }

            if (entity.CharacterData.CharacterType == CharacterType.Monster)
            {
                return true;
            }

            // 필드 잔여 적은 탐지 반경 밖에 있어도 정리한다. 교전 도중 반경을 벗어나 타겟을 반복해서 잃지 않는다.
            if (RoundPhase == CombatRoundPhase.RoundCombat ||
                RoundPhase == CombatRoundPhase.HolyPlaceCombat ||
                RoundPhase == CombatRoundPhase.HolyPlaceCleanup)
            {
                return true;
            }

            if (RoundPhase != CombatRoundPhase.TravelToHolyPlace)
            {
                return false;
            }

            if (entity.EnemyTarget == target)
            {
                return true;
            }

            var radius = Mathf.Max(entity.CharacterData.CharTargetSearchRadius, entity.CharacterData.CharDistanceToTarget);

            return Vector2.SqrMagnitude(entity.GetWorldPosition() - CombatWorld.GetTargetPosition(target)) <= radius * radius;
        }

        private static void Shuffle<T>(IList<T> values)
        {
            for (var i = values.Count - 1; i > 0; i--)
            {
                var randomIndex = UnityEngine.Random.Range(0, i + 1);
                (values[i], values[randomIndex]) = (values[randomIndex], values[i]);
            }
        }

        private void ChangeAllCharactersToDetectState()
        {
            var heroes = SpawnHandler.GetAliveHeroes();
            var monsters = SpawnHandler.GetAliveMonsters();

            for (var i = 0; i < heroes.Count; i++)
            {
                heroes[i].ChangeState(CombatState.DetectTarget);
            }

            for (var i = 0; i < monsters.Count; i++)
            {
                monsters[i].ChangeState(CombatState.DetectTarget);
            }
        }

        private void ChangeAliveCharactersToWaitState()
        {
            var heroes = SpawnHandler.GetAliveHeroes();
            var monsters = SpawnHandler.GetAliveMonsters();

            for (var i = 0; i < heroes.Count; i++)
            {
                heroes[i].ChangeState(CombatState.Wait);
            }

            for (var i = 0; i < monsters.Count; i++)
            {
                monsters[i].ChangeState(CombatState.Wait);
            }
        }
    }
}
