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
        public const string AssetPath = "Assets/FinkFramework/Runtime/Resources/GlobalSettingsAsset.asset";
        
        /// <summary>
        /// 加载全局设置 SO，如果不存在就自动创建。
        /// </summary>
        public static GlobalSettingsAsset LoadOrCreate()
        {
            EnsureParentDirectoryExists(AssetPath);
            // 尝试加载
            var asset = AssetDatabase.LoadAssetAtPath<GlobalSettingsAsset>(AssetPath);

            // 文件不存在 → 创建新文件
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<GlobalSettingsAsset>();
                AssetDatabase.CreateAsset(asset, AssetPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                LogUtil.Info("FinkFramework", "已自动创建 GlobalSettingsAsset");
            }

            return asset;
        }
        
        /// <summary>
        /// 自动递归创建父目录（Unity 官方不会自动创建）
        /// </summary>
        private static void EnsureParentDirectoryExists(string assetPath)
        {
            string folderPath = assetPath.Substring(0, assetPath.LastIndexOf('/'));

            if (AssetDatabase.IsValidFolder(folderPath))
                return;

            string[] parts = folderPath.Split('/');
            string current = parts[0]; // "Assets"

            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";

                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);

                current = next;
            }
        }
    }
}