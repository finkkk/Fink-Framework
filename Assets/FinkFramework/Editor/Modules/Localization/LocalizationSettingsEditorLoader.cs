using FinkFramework.Runtime.Localization;
using FinkFramework.Runtime.Settings.ScriptableObjects;
using FinkFramework.Editor.Modules.Settings.Loaders;

namespace FinkFramework.Editor.Modules.Localization
{
    /// <summary>本地化配置的编辑器加载与绑定入口，运行时通过全局引用读取。</summary>
    internal static class LocalizationSettingsEditorLoader
    {
        public const string AssetPath = LocalizationPath.SettingsAssetPath;

        public static LocalizationSettingsAsset LoadOrCreate()
            => GlobalSettingsEditorLoader.LoadOrCreate().LocalizationSettings;

        internal static LocalizationSettingsAsset GetOrCreateLocalizationSettings(GlobalSettingsAsset global)
            => SettingsAssetEditorUtility.Bind(global, global.LocalizationSettings, AssetPath,
                value => global.LocalizationSettings = value);
    }
}
