using System;
using ThreeSouls.Data.Generated;
using ThreeSouls.Manager;
using UnityEngine;
using UnityEngine.UI;

namespace ThreeSouls.UI
{
    /// <summary>
    /// 캐릭터 탭에서 영웅의 기본 능력치와 현재 장비 보너스를 표시합니다.
    /// </summary>
    public sealed class UI_LobbyCharacters : MonoBehaviour
    {
        [SerializeField] private Text[] Stats;
        [SerializeField] private Text[] EquipmentLabels;
        [SerializeField] private Text[] HeroNames;
        [SerializeField] private UI_HeroGrowth GrowthPanel;
        [SerializeField] private UI_RuneStones RunePanel;
        private EquipmentManager Equipment;
        private TableManager Tables;
        private HeroGrowthManager Growth;
        private readonly EquipSlot[] Slots = { EquipSlot.Weapon, EquipSlot.Armor, EquipSlot.Accessory };

        public void SetUp(EquipmentManager equipment, TableManager tables, HeroGrowthManager growth)
        {
            Disconnect();
            Equipment = equipment ?? throw new ArgumentNullException(nameof(equipment));
            Tables = tables ?? throw new ArgumentNullException(nameof(tables));
            Growth = growth ?? throw new ArgumentNullException(nameof(growth));
            GrowthPanel.SetUp(growth, UserManager.Get(), equipment, tables);

            if (isActiveAndEnabled == true)
            {
                Connect();
            }
        }

        private void Connect()
        {
            Refresh();
            Equipment.OnEquipmentChangedEventHandler += OnEquipmentChanged;
            Growth.OnHeroGrowthChangedEventHandler += OnEquipmentChanged;
        }

        private void Disconnect()
        {
            if (Equipment != null)
            {
                Equipment.OnEquipmentChangedEventHandler -= OnEquipmentChanged;
                Growth.OnHeroGrowthChangedEventHandler -= OnEquipmentChanged;
            }
        }

        private void Refresh()
        {
            for (var i = 0; i < Stats.Length; i++)
            {
                var key = LobbyPresentationData.GetHeroKey(i);
                var hero = Tables.GetCharacterTableDataTable(key);
                var bonus = Equipment.GetEquipmentBonuses(hero);
                var growth = Growth.GetBonuses(hero);
                HeroNames[i].text = LobbyPresentationData.GetHeroName(i) + $"  Lv.{Growth.GetHero(key).Level}";
                Stats[i].text = $"공격력  {hero.AttackDmg + bonus.AttackDmg + growth.Attack:0.###}  (+{bonus.AttackDmg + growth.Attack:0.###})\n"
                    + $"최대 체력  {hero.MaxHP + bonus.MaxHP + growth.MaxHealth:0.###}  (+{bonus.MaxHP + growth.MaxHealth:0.###})\n"
                    + $"공격속도 보너스  +{bonus.AttackSpeedBonus + growth.AttackSpeed:P1}\n"
                    + $"치명타  {growth.CriticalChance:P1} / 피해 {growth.CriticalMultiplier:P0}\n행운  {growth.Luck:0.###}";
                var labels = string.Empty;

                for (var slotIndex = 0; slotIndex < Slots.Length; slotIndex++)
                {
                    var slot = Slots[slotIndex];
                    var item = Equipment.GetEquippedEquipment(key, slot);
                    labels += LobbyPresentationData.GetSlotName(slot) + "  ·  "
                        + (item == null ? "미장착" : Tables.GetEquipmentName(item.ItemKey) + " +" + item.Growth.Level) + "\n";
                }

                EquipmentLabels[i].text = labels.TrimEnd();
            }
        }

        private void OnEquipmentChanged(string heroKey)
        {
            Refresh();
        }

        public void OpenGrowth(int heroIndex)
        {
            GrowthPanel.OpenHero(heroIndex);
        }

        public void OpenRunes()
        {
            RunePanel.Open();
        }

        private void OnEnable()
        {
            if (Equipment != null)
            {
                Connect();
            }
        }

        private void OnDisable()
        {
            RunePanel?.Close();
            if (GrowthPanel != null)
            {
                GrowthPanel.Close();
            }

            Disconnect();
        }

        private void OnDestroy()
        {
            Disconnect();
        }
    }
}
