using FinkFramework.Editor.Modules.Localization;
using FinkFramework.Runtime.Environments;
using FinkFramework.Runtime.Settings.Loaders;
using FinkFramework.Runtime.Settings.ScriptableObjects;
using UnityEditor;

namespace FinkFramework.Editor.Modules.Settings.Loaders
{
    /// <summary>框架配置入口，统一补齐模块 SO 引用；只在编辑器主线程显式调用时创建资产。</summary>
    [InitializeOnLoad]
    public static class GlobalSettingsEditorLoader
    {
        static GlobalSettingsEditorLoader()
            => GlobalSettingsRuntimeLoader.RegisterEditorLoader(LoadOrCreate);

        /// <summary>默认框架配置路径。</summary>
        public const string AssetPath = "Assets/FinkFramework_Assets/Resources/FinkFramework/Settings/GlobalSettingsAsset.asset";

        /// <summary>加载或创建根配置和各模块配置；不读取或迁移旧模块字段。</summary>
        /// <returns>模块引用完整的根配置；路径冲突时抛出配置错误，不覆盖原文件。</returns>
        public static GlobalSettingsAsset LoadOrCreate()
        {
            var global = SettingsAssetEditorUtility.LoadOrCreate<GlobalSettingsAsset>(AssetPath);
            DataSettingsEditorLoader.GetOrCreateDataSettings(global);
            SaveSettingsEditorLoader.GetOrCreateSaveSettings(global);
            InputSettingsEditorLoader.GetOrCreateInputSettings(global);
            UISettingsEditorLoader.GetOrCreateUISettings(global);
            var backend = ResBackendSettingsEditorLoader.GetOrCreateResourceBackendSettings(global);
            if (backend.ResourceBackend == EnvironmentState.ResourceBackendType.AssetBundle)
                ResBackendSettingsEditorLoader.GetOrCreateAssetBundleSettings(backend);
            else if (backend.ResourceBackend == EnvironmentState.ResourceBackendType.Addressables)
                ResBackendSettingsEditorLoader.GetOrCreateAddressablesSettings(backend);
            AudioSettingsEditorLoader.GetOrCreateAudioSettings(global);
            LocalizationSettingsEditorLoader.GetOrCreateLocalizationSettings(global);
            return global;
        }
    }
}
