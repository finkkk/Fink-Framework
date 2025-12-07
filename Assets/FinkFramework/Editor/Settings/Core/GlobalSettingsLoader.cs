using FinkFramework.Runtime.Settings;
using FinkFramework.Runtime.Utils;
using UnityEditor;
using UnityEngine;

namespace FinkFramework.Editor.Settings.Core
{
    /// <summary>
    /// 全局配置文件加载器（Editor-only）
    /// </summary>
    public static class GlobalSettingsLoader
    {
        /// <summary>
        /// 全局配置文件的存储路径
        /// </summary>
        public const string AssetPath = "Assets/FinkFramework/Resources/GlobalSettingsAsset.asset";
        
        /// <summary>
        /// 加载全局设置 SO，如果不存在就自动创建。
        /// </summary>
        public static GlobalSettingsAsset LoadOrCreate()
        {
            // 尝试加载
            var asset = AssetDatabase.LoadAssetAtPath<GlobalSettingsAsset>(AssetPath);

            // 文件不存在 → 创建新文件
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<GlobalSettingsAsset>();
                AssetDatabase.CreateAsset(asset, AssetPath);
                AssetDatabase.SaveAssets();

                LogUtil.Info("FinkFramework", "已自动创建 GlobalSettingsAsset");
            }

            return asset;
        }
    }
}