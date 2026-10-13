using System.Collections.Generic;
using ThreeSouls.Character;
using ThreeSouls.Data;
using ThreeSouls.Data.Generated;
using ThreeSouls.Manager;
using UnityEngine;

namespace ThreeSouls.Combat
{
    /// <summary>
    /// 생존 부대의 체력에 공급품 회복·자동 사용·공통 쿨타임을 적용합니다.
    /// </summary>
    internal sealed class CombatSupplyHandler
    {
        private CombatSupplyManager Supplies;
        private IReadOnlyList<BaseEntity> Heroes;
        private float ReadyAt;
        private float AutoHealRatio;
        private float Cooldown;
        private bool Running;

        internal void Initialize(CombatSupplyManager supplies, IReadOnlyList<BaseEntity> heroes)
        {
            Supplies = supplies;
            Heroes = heroes;
            var options = LobbyTables.Get<AutoHealTableDataTable>().Items;
            for (var i = 0; i < options.Length; i++)
            {
                if (options[i].Threshold == supplies.GetThreshold())
                {
                    AutoHealRatio = options[i].Enabled == true ? options[i].HPRatio : 0;
                }
            }
            Cooldown = LobbyTables.GetSupplyRules().PlannedCooldownSeconds;
            ReadyAt = 0;
            Running = true;
        }

        internal void Stop()
        {
            Running = false;
        }

        internal void Release()
        {
            Stop();
            Supplies = null;
            Heroes = null;
        }

        internal float GetCooldownRemaining()
        {
            return Mathf.Max(0, ReadyAt - Time.time);
        }

        internal bool CanUse(int index)
        {
            return Running == true && Supplies.GetSlot(index).Count > 0 && GetCooldownRemaining() == 0 &&
                GetLowestHealthHero() != null;
        }

        internal bool TryUse(int index)
        {
            if (CanUse(index) == false)
            {
                return false;
            }
            var slot = Supplies.GetSlot(index);
            var definition = ConsumableCatalog.GetDefinition(slot.Type);
            if (slot.Type == ConsumableType.SmallHealingPotion)
            {
                Heal(GetLowestHealthHero(), definition.HealMaxHPRatio);
            }
            else
            {
                for (var i = 0; i < Heroes.Count; i++)
                {
                    var hero = Heroes[i];
                    if (hero.CharHealth.IsAlive == true)
                    {
                        Heal(hero, definition.HealMaxHPRatio);
                    }
                }
            }
            Supplies.Consume(index);
            ReadyAt = Time.time + Cooldown;
            ThreeSouls.Audio.GameAudio.Play(ThreeSouls.Audio.SoundCue.Heal);
            return true;
        }

        internal void UpdateAutoHeal()
        {
            if (Running == false || AutoHealRatio == 0 || GetCooldownRemaining() > 0)
            {
                return;
            }
            var hero = GetLowestHealthHero();
            if (hero == null)
            {
                return;
            }
            hero.CharHealth.GetHealth(out var maximum, out var current);
            if (current > maximum * AutoHealRatio)
            {
                return;
            }
            for (var i = 0; i < SupplyLoadout.SlotCount; i++)
            {
                if (TryUse(i) == true)
                {
                    return;
                }
            }
        }

        private BaseEntity GetLowestHealthHero()
        {
            BaseEntity lowest = null;
            var ratio = 1f;
            for (var i = 0; i < Heroes.Count; i++)
            {
                var hero = Heroes[i];
                hero.CharHealth.GetHealth(out var maximum, out var current);
                var candidate = maximum > 0 ? current / maximum : 1;
                if (hero.CharHealth.IsAlive == true && candidate < ratio)
                {
                    ratio = candidate;
                    lowest = hero;
                }
            }
            return lowest;
        }

        private static void Heal(BaseEntity hero, float ratio)
        {
            hero.CharHealth.GetHealth(out var maximum, out _);
            hero.CharHealth.TryHeal(maximum * ratio);
        }
    }
}
