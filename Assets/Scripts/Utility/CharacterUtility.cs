using System.Collections.Generic;
using ThreeSouls.Enumeration;
using UnityEngine;

namespace Sky.Utility
{
    public static class CharacterUtility 
    {
        private static readonly Dictionary<CharacterAnimatorParams, int> CharAnimatorParamsHashContainer = new();
        
        public static int GetCharacterAnimatorParametersHash(AnimatorStateParams animatorParam)
        {
            var param = ConvertAIAnimatorParamsToCharacterAnimatorParams(animatorParam);
            return GetCharacterAnimatorParametersHash(param);
        }
        
        public static CharacterAnimatorParams ConvertAIAnimatorParamsToCharacterAnimatorParams(AnimatorStateParams animatorParam)
        {
            switch (animatorParam)
            {
                case AnimatorStateParams.Idle: return CharacterAnimatorParams.Idle;
                case AnimatorStateParams.Ready: return CharacterAnimatorParams.Ready;
                case AnimatorStateParams.Walking: return CharacterAnimatorParams.Walking;
                case AnimatorStateParams.Running: return CharacterAnimatorParams.Running;
                case AnimatorStateParams.Attack: return CharacterAnimatorParams.Attack;
                case AnimatorStateParams.Dead: return CharacterAnimatorParams.Dead;
                case AnimatorStateParams.Hit: return CharacterAnimatorParams.Hit;
                default:
                {
                    Debug.LogError($"Convert error CharacterAnimatorParams [AIAnimatorParams : {animatorParam}]");
                    return CharacterAnimatorParams.Idle;
                }
            }
        }

        public static int GetCharacterAnimatorParametersHash(CharacterAnimatorParams param)
        {
            if (CharAnimatorParamsHashContainer.TryGetValue(param, out var hash) == true)
            {
                return hash;
            }
            
            hash  = Animator.StringToHash(param.ToString());
            CharAnimatorParamsHashContainer.Add(param, hash);

            return hash;
        }
    }
}
