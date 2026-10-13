using System.Collections.Generic;
using UnityEngine;

namespace ThreeSouls.Utility
{
    /// <summary>Wall-clock startup milestones; no save data or per-frame logging.</summary>
    public static class StartupLoadTrace
    {
        public static readonly Dictionary<string, double> Seconds = new();
        private static double Started;
        public static void Begin()
        {
            Seconds.Clear();
            Started = Time.realtimeSinceStartupAsDouble;
            Mark("Start");
        }
        public static void Mark(string phase)
        {
            if (!Seconds.ContainsKey(phase)) Seconds[phase] = Time.realtimeSinceStartupAsDouble - Started;
        }
    }
}
