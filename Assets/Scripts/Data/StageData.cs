using UnityEngine;

namespace ThreeSouls.Data
{
    public class StageData
    {
        private int Round;
        public int CurrentRound
        {
            get => Round;
            set => Round = Mathf.Max(1, value);
        }

        private int MaxStage;
        public int Stage
        {
            get => MaxStage;
            set => MaxStage = Mathf.Max(1, value);
        }
    }
}
