using FinkFramework.Runtime.Environments;
using UnityEngine;

namespace FinkFramework.Runtime.Settings.ScriptableObjects
{
    /// <summary>资源后端选择及各后端的独立配置引用。</summary>
    [CreateAssetMenu(fileName = "ResourceBackendSettingsAsset", menuName = "Fink Framework/Settings/ResourceBackend Settings")]
    public sealed class ResourceBackendSettingsAsset : ScriptableObject
    {
        #region ===== 资源配置 =====

        [Header("资源构建型后端")]
        [Tooltip("是否启用构建型资源系统（AB / Addressables 等）")]
        public EnvironmentState.ResourceBackendType ResourceBackend = EnvironmentState.ResourceBackendType.None;

        [Tooltip("AssetBundle 后端配置（仅当 ResourceBackend=AssetBundle 时使用）")]
        public AssetBundleBackendSettingsAsset AssetBundleSettings;

        [Tooltip("Addressables 后端配置（仅当 ResourceBackend=Addressables 时使用）")]
        public AddressablesBackendSettingsAsset AddressablesSettings;

        [Tooltip("自定义资源后端配置（仅当 ResourceBackend=Custom 时使用）")]
        public ScriptableObject CustomBackendSettings;

        #endregion
    }
}
