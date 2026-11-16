using FinkFramework.Runtime.Settings.ScriptableObjects;
using FinkFramework.Runtime.Localization;

namespace FinkFramework.Runtime.Settings.Loaders
{
    /// <summary>本地化配置入口；已有引用直接复用，缺失时使用默认配置，必须在 Unity 主线程访问。</summary>
    public static class LocalizationSettingsRuntimeLoader
    {
        /// <summary>只检查模块配置，不初始化本地化服务或加载语言数据。</summary>
        public static bool IsEnabled => TryGet(out var settings) && settings.EnableLocalization;

        /// <summary>已有或自动补齐的模块配置。</summary>
        public static LocalizationSettingsAsset Current
            => GlobalSettingsRuntimeLoader.GetOrCreateModule(ref GlobalSettingsRuntimeLoader.Current.LocalizationSettings);

        /// <summary>沿全局引用读取模块配置，必须在 Unity 主线程调用。</summary>
        /// <param name="settings">已有或默认模块配置。</param>
        /// <returns>成功取得配置时为 true，缺失时自动补齐。</returns>
        public static bool TryGet(out LocalizationSettingsAsset settings)
        {
            settings = Current;
            return settings;
        }
    }
}
