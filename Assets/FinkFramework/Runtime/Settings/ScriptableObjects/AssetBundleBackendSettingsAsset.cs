using UnityEngine;

namespace FinkFramework.Runtime.Settings.ScriptableObjects
{
    public class AssetBundleBackendSettingsAsset : ScriptableObject
    {
        [Header("内置 AB 根路径")]
        [Tooltip("通常为 StreamingAssets 下的目录")]
        public string BuiltInRootPath;

        [Header("热更 AB 根路径")]
        [Tooltip("通常为 PersistentDataPath 下的目录")]
        public string HotfixRootPath;
        
        [Header("AB 平台目录名")]
        [Tooltip("AssetBundle 构建输出的子目录名，例如 StandaloneWindows64 / Android / iOS")]
        public string PlatformName;

        [Header("是否启用 Hotfix AB")]
        public bool EnableHotfix = false;
    }
}