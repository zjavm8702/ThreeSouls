using System;

namespace ThreeSouls.Data
{
    /// <summary>세션 내 승리 진행과 반복 파밍 횟수. 저장하지 않습니다.</summary>
    public sealed partial class CampaignProgress
    {
        private readonly int[] Clears = new int[20];
        public int ClearedProgress { get; private set; }
        public static int Index(int region, int stage)
        {
            if (region < 1 || region > 4 || stage < 1 || stage > 5) throw new ArgumentOutOfRangeException(nameof(stage));
            return (region - 1) * 5 + stage;
        }
        public bool CanEnter(int region, int stage) => Index(region, stage) <= ClearedProgress + 1;
        public int GetClears(int region, int stage) => Clears[Index(region, stage) - 1];
        internal void RecordVictory(int region, int stage)
        {
            var progress = Index(region, stage);
            if (!CanEnter(region, stage)) throw new InvalidOperationException("선행 스테이지 승리가 필요합니다.");
            Clears[progress - 1] = (int)Math.Min(int.MaxValue, (long)Clears[progress - 1] + 1);
            ClearedProgress = Math.Max(ClearedProgress, progress);
        }
    }
}
