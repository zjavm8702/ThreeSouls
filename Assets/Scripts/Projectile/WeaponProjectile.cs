using System;
using ThreeSouls.Character;
using ThreeSouls.Enumeration;
using ThreeSouls.Enumeration.AddressableKey;
using ThreeSouls.Interface;
using ThreeSouls.Utility;
using UnityEngine;
using UnityEngine.U2D.Animation;

namespace ThreeSouls.Projectile
{
    public class WeaponProjectile : MonoBehaviour, IProjectileStateProvider
    {
        public Vector2 CurrentWorldPosition => ProjectileRigid2D.position;
        public Vector2 TargetWorldPosition => Target.CurrentWorldPosition;
        public bool IsTargetAlive => Target != null && Target.CharHealth.IsAlive;
        private BaseEntity Source = null;
        private BaseEntity Target = null;
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
            ProjectileController.Initialize(this);
        }

        public void SetUp(BaseEntity source, BaseEntity target, SpriteLibraryAsset libraryAsset, AttackType attackType, Action<WeaponProjectile> despawnCallback)
        {
            Source = source;
            Target = target;
            DespawnCallback = despawnCallback;
            IsDespawnRequested = false;
            ProjectileSpriteLibrary.spriteLibraryAsset = libraryAsset;

            ProjectileController.IsPlay = true;
            ProjectileController.ChangeState(CombatState.MoveToTarget);
        }

        public void Despawn()
        {
            if (IsDespawnRequested)
            {
                return;
            }

            IsDespawnRequested = true;
            ProjectileController.IsPlay = false;
            DespawnCallback?.Invoke(this);
        }

        public void PrepareForRecycle()
        {
            Source = null;
            Target = null;
            DespawnCallback = null;
        }

        public void ApplyHit()
        {
            if (Source == null || IsTargetAlive == false)
            {
                return;
            }

            Target.CharHealth.OnDecreaseHealthPoints(Source.gameObject);
        }

        public void SetPosition(Vector3 pos)
        {
            ProjectileRigid2D.position = pos;
        }

        public void SetRotation(Quaternion quaternion)
        {
            ProjectileRigid2D.SetRotation(quaternion);
        }

        public void SetSpriteRendererFlip(bool flag)
        {
            var prevFlag = ProjectileSpriteRenderer.flipX;

            if (prevFlag != flag)
            {
                ProjectileSpriteRenderer.flipX = flag;
            }
        }
    }
}
