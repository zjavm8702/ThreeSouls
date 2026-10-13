namespace ThreeSouls.Quests
{
    /// <summary>
    /// 사용자 세션의 순차 이야기 진행과 현재 목표 상태를 보관합니다.
    /// </summary>
    public sealed partial class QuestJournal
    {
        public int CompletedCount { get; private set; }
        public bool Accepted { get; private set; }
        public bool ObjectiveComplete { get; private set; }

        internal void Accept()
        {
            Accepted = true;
        }

        internal void CompleteObjective()
        {
            ObjectiveComplete = true;
        }

        internal void Advance()
        {
            CompletedCount++;
            Accepted = false;
            ObjectiveComplete = false;
        }
    }
}
