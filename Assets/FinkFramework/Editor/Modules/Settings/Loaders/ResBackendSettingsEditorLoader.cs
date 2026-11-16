using FinkFramework.Runtime.Settings.ScriptableObjects;

namespace FinkFramework.Editor.Modules.Settings.Loaders
{
    /// <summary>ResourceBackend 配置的编辑器加载、创建及绑定入口，不迁移旧字段。</summary>
    public static class ResBackendSettingsEditorLoader
    {
        /// <summary>默认配置资产路径；Runtime 沿全局引用读取。</summary>
        public const string AssetPath = "Assets/FinkFramework_Assets/Resources/FinkFramework/Settings/ResourceBackendSettingsAsset.asset";

        /// <summary>沿全局引用获取模块配置，缺失时创建并保存。必须在 Unity 主线程调用。</summary>
        /// <returns>已有或新创建的配置；路径冲突时抛出配置错误。</returns>
        public static ResourceBackendSettingsAsset LoadOrCreate() => GlobalSettingsEditorLoader.LoadOrCreate().ResourceBackendSettings;

        /// <summary>复用引用，或创建默认 SO 并绑定。必须在 Unity 主线程调用，不覆盖占用路径。</summary>
        /// <param name="global">要绑定的全局配置。</param>
        /// <returns>已有或新创建的模块配置；路径冲突时抛出配置错误。</returns>
        public static ResourceBackendSettingsAsset GetOrCreateResourceBackendSettings(GlobalSettingsAsset global)
            => SettingsAssetEditorUtility.Bind(global, global.ResourceBackendSettings, AssetPath, value => global.ResourceBackendSettings = value);

        /// <summary>加载或创建 AssetBundle 子配置并绑定到资源后端配置，必须在主线程调用。</summary>
        /// <param name="settings">资源后端配置。</param>
        /// <returns>已有或新创建的子配置；路径冲突时抛出配置错误。</returns>
        public static AssetBundleBackendSettingsAsset GetOrCreateAssetBundleSettings(ResourceBackendSettingsAsset settings)
            => SettingsAssetEditorUtility.Bind(settings, settings.AssetBundleSettings,
                "Assets/FinkFramework_Assets/Resources/FinkFramework/Settings/AssetBundleBackendSettings.asset",
                value => settings.AssetBundleSettings = value);

        /// <summary>加载或创建 Addressables 子配置并绑定到资源后端配置，必须在主线程调用。</summary>
        /// <param name="settings">资源后端配置。</param>
        /// <returns>已有或新创建的子配置；路径冲突时抛出配置错误。</returns>
        public static AddressablesBackendSettingsAsset GetOrCreateAddressablesSettings(ResourceBackendSettingsAsset settings)
            => SettingsAssetEditorUtility.Bind(settings, settings.AddressablesSettings,
                "Assets/FinkFramework_Assets/Resources/FinkFramework/Settings/AddressablesBackendSettings.asset",
                value => settings.AddressablesSettings = value);

    }
}
