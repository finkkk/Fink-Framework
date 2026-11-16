using FinkFramework.Runtime.Settings.ScriptableObjects;

namespace FinkFramework.Runtime.Settings.Loaders
{
    /// <summary>UI配置入口；已有引用直接复用，缺失时使用默认配置，必须在 Unity 主线程访问。</summary>
    public static class UISettingsRuntimeLoader
    {
        /// <summary>已有或自动补齐的模块配置。</summary>
        public static UISettingsAsset Current
            => GlobalSettingsRuntimeLoader.GetOrCreateModule(ref GlobalSettingsRuntimeLoader.Current.UISettings);

        /// <summary>沿全局引用读取模块配置，必须在 Unity 主线程调用。</summary>
        /// <param name="settings">已有或默认模块配置。</param>
        /// <returns>成功取得配置时为 true，缺失时自动补齐。</returns>
        public static bool TryGet(out UISettingsAsset settings)
        {
            settings = Current;
            return settings;
        }

        /// <summary>当前面板 Resources 相对目录，不含 res:// 前缀。</summary>
        public static string UIPanelResourcesPath => UISettingsAsset.GetUIPanelResourcesPath(Current.UIPanelPrefabOutputRelativePath);
    }
}
