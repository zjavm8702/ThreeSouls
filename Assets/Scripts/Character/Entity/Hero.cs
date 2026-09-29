using ThreeSouls.Data.Generated;
using ThreeSouls.Enumeration;
using ThreeSouls.Global;
using ThreeSouls.Interface;
using ThreeSouls.Manager;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.U2D.Animation;

namespace ThreeSouls.Character
{
    public class Hero : BaseEntity
    {
        public override LayerMask EnemyLayerMask => LayerMask.GetMask(StringConstants.LAYERMASK_ENEMY_MONSTER);

        public override void SetUp(
            CharacterTableDataRow chareData,
            ThreeSoulsRuleDataRow ruleData,
            SpriteLibraryAsset sprLibraryAssetData,
            ICombatWorld combatWorld)
        {
            base.SetUp(chareData, ruleData, sprLibraryAssetData, combatWorld);
        }

        public override void ChangeState(CombatState state)
        {
            base.ChangeState(state);
        }

        public override void Despawn()
        {
            base.Despawn();
        }
        
        #if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            var newP = new Vector2(transform.position.x, transform.position.y + 0.5f);

            UnityEditor.Handles.color = Color.red;
            UnityEditor.Handles.DrawWireDisc(
                newP,
                Vector3.forward,
                Constants.CIRCLE_RADIUS_HERO
            );
        }
        #endif
    }
}
