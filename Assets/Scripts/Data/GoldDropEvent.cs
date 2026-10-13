using ThreeSouls.Interface;
using UnityEngine;
namespace ThreeSouls.Data
{
    public readonly struct Event_GoldDropped : IEventValue
    {
        public readonly int Amount;
        public readonly Sprite Icon;
        public readonly Vector3 WorldPosition;
        public Event_GoldDropped(int amount, Sprite icon, Vector3 position)
        { Amount = amount; Icon = icon; WorldPosition = position; }
    }
}
