using UnityEngine;

namespace ThreeSouls.Data
{
    public class StageData
    {
        private int Region;
        public int RegionNumber
        {
            get => Region;
            set => Region = Mathf.Max(1, value);
        }

        private int Stage;
        public int StageNumber
        {
            get => Stage;
            set => Stage = Mathf.Max(1, value);
        }
    }
}
