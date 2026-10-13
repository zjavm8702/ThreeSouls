using System;
using System.Collections.Generic;

namespace ThreeSouls.Runes
{
    /// <summary>
    /// 종류별로 하나씩 보유하는 룬의 단계와 성소·스킬 장착 상태를 보호합니다.
    /// </summary>
    public sealed partial class RuneCollection
    {
        private readonly Dictionary<string, int> Ranks = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> Sockets = new(StringComparer.Ordinal);
        private bool TestGranted;
        private int Revision;

        public int GetRank(string runeKey)
        {
            return Ranks.TryGetValue(runeKey, out var rank) == true ? rank : 0;
        }

        public string GetEquipped(string socket)
        {
            return Sockets.TryGetValue(socket, out var key) == true ? key : null;
        }

        internal int GetRevision()
        {
            return Revision;
        }

        internal bool HasTestGrant()
        {
            return TestGranted;
        }

        internal void CompleteTestGrant()
        {
            TestGranted = true;
        }

        internal void Add(string key)
        {
            Ranks.Add(key, 1);
            Revision++;
        }

        internal void Upgrade(string key)
        {
            Ranks[key]++;
            Revision++;
        }

        internal void Remove(string key)
        {
            Ranks.Remove(key);
            Revision++;
        }

        internal void Equip(string socket, string key)
        {
            Sockets[socket] = key;
            Revision++;
        }

        internal void Unequip(string socket)
        {
            Sockets.Remove(socket);
            Revision++;
        }
    }
}
