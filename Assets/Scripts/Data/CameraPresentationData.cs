using UnityEngine;

namespace ThreeSouls.Data
{
    /// <summary>
    /// 전투 시작 카메라 연출과 전투 중 추적에 사용하는 설정 데이터입니다.
    /// </summary>
    [CreateAssetMenu(fileName = "CameraPresentationData", menuName = "Three Souls/Combat/Camera Presentation Data")]
    public class CameraPresentationData : ScriptableObject
    {
        [Header("Timing")]
        [SerializeField, Min(0f)] private float CameraIntroDelay = 1f;
        [SerializeField, Min(0.1f)] private float MonsterFocusDuration = 0.8f;
        [SerializeField, Min(0.1f)] private float HeroFocusDuration = 1f;

        [Header("Lens")]
        [SerializeField, Min(0.1f)] private float IntroOrthographicSize = 7f;
        [SerializeField, Min(0.1f)] private float CombatOrthographicSize = 5f;

        [Header("Tracking")]
        [SerializeField] private Vector2 CameraDeadZoneSize = new(0.2f, 0.15f);
        [SerializeField, Range(0f, 5f)] private float CameraConfinerDamping = 0.35f;
        [SerializeField, Min(0f)] private float CameraConfinerSlowingDistance = 1f;

        public float IntroDelay => CameraIntroDelay;
        public float MonsterFocusTime => MonsterFocusDuration;
        public float HeroFocusTime => HeroFocusDuration;
        public float IntroCameraSize => IntroOrthographicSize;
        public float CombatCameraSize => CombatOrthographicSize;
        public Vector2 DeadZoneSize => CameraDeadZoneSize;
        public float ConfinerDamping => CameraConfinerDamping;
        public float ConfinerSlowingDistance => CameraConfinerSlowingDistance;

        private void OnValidate()
        {
            CameraDeadZoneSize.x = Mathf.Clamp01(CameraDeadZoneSize.x);
            CameraDeadZoneSize.y = Mathf.Clamp01(CameraDeadZoneSize.y);
        }
    }
}
