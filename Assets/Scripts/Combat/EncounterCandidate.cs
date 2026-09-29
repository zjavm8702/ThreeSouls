using ThreeSouls.Character;
using UnityEngine;

namespace ThreeSouls.Combat
{
    /// <summary>
    /// 리더 영웅이 특정 몬스터와 어디서, 언제 만날 수 있는지 계산한 후보 데이터
    /// </summary>
    internal sealed class EncounterCandidate
    {
        public BaseEntity Monster { get; }
        public float TriggerTime { get; }
        public Vector2 MeetingPoint { get; }

        public EncounterCandidate(BaseEntity monster, float triggerTime, Vector2 meetingPoint)
        {
            Monster = monster;
            TriggerTime = triggerTime;
            MeetingPoint = meetingPoint;
        }
    }
}
