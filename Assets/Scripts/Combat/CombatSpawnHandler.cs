using System;
using System.Collections.Generic;
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
    public sealed class CombatSpawnHandler : IEventListener<Event_DeathAnimationCompleted>
    {
        public int CallbackOrder => 0;
        public int CurrentMonsterSpawnGroupIndex { get; private set; }
        public int MonsterSpawnGroupCount => MonsterSpawnGroups.Count;
        public IReadOnlyList<BaseEntity> AliveHeroes => AliveHeroContainer;
        public IReadOnlyList<BaseEntity> AliveMonsters => AliveMonsterContainer;
        public IReadOnlyList<BaseEntity> CurrentMonsterSpawnGroup =>
            MonsterSpawnGroups.Count == 0
                ? Array.Empty<BaseEntity>()
                : MonsterSpawnGroups[CurrentMonsterSpawnGroupIndex];

        private readonly List<BaseEntity> SpawnCharacterContainer = new();
        private readonly List<WeaponProjectile> SpawnProjectileContainer = new();
        private readonly List<BaseEntity> AliveHeroContainer = new();
        private readonly List<BaseEntity> AliveMonsterContainer = new();
        private readonly Dictionary<GameObject, BaseEntity> CharacterInstanceContainer = new();
        private readonly List<List<BaseEntity>> MonsterSpawnGroups = new();
        private readonly Dictionary<CharacterType, ComponentPool<BaseEntity>> CharacterPoolContainer = new();
        private readonly ComponentPool<WeaponProjectile> ProjectilePool = new();

        private CombatHealthBarHandler HealthBarHandler = null;
        private bool IsInitialized;

        public void Initialize(CombatHealthBarHandler healthBarHandler)
        {
            if (IsInitialized == true)
            {
                return;
            }

            HealthBarHandler = healthBarHandler ?? throw new ArgumentNullException(nameof(healthBarHandler));
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
            IsInitialized = false;
        }

        public BaseEntity SpawnCharacter(in CharacterSpawnRequest request)
        {
            var pool = CharacterPoolContainer[request.CharacterType];
            var spriteLibrary = GetSpriteLibraryAsset(request.SpriteLibraryName);
            var entity = pool.Spawn(request.Parent, request.Position);

            entity.SetUp(request.CharacterData, request.RuleData, spriteLibrary, request.CombatWorld);
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
                RegisterMonsterGroup(entity, request.MonsterGroupIndex);
            }

            entity.ChangeState(CombatState.Wait);
            return entity;
        }

        public WeaponProjectile SpawnProjectile(Transform parent, Vector3 spawnPosition, SpriteLibrarys sprLibraryName, BaseEntity source, BaseEntity target)
        {
            var projectile = ProjectilePool.Spawn(parent, spawnPosition);
            SpawnProjectileContainer.Add(projectile);
            var asset = GetSpriteLibraryAsset(sprLibraryName.ToString());
            projectile.SetUp(source, target, asset, source.CharacterData.CharacterAttackType, DespawnProjectile);
            return projectile;
        }

        public void DespawnProjectile(GameObject projectileObject)
        {
            var projectile = GetProjectileInstance(projectileObject);

            if (projectile == null)
            {
                return;
            }

            projectile.Despawn();
        }

        private void DespawnProjectile(WeaponProjectile projectile)
        {
            if (projectile == null || SpawnProjectileContainer.Remove(projectile) == false)
            {
                return;
            }

            projectile.PrepareForRecycle();
            ProjectilePool.Recycle(projectile);
        }

        public BaseEntity GetCharacterInstance(GameObject spawnObject)
        {
            return spawnObject != null && CharacterInstanceContainer.TryGetValue(spawnObject, out var entity)
                ? entity
                : null;
        }

        public WeaponProjectile GetProjectileInstance(GameObject spawnObject)
        {
            return SpawnProjectileContainer.Find(projectile => projectile.gameObject == spawnObject);
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

        public void ResetMonsterGroups()
        {
            MonsterSpawnGroups.Clear();
            CurrentMonsterSpawnGroupIndex = 0;
        }

        public IReadOnlyList<BaseEntity> GetMonsterSpawnGroup(int index)
        {
            return MonsterSpawnGroups[index];
        }

        public bool TryAdvanceMonsterGroup()
        {
            if (CurrentMonsterSpawnGroup.Count > 0 ||
                CurrentMonsterSpawnGroupIndex + 1 >= MonsterSpawnGroups.Count)
            {
                return false;
            }

            CurrentMonsterSpawnGroupIndex++;
            RefreshEnemyTargets();
            return true;
        }

        public void MarkHealthDepleted(BaseEntity entity)
        {
            if (entity.CharHealth.IsAlive)
            {
                throw new InvalidOperationException($"Entity health is not depleted: {entity.name}");
            }

            if (entity is Hero)
            {
                AliveHeroContainer.Remove(entity);
            }
            else
            {
                AliveMonsterContainer.Remove(entity);

                for (var i = 0; i < MonsterSpawnGroups.Count; i++)
                {
                    MonsterSpawnGroups[i].Remove(entity);
                }
            }

            RefreshEnemyTargets();
        }

        public void RefreshEnemyTargets()
        {
            var heroTargets = CreateTargetArray(AliveHeroContainer);
            var monsterTargets = CreateTargetArray(CurrentMonsterSpawnGroup);

            for (var i = 0; i < AliveHeroContainer.Count; i++)
            {
                AliveHeroContainer[i].EnemySpawnTargets = monsterTargets;
            }

            for (var i = 0; i < AliveMonsterContainer.Count; i++)
            {
                var monster = AliveMonsterContainer[i];
                monster.EnemySpawnTargets = heroTargets;
                monster.ChangeState(CombatState.Wait);
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

        private void RegisterMonsterGroup(BaseEntity monster, int groupIndex)
        {
            if (groupIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(groupIndex));
            }

            while (MonsterSpawnGroups.Count <= groupIndex)
            {
                MonsterSpawnGroups.Add(new List<BaseEntity>());
            }

            MonsterSpawnGroups[groupIndex].Add(monster);
        }

        private static GameObject[] CreateTargetArray(IReadOnlyList<BaseEntity> entities)
        {
            var result = new GameObject[entities.Count];

            for (var i = 0; i < entities.Count; i++)
            {
                result[i] = entities[i].gameObject;
            }

            return result;
        }

        private void InitializePool()
        {
            var heroEntity = LoadRequiredComponent<BaseEntity>(Prefabs.Hero.ToAddress());
            var monsterEntity = LoadRequiredComponent<BaseEntity>(Prefabs.Monster.ToAddress());
            var projectile = LoadRequiredComponent<WeaponProjectile>(Prefabs.Projectile.ToAddress());
            var heroPool = new ComponentPool<BaseEntity>();
            var monsterPool = new ComponentPool<BaseEntity>();

            heroPool.CreatePool(heroEntity, 3);
            monsterPool.CreatePool(monsterEntity, 10);
            ProjectilePool.CreatePool(projectile, 10);
            CharacterPoolContainer.Add(CharacterType.Hero, heroPool);
            CharacterPoolContainer.Add(CharacterType.Monster, monsterPool);
        }

        private static T LoadRequiredComponent<T>(string address) where T : Component
        {
            var asset = AddressableManager.Get().LoadAsset<GameObject>(address);

            if (asset == null)
            {
                throw new InvalidOperationException($"Required combat asset was not loaded: {address}");
            }

            if (asset.TryGetComponent<T>(out var component) == false)
            {
                throw new InvalidOperationException(
                    $"Required component {typeof(T).Name} was not found: {address}");
            }

            return component;
        }

        private static SpriteLibraryAsset GetSpriteLibraryAsset(string libraryName)
        {
            if (Enum.TryParse<SpriteLibrarys>(libraryName, true, out var key) == false)
            {
                throw new InvalidOperationException($"SpriteLibrary key does not match: {libraryName}");
            }

            var address = key.ToAddress();
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
                HealthBarHandler.Unregister(SpawnCharacterContainer[i]);
            }

            foreach (var pool in CharacterPoolContainer.Values)
            {
                pool.Release();
            }

            CharacterPoolContainer.Clear();
            SpawnCharacterContainer.Clear();
            AliveHeroContainer.Clear();
            AliveMonsterContainer.Clear();
            CharacterInstanceContainer.Clear();
            MonsterSpawnGroups.Clear();
            CurrentMonsterSpawnGroupIndex = 0;
        }

        private void ProjectileRelease()
        {
            ProjectilePool.Release();
            SpawnProjectileContainer.Clear();
        }
    }
}
