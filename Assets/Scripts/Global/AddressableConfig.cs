using System.IO;
using UnityEngine;

namespace ThreeSouls.Global
{
    public class AddressableConfig : MonoBehaviour
    {
        public static string TargetEnvironment { get; private set; }
        public static string TargetVersion { get; private set; }
        public static string TargetMarket { get; private set; }

        public static bool IsConfigured =>
            string.IsNullOrWhiteSpace(TargetEnvironment) == false &&
            string.IsNullOrWhiteSpace(TargetVersion) == false &&
            string.IsNullOrWhiteSpace(TargetMarket) == false;

        public static string CatalogPath => "https://zjavm8702.github.io/AddressableAssetsData/Android/catalog_1.0.json";
            //IsConfigured
            //? Path.Combine(
            //    //HMStringConstruct.CDNAdress,
            //    TargetEnvironment,
            //    TargetMarket,
            //    TargetVersion//,
            //                 //HMStringConstruct.ContentDirectoryPath,
            //    /*HMUtil.GetCatalogName(VersionInfo.TargetMarket)*/).Replace('\\', '/')
            //: string.Empty;

        public static void SetAddressableConfig(string environment, string version, string market)
        {
            TargetEnvironment = environment?.Trim();
            TargetVersion = version?.Trim();
            TargetMarket = market?.Trim();
        }
    }
}
