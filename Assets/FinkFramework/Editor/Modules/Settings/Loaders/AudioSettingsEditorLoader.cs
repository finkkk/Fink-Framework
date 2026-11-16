using FinkFramework.Runtime.Settings.ScriptableObjects;

namespace FinkFramework.Editor.Modules.Settings.Loaders
{
    /// <summary>Audio 配置的编辑器加载、创建及绑定入口，不迁移旧字段。</summary>
    public static class AudioSettingsEditorLoader
    {
        /// <summary>默认配置资产路径；Runtime 沿全局引用读取。</summary>
        public const string AssetPath = "Assets/FinkFramework_Assets/Resources/FinkFramework/Settings/AudioSettingsAsset.asset";

        /// <summary>沿全局引用获取模块配置，缺失时创建并保存。必须在 Unity 主线程调用。</summary>
        /// <returns>已有或新创建的配置；路径冲突时抛出配置错误。</returns>
        public static AudioSettingsAsset LoadOrCreate() => GlobalSettingsEditorLoader.LoadOrCreate().AudioSettings;

        /// <summary>复用引用，或创建默认 SO 并绑定。必须在 Unity 主线程调用，不覆盖占用路径。</summary>
        /// <param name="global">要绑定的全局配置。</param>
        /// <returns>已有或新创建的模块配置；路径冲突时抛出配置错误。</returns>
        public static AudioSettingsAsset GetOrCreateAudioSettings(GlobalSettingsAsset global)
            => SettingsAssetEditorUtility.Bind(global, global.AudioSettings, AssetPath, value => global.AudioSettings = value);

    }
}
