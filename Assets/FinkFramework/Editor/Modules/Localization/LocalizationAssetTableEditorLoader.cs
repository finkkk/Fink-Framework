using FinkFramework.Runtime.Localization;
using UnityEditor;
using UnityEngine;

namespace FinkFramework.Editor.Modules.Localization
{
    /// <summary>
    /// 本地化资源表资产的 Editor-only 加载器。
    /// 资源表和本地化设置一样属于可直接从菜单打开的核心配置，缺失时应自行恢复。
    /// </summary>
    internal static class LocalizationAssetTableEditorLoader
    {
        /// <summary>
        /// 加载资源本地化表；资产或父目录不存在时创建空表。
        /// </summary>
        public static LocalizationAssetTable LoadOrCreate()
        {
            const string assetPath = LocalizationPath.AssetTablePath;
            EnsureParentDirectoryExists(assetPath);

            LocalizationAssetTable asset = AssetDatabase.LoadAssetAtPath<LocalizationAssetTable>(assetPath);
            if (asset != null)
                return asset;

            asset = ScriptableObject.CreateInstance<LocalizationAssetTable>();
            AssetDatabase.CreateAsset(asset, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return asset;
        }

        /// <summary>
        /// 递归创建 Unity AssetDatabase 所需的父目录，不使用文件系统 API 绕过导入流程。
        /// </summary>
        private static void EnsureParentDirectoryExists(string assetPath)
        {
            string folderPath = assetPath[..assetPath.LastIndexOf('/')];
            if (AssetDatabase.IsValidFolder(folderPath))
                return;

            string[] parts = folderPath.Split('/');
            string current = parts[0];
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
