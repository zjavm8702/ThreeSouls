using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ThreeSouls.Utility
{
    public enum ThreeSoulsLogLevel
    {
        Info,
        Warning,
        Error
    }

    /// <summary>
    /// 에디터에서는 모든 로그, 플레이어에서는 THREESOULS_LOGS 심볼과 includeInBuild가 모두 켜진 로그를 출력합니다.
    /// 심볼 없는 플레이어에서는 호출과 인자 평가가 생략되므로 인자에 필수 동작을 넣지 마세요.
    /// </summary>
    public static class ThreeSoulsLog
    {
        public static bool Enabled { get; set; } = true;
        public static ThreeSoulsLogLevel MinimumLevel { get; set; } = ThreeSoulsLogLevel.Info;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetSettings()
        {
            Enabled = true;
            MinimumLevel = ThreeSoulsLogLevel.Info;
        }

        public static bool IsEnabled(bool includeInBuild = false, ThreeSoulsLogLevel level = ThreeSoulsLogLevel.Info)
        {
#if UNITY_EDITOR
            return Enabled && level >= MinimumLevel;
#elif THREESOULS_LOGS
            return includeInBuild && Enabled && level >= MinimumLevel;
#else
            return false;
#endif
        }

        [Conditional("UNITY_EDITOR"), Conditional("THREESOULS_LOGS")]
        [HideInCallstack]
        public static void Info(string message, bool includeInBuild = false,
            Object context = null, [CallerMemberName] string member = "",
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            Write(ThreeSoulsLogLevel.Info, message, includeInBuild, context, member, file, line);
        }

        [Conditional("UNITY_EDITOR"), Conditional("THREESOULS_LOGS")]
        [HideInCallstack]
        public static void Warning(string message, bool includeInBuild = false,
            Object context = null, [CallerMemberName] string member = "",
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            Write(ThreeSoulsLogLevel.Warning, message, includeInBuild, context, member, file, line);
        }

        [Conditional("UNITY_EDITOR"), Conditional("THREESOULS_LOGS")]
        [HideInCallstack]
        public static void Error(string message, bool includeInBuild = false,
            Object context = null, [CallerMemberName] string member = "",
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            Write(ThreeSoulsLogLevel.Error, message, includeInBuild, context, member, file, line);
        }

        [HideInCallstack]
        private static void Write(ThreeSoulsLogLevel level, string message, bool includeInBuild,
            Object context, string member, string file, int line)
        {
            if (IsEnabled(includeInBuild, level) == false)
            {
                return;
            }

            var text = $"[ThreeSouls][{level}] {message} ({Path.GetFileName(file)}:{line} {member})";
            switch (level)
            {
                case ThreeSoulsLogLevel.Warning:
                    UnityEngine.Debug.LogWarning(text, context);
                    break;
                case ThreeSoulsLogLevel.Error:
                    UnityEngine.Debug.LogError(text, context);
                    break;
                default:
                    UnityEngine.Debug.Log(text, context);
                    break;
            }
        }
    }
}
