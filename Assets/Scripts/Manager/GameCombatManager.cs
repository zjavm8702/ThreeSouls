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

namespace ThreeSouls.Manager
{
    /// <summary>
    /// 전투의 준비, 시작과 종료를 조립하고 담당 객체의 요청 및 이벤트를 연결합니다.
    /// </summary>
    public class GameCombatManager : BaseManager, 
        IManager, ICombatWorld, ICombatSkillWorld, ICombatRewardWorld,
        IEventListener<Event_CharacterHealthDepleted>, IEventListener<Event_CombatStageCompleted>
    {
        public int CallbackOrder => 0;

        private readonly CombatSkillHandler SkillHandler = new();
        private readonly CombatSpawnHandler SpawnHandler = new();
        private CombatSanctuaryHandler SanctuaryHandler;
        private RuneManager RuneSessionManager;
        public delegate void CombatSanctuaryChangedDelegate();
        private CombatSanctuaryChangedDelegate onSanctuaryChangedEventHandler;
        public event CombatSanctuaryChangedDelegate OnSanctuaryChangedEventHandler
        {
            add
            {
                onSanctuaryChangedEventHandler -= value;
                onSanctuaryChangedEventHandler += value;
            }
            remove
            {
                onSanctuaryChangedEventHandler -= value;
            }
        }
        private readonly CombatGroundHandler GroundHandler = new();
        private readonly CombatHealthBarHandler HealthBarHandler = new();
        private readonly CombatRewardHandler RewardHandler = new();
        private readonly CombatAssetLoader AssetLoader = new();
        private CombatNavigationHandler NavigationHandler;
        private CombatFormationHandler FormationHandler;
        private CombatRoundHandler RoundHandler;
        private CombatCameraHandler CameraHandler;
        private CombatTweenHandler TweenHandler;
        private CombatResultPresenter ResultPresenter;
        private RectTransform GoldRewardTarget;
        private Sprite GoldRewardIcon;
        private Transform CharacterParent;
        private Vector3 HeroSpawnPoint;
        private int RegionNumber = 1;
        private int StageNumber = 1;
        private ThreeSouls.Runes.RuneCombatSnapshot DepartureRunes;
        private CombatSupplyManager Supplies;
        private readonly CombatSupplyHandler SupplyHandler = new();
        private readonly Dictionary<Hero, Vector2> AudioMovementPositions = new();
        public delegate void ReturnToLobbyDelegate();
        private ReturnToLobbyDelegate ReturnToLobby;

        public void ConfigureDeparture(ThreeSouls.Dungeon.DungeonDeparturePlan plan, CombatSupplyManager supplies,
            ReturnToLobbyDelegate returnToLobby)
        {
            ConfigureStage(plan.RegionNumber, plan.StageNumber);
            DepartureRunes = plan.Runes;
            Supplies = supplies ?? throw new ArgumentNullException(nameof(supplies));
            ReturnToLobby = returnToLobby ?? throw new ArgumentNullException(nameof(returnToLobby));
        }

        public bool CanUseSupply(int index)
        {
            return SupplyHandler.CanUse(index);
        }

        public bool TryUseSupply(int index)
        {
            return SupplyHandler.TryUse(index);
        }

        public float GetSupplyCooldownRemaining()
        {
            return SupplyHandler.GetCooldownRemaining();
        }

        public static GameCombatManager Get()
        {
            return GameManager.Instance.Get<GameCombatManager>();
        }

        public override void Initialize()
        {
            NavigationHandler = new CombatNavigationHandler(GroundHandler, SpawnHandler);
            FormationHandler = new CombatFormationHandler(SpawnHandler, GroundHandler);
            SanctuaryHandler = new CombatSanctuaryHandler(SpawnHandler);
            SanctuaryHandler.OnSanctuaryChangedEventHandler += NotifySanctuaryChanged;
            RoundHandler = new CombatRoundHandler(SpawnHandler, GroundHandler, NavigationHandler, FormationHandler, this, SanctuaryHandler);
            CameraHandler = new CombatCameraHandler(this);
            FormationHandler.OnLeaderChangeEventHandler += CameraHandler.SetLeader;
            CameraHandler.OnPresentationCompletEventHandler += StartCombat;
            EventUtility.AddListener<Event_CharacterHealthDepleted>(this);
            EventUtility.AddListener<Event_CombatStageCompleted>(this);
        }

        public override void OnSceneChanged()
        {
            EndCombatSession();
        }

        public override void Release()
        {
            EndCombatSession();
            onSanctuaryChangedEventHandler = null;
            SanctuaryHandler.OnSanctuaryChangedEventHandler -= NotifySanctuaryChanged;
            FormationHandler.OnLeaderChangeEventHandler -= CameraHandler.SetLeader;
            CameraHandler.OnPresentationCompletEventHandler -= StartCombat;
            EventUtility.RemoveListener<Event_CharacterHealthDepleted>(this);
            EventUtility.RemoveListener<Event_CombatStageCompleted>(this);
        }

        private void EndCombatSession()
        {
            AudioMovementPositions.Clear();
            ResultPresenter?.Release();
            ResultPresenter = null;
            RewardHandler.Release();
            SanctuaryHandler.Release();
            RuneSessionManager?.EndCombatSession();
            RuneSessionManager = null;
            TweenHandler?.Release();
            TweenHandler = null;
            GoldRewardTarget = null;
            GoldRewardIcon = null;
            CameraHandler.Release();
            SkillHandler.Release();
            RoundHandler.Release();
            NavigationHandler.Release();
            SpawnHandler.Release();
            HealthBarHandler.Release();
            GroundHandler.Release();
            CharacterParent = null;
            SupplyHandler.Release();
            DepartureRunes = null;
            Supplies = null;
            ReturnToLobby = null;
        }

        /// <summary>
        /// 다음 전투의 지역과 스테이지를 지정합니다. 진행 중 변경은 호출 계약 위반입니다.
        /// </summary>
        public void ConfigureStage(int regionNumber, int stageNumber)
        {
            if (regionNumber < 1 || stageNumber < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(stageNumber), "지역과 스테이지 번호는 1 이상이어야 합니다.");
            }

            if (RoundHandler.RoundPhase != CombatRoundPhase.Preparing)
            {
                throw new InvalidOperationException("진행 중인 전투의 스테이지를 변경할 수 없습니다.");
            }

            RegionNumber = regionNumber;
            StageNumber = stageNumber;
        }

        public IEnumerator AssetsPreLoad()
        {
            return AssetLoader.PreLoad(RegionNumber, StageNumber);
        }

        public void SetUp()
        {
            var rule = TableManager.Get().GetThreeSoulsRuleDataTable();
            var ground = TableManager.Get().GetGroundTableData(RegionNumber, StageNumber);

            if (rule == null || ground == null)
            {
                throw new InvalidOperationException($"전투 테이블 누락 : Region {RegionNumber}, Stage {StageNumber}");
            }

            if (GoldRewardTarget == null)
            {
                throw new InvalidOperationException("전투 준비 전에 골드 보상 도착 지점을 연결해야 합니다.");
            }

            GoldRewardIcon = AddressableManager.Get().LoadAsset<Sprite>(Images.Icon_Gold.ToAddress());

            if (GoldRewardIcon == null)
            {
                throw new InvalidOperationException("골드 보상 표시 이미지가 필요합니다.");
            }

            var uiManager = UIManager.Get();
            var resultPrefab = AddressableManager.Get().LoadAsset<GameObject>(Prefabs.UI_Popup_Result.ToAddress());

            if (resultPrefab == null || resultPrefab.TryGetComponent<UI_Popup_Result>(out var resultOrigin) == false)
            {
                throw new InvalidOperationException("전투 결과 원본에 UI_Popup_Result가 필요합니다.");
            }

            ResultPresenter?.Release();
            ResultPresenter = new CombatResultPresenter(uiManager, resultOrigin, RequestReturnToLobby);
            var valuePrefab = AddressableManager.Get().LoadAsset<GameObject>(Prefabs.UI_Item_CombatValue.ToAddress());

            if (valuePrefab == null || valuePrefab.TryGetComponent<UI_Item_CombatValue>(out var valueOrigin) == false)
            {
                throw new InvalidOperationException("전투 수치 표시 원본에 UI_Item_CombatValue가 필요합니다.");
            }

            TweenHandler = new CombatTweenHandler(uiManager, valueOrigin, uiManager.GetItemCanvas(), Camera.main);
            RuneSessionManager = UserManager.Get().GetRuneManager();
            var runeSnapshot = DepartureRunes ?? RuneSessionManager.BeginCombatSession();
            SanctuaryHandler.Initialize(runeSnapshot);
            SkillHandler.Initialize(this, TableManager.Get().GetSkillTableData(), AddressableManager.Get(), runeSnapshot);
          
            HealthBarHandler.Initialize();
           
            UserManager.Get();
            SpawnHandler.Initialize(HealthBarHandler, EquipmentManager.Get(), UserManager.Get().GetHeroGrowthManager(), this);
           
            GroundHandler.CreateStageMap(ground.MapName);
           
            SetUpSceneReferences(out var holyPlaces, out var camera);
           
            SpawnHandler.SpawnHeroes(GroundHandler, HeroSpawnPoint, CharacterParent, this, rule);
            if (Supplies != null)
            {
                SupplyHandler.Initialize(Supplies, SpawnHandler.GetAliveHeroes());
            }
           
            RoundHandler.Initialize(ground, rule, CharacterParent, HeroSpawnPoint, holyPlaces.Select(place => place.position).ToArray(), RegionNumber, StageNumber);
            RewardHandler.Initialize(StageNumber, rule.SpecialItemDropMultiplier, runeSnapshot, RegionNumber);
         
            SpawnHandler.RefreshEnemyTargets();
          
            var settings = AddressableManager.Get().LoadAsset<CameraPresentationData>(ScriptableObjectDatas.CameraPresentationData.ToAddress());
          
            CameraHandler.Initialize(camera, GroundHandler.CameraBounds, holyPlaces, settings);
            CameraHandler.SetLeader(FormationHandler.Leader);
            CameraHandler.BeginPresentation();
        }

        public void SpawnDamageText(Vector3 spawnPos, float damage)
        {
            SpawnDamageText(spawnPos, new ThreeSouls.Combat.CombatDamageInfo(damage));
        }

        public void SpawnDamageText(Vector3 spawnPos, ThreeSouls.Combat.CombatDamageInfo damage)
        {
            if(damage.Amount > 0) ThreeSouls.Audio.GameAudio.Play(ThreeSouls.Audio.SoundCue.Hit);
            TweenHandler.SpawnDamageText(spawnPos, damage);
        }

        public void ConfigureRewardTarget(RectTransform target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            GoldRewardTarget = target;
        }

        public void SpawnRewardText(Monster monster)
        {
            var gold = RewardHandler.GrantMonsterReward(monster);

            if (gold > 0)
            {
                ThreeSouls.Combat.GoldWorldDrop.Create(monster.transform.position, GoldRewardIcon, gold);
            }
        }

        public void StartCombat()
        {
            if (RoundHandler.RoundPhase != CombatRoundPhase.Preparing)
            {
                return;
            }

            CameraHandler.StopPresentation();
            RewardHandler.BeginCombat();
            SkillHandler.StartCombat();
            RoundHandler.StartRound(0);
            ThreeSouls.Audio.GameAudio.BeginBattle();
        }

        private void Update()
        {
            if (Time.timeScale <= 0) return;
            SupplyHandler.UpdateAutoHeal();
            SkillHandler.Update();
            RoundHandler.Update();
        }

        public IReadOnlyList<BaseEntity> GetAliveHeroes()
        {
            return SpawnHandler.GetAliveHeroes();
        }

        public BaseEntity GetCharacterInstance(GameObject spawnObject)
        {
            return SpawnHandler.GetCharacterInstance(spawnObject);
        }

        public void OnEvent<TSender>(TSender sender, Event_CharacterHealthDepleted eventData)
            where TSender : class
        {
            if (eventData.Entity is not BaseEntity entity)
            {
                throw new InvalidOperationException("체력 소진 이벤트의 대상은 BaseEntity여야 합니다.");
            }

            entity.ChangeState(CombatState.Dead);
            NavigationHandler.RemoveEntity(entity);
            SpawnHandler.MarkHealthDepleted(entity);
            ThreeSouls.Audio.GameAudio.Play(ThreeSouls.Audio.SoundCue.Death);
        }

        public void OnEvent<TSender>(TSender sender, Event_CombatStageCompleted eventData) where TSender : class
        {
            if (ReferenceEquals(sender, RoundHandler) == false)
            {
                return;
            }

            var outcome = eventData.IsVictory == true ? CombatOutcome.Victory : CombatOutcome.Defeat;
            var result = RewardHandler.Complete(eventData.RegionNumber, eventData.StageNumber, outcome);
            Supplies?.ReturnUnused();
            ThreeSouls.Persistence.PersistentSession.Current?.CompleteCombat(result,eventData.IsVictory);
            RuneSessionManager.EndCombatSession();
            ResultPresenter.ShowResult(result, outcome);
            ThreeSouls.Audio.GameAudio.CompleteBattle(eventData.IsVictory);
            SupplyHandler.Stop();
        }

        private void RequestReturnToLobby()
        {
            ReturnToLobby?.Invoke();
        }

        public float ElapsedCombatSeconds => RewardHandler.GetElapsedSeconds();

        public bool CanEngageTarget(ICombatStateProvider entity, GameObject target)
        {
            return RoundHandler.CanEngageTarget(entity, target);
        }

        public bool TryGetDestination(ICombatStateProvider entity, out Vector2 destination)
        {
            return FormationHandler.TryGetDestination(entity, out destination);
        }

        public MovePathResult GetDestinationPath(ICombatStateProvider entity, Vector2 destination, out Vector2[] path)
        {
            return NavigationHandler.GetDestinationPath(entity, destination, out path);
        }

        public bool CanMoveDirectly(ICombatStateProvider entity, Vector2 destination)
        {
            return NavigationHandler.CanMoveDirectly(entity, destination);
        }

        public MovePathResult GetMovePath(ICombatStateProvider entity, out Vector2[] path)
        {
            return NavigationHandler.GetMovePath(entity, out path);
        }

        public bool MoveTowards(ICombatStateProvider entity, Vector2 destination, float moveSpeed)
        {
            var before = entity.GetWorldPosition();
            var moved = NavigationHandler.MoveTowards(entity, destination, moveSpeed);
            if (entity is Hero hero)
            {
                // Rigidbody2D.MovePosition commits in physics, so compare observed positions across calls.
                if (AudioMovementPositions.TryGetValue(hero, out var previous) && Vector2.Distance(previous, before) > .001f)
                    ThreeSouls.Audio.GameAudio.Step(RegionNumber);
                AudioMovementPositions[hero] = before;
            }
            return moved;
        }

        public bool IsTargetExcluded(ICombatStateProvider entity, GameObject target)
        {
            return NavigationHandler.IsTargetExcluded(entity, target);
        }

        public void ExcludeTarget(ICombatStateProvider entity, GameObject target)
        {
            NavigationHandler.ExcludeTarget(entity, target);
        }

        public Vector2 GetTargetPosition(GameObject target)
        {
            return NavigationHandler.GetTargetPosition(target);
        }

        public bool IsTargetAlive(GameObject target)
        {
            return NavigationHandler.IsTargetAlive(target);
        }

        public bool IsTargetInAttackRange(ICombatStateProvider attacker, GameObject target)
        {
            return NavigationHandler.IsTargetInAttackRange(attacker, target);
        }

        public bool IsCombatActive()
        {
            var phase = RoundHandler.RoundPhase;

            return phase == CombatRoundPhase.RoundCombat ||
                phase == CombatRoundPhase.HolyPlaceCombat ||
                phase == CombatRoundPhase.HolyPlaceCleanup;
        }

        public string GetSanctuaryDescription()
        {
            return SanctuaryHandler.GetDescription();
        }

        private void NotifySanctuaryChanged()
        {
            onSanctuaryChangedEventHandler?.Invoke();
        }

        /// <summary>
        /// HUD에서 사용할 스킬 테이블 행을 조회합니다.
        /// </summary>
        public IReadOnlyList<SkillTableDataRow> GetSkills()
        {
            return SkillHandler.GetSkills();
        }

        /// <summary>
        /// 준비 단계에서 로드한 스킬 아이콘을 조회합니다.
        /// </summary>
        public Sprite GetSkillIcon(string key)
        {
            return SkillHandler.GetSkillIcon(key);
        }

        /// <summary>
        /// 자동 사용과 인게임 검증에서 동일한 발동 조건을 거쳐 스킬을 사용합니다.
        /// </summary>
        public bool TryUseSkill(string key)
        {
            return SkillHandler.TryUseSkill(key);
        }

        /// <summary>
        /// HUD에 표시할 남은 시작 딜레이 또는 재사용 대기 시간을 조회합니다.
        /// </summary>
        public float GetSkillCooldownRemaining(string key)
        {
            return SkillHandler.GetCooldownRemaining(key);
        }

        private void SetUpSceneReferences(out Transform[] holyPlaces, out CinemachineCamera camera)
        {
            var parent = GameObject.FindGameObjectWithTag(StringConstants.TAG_CHARACTER_PARENT);
            var heroSpawn = GameObject.FindGameObjectWithTag(StringConstants.TAG_SPAWN_HERO);
            var monsterSpawns = GameObject.FindGameObjectsWithTag(StringConstants.TAG_SPAWN_MONSTER);
            var cameraObject = GameObject.FindGameObjectWithTag(StringConstants.TAG_FOLLOW_CAMERA);

            if (parent == null || heroSpawn == null || monsterSpawns.Length == 0 || cameraObject == null)
            {
                throw new InvalidOperationException("전투 씬의 캐릭터 부모, 영웅/성소 스폰 지점 및 추적 카메라 태그를 확인하세요.");
            }

            camera = cameraObject.GetComponent<CinemachineCamera>();
            CharacterParent = parent.transform;
            CharacterParent.position = Vector3.zero;
            HeroSpawnPoint = heroSpawn.transform.position;
            holyPlaces = monsterSpawns.OrderBy(spawn => spawn.name, StringComparer.Ordinal)
                .Select(spawn => spawn.transform).ToArray();
        }
    }
}
