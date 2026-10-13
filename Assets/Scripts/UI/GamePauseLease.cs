using System;
using UnityEngine;

namespace ThreeSouls.UI
{
    /// <summary>Nested dialogs and ad views share pause ownership, independent of close order.</summary>
    public sealed class GamePauseLease : IDisposable
    {
        private static int Owners;
        private static float Previous=1;
        private bool Released;
        private GamePauseLease(){if(Owners++==0)Previous=Time.timeScale;Time.timeScale=0;}
        public static GamePauseLease Acquire()=>new GamePauseLease();
        public void Dispose(){if(Released)return;Released=true;if(--Owners==0)Time.timeScale=Previous;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset(){Owners=0;Previous=1;}
    }
}
