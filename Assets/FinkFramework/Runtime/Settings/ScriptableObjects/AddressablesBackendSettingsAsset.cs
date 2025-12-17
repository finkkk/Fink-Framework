using UnityEngine;

namespace FinkFramework.Runtime.Settings.ScriptableObjects
{
    /// <summary>
    /// Addressables 后端配置
    /// </summary>
    public class AddressablesBackendSettingsAsset : ScriptableObject
    {
        [Header("加载策略")]
        [Tooltip("是否允许使用同步 Load（不推荐在运行期使用）")]
        public bool AllowSyncLoad = false;
    }
}