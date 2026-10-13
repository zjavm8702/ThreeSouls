using System;
using UnityEngine;

namespace ThreeSouls.Audio
{
    public enum SoundCue { Click, Open, Close, Error, Trade, Equip, Sword, Bow, Book, Upgrade, Rune, Magic, Heal, Sanctuary, Hit, Death, Dismantle, Quest, Loot, FootGrass1, FootGrass2, FootStone1, FootStone2, FootSnow1, FootSnow2, Victory, Defeat }

    [CreateAssetMenu(menuName = "Three Souls/Audio Catalog")]
    public sealed class GameAudioCatalog : ScriptableObject
    {
        [Serializable] public struct Cue
        {
            public SoundCue Kind;
            public AudioClip Clip;
            [Range(0, 1)] public float Volume;
            [Min(0)] public float Interval;
        }
        public AudioClip Village;
        public AudioClip Battle;
        public AudioClip Cave;
        public Cue[] Effects = Array.Empty<Cue>();
        [Range(0,1)] public float MusicVolume = .18f;
        [Range(0,1)] public float EffectsVolume = .55f;
    }
}
