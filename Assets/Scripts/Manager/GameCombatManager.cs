using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ThreeSouls.Character;
using ThreeSouls.Combat;
using ThreeSouls.Data;
using ThreeSouls.Data.Generated;
using ThreeSouls.Enumeration;
using ThreeSouls.Enumeration.AddressableKey;
using ThreeSouls.Global;
using ThreeSouls.Interface;
using ThreeSouls.Projectile;
using ThreeSouls.Utility;
using ThreeSouls.UI;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.U2D.Animation;

namespace ThreeSouls.Manager
{
    public class GameCombatManager : MonoBehaviour, IManager,
        ICombatWorld,
        IEventListener<Event_CharacterHealthDepleted>,
        IEventListener<Event_CombatMoveCompleted>
    {
        public int CallbackOrder => 0;
        public int CurrentStage { get; set; } = 0;

        private readonly CombatSpawnHandler SpawnHandler = new ();
        private readonly CombatGroundHandler GroundHandler = new();
        private readonly CombatFormationHandler FormationHandler = new();
        private readonly CombatHealthBarHandler HealthBarHandler = new();
        private AStarPathfinder PathFinder = null;
        private Transform CharacterParent = null;
        private Vector3 HeroSpawnPoint = Vector3.zero;
        private Vector3[] MonsterSpawnsPoint = Array.Empty<Vector3>();
        private CinemachineCamera FollowCamera = null;
        private CinemachineConfiner2D CameraConfiner = null;
        private CinemachinePositionComposer CameraPositionComposer = null;
        private Coroutine CameraSequenceCoroutine = null;
        private Coroutine HeroFollowCoroutine = null;
        private BaseEntity CurrentLeader = null;
        private readonly Dictionary<ICombatStateProvider, Vector2[]> PlannedMovePaths = new();
        private readonly Dictionary<ICombatStateProvider, GameObject> PlannedMoveTargets = new();
        private readonly HashSet<ICombatStateProvider> EngagedMonsters = new();
        private CameraPresentationData CameraPresentationSettings = null;

        public static GameCombatManager Get()   
        {
            return GameManager.Instance.Get<GameCombatManager>();
        }

        public void Initialize()
        {
            PathFinder = new AStarPathfinder(GroundHandler);

            EventUtility.AddListener<Event_CharacterHealthDepleted>(this);
            EventUtility.AddListener<Event_CombatMoveCompleted>(this);
        }

        public void OnSceneChanged() { }

        public void Release()
        {
            if (CameraSequenceCoroutine != null)
            {
                StopCoroutine(CameraSequenceCoroutine);
                CameraSequenceCoroutine = null;
            }

            StopHeroFollow();

            SpawnHandler.Release();
            HealthBarHandler.Release();
            EventUtility.RemoveListener<Event_CharacterHealthDepleted>(this);
            EventUtility.RemoveListener<Event_CombatMoveCompleted>(this);
            GroundHandler.Release();
            FormationHandler.Clear();
            PlannedMovePaths.Clear();
            PlannedMoveTargets.Clear();
            EngagedMonsters.Clear();
            CurrentLeader = null;
        }

        public IEnumerator AssetsPreLoad()
        {
            yield return TableManager.Get().AssetsPreLoad();

            yield return AddressableManager.Get().AddressableLoadAssetAsync<GameObject>(Prefabs.Hero.ToAddress(), true);
            yield return AddressableManager.Get().AddressableLoadAssetAsync<GameObject>(Prefabs.Monster.ToAddress(), true);
            yield return AddressableManager.Get().AddressableLoadAssetAsync<GameObject>(Prefabs.Grid_Cave.ToAddress(), true);
            yield return AddressableManager.Get().AddressableLoadAssetAsync<GameObject>(Prefabs.Projectile.ToAddress(), true);
            yield return AddressableManager.Get().AddressableLoadAssetAsync<GameObject>(Prefabs.UI_Item_HealthBar.ToAddress(), true);
            yield return AddressableManager.Get().AddressableLoadAssetAsync<ScriptableObject>(ScriptableObjectDatas.CameraPresentationData.ToAddress(), false);

            var enums = Enum.GetNames(typeof(SpriteLibrarys));

            for (int i = 0; i < enums.Length; i++)
            {
                Enum.TryParse<SpriteLibrarys>(enums[i], false, out var key);
                yield return AddressableManager.Get().AddressableLoadAssetAsync<SpriteLibraryAsset>(key.ToAddress(), true);
            }
        }

        public void ReadyCombat()
        {
            HealthBarHandler.Initialize();
            SpawnHandler.Initialize(HealthBarHandler);

            var ruleTableData = TableManager.Get().GetThreeSoulsRuleDataTable();
            var groundTableData = TableManager.Get().GetGroundTableData(CurrentStage);

            if (ruleTableData == null || groundTableData == null)
            {
                ThreeSoulsLog.Error($"Combat TableData null : Stage {CurrentStage}");
                return;
            }

            if (GroundHandler.CreateStageMap(groundTableData.MapName) == false)
            {
                return;
            }

            if (SetCombatVariables() == false)
            {
                return;
            }

            SpawnHeros(ruleTableData.MaxCharacterCombat, ruleTableData);
            SpawnMonsters(groundTableData, ruleTableData);
            SpawnHandler.RefreshEnemyTargets();

            if (SetUpCamera() == false)
            {
                StartCombat();
                return;
            }

            if (CameraSequenceCoroutine != null)
            {
                StopCoroutine(CameraSequenceCoroutine);
            }

            CameraSequenceCoroutine = StartCoroutine(PlayCameraPresentation());
        }

        /// <summary>
        /// 전투 인트로와 전투 중 추적에 사용할 Cinemachine 구성 요소를 초기화한다.
        /// 인트로 줌 크기와 Dead Zone 값을 준비하고, 현재 스테이지의 카메라 경계를 Confiner에 연결한다.
        /// </summary>
        /// <returns>카메라 연출에 필요한 참조가 모두 준비됐으면 true</returns>
        private bool SetUpCamera()
        {
            CameraPresentationSettings = AddressableManager.Get().LoadAsset<CameraPresentationData>(ScriptableObjectDatas.CameraPresentationData.ToAddress());

            if (CameraPresentationSettings == null)
            {
                ThreeSoulsLog.Error("CameraPresentationData null");
                return false;
            }

            if (FollowCamera == null || GroundHandler.CameraBounds == null)
            {
                ThreeSoulsLog.Error("Camera presentation setup failed");
                return false;
            }

            FollowCamera.Lens.OrthographicSize = CameraPresentationSettings.IntroCameraSize;

            CameraPositionComposer = FollowCamera.GetComponent<CinemachinePositionComposer>();

            if (CameraPositionComposer != null)
            {
                var composition = CameraPositionComposer.Composition;
                composition.DeadZone.Enabled = false;
                composition.DeadZone.Size = CameraPresentationSettings.DeadZoneSize;
                CameraPositionComposer.Composition = composition;
            }

            CameraConfiner = FollowCamera.GetComponent<CinemachineConfiner2D>();

            if (CameraConfiner == null)
            {
                ThreeSoulsLog.Error("Not Found Component CinemachineConfiner2D");
                return false;
            }

            CameraConfiner.BoundingShape2D = GroundHandler.CameraBounds;
            CameraConfiner.Damping = CameraPresentationSettings.ConfinerDamping;
            CameraConfiner.SlowingDistance = CameraPresentationSettings.ConfinerSlowingDistance;
            CameraConfiner.OversizeWindow = new CinemachineConfiner2D.OversizeWindowSettings
            {
                Enabled = false,
                MaxWindowSize = CameraPresentationSettings.IntroCameraSize
            };
            CameraConfiner.InvalidateBoundingShapeCache();

            return true;
        }

        /// <summary>
        /// 시작 지연 후 스폰된 몬스터를 순서대로 보여주고 마지막 영웅을 포커싱한다.
        /// 영웅 포커싱 중 Orthographic Size를 전투용 크기로 줄인 뒤 전투를 시작한다.
        /// </summary>
        private IEnumerator PlayCameraPresentation()
        {
            yield return new WaitForSecondsRealtime(CameraPresentationSettings.IntroDelay);

            for (var i = 0; i < SpawnHandler.MonsterSpawnGroupCount; i++)
            {
                SetCameraTarget(SpawnHandler.GetMonsterSpawnGroup(i)[0].transform);
                yield return new WaitForSecondsRealtime(CameraPresentationSettings.MonsterFocusTime);
            }

            var heroes = SpawnHandler.AliveHeroes;
            var leader = SelectLeader(heroes);

            SetLeader(leader);

            var elapsedTime = 0f;
            var startSize = FollowCamera.Lens.OrthographicSize;

            while (elapsedTime < CameraPresentationSettings.HeroFocusTime)
            {
                elapsedTime += Time.unscaledDeltaTime;
                var progress = Mathf.Clamp01(elapsedTime / CameraPresentationSettings.HeroFocusTime);
                FollowCamera.Lens.OrthographicSize = Mathf.Lerp(
                    startSize,
                    CameraPresentationSettings.CombatCameraSize,
                    Mathf.SmoothStep(0f, 1f, progress));

                yield return null;
            }

            FollowCamera.Lens.OrthographicSize = CameraPresentationSettings.CombatCameraSize;
            CameraConfiner?.InvalidateLensCache();

            StartCombat();
            CameraSequenceCoroutine = null;
        }

        /// <summary>
        /// Cinemachine Camera가 추적할 대상을 변경한다.
        /// 실제 카메라 이동은 Position Composer의 Damping 설정에 따라 보간된다.
        /// </summary>
        /// <param name="trackingTarget">새로 추적할 대상 Transform</param>
        private void SetCameraTarget(Transform trackingTarget)
        {
            FollowCamera.Target = new CameraTarget
            {
                TrackingTarget = trackingTarget
            };
        }

        /// <summary>
        /// 현재 부대 리더를 갱신하고 카메라가 새 리더를 추적하게 한다.
        /// 리더가 사망한 경우 CreateEncounterPlan에서 다음 생존 영웅이 선택되어 이 메서드로 전달된다.
        /// </summary>
        private void SetLeader(BaseEntity leader)
        {
            if (CurrentLeader == leader)
            {
                return;
            }

            CurrentLeader = leader;

            if (CurrentLeader != null && FollowCamera != null)
            {
                SetCameraTarget(CurrentLeader.transform);
            }
        }

        /// <summary>
        /// Position Composer의 Dead Zone을 켜거나 끈다.
        /// 인트로 중에는 정확한 포커싱을 위해 끄고, 전투 시작 시 작은 움직임을 무시하도록 켠다.
        /// </summary>
        /// <param name="enabled">Dead Zone 활성화 여부</param>
        private void SetCameraDeadZone(bool enabled)
        {
            if (CameraPositionComposer == null)
            {
                return;
            }

            var composition = CameraPositionComposer.Composition;
            composition.DeadZone.Enabled = enabled;
            CameraPositionComposer.Composition = composition;
        }

        /// <summary>
        /// 카메라 인트로가 끝난 뒤 모든 영웅과 몬스터를 탐지 상태로 전환해 전투를 시작한다.
        /// </summary>
        private void StartCombat()
        {
            SetCameraDeadZone(true);

            if (CreateEncounterPlan() == false)
            {
                ChangeAllCharactersToDetectState();
            }
        }

        private void SpawnHeros(int maxPlayer, ThreeSoulsRuleDataRow ruleTableData)
        {
            for (int i = 0; i < maxPlayer; i++)
            {
                var pos = new Vector3(HeroSpawnPoint.x + i, HeroSpawnPoint.y);
                var characterTableData = TableManager.Get().GetCharacterTableDataTable(ruleTableData.BaseCharacterID[i]);
                var request = new CharacterSpawnRequest(
                    CharacterType.Hero,
                    characterTableData,
                    ruleTableData,
                    characterTableData.ResourceName,
                    CharacterParent,
                    pos,
                    this);

                SpawnHandler.SpawnCharacter(request);
            }
        }

        private void SpawnMonsters(GroundTableDataRow groundTableData, ThreeSoulsRuleDataRow ruleTableData)
        {
            SpawnHandler.ResetMonsterGroups();
            EngagedMonsters.Clear();

            for (int groupIndex = 0; groupIndex < groundTableData.SpawnCounts.Length; groupIndex++)
            {
                var characterTableData = TableManager.Get().GetCharacterTableDataTable(groundTableData.Monsters[groupIndex]);
                //스폰 지역에서 스폰 가능한 곳을 찾음
                var spawnCells = GroundHandler.GetWalkableCellsAround(MonsterSpawnsPoint[groupIndex], Constants.MONSTER_SPAWN_CELL_RADIUS);
                //랜덤으로 스폰
                Shuffle(spawnCells);
                
                var spawnCount = Mathf.Min(groundTableData.SpawnCounts[groupIndex], spawnCells.Count);

                for (var spawnIndex = 0; spawnIndex < spawnCount; spawnIndex++)
                {
                    var cell = spawnCells[spawnIndex];
                    var spawnPosition = GroundHandler.CellToWorldCenter(cell);
                    var request = new CharacterSpawnRequest(
                        CharacterType.Monster,
                        characterTableData,
                        ruleTableData,
                        characterTableData.ResourceName,
                        CharacterParent,
                        spawnPosition,
                        this,
                        groupIndex);

                    SpawnHandler.SpawnCharacter(request);
                }
            }
        }

        private static void Shuffle<T>(IList<T> values)
        {
            for (var i = values.Count - 1; i > 0; i--)
            {
                var randomIndex = UnityEngine.Random.Range(0, i + 1);
                (values[i], values[randomIndex]) = (values[randomIndex], values[i]);
            }
        }

        /// <summary>
        /// 현재 그룹에서 이동을 대표할 영웅과 가장 먼저 감지될 몬스터를 찾아
        /// 양 진영이 공유할 전투 위치와 A* 경로를 만든다.
        /// </summary>
        private bool CreateEncounterPlan(bool startHeroesSequentially = false)
        {
            StopHeroFollow();
            PlannedMovePaths.Clear();
            PlannedMoveTargets.Clear();

            var heroes = SpawnHandler.AliveHeroes;
            var monsters = SpawnHandler.CurrentMonsterSpawnGroup;
            var previousGroupIndex = SpawnHandler.CurrentMonsterSpawnGroupIndex;

            while (monsters.Count == 0 && SpawnHandler.TryAdvanceMonsterGroup())
            {
                monsters = SpawnHandler.CurrentMonsterSpawnGroup;
            }

            var leader = SelectLeader(heroes);
            SetLeader(leader);

            // 살아 있는 영웅이 없으면 몬스터를 멈추고 패배 처리를 기다린다.
            if (leader == null)
            {
                ChangeAliveCharactersToWaitState();
                ThreeSoulsLog.Info("Encounter plan ended : No alive hero");
                return true;
            }

            // 마지막 그룹까지 모두 처치했으면 영웅을 대기시키고 전투 종료 처리를 기다린다.
            if (monsters.Count == 0)
            {
                ChangeAliveCharactersToWaitState();
                ThreeSoulsLog.Info("Encounter plan ended : All monster groups cleared");
                return true;
            }

            if (previousGroupIndex != SpawnHandler.CurrentMonsterSpawnGroupIndex)
            {
                EngagedMonsters.Clear();
                ThreeSoulsLog.Info($"Monster wave changed : {SpawnHandler.CurrentMonsterSpawnGroupIndex + 1}");
                BeginHeroFormation(heroes, leader, monsters);
                return true;
            }

            EncounterCandidate bestCandidate = null;

            for (var i = 0; i < monsters.Count; i++)
            {
                var monster = monsters[i];
                
                if (TryCreateEncounterCandidate(leader, monster, out var candidate) == false)
                {
                    continue;
                }

                if (bestCandidate == null || candidate.TriggerTime < bestCandidate.TriggerTime)
                {
                    bestCandidate = candidate;
                }
            }

            if (bestCandidate == null ||
                TryFindCombatCells(leader, bestCandidate.Monster, bestCandidate.MeetingPoint,
                    out var heroCell, out var monsterCell) == false)
            {
                return false;
            }

            var heroPath = CreateWorldPath(leader.CurrentWorldPosition, heroCell);
            var monsterPath = CreateWorldPath(bestCandidate.Monster.CurrentWorldPosition, monsterCell);

            if (heroPath == null || monsterPath == null)
            {
                return false;
            }

            ThreeSoulsLog.Info(
                $"Encounter plan : Leader={leader.name}, Anchor={bestCandidate.Monster.name}, " +
                $"HeroCell={heroCell}, MonsterCell={monsterCell}");

            // 일렬 대형을 유지하도록 모든 영웅이 리더 경로를 공유한다.
            for (var i = 0; i < heroes.Count; i++)
            {
                var hero = heroes[i];
                PlannedMovePaths[hero] = heroPath;
                PlannedMoveTargets[hero] = bestCandidate.Monster.gameObject;
                hero.EnemyTarget = bestCandidate.Monster.gameObject;

                if (startHeroesSequentially == false)
                {
                    hero.ChangeState(CombatState.DetectTarget);
                }
                else
                {
                    hero.ChangeState(CombatState.Wait);
                }
            }

            if (startHeroesSequentially)
            {
                HeroFollowCoroutine = StartCoroutine(StartHeroFollow());
            }

            // 모든 몬스터의 경로는 미리 준비하지만 최초 이동은 영웅이 각자의 탐지 범위에 들어온 뒤 시작한다.
            // 이미 교전한 몬스터는 영웅이 죽어도 어그로를 유지하고 새 리더를 계속 추적한다.
            for (var i = 0; i < monsters.Count; i++)
            {
                var monster = monsters[i];
                var path = monster == bestCandidate.Monster ? monsterPath : CreateWorldPath(monster.CurrentWorldPosition, monsterCell);

                if (path != null)
                {
                    PlannedMovePaths[monster] = path;
                }

                PlannedMoveTargets[monster] = leader.gameObject;
                monster.EnemyTarget = leader.gameObject;
                monster.ChangeState(CombatState.DetectTarget);
            }

            return true;
        }

        private void BeginHeroFormation(
            IReadOnlyList<BaseEntity> heroes,
            BaseEntity leader,
            IReadOnlyList<BaseEntity> monsters)
        {
            FormationHandler.Begin(
                heroes,
                leader,
                monsters,
                GroundHandler.WorldToCell,
                (hero, targetCell) => CreateWorldPath(hero.CurrentWorldPosition, targetCell),
                GroundHandler.IsWalkable);
        }

        private void StopHeroFollow()
        {
            if (HeroFollowCoroutine == null)
            {
                return;
            }

            StopCoroutine(HeroFollowCoroutine);
            HeroFollowCoroutine = null;
        }

        private IEnumerator StartHeroFollow()
        {
            yield return FormationHandler.StartFollowing(Constants.HERO_FOLLOW_START_DELAY);
            HeroFollowCoroutine = null;
        }


        /// <summary>
        /// Tank, MeleeDealer, RangedDealer 순으로 우선하며 같은 역할에서는 사거리가 짧은 영웅을 선택
        /// </summary>
        /// <param name="heroes">생존 하고 있는 영웅</param>
        /// <returns>리더 영웅</returns>
        private static BaseEntity SelectLeader(IReadOnlyList<BaseEntity> heroes)
        {
            return heroes
                .OrderBy(hero => GetLeaderRolePriority(hero.CharacterData.CharacterRole))
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

        /// <summary>
        /// 리더에서 몬스터까지의 A* 경로가 몬스터 감지 반경에 처음 진입하는 시간과
        /// 감지 이후 양쪽 속도를 반영한 예상 만남 위치를 계산한다.
        /// </summary>
        private bool TryCreateEncounterCandidate(BaseEntity leader, BaseEntity monster, out EncounterCandidate candidate)
        {
            candidate = null;

           
            var monsterCell = GroundHandler.WorldToCell(monster.CurrentWorldPosition);
            var path = CreateWorldPathWithStart(leader.CurrentWorldPosition, monsterCell);

            if (path == null || path.Length < 2)
            {
                return false;
            }

            if (TryFindFirstRadiusEntry(path, monster.CurrentWorldPosition,
                    Constants.CIRCLE_RADIUS_MONSTER, out var triggerDistance) == false)
            {
                return false;
            }

            var totalDistance = GetPathLength(path);
            var triggerTime = CalculateTravelTime(
                triggerDistance,
                totalDistance,
                leader.CharacterData);
            var meetingPoint = SimulateMeetingPoint(
                path,
                triggerDistance,
                totalDistance,
                leader.CharacterData,
                monster.CharacterData);

            candidate = new EncounterCandidate(monster, triggerTime, meetingPoint);

            return true;
        }

        /// <summary>
        /// A* 결과 앞에 실제 시작 위치를 붙여 거리 예측용 경로를 만듬
        /// </summary>
        /// <param name="startPosition">캐릭터 월드 포지션</param>
        /// <param name="goalCell">해당 타겟 셀 포지션</param>
        /// <returns>거리 예측용 경로</returns>
        private Vector2[] CreateWorldPathWithStart(Vector2 startPosition, Vector2Int goalCell)
        {
            var path = CreateWorldPath(startPosition, goalCell);

            if (path == null)
            {
                return null;
            }

            //캐릭터 실제 위치 -> 다음 셀 중앙 -> 목적지
            var result = new Vector2[path.Length + 1];
            result[0] = startPosition;
            Array.Copy(path, 0, result, 1, path.Length);
            return result;
        }

        /// <summary>
        /// A* 셀 경로를 캐릭터가 따라갈 월드 좌표 배열로 변환
        /// </summary>
        /// <param name="startPosition">캐릭터 월드 포지션</param>
        /// <param name="goalCell">해당 타겟 셀 포지션</param>
        /// <returns>A* 셀 경로</returns>
        private Vector2[] CreateWorldPath(Vector2 startPosition, Vector2Int goalCell)
        {
            var startCell = GroundHandler.WorldToCell(startPosition);
            var startNode = GroundHandler.GetNodeData(startCell);
            var goalNode = GroundHandler.GetNodeData(goalCell);

            if (startNode == null || goalNode == null)
            {
                return null;
            }

            var path = PathFinder.FindPath(startNode, goalNode);

            if (path == null)
            {
                return null;
            }

            //시작 셀 중앙을 제거
            return path
                .Skip(1)
                .Select(node => (Vector2)GroundHandler.CellToWorldCenter(node.CellPosition))
                .ToArray();
        }

        /// <summary>
        /// 경로가 CIRCLE 탐지 범위에 처음 진입하는 누적 거리를 구한다
        /// </summary>
        /// <param name="path">리더 영웅에서 몬스터까지의 A* 경로</param>
        /// <param name="center">타겟 월드 포지션</param>
        /// <param name="radius">CIRCLE 반지름</param>
        /// <param name="pathDistance">감지될 때까지 이동해야 하는 거리</param>
        /// <returns>경로가 CIRCLE 범위 안으로 들어갔는지</returns>
        private static bool TryFindFirstRadiusEntry(Vector2[] path, Vector2 center, float radius, out float pathDistance)
        {   
            pathDistance = 0;

            if (Vector2.Distance(path[0], center) <= radius)
            {
                return true;
            }

            //이동 경로(선분)가 원의 영역을 교차하는가
            for (var i = 0; i < path.Length - 1; i++)
            {
                var start = path[i];
                var end = path[i + 1];
                //선분 방향
                var direction = end - start;
                //중심 방향 벡터
                var startToCenter = center - start;
                var a = Vector2.Dot(direction, direction);
                var b = -2f * Vector2.Dot(startToCenter, direction);
                var c = Vector2.Dot(startToCenter, startToCenter) - radius * radius;
                var discriminant = b * b - 4f * a * c;

                if (a > 0 && discriminant >= 0)
                {
                    var entry = (-b - Mathf.Sqrt(discriminant)) / (2f * a);

                    if (entry >= 0 && entry <= 1)
                    {
                        pathDistance += direction.magnitude * entry;
                        return true;
                    }
                }

                pathDistance += direction.magnitude;
            }

            return false;
        }

        /// <summary>걷기와 근접 가속 구간을 반영해 지정 거리까지의 이동 시간을 계산한다.</summary>
        private static float CalculateTravelTime(float travelDistance, float totalDistance, CharacterCombatData data)
        {
            var runningStartDistance = data.CharDistanceToTarget + Constants.RUNNING_DISTANCE;
            var walkingEndDistance = Mathf.Max(0, totalDistance - runningStartDistance);
            var walkingDistance = Mathf.Min(travelDistance, walkingEndDistance);
            var runningDistance = Mathf.Max(0, travelDistance - walkingDistance);
            var runningSpeed = data.CharIsRunningPlay
                ? data.CharMoveSpeed * data.CharRunningSpeed
                : data.CharMoveSpeed;

            return walkingDistance / data.CharMoveSpeed + runningDistance / runningSpeed;
        }

        /// <summary>탐지 이후 영웅과 몬스터가 서로 이동할 때의 예상 만남 위치를 계산한다.</summary>
        private static Vector2 SimulateMeetingPoint(Vector2[] path, float triggerDistance,
            float totalDistance, CharacterCombatData heroData, CharacterCombatData monsterData)
        {
            var remainingDistance = Mathf.Max(0, totalDistance - triggerDistance);
            var heroProgress = 0f;
            var monsterProgress = 0f;

            while (heroProgress + monsterProgress < remainingDistance)
            {
                var separation = remainingDistance - heroProgress - monsterProgress;
                var heroSpeed = GetPredictedSpeed(heroData, separation);
                var monsterSpeed = GetPredictedSpeed(monsterData, separation);
                heroProgress += heroSpeed * Constants.ENCOUNTER_SIMULATION_STEP;
                monsterProgress += monsterSpeed * Constants.ENCOUNTER_SIMULATION_STEP;
            }

            return GetPointAtDistance(path, triggerDistance + Mathf.Min(heroProgress, remainingDistance));
        }

        /// <summary>남은 거리를 기준으로 걷기 또는 가속 이동 속도를 반환한다.</summary>
        private static float GetPredictedSpeed(CharacterCombatData data, float remainingDistance)
        {
            if (data.CharIsRunningPlay &&
                remainingDistance <= data.CharDistanceToTarget + Constants.RUNNING_DISTANCE)
            {
                return data.CharMoveSpeed * data.CharRunningSpeed;
            }

            return data.CharMoveSpeed;
        }

        /// <summary>경로 시작점에서 지정 누적 거리만큼 떨어진 월드 위치를 반환한다.</summary>
        private static Vector2 GetPointAtDistance(Vector2[] path, float distance)
        {
            for (var i = 0; i < path.Length - 1; i++)
            {
                var segmentDistance = Vector2.Distance(path[i], path[i + 1]);

                if (distance <= segmentDistance)
                {
                    return Vector2.Lerp(path[i], path[i + 1],
                        segmentDistance <= 0 ? 0 : distance / segmentDistance);
                }

                distance -= segmentDistance;
            }

            return path[path.Length - 1];
        }

        /// <summary>월드 좌표 경로의 전체 길이를 계산한다.</summary>
        private static float GetPathLength(Vector2[] path)
        {
            var result = 0f;

            for (var i = 0; i < path.Length - 1; i++)
            {
                result += Vector2.Distance(path[i], path[i + 1]);
            }

            return result;
        }

        /// <summary>
        /// 예상 만남 위치 근처에서 같은 Y를 사용하는 좌우 인접 셀을 찾는다.
        /// 동일 진영 캐릭터끼리 같은 셀을 사용하는 것은 허용한다.
        /// </summary>
        private bool TryFindCombatCells(BaseEntity leader, BaseEntity monster, Vector2 meetingPoint,
            out Vector2Int heroCell, out Vector2Int monsterCell)
        {
            heroCell = default;
            monsterCell = default;
            var centerCell = GroundHandler.WorldToCell(meetingPoint);
            var heroIsLeft = leader.CurrentWorldPosition.x <= monster.CurrentWorldPosition.x;

            for (var radius = 0; radius <= Constants.ENCOUNTER_CELL_SEARCH_RADIUS; radius++)
            {
                for (var x = -radius; x <= radius; x++)
                {
                    for (var y = -radius; y <= radius; y++)
                    {
                        if (radius > 0 && Mathf.Abs(x) != radius && Mathf.Abs(y) != radius)
                        {
                            continue;
                        }

                        var leftCell = centerCell + new Vector2Int(x, y);
                        var rightCell = leftCell + Vector2Int.right;

                        if (GroundHandler.IsWalkable(leftCell) == false || GroundHandler.IsWalkable(rightCell) == false)
                        {
                            continue;
                        }

                        heroCell = heroIsLeft ? leftCell : rightCell;
                        monsterCell = heroIsLeft ? rightCell : leftCell;
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// 상태가 매니저에서 계산된 타겟과 경로를 받을 때 사용한다.
        /// 계획이 없는 몬스터는 현재 전투 위치의 몬스터 셀로 개별 A* 경로를 만든다.
        /// </summary>
        /// <summary>캐릭터가 이번 교전에서 따라갈 A* 월드 경로를 반환한다.</summary>
        public bool TryGetMovePath(ICombatStateProvider entity, out Vector2[] path)
        {
            if (entity is Monster && IsTargetAlive(entity.EnemyTarget))
            {
                EngagedMonsters.Add(entity);
            }

            if (PlannedMovePaths.TryGetValue(entity, out path))
            {
                return true;
            }

            if (IsTargetAlive(entity.EnemyTarget) == false)
            {
                path = null;
                return false;
            }

            // 계획 이동이 끝난 뒤 새 타겟을 잡았을 때는 타겟의 현재 셀까지 다시 A*를 계산한다.
            // 이동 상태가 실제 공격 사거리에 진입하는 순간 경로 추적을 중단한다.
            var targetCell = GroundHandler.WorldToCell(entity.EnemyTarget.transform.position);
            path = CreateWorldPath(entity.CurrentWorldPosition, targetCell);
            return path != null;
        }

        /// <summary>캐릭터에게 지정된 교전 타겟이 아직 유효하면 반환한다.</summary>
        public bool TryGetPlannedTarget(ICombatStateProvider entity, out GameObject target)
        {
            if (PlannedMoveTargets.TryGetValue(entity, out target) == false ||
                IsTargetAlive(target) == false)
            {
                return false;
            }

            if (entity is not Monster || EngagedMonsters.Contains(entity))
            {
                return true;
            }

            var targetPosition = (Vector2)target.transform.position;

            if (Vector2.Distance(entity.CurrentWorldPosition, targetPosition) >
                Constants.CIRCLE_RADIUS_MONSTER)
            {
                return false;
            }

            EngagedMonsters.Add(entity);
            return true;
        }

        /// <summary>스폰된 타겟이 현재 전투 가능한 상태인지 확인한다.</summary>
        public bool IsTargetAlive(GameObject target)
        {
            return SpawnHandler.IsAlive(target);
        }

        /// <summary>두 캐릭터의 실제 월드 거리가 공격자의 전투 사거리 안인지 확인한다.</summary>
        public bool IsTargetInAttackRange(ICombatStateProvider attacker, GameObject target)
        {
            if (IsTargetAlive(target) == false)
            {
                return false;
            }

            var distance = Vector2.Distance(attacker.CurrentWorldPosition, target.transform.position);
            return distance <= attacker.CharacterData.CharDistanceToTarget +
                   Constants.ATTACK_RANGE_TOLERANCE;
        }

        /// <summary>계획 생성에 실패했을 때 살아 있는 현재 교전 인원을 기존 탐지 상태로 전환한다.</summary>
        private void ChangeAllCharactersToDetectState()
        {
            var heroes = SpawnHandler.AliveHeroes;
            var monsters = SpawnHandler.CurrentMonsterSpawnGroup;

            for (var i = 0; i < heroes.Count; i++)
            {
                heroes[i].ChangeState(CombatState.DetectTarget);
            }

            for (var i = 0; i < monsters.Count; i++)
            {
                monsters[i].ChangeState(CombatState.DetectTarget);
            }
        }

        /// <summary>
        /// 승패가 결정된 뒤 살아 있는 캐릭터의 이동과 공격을 멈춘다.
        /// 실제 결과 UI와 다음 씬 전환은 별도의 전투 종료 흐름에서 연결한다.
        /// </summary>
        private void ChangeAliveCharactersToWaitState()
        {
            var heroes = SpawnHandler.AliveHeroes;
            var monsters = SpawnHandler.AliveMonsters;

            for (var i = 0; i < heroes.Count; i++)
            {
                heroes[i].ChangeState(CombatState.Wait);
            }

            for (var i = 0; i < monsters.Count; i++)
            {
                monsters[i].ChangeState(CombatState.Wait);
            }
        }

        public WeaponProjectile SpawnProjectile(Transform parent, Vector3 spawnPos, SpriteLibrarys sprLibraryName, BaseEntity source, BaseEntity target)
        {
            return SpawnHandler.SpawnProjectile(parent, spawnPos, sprLibraryName, source, target);
        }

        public void DespawnProjectile(GameObject projectileObject)
        {
            SpawnHandler.DespawnProjectile(projectileObject);
        }

        public BaseEntity GetCharacterInstance(GameObject spawnObject)
        {
            return SpawnHandler.GetCharacterInstance(spawnObject);
        }

        public void OnEvent<TSender>(TSender sender, Event_CharacterHealthDepleted eventData) where TSender : class
        {
            if (eventData.Entity is not BaseEntity entity)
            {
                throw new InvalidOperationException("Health-depleted combat entity must inherit BaseEntity.");
            }

            entity.ChangeState(CombatState.Dead);
            PlannedMovePaths.Remove(entity);
            PlannedMoveTargets.Remove(entity);
            EngagedMonsters.Remove(entity);
            FormationHandler.Cancel();
            SpawnHandler.MarkHealthDepleted(entity);

            if (CreateEncounterPlan() == false)
            {
                ChangeAllCharactersToDetectState();
            }
        }

        public void OnEvent<TSender>(TSender sender, Event_CombatMoveCompleted eventData) where TSender : class
        {
            if (FormationHandler.TryMarkArrived(eventData.Entity, out var formationCompleted))
            {
                if (formationCompleted && CreateEncounterPlan(true) == false)
                {
                    ChangeAllCharactersToDetectState();
                }

                return;
            }

            PlannedMovePaths.Remove(eventData.Entity);
            PlannedMoveTargets.Remove(eventData.Entity);
        }

        private bool SetCombatVariables()
        {
            var parent = GameObject.FindGameObjectWithTag(StringConstants.TAG_CHARACTER_PARENT);

            if (parent == null)
            {
                ThreeSoulsLog.Error($"Not found with tag :  {StringConstants.TAG_CHARACTER_PARENT}");
                return false;
            }

            CharacterParent = parent.transform;
            CharacterParent.position = Vector3.zero;

            var heroSpawn = GameObject.FindGameObjectWithTag(StringConstants.TAG_SPAWN_HERO);

            if (heroSpawn == null)
            {
                ThreeSoulsLog.Error($"Not found with tag :  {StringConstants.TAG_SPAWN_HERO}");
                return false;
            }

            HeroSpawnPoint = heroSpawn.transform.position;

            var monSpawns = GameObject.FindGameObjectsWithTag(StringConstants.TAG_SPAWN_MONSTER);

            if (monSpawns == null || monSpawns.Length == 0)
            {
                ThreeSoulsLog.Error($"Not found with tag :  {StringConstants.TAG_SPAWN_MONSTER}");
                return false;
            }

            MonsterSpawnsPoint = monSpawns
                .OrderBy(spawn => spawn.name, StringComparer.Ordinal)
                .Select(spawn => spawn.transform.position)
                .ToArray();

            var camera = GameObject.FindGameObjectWithTag(StringConstants.TAG_FOLLOW_CAMERA);

            if (camera == null)
            {
                ThreeSoulsLog.Error($"Not found with tag :  {StringConstants.TAG_FOLLOW_CAMERA}");
                return false;
            }

            FollowCamera = camera.GetComponent<CinemachineCamera>();

            return true;
        }
    }
}
