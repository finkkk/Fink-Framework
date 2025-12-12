using FinkFramework.Runtime.Utils;
using UnityEngine;

namespace FinkFramework.Runtime.Settings
{
    /// <summary>
    /// 全局配置 SO 只读加载器（Runtime专用）
    /// </summary>
    public static class GlobalSettings
    {
        private static GlobalSettingsAsset _instance;
        
        /// <summary>
        /// 获取框架的全局配置（只读）
        /// </summary>
        public static GlobalSettingsAsset Current
        {
            get
            {
                if (_instance)
                    return _instance;

                _instance = Resources.Load<GlobalSettingsAsset>("FinkFramework/GlobalSettingsAsset");

#if UNITY_EDITOR
                if (!_instance)
                {
                    LogUtil.Error("FinkFramework","GlobalSettingsAsset 缺失！请在 Editor 中打开 Settings 面板以自动修复。");
                }
#endif
                return _instance;
            }
        }
        
        public static bool TryGet(out GlobalSettingsAsset settings)
        {
            // 已经加载成功
            if (_instance != null)
            {
                settings = _instance;
                return true;
            }

            // 尝试加载
            _instance = Resources.Load<GlobalSettingsAsset>("FinkFramework/GlobalSettingsAsset");

            // 如果仍然没有，表示还没加载到，但不是错误（例如首次导入）
            if (!_instance)
            {
                settings = null;
                return false;
            }

            settings = _instance;
            return true;
        }
    }
}