using System;
using UnityEngine;

namespace ThreeSouls.UI
{
    /// <summary>Session preferences shared by village and combat. No persistent player data.</summary>
    public static class GameSettings
    {
        public static event Action Changed;
        public static float MasterVolume { get; private set; } = 1;
        public static float MusicVolume { get; private set; } = 1;
        public static float EffectsVolume { get; private set; } = 1;
        public static bool Muted { get; private set; }
        public static bool ShowDamage { get; private set; } = true;
        public static bool ShowLoot { get; private set; } = true;
        public static void SetMaster(float value) { MasterVolume = Mathf.Clamp01(value); Changed?.Invoke(); }
        public static void SetMusic(float value) { MusicVolume = Mathf.Clamp01(value); Changed?.Invoke(); }
        public static void SetEffects(float value) { EffectsVolume = Mathf.Clamp01(value); Changed?.Invoke(); }
        public static void SetMuted(bool value) { Muted = value; Changed?.Invoke(); }
        public static void SetDamage(bool value) { ShowDamage = value; Changed?.Invoke(); }
        public static void SetLoot(bool value) { ShowLoot = value; Changed?.Invoke(); }
        public static void RestoreDefaults()
        {
            MasterVolume = MusicVolume = EffectsVolume = 1;
            Muted = false; ShowDamage = ShowLoot = true;
            Changed?.Invoke();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void BeginSession() { Changed = null; RestoreDefaults(); }
    }
}
