using UnityEngine;

namespace ThreeSouls.Data
{
    /// <summary>
    /// BaseEntity의 임시 테이블 데이터입니다.
    /// 추후 Google Spreadsheet 기반 데이터가 준비되면 이 에셋 대신 해당 데이터를 주입할 수 있습니다.
    /// </summary>
    [CreateAssetMenu(fileName = "BaseEntityData", menuName = "Three Souls/Character/Base Entity Data")]
    public class BaseEntityData : ScriptableObject
    {
        [Header("Movement")]
        [SerializeField, Min(0f)] private float DetectDelayTime = 0.5f;
        [SerializeField, Min(0f)] private float MoveSpeed = 3f;

        [Header("Targeting")]
        [SerializeField, Min(0f)] private float DistanceToTarget = 1f;

        [Header("Running")]
        [SerializeField] private bool RunningPlay;
        [SerializeField, Min(0f)] private float RunningDistance = 2f;
        [SerializeField, Min(0f)] private float RunningSpeed = 1.5f;

        public float DetectDelay => DetectDelayTime;
        public float Speed => MoveSpeed;
        public float TargetDistance => DistanceToTarget;
        public bool UseRunning => RunningPlay;
        public float RunDistance => RunningDistance;
        public float RunSpeedMultiplier => RunningSpeed;
    }
}
