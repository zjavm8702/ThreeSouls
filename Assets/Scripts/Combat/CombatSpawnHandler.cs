using System;
using System.Collections.Generic;
using System.Linq;
using ThreeSouls.Data.Generated;
using ThreeSouls.Character;
using ThreeSouls.Data;
using ThreeSouls.Enumeration;
using ThreeSouls.Enumeration.AddressableKey;
using ThreeSouls.Interface;
using ThreeSouls.Manager;
using ThreeSouls.Projectile;
using ThreeSouls.Utility;
using UnityEngine;
using UnityEngine.U2D.Animation;

namespace ThreeSouls.Combat
{
    /// <summary>
    /// 전투 객체의 풀, 스폰 설정, 활성/생존 목록과 최종 풀 반환을 소유합니다.
    /// </summary>
    internal sealed class CombatSpawnHandler : IEventListener<Event_DeathAnimationCompleted>, IProjectileSpawner
    {
        public int CallbackOrder => 0;

        private readonly List<BaseEntity> SpawnCharacterContainer = new();
        private readonly List<WeaponProjectile> SpawnProjectileContainer = new();
        private readonly List<BaseEntity> AliveHeroContainer = new();
        private readonly List<BaseEntity> AliveMonsterContainer = new();
        private readonly Dictionary<GameObject, BaseEntity> CharacterInstanceContainer = new();
        private readonly Dictionary<CharacterType, ComponentPool<BaseEntity>> CharacterPoolContainer = new();
        private readonly ComponentPool<WeaponProjectile> ProjectilePool = new();
        private CombatHealthBarHandler HealthBarHandler = null;
        private bool IsInitialized;
        private IHeroEquipmentSource EquipmentSource;
        private IHeroGrowthSource GrowthSource;
        private ICombatRewardWorld RewardWorld;
        private readonly IReadOnlyList<BaseEntity> AliveHeroesView;
        private readonly IReadOnlyList<BaseEntity> AliveMonstersView;

        /// <summary>
        /// 현재 생존한 영웅 목록을 복사 없이 조회한다.
        /// </summary>
        public IReadOnlyList<BaseEntity> GetAliveHeroes()
        {
            return AliveHeroesView;
        }

        /// <summary>
        /// 현재 생존한 몬스터 목록을 복사 없이 조회한다.
        /// </summary>
        public IReadOnlyList<BaseEntity> GetAliveMonsters()
        {
            return AliveMonstersView;
        }

        public CombatSpawnHandler()
        {
            AliveHeroesView = AliveHeroContainer.AsReadOnly();
            AliveMonstersView = AliveMonsterContainer.AsReadOnly();
        }

        public void Initialize(CombatHealthBarHandler healthBarHandler, IHeroEquipmentSource equipmentSource,
            IHeroGrowthSource growthSource, ICombatRewardWorld rewardWorld)
        {
            if (IsInitialized == true)
            {
                return;
            }

            HealthBarHandler = healthBarHandler ?? throw new ArgumentNullException(nameof(healthBarHandler));
            EquipmentSource = equipmentSource ?? throw new ArgumentNullException(nameof(equipmentSource));
            GrowthSource = growthSource ?? throw new ArgumentNullException(nameof(growthSource));
            RewardWorld = rewardWorld ?? throw new ArgumentNullException(nameof(rewardWorld));
            InitializePool();
            EventUtility.AddListener(this);
            IsInitialized = true;
        }

        public void Release()
        {
            if (IsInitialized == false)
            {
                return;
            }

            EventUtility.RemoveListener(this);
            CharacterRelease();
            ProjectileRelease();
            HealthBarHandler = null;
            EquipmentSource = null;
            GrowthSource = null;
            RewardWorld = null;
            IsInitialized = false;
        }

        internal void SpawnHeroes(CombatGroundHandler ground, Vector3 spawnPoint, Transform parent, ICombatWorld world, ThreeSoulsRuleDataRow ruleTableData)
        {
            var maxPlayer = ruleTableData.MaxCharacterCombat;
            var origin = AddressableManager.Get().LoadAsset<GameObject>(Prefabs.Hero.ToAddress());
            var collider = origin.GetComponent<CircleCollider2D>();
            var radius = collider.radius * Mathf.Max(Mathf.Abs(origin.transform.lossyScale.x), Mathf.Abs(origin.transform.lossyScale.y));
            var cells = ground.GetWalkableCellsAround(spawnPoint, 4)
                .Where(cell => ground.CanMove(ground.CellToWorldCenter(cell), ground.CellToWorldCenter(cell), radius, Vector2.zero))
                .OrderBy(cell => Vector2.SqrMagnitude((Vector2)ground.CellToWorldCenter(cell) - (Vector2)spawnPoint))
                .ToArray();

            if (cells.Length < maxPlayer)
            {
                throw new InvalidOperationException("영웅 스폰 공간 부족");
            }

            for (int i = 0; i < maxPlayer; i++)
            {
                var pos = ground.CellToWorldCenter(cells[i]);
                var characterTableData = TableManager.Get().GetCharacterTableDataTable(ruleTableData.BaseCharacterID[i]);
                var request = new CharacterSpawnRequest(
                    CharacterType.Hero,
                    characterTableData,
                    ruleTableData,
                    characterTableData.ResourceName,
                    parent,
                    pos,
                    world);
                SpawnCharacter(request);
            }
        }

        public BaseEntity SpawnCharacter(in CharacterSpawnRequest request)
        {
            var pool = CharacterPoolContainer[request.CharacterType];
            var spriteLibrary = GetSpriteLibraryAsset(request.SpriteLibraryName);
            var entity = pool.Spawn(request.Parent, request.Position);

            if (entity is Monster monster)
            {
                monster.BindRewardWorld(RewardWorld);
            }

            entity.SetUp(request.CharacterData, request.RuleData, spriteLibrary, request.CombatWorld, this);

            if (entity is Hero hero)
            {
                hero.BindEquipment(EquipmentSource, GrowthSource, request.CharacterData, request.RuleData);
            }

            SpawnCharacterContainer.Add(entity);
            CharacterInstanceContainer.Add(entity.gameObject, entity);
            HealthBarHandler.Register(entity);

            if (request.CharacterType == CharacterType.Hero)
            {
                AliveHeroContainer.Add(entity);
            }
            else
            {
                AliveMonsterContainer.Add(entity);
            }

            entity.ChangeState(CombatState.Wait);

            return entity;
        }

        public WeaponProjectile SpawnProjectile(
            Transform parent,
            Vector3 spawnPosition,
            SpriteLibrarys sprLibraryName,
            BaseEntity source,
            BaseEntity target)
        {
            var projectile = ProjectilePool.Spawn(parent, spawnPosition);
            SpawnProjectileContainer.Add(projectile);
            var asset = GetSpriteLibraryAsset(sprLibraryName.ToString());
            projectile.SetUp(source, target, asset, DespawnProjectile);

            return projectile;
        }

        private void DespawnProjectile(WeaponProjectile projectile)
        {
            if (SpawnProjectileContainer.Remove(projectile) == false)
            {
                throw new InvalidOperationException("활성 투사체 목록에 없는 객체의 반환 요청입니다.");
            }

            projectile.PrepareForRecycle();
            ProjectilePool.Recycle(projectile);
        }

        public BaseEntity GetCharacterInstance(GameObject spawnObject)
        {
            return spawnObject != null && CharacterInstanceContainer.TryGetValue(spawnObject, out var entity) == true ? entity : null;
        }

        public bool IsAlive(GameObject target)
        {
            var entity = GetCharacterInstance(target);

            if (entity == null)
            {
                return false;
            }

            return entity.CharHealth.IsAlive;
        }

        public void MarkHealthDepleted(BaseEntity entity)
        {
            if (entity.CharHealth.IsAlive == true)
            {
                throw new InvalidOperationException($"Entity health is not depleted: {entity.name}");
            }

            if (entity is Hero)
            {
                if (AliveHeroContainer.Remove(entity) == false)
                {
                    throw new InvalidOperationException($"생존 목록에 없는 영웅의 체력 소진 이벤트 : {entity.name}");
                }
            }
            else
            {
                if (AliveMonsterContainer.Remove(entity) == false)
                {
                    throw new InvalidOperationException($"생존 목록에 없는 몬스터의 체력 소진 이벤트 : {entity.name}");
                }
            }

            RefreshEnemyTargets();
        }

        public void RefreshEnemyTargets()
        {
            var heroTargets = CreateTargetArray(AliveHeroContainer);
            var monsterTargets = CreateTargetArray(GetAliveMonsters());

            for (var i = 0; i < AliveHeroContainer.Count; i++)
            {
                AliveHeroContainer[i].SetEnemySpawnTargets(monsterTargets);
            }

            for (var i = 0; i < AliveMonsterContainer.Count; i++)
            {
                var monster = AliveMonsterContainer[i];
                monster.SetEnemySpawnTargets(heroTargets);
            }
        }

        public void OnEvent<TSender>(TSender sender, Event_DeathAnimationCompleted eventData)
            where TSender : class
        {
            if (eventData.Entity is not BaseEntity entity)
            {
                throw new InvalidOperationException("Death event entity must be BaseEntity.");
            }

            DespawnCharacter(entity);
        }

        private void DespawnCharacter(BaseEntity entity)
        {
            if (SpawnCharacterContainer.Remove(entity) == false)
            {
                throw new InvalidOperationException($"Entity is not registered as spawned: {entity.name}");
            }

            CharacterInstanceContainer.Remove(entity.gameObject);
            HealthBarHandler.Unregister(entity);
            entity.Despawn();
            CharacterPoolContainer[entity.CharacterData.CharacterType].Recycle(entity);
        }

        private static IReadOnlyList<GameObject> CreateTargetArray(IReadOnlyList<BaseEntity> entities)
        {
            var result = new GameObject[entities.Count];

            for (var i = 0; i < entities.Count; i++)
            {
                result[i] = entities[i].gameObject;
            }

            return Array.AsReadOnly(result);
        }

        private void InitializePool()
        {
            var heroEntity = LoadRequiredComponent<Hero>(Prefabs.Hero.ToAddress());
            var monsterEntity = LoadRequiredComponent<Monster>(Prefabs.Monster.ToAddress());
            var projectile = LoadRequiredComponent<WeaponProjectile>(Prefabs.Projectile.ToAddress());
            var heroPool = new ComponentPool<BaseEntity>();
            var monsterPool = new ComponentPool<BaseEntity>();
            heroPool.CreatePool(heroEntity, 3);
            monsterPool.CreatePool(monsterEntity, 10);
            ProjectilePool.CreatePool(projectile, 10);
            CharacterPoolContainer.Add(CharacterType.Hero, heroPool);
            CharacterPoolContainer.Add(CharacterType.Monster, monsterPool);
        }

        private static T LoadRequiredComponent<T>(string address)
            where T : Component
        {
            var asset = AddressableManager.Get().LoadAsset<GameObject>(address);

            if (asset == null)
            {
                throw new InvalidOperationException($"Required combat asset was not loaded: {address}");
            }

            if (asset.TryGetComponent<T>(out var component) == false)
            {
                throw new InvalidOperationException($"Required component {typeof(T).Name} was not found: {address}");
            }

            return component;
        }

        public static string GetSpriteLibraryAddress(string libraryName)
        {
            if (Enum.TryParse<SpriteLibrarys>(libraryName, true, out var key) == true)
            {
                return key.ToAddress();
            }

            return $"Assets/SpriteLibrarys/Character/{libraryName}.spriteLib";
        }

        private static SpriteLibraryAsset GetSpriteLibraryAsset(string libraryName)
        {
            var address = GetSpriteLibraryAddress(libraryName);
            var spriteLibrary = AddressableManager.Get().LoadAsset<SpriteLibraryAsset>(address);

            if (spriteLibrary == null)
            {
                throw new InvalidOperationException($"SpriteLibrary was not loaded: {address}");
            }

            return spriteLibrary;
        }

        private void CharacterRelease()
        {
            for (var i = 0; i < SpawnCharacterContainer.Count; i++)
            {
                SpawnCharacterContainer[i].Despawn();
                HealthBarHandler.Unregister(SpawnCharacterContainer[i]);
            }

            using var pools = CharacterPoolContainer.Values.GetEnumerator();

            while (pools.MoveNext() == true)
            {
                pools.Current.Release();
            }

            CharacterPoolContainer.Clear();
            SpawnCharacterContainer.Clear();
            AliveHeroContainer.Clear();
            AliveMonsterContainer.Clear();
            CharacterInstanceContainer.Clear();
        }

        private void ProjectileRelease()
        {
            ProjectilePool.Release();
            SpawnProjectileContainer.Clear();
        }
    }
}
