using System.Diagnostics;

namespace ThreeSouls.Data
{
    public delegate void OnTimeDelegate(double elapsedSeconds);

    public class TimeData
    {
        private OnTimeDelegate onTimeEventHandler = null;
        public event OnTimeDelegate OnTimeEventHandler
        {
            add
            {
                onTimeEventHandler -= value;
                onTimeEventHandler += value;
            }
            remove => onTimeEventHandler -= value;
        }

        public Stopwatch TimeStopwatch = new();
        public double ReferenceSeconds = 0;
        public bool IsReleaseSceneChange = false;

        public void Release()
        {
            var addHandlerList = onTimeEventHandler?.GetInvocationList();

            if (addHandlerList == null)
            {
                return;
            }
            
            for (var i = 0; i < addHandlerList.Length; i++)
            {
                onTimeEventHandler -= (OnTimeDelegate)addHandlerList[i];
            }
        }

        public void Action(double elapsedSeconds) 
        {
            onTimeEventHandler?.Invoke(elapsedSeconds);
        }
    }
}