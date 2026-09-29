using ThreeSouls.Data;
using ThreeSouls.Enumeration;
using UnityEngine;

namespace ThreeSouls.Interface
{
    public interface ICombatStateProvider
    {
        /// <summary>
        /// 애니메이터 매개변수 Boolean 타입 변경
        /// </summary>
        /// <param name="animatorParam">Animator Parameter</param>
        /// <param name="flag">Parameter Value</param>
        void SetAnimator(AnimatorStateParams animatorParam, bool flag);

        /// <summary>
        /// 애니메이터 매개변수 Boolean 타입 현재 상태 반환
        /// </summary>
        /// <param name="animatorParam">Animator Parameter</param>
        /// <returns>IsPlay</returns>
        bool IsAnimationPlay(AnimatorStateParams animatorParam);

        /// <summary>
        /// 애니메이션을 재생
        /// </summary>
        /// <param name="animatorParam">Animator Parameter</param>
        /// <param name="layer">Animator Layer</param>
        /// <param name="normalizedTime">Clip normalizedTime</param>
        void PlayAnimation(AnimatorStateParams animatorParam, int layer = 0, float normalizedTime = 0);

        /// <summary>
        /// 현재 재생중인 클립 NormalizedTime
        /// </summary>
        /// <param name="animatorParam">Animator Parameter</param>
        /// <returns>NormalizedTime</returns>
        float GetPlayAnimationNormalizedTime(AnimatorStateParams animatorParam);

        /// <summary>
        /// 위치
        /// </summary>
        /// <param name="pos">Move Position</param>
        void SetPosition(Vector3 pos);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="flag"></param>
        void SetSpriteRendererFlip(bool flag);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="state"></param>
        void ChangeState(CombatState state);

        /// <summary>
        /// 
        /// </summary>
        void Despawn();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="state"></param>
        /// <param name="play"></param>
        void SetEffect(CombatState state, bool play);

        /// <summary>
        /// 
        /// </summary>
        Vector2[] CurrentDestinations { get; }

        /// <summary>
        /// 
        /// </summary>
        GameObject[] EnemySpawnTargets { get; }
       
        /// <summary>
        /// 
        /// </summary>
        GameObject EnemyTarget { get; set; }

        /// <summary>
        /// 
        /// </summary>
        Vector2 CurrentWorldPosition { get; }

        /// <summary>
        /// 
        /// </summary>
        CharacterCombatData CharacterData { get; }

        /// <summary>
        /// 
        /// </summary>
        LayerMask EnemyLayerMask { get; }
    }

    public interface IProjectileStateProvider
    {
        void SetPosition(Vector3 pos);
        void SetRotation(Quaternion quaternion);
        void Despawn();
        void ApplyHit();
        void SetSpriteRendererFlip(bool flag);
        bool IsTargetAlive { get; }
        Vector2 CurrentWorldPosition { get; }
        Vector2 TargetWorldPosition { get; }
    }
}
