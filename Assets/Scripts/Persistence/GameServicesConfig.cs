using UnityEngine;

namespace ThreeSouls.Persistence
{
    [CreateAssetMenu(menuName="Three Souls/Services Configuration")]
    public sealed class GameServicesConfig : ScriptableObject
    {
        [Header("Google Play Games — public identifiers only")]
        public string GoogleGameId="";
        public string GoogleWebClientId="";
        [Header("LevelPlay — Android")]
        public string AndroidAppKey="";
        public string ResultAdUnitId="";
        public string SupplyAdUnitId="";
        public string ResultPlacement="DungeonGoldBonus";
        public string SupplyPlacement="VillagePotion";
        [Range(0,1)] public float ResultGoldRatio=.5f;
        public int ResultAdsPerDay=3;
        public int SupplyAdsPerDay=3;
        public int SupplyPotions=2;
        public bool AdsEnabled;
        public bool ChildDirected;
        public static GameServicesConfig Load()=>Resources.Load<GameServicesConfig>("GameServicesConfig");
        public bool GoogleConfigured=>!string.IsNullOrWhiteSpace(GoogleGameId) && !string.IsNullOrWhiteSpace(GoogleWebClientId);
        public bool AdsConfigured=>AdsEnabled && !string.IsNullOrWhiteSpace(AndroidAppKey) && !string.IsNullOrWhiteSpace(ResultAdUnitId) && !string.IsNullOrWhiteSpace(SupplyAdUnitId);
    }
}
