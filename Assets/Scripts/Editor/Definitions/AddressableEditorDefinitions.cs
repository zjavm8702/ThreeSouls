#if UNITY_EDITOR
using UnityEditor;

namespace ThreeSouls.Editor
{
    public static class AddressableDefinition
    {
        public static readonly string ProfileName = "ThreeSouls";
        public static readonly string RemoteBuildPath = "Remote.BuildPath";
        public static readonly string RemoteBuildPathValue = "Bundle/[BuildTarget]";
        public static readonly string RemoteLoadPath = "Remote.LoadPath";
        public static readonly string RemoteLoadPathValue = "";
        public static readonly string KeysEnumNameSpaceName = "ThreeSouls.Enumeration.AddressableKey";
        public static readonly string KeysEnumScriptPath = "Assets/Scripts/Enumeration/AddressableKeysEnum.cs";
    }
}
#endif
