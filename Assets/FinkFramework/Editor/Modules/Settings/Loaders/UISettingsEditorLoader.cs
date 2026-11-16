using FinkFramework.Runtime.Settings.ScriptableObjects;

namespace FinkFramework.Editor.Modules.Settings.Loaders
{
    /// <summary>UI 配置的编辑器加载、创建及绑定入口，不迁移旧字段。</summary>
    public static class UISettingsEditorLoader
    {
        /// <summary>默认配置资产路径；Runtime 沿全局引用读取。</summary>
        public const string AssetPath = "Assets/FinkFramework_Assets/Resources/FinkFramework/Settings/UISettingsAsset.asset";

        /// <summary>沿全局引用获取模块配置，缺失时创建并保存。必须在 Unity 主线程调用。</summary>
        /// <returns>已有或新创建的配置；路径冲突时抛出配置错误。</returns>
        public static UISettingsAsset LoadOrCreate() => GlobalSettingsEditorLoader.LoadOrCreate().UISettings;

        /// <summary>复用引用，或创建默认 SO 并绑定。必须在 Unity 主线程调用，不覆盖占用路径。</summary>
        /// <param name="global">要绑定的全局配置。</param>
        /// <returns>已有或新创建的模块配置；路径冲突时抛出配置错误。</returns>
        public static UISettingsAsset GetOrCreateUISettings(GlobalSettingsAsset global)
            => SettingsAssetEditorUtility.Bind(global, global.UISettings, AssetPath, value => global.UISettings = value);

        /// <summary>根据当前资源后端判断指定 UI 配置的面板是否使用 Resources。</summary>
        /// <param name="settings">UI 配置。</param>
        /// <returns>解析为 Resources 来源时返回 true。</returns>
        public static bool UsesResources(UISettingsAsset settings)
            => settings.UsesResources(ResBackendSettingsEditorLoader.LoadOrCreate().ResourceBackend);

    }
}
