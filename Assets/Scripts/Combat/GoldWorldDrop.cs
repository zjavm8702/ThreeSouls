using ThreeSouls.Data;
using ThreeSouls.Interface;
using ThreeSouls.Manager;
using ThreeSouls.Utility;
using UnityEngine;

namespace ThreeSouls.Combat
{
    /// <summary>Visual pending loot. Reward accounting remains in CombatRewardHandler.</summary>
    public sealed class GoldWorldDrop : MonoBehaviour, IEventListener<Event_CombatStageCompleted>
    {
        private float ReadyAt;
        private float NextCheck;
        private float ExpiresAt;
        private const float DisplayDuration = 5f;
        public int CallbackOrder => 0;
        public static void Create(Vector3 position, Sprite icon, int amount)
        {
            var root = new GameObject("Gold_Pending_" + amount);
            root.transform.position = position + (Vector3)Random.insideUnitCircle * .25f;
            var view = root.AddComponent<SpriteRenderer>();
            view.sprite = icon; view.sortingOrder = 30;
            if (icon != null && icon.bounds.size.y > 0) root.transform.localScale = Vector3.one * (.35f / icon.bounds.size.y);
            var drop = root.AddComponent<GoldWorldDrop>();
            drop.ReadyAt = Time.time + .3f;
            drop.ExpiresAt = Time.time + DisplayDuration;
            EventUtility.TriggerEvent(drop, new Event_GoldDropped(amount, icon, root.transform.position));
        }
        private void OnEnable() => EventUtility.AddListener<Event_CombatStageCompleted>(this);
        private void OnDisable() => EventUtility.RemoveListener<Event_CombatStageCompleted>(this);
        public void OnEvent<T>(T sender, Event_CombatStageCompleted value) where T : class => Destroy(gameObject);
        private void Update()
        {
            // The HUD label expires separately; its removal never destroys this world sprite.
            // Expire using game time so a paused settings/advertising popup freezes the display.
            if (Time.time >= ExpiresAt) { Destroy(gameObject); return; }
            if (Time.time < ReadyAt || Time.time < NextCheck) return;
            NextCheck = Time.time + .2f;
            var heroes = GameCombatManager.Get().GetAliveHeroes();
            foreach (var hero in heroes)
                if (hero.CharHealth != null && hero.CharHealth.IsAlive && Vector2.SqrMagnitude(hero.GetWorldPosition() - (Vector2)transform.position) <= 2.25f)
                { Destroy(gameObject); return; }
        }
    }
}
