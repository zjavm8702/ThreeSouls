namespace ThreeSouls.Data
{
    /// <summary>
    /// 이번 던전에서 실제 수집한 장비 종류와 수량을 보관합니다.
    /// </summary>
    public readonly struct CombatResultItem
    {
        public string ItemKey { get; }
        public int Count { get; }

        internal CombatResultItem(string itemKey, int count)
        {
            ItemKey = itemKey;
            Count = count;
        }
    }
}
