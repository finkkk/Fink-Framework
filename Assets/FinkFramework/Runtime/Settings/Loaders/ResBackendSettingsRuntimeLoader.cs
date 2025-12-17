using FinkFramework.Runtime.Settings.ScriptableObjects;
using UnityEngine;

namespace FinkFramework.Runtime.Settings.Loaders
{
    /// <summary>
    /// 资源后端配置 SO 只读加载器（Runtime 专用）
    /// </summary>
    public class ResBackendSettingsRuntimeLoader
    {
        /// <summary>
        /// 获取当前 Addressables 后端配置
        /// 若未启用 Addressables 或未配置，则返回 null
        /// </summary>
        public static AddressablesBackendSettingsAsset Addressables
        {
            get
            {
                if (!GlobalSettingsRuntimeLoader.TryGet(out var global))
                    return null;

                if (global.ResourceBackend != Environments.EnvironmentState.ResourceBackendType.Addressables)
                    return null;

                return global.AddressablesSettings;
            }
        }

        /// <summary>
        /// 获取当前 AssetBundle 后端配置
        /// 若未启用 AssetBundle 或未配置，则返回 null
        /// </summary>
        public static AssetBundleBackendSettingsAsset AssetBundle
        {
            get
            {
                if (!GlobalSettingsRuntimeLoader.TryGet(out var global))
                    return null;

                if (global.ResourceBackend != Environments.EnvironmentState.ResourceBackendType.AssetBundle)
                    return null;

                return global.AssetBundleSettings;
            }
        }
        
        /// <summary>
        /// 尝试获取当前资源后端配置（非关心具体类型的通用入口）
        /// </summary>
        public static bool TryGetCurrent(out ScriptableObject backendSettings)
        {
            backendSettings = null;

            if (!GlobalSettingsRuntimeLoader.TryGet(out var global))
                return false;

            switch (global.ResourceBackend)
            {
                case Environments.EnvironmentState.ResourceBackendType.AssetBundle:
                    backendSettings = global.AssetBundleSettings;
                    break;

                case Environments.EnvironmentState.ResourceBackendType.Addressables:
                    backendSettings = global.AddressablesSettings;
                    break;

                default:
                    return false;
            }

            return backendSettings != null;
        }
    }
}