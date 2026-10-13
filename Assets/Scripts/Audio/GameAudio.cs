using System;
using System.Collections;
using System.Collections.Generic;
using ThreeSouls.Character;
using ThreeSouls.Data;
using ThreeSouls.Enumeration.AddressableKey;
using ThreeSouls.Interface;
using ThreeSouls.Manager;
using ThreeSouls.Utility;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ThreeSouls.Audio
{
    /// <summary>One session owner, two crossfading music voices and a bounded effects pool.</summary>
    public sealed class GameAudio : BaseManager,
        IEventListener<Event_CombatAttackCycleCompleted>, IEventListener<Event_CombatHolyPlaceStarted>,
        IEventListener<Event_EquipmentDropped>
    {
        private static GameAudio Current;
        private GameAudioCatalog Catalog;
        private readonly Dictionary<SoundCue, GameAudioCatalog.Cue> Cues = new();
        private readonly Dictionary<SoundCue, double> ReadyAt = new();
        private readonly Dictionary<SoundCue, int> Counts = new();
        public IReadOnlyDictionary<SoundCue, int> CuePlayCounts => Counts;
        private readonly AudioSource[] Music = new AudioSource[2];
        private readonly AudioSource[] Voices = new AudioSource[10];
        private readonly float[] VoiceGains = new float[10];
        private readonly float[] MusicTargets = new float[2];
        private int MusicIndex;
        private bool ResultShown;
        private double StepReady;
        private int StepIndex;
        private AudioListener Listener;
        private bool Loading;
        public int CallbackOrder => 100;
        public bool Ready => Catalog != null;
        public int PlayedEffects { get; private set; }
        public SoundCue LastCue { get; private set; }
        public float MusicVolume { get; private set; } = 1;
        public float EffectsVolume { get; private set; } = 1;

        public override void Initialize()
        {
            Current = this;
            ThreeSouls.UI.GameSettings.Changed += ApplySettings;
            ApplySettings();
            // Scene cameras retain their serialized settings; ownership changes only during play.
            foreach(var listener in UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include)) listener.enabled=false;
            var ear=new GameObject("GameAudioListener");ear.transform.SetParent(transform,false);
            Listener=ear.AddComponent<AudioListener>();
            for (var i=0; i<Music.Length; i++) Music[i]=CreateVoice("Music"+i, true);
            for (var i=0; i<Voices.Length; i++) Voices[i]=CreateVoice("Effect"+i, false);
            SceneManager.sceneLoaded += OnSceneLoaded;
            EventUtility.AddListener<Event_CombatAttackCycleCompleted>(this);
            EventUtility.AddListener<Event_CombatHolyPlaceStarted>(this);
            EventUtility.AddListener<Event_EquipmentDropped>(this);
        }

        private AudioSource CreateVoice(string label, bool loop)
        {
            var child=new GameObject(label); child.transform.SetParent(transform, false);
            var source=child.AddComponent<AudioSource>();
            source.playOnAwake=false; source.loop=loop; source.spatialBlend=0;
            source.volume=0; source.priority=loop?64:128;
            return source;
        }

        public void LoadCatalog()
        {
            if(Loading || Ready)return;
            Loading=true;StartCoroutine(Load());
        }
        private IEnumerator Load()
        {
            var assets=AddressableManager.Get();
            yield return assets.AddressableLoadAssetAsync<GameAudioCatalog>(ScriptableObjectDatas.GameAudioCatalog.ToAddress(), false);
            Catalog=assets.LoadAsset<GameAudioCatalog>(ScriptableObjectDatas.GameAudioCatalog.ToAddress());
            Loading=false;
            if(Catalog==null) yield break;
            foreach(var cue in Catalog.Effects) Cues.Add(cue.Kind,cue);
            OnSceneLoaded(SceneManager.GetActiveScene(),LoadSceneMode.Single);
        }

        public static void Play(SoundCue cue)
        {
            if(Application.isPlaying && Current!=null) Current.PlayEffect(cue);
        }

        public static void Step(int region = 0)
        {
            if(Current==null || !Current.Ready || Current.ResultShown || Time.realtimeSinceStartupAsDouble<Current.StepReady)return;
            Current.StepReady=Time.realtimeSinceStartupAsDouble+.38;
            var offset=Current.StepIndex++%2;
            Play((SoundCue)((int)(region==4?SoundCue.FootSnow1:region==0||region==3?SoundCue.FootGrass1:SoundCue.FootStone1)+offset));
        }

        private void PlayEffect(SoundCue kind)
        {
            if(!Ready || !Cues.TryGetValue(kind,out var cue) || cue.Clip==null) return;
            if(SceneManager.GetActiveScene().name=="Scene_Start")return;
            var now=Time.realtimeSinceStartupAsDouble;
            if(ReadyAt.TryGetValue(kind,out var at) && now<at) return;
            if(ResultShown && (kind==SoundCue.Hit || kind==SoundCue.Sword || kind==SoundCue.Bow || kind==SoundCue.Magic || kind==SoundCue.Death || kind==SoundCue.Heal || kind==SoundCue.Loot)) return;
            AudioSource voice=null;
            var combatCue=kind==SoundCue.Hit||kind==SoundCue.Sword||kind==SoundCue.Bow||kind==SoundCue.Magic||kind==SoundCue.Death||kind.ToString().StartsWith("Foot");
            for(var i=combatCue?2:0; i<Voices.Length; i++) if(!Voices[i].isPlaying){voice=Voices[i];break;}
            // Short combat chatter cannot steal UI or victory voices.
            if(voice==null) return;
            ReadyAt[kind]=now+cue.Interval;
            VoiceGains[Array.IndexOf(Voices, voice)] = cue.Volume;
            voice.clip=cue.Clip; voice.pitch=1; voice.volume=Catalog.EffectsVolume*EffectsVolume*cue.Volume;
            voice.Play(); PlayedEffects++;LastCue=kind;
            Counts[kind]=Counts.TryGetValue(kind,out var count)?count+1:1;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            foreach(var listener in UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include)) if(listener!=Listener)listener.enabled=false;
            ResultShown=false;
            if(!Ready)return;
            SetMusic(scene.name=="Scene_Dungeon"?Catalog.Cave:Catalog.Village);
        }

        private void SetMusic(AudioClip clip)
        {
            if(clip==null)return;
            if(Music[MusicIndex].clip==clip && Music[MusicIndex].isPlaying){MusicTargets[MusicIndex]=1;return;}
            MusicTargets[MusicIndex]=0;MusicIndex=1-MusicIndex;
            var next=Music[MusicIndex];next.Stop();next.clip=clip;next.volume=0;next.Play();MusicTargets[MusicIndex]=1;
        }

        public static void BeginBattle()
        {
            if(Current==null || !Current.Ready)return;
            Current.ResultShown=false;Current.SetMusic(Current.Catalog.Battle);
        }
        public static void CompleteBattle(bool victory)
        {
            if(Current==null || Current.ResultShown)return;
            Current.ResultShown=true;
            foreach(var source in Current.Voices)source.Stop();
            Current.MusicTargets[0]=Current.MusicTargets[1]=0;
            Play(victory?SoundCue.Victory:SoundCue.Defeat);
        }
        public void SetVolumes(float music, float effects)
        {
            MusicVolume=Mathf.Clamp01(music);EffectsVolume=Mathf.Clamp01(effects);
            for(var i=0;i<Voices.Length;i++) if(Voices[i]!=null && Catalog!=null)
                Voices[i].volume=Catalog.EffectsVolume*EffectsVolume*VoiceGains[i];
        }
        private void ApplySettings()
        {
            var master = ThreeSouls.UI.GameSettings.Muted ? 0 : ThreeSouls.UI.GameSettings.MasterVolume;
            SetVolumes(ThreeSouls.UI.GameSettings.MusicVolume * master, ThreeSouls.UI.GameSettings.EffectsVolume * master);
            if(Catalog!=null) for(var i=0;i<Music.Length;i++)
                Music[i].volume=MusicTargets[i]*Catalog.MusicVolume*MusicVolume;
        }
        private void Update()
        {
            if(!Ready)return;
            for(var i=0;i<Music.Length;i++){
                var target=MusicTargets[i]*Catalog.MusicVolume*MusicVolume;
                Music[i].volume=Mathf.MoveTowards(Music[i].volume,target,Time.unscaledDeltaTime*Catalog.MusicVolume/.8f);
                if(MusicTargets[i]==0 && Music[i].volume==0 && Music[i].isPlaying)Music[i].Stop();
            }
        }
        public override void OnSceneChanged()
        {
            foreach(var source in Voices)source.Stop();ReadyAt.Clear();StepReady=0;
            MusicTargets[0]=MusicTargets[1]=0;
        }
        public void OnEvent<T>(T sender,Event_CombatAttackCycleCompleted value)where T:class
        {
            if(value.Entity is Hero entity && entity.CharHealth.IsAlive && value.Target!=null)
                Play(entity.CharacterData.CharacterKey=="Key_night"?SoundCue.Sword:entity.CharacterData.CharacterKey=="Key_archer"?SoundCue.Bow:SoundCue.Magic);
        }
        public void OnEvent<T>(T sender,Event_CombatHolyPlaceStarted value)where T:class => Play(SoundCue.Sanctuary);
        public void OnEvent<T>(T sender,Event_EquipmentDropped value)where T:class => Play(SoundCue.Loot);
        public override void Release()
        {
            ThreeSouls.UI.GameSettings.Changed -= ApplySettings;
            StopAllCoroutines();SceneManager.sceneLoaded-=OnSceneLoaded;
            EventUtility.RemoveListener<Event_CombatAttackCycleCompleted>(this);
            EventUtility.RemoveListener<Event_CombatHolyPlaceStarted>(this);
            EventUtility.RemoveListener<Event_EquipmentDropped>(this);
            foreach(var source in Music)if(source!=null)source.Stop();
            foreach(var source in Voices)if(source!=null)source.Stop();
            Cues.Clear();ReadyAt.Clear();Counts.Clear();Catalog=null;
            Loading=false;
            if(Listener!=null)Listener.enabled=false;
            if(Current==this)Current=null;
        }
    }
}
