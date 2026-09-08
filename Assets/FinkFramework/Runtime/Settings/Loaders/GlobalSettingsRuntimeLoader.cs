using FinkFramework.Runtime.Settings.ScriptableObjects;
using FinkFramework.Runtime.Utils;
using UnityEngine;

namespace FinkFramework.Runtime.Settings.Loaders
{
    /// <summary>
    /// 全局配置 SO 只读加载器（Runtime 专用）。
    ///
    /// 配置资产位于项目外部框架数据目录：
    /// Assets/FinkFramework_Assets/Resources/FinkFramework/Settings/Global/GlobalSettingsAsset.asset
    ///
    /// Resources.Load 使用的是 Resources 后的资源相对路径，因此这里不应写 Assets 前缀，
    /// 也不依赖资产位于哪个顶层目录。
    /// </summary>
    public static class GlobalSettingsRuntimeLoader
    {
        private static GlobalSettingsAsset _instance;
        
        private const string ResourcesPath = "FinkFramework/Settings/Global/GlobalSettingsAsset";

#if UNITY_EDITOR
        private const string EditorAssetPath =
            "Assets/FinkFramework_Assets/Resources/FinkFramework/Settings/Global/GlobalSettingsAsset.asset";
#endif
        
        /// <summary>
        /// 获取框架的全局配置（只读）。
        /// </summary>
        public static GlobalSettingsAsset Current
        {
            get
            {
                if (_instance)
                    return _instance;

                _instance = Resources.Load<GlobalSettingsAsset>(ResourcesPath);

#if UNITY_EDITOR
                if (!_instance)
                {
                    LogUtil.Error(
                        "FinkFramework",
                        $"未找到外部 GlobalSettingsAsset：{EditorAssetPath}。"
                        + "请确认该资产已被 Unity 导入，并位于 Resources 目录下。");
                }
#endif
                return _instance;
            }
        }
        
        public static bool TryGet(out GlobalSettingsAsset settings)
        {
            // 已经加载成功
            if (_instance)
            {
                settings = _instance;
                return true;
            }

            // 尝试加载
            _instance = Resources.Load<GlobalSettingsAsset>(ResourcesPath);

            // 如果仍然没有，表示还没加载到，但不是错误（例如首次导入）
            if (!_instance)
            {
                settings = null;
                return false;
            }

            settings = _instance;
            return true;
        }

        /// <summary>
        /// 获取所有脚本生成器共用的 Assets 下相对脚本根目录。
        /// 配置缺失或内容无效时回退到默认值，保证路径计算始终可用。
        /// </summary>
        public static string ScriptRootDirectory
        {
            get
            {
                return GlobalSettingsAsset.NormalizeScriptRootDirectory(
                    TryGet(out GlobalSettingsAsset settings)
                        ? settings?.ScriptRootDirectory
                        : null);
            }
        }
    }
}
