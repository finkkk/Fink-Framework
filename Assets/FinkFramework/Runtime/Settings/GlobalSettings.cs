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

                _instance = Resources.Load<GlobalSettingsAsset>("GlobalSettingsAsset");

#if UNITY_EDITOR
                if (!_instance)
                {
                    LogUtil.Error("FinkFramework","GlobalSettingsAsset 缺失！请在 Editor 中打开 Settings 面板以自动修复。");
                }
#endif
                return _instance;
            }
        }
    }
}