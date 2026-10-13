using System;
using ThreeSouls.Character;
using ThreeSouls.Runes;

namespace ThreeSouls.Combat
{
    /// <summary>
    /// 성소 전투 한 구간의 무작위 부대 디버프를 적용하고 종료 시 해제합니다.
    /// </summary>
    internal sealed class CombatSanctuaryHandler
    {
        public delegate void OnSanctuaryChangedDelegate();
        private OnSanctuaryChangedDelegate onSanctuaryChangedEventHandler;
        public event OnSanctuaryChangedDelegate OnSanctuaryChangedEventHandler
        {
            add
            {
                onSanctuaryChangedEventHandler -= value;
                onSanctuaryChangedEventHandler += value;
            }
            remove
            {
                onSanctuaryChangedEventHandler -= value;
            }
        }

        private readonly CombatSpawnHandler Spawns;
        private RuneCombatSnapshot Snapshot;
        private SanctuaryDebuff Debuff;
        private bool Active;
        private string RoundKey;

        internal CombatSanctuaryHandler(CombatSpawnHandler spawns)
        {
            Spawns = spawns ?? throw new ArgumentNullException(nameof(spawns));
        }

        internal void Initialize(RuneCombatSnapshot snapshot)
        {
            End();
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        }

        internal void Begin(string roundKey, int randomIndex)
        {
            if (Snapshot == null || Active == true)
            {
                throw new InvalidOperationException("성소 효과는 준비 후 구간마다 한 번 시작하세요.");
            }

            if (string.IsNullOrWhiteSpace(roundKey) == true || randomIndex < 0 || randomIndex > 2)
            {
                throw new ArgumentOutOfRangeException(nameof(randomIndex));
            }

            RoundKey = roundKey;
            Debuff = randomIndex switch
            {
                0 => SanctuaryDebuff.Attack,
                1 => SanctuaryDebuff.AttackSpeed,
                _ => SanctuaryDebuff.MaxHealth
            };
            Active = true;
            var heroes = Spawns.GetAliveHeroes();
            for (var i = 0; i < heroes.Count; i++)
            {
                ((Hero)heroes[i]).SetSanctuaryDebuff(Debuff, Snapshot.DebuffRatio);
            }

            onSanctuaryChangedEventHandler?.Invoke();
        }

        internal void End()
        {
            if (Active == false)
            {
                return;
            }

            Active = false;
            Debuff = SanctuaryDebuff.None;
            var heroes = Spawns.GetAliveHeroes();
            for (var i = 0; i < heroes.Count; i++)
            {
                ((Hero)heroes[i]).SetSanctuaryDebuff(SanctuaryDebuff.None, 0);
            }

            onSanctuaryChangedEventHandler?.Invoke();
        }

        internal string GetDescription()
        {
            if (Snapshot == null)
            {
                return string.Empty;
            }

            var buffs = $"장비 +{Snapshot.EquipmentDropMultiplier - 1:P0}  골드 +{Snapshot.GoldMultiplier - 1:P0}  보석 준비 중";
            if (Active == false)
            {
                return "성소 효과 · 성소 전투에서 적용\n" + buffs;
            }

            var stat = Debuff switch
            {
                SanctuaryDebuff.Attack => "공격력",
                SanctuaryDebuff.AttackSpeed => "공격속도",
                _ => "최대 체력"
            };
            return $"성소 {RoundKey} · {stat} -{Snapshot.DebuffRatio:P0}\n" + buffs;
        }

        internal void Release()
        {
            End();
            Snapshot = null;
            RoundKey = null;
            onSanctuaryChangedEventHandler = null;
        }
    }
}
