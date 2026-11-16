using FinkFramework.Runtime.Settings.ScriptableObjects;

namespace FinkFramework.Runtime.Settings.Loaders
{
    /// <summary>资源后端配置入口；已有引用直接复用，缺失时使用默认配置，必须在 Unity 主线程访问。</summary>
    public static class ResBackendSettingsRuntimeLoader
    {
        /// <summary>已有或自动补齐的模块配置。</summary>
        public static ResourceBackendSettingsAsset Current
            => GlobalSettingsRuntimeLoader.GetOrCreateModule(ref GlobalSettingsRuntimeLoader.Current.ResourceBackendSettings);

        /// <summary>沿全局引用读取模块配置，必须在 Unity 主线程调用。</summary>
        /// <param name="settings">已有或默认模块配置。</param>
        /// <returns>成功取得配置时为 true，缺失时自动补齐。</returns>
        public static bool TryGet(out ResourceBackendSettingsAsset settings)
        {
            settings = Current;
            return settings;
        }

        /// <summary>当前后端为 Addressables 时的配置，否则为 null。</summary>
        public static AddressablesBackendSettingsAsset Addressables
        {
            get
            {
                var settings = Current;
                return settings.ResourceBackend == Environments.EnvironmentState.ResourceBackendType.Addressables
                    ? GlobalSettingsRuntimeLoader.GetOrCreateModule(ref settings.AddressablesSettings) : null;
            }
        }

        /// <summary>当前后端为 AssetBundle 时的配置，否则为 null。</summary>
        public static AssetBundleBackendSettingsAsset AssetBundle
        {
            get
            {
                var settings = Current;
                return settings.ResourceBackend == Environments.EnvironmentState.ResourceBackendType.AssetBundle
                    ? GlobalSettingsRuntimeLoader.GetOrCreateModule(ref settings.AssetBundleSettings) : null;
            }
        }

        /// <summary>获取当前选中后端的子配置，必须在 Unity 主线程调用。</summary>
        /// <param name="backendSettings">已有或默认后端配置；未选择后端或未提供自定义配置时为 null。</param>
        /// <returns>选中的后端有配置时返回 true，框架内置后端缺失配置会自动补齐。</returns>
        public static bool TryGetCurrent(out UnityEngine.ScriptableObject backendSettings)
        {
            backendSettings = null;
            if (!TryGet(out var settings)) return false;
            backendSettings = settings.ResourceBackend switch
            {
                Environments.EnvironmentState.ResourceBackendType.AssetBundle => AssetBundle,
                Environments.EnvironmentState.ResourceBackendType.Addressables => Addressables,
                Environments.EnvironmentState.ResourceBackendType.Custom => settings.CustomBackendSettings,
                _ => null
            };
            return backendSettings;
        }
    }
}
