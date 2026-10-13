using System;
using ThreeSouls.Character;
using ThreeSouls.Data;
using ThreeSouls.Interface;
using UnityEngine;
using UnityEngine.U2D.Animation;

namespace ThreeSouls.Projectile
{
    public class WeaponProjectile : MonoBehaviour, IProjectileStateProvider
    {

        private BaseEntity Source = null;
        private BaseEntity Target = null;
        private CharacterCombatData SourceData;
        private CharacterCombatData TargetData;
        private Action<WeaponProjectile> DespawnCallback = null;
        private ProjectileController ProjectileController = null;
        private Rigidbody2D ProjectileRigid2D = null;
        private SpriteLibrary ProjectileSpriteLibrary = null;
        private SpriteRenderer ProjectileSpriteRenderer = null;
        private bool IsDespawnRequested;

        private void Awake()
        {
            ProjectileRigid2D = GetComponent<Rigidbody2D>();
            ProjectileController = GetComponent<ProjectileController>();
            ProjectileSpriteLibrary = GetComponent<SpriteLibrary>();
            ProjectileSpriteRenderer = GetComponent<SpriteRenderer>();
        }

        public void SetUp(
            BaseEntity source,
            BaseEntity target,
            SpriteLibraryAsset libraryAsset,
            Action<WeaponProjectile> despawnCallback)
        {
            Source = source;
            Target = target;
            SourceData = source.CharacterData;
            TargetData = target.CharacterData;
            DespawnCallback = despawnCallback;
            IsDespawnRequested = false;
            ProjectileSpriteLibrary.spriteLibraryAsset = libraryAsset;
            ProjectileController.BeginFlight(this);
        }

        public void Despawn()
        {
            if (IsDespawnRequested == true)
            {
                return;
            }

            IsDespawnRequested = true;
            ProjectileController.Stop();
            DespawnCallback(this);
        }

        public void PrepareForRecycle()
        {
            Source = null;
            Target = null;
            DespawnCallback = null;
            SourceData = null;
            TargetData = null;
            IsDespawnRequested = true;
        }

        public void ApplyHit()
        {
            if (IsDespawnRequested == true || Source == null || Source.CharacterData != SourceData ||
                Source.CharHealth.IsAlive == false || IsTargetAlive() == false)
            {
                return;
            }

            ThreeSouls.Combat.CombatHitResolver.ApplyDamage(Source, Target, Source.CharacterData.CharAttackDmg);
        }

        public Vector2 GetWorldPosition()
        {
            return ProjectileRigid2D.position;
        }

        public Vector2 GetTargetWorldPosition()
        {
            return Target.GetWorldPosition();
        }

        public bool IsTargetAlive()
        {
            return Target != null && Target.CharacterData == TargetData && Target.CharHealth.IsAlive == true;
        }

        public void SetPosition(Vector3 pos)
        {
            ProjectileRigid2D.position = pos;
        }

        /// <summary>
        /// 왼쪽을 향한 투사체 원본을 타깃 방향으로 회전합니다.
        /// </summary>
        public void SetDirection(Vector2 direction)
        {
            if (direction.sqrMagnitude == 0)
            {
                return;
            }

            ProjectileSpriteRenderer.flipX = false;
            ProjectileRigid2D.rotation = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 180f;
        }
    }
}
