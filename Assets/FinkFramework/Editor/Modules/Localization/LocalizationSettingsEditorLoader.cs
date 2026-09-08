using FinkFramework.Runtime.Localization;
using FinkFramework.Editor.Modules.Settings.Loaders;
using UnityEditor;
using UnityEngine;

namespace FinkFramework.Editor.Modules.Localization
{
    /// <summary>
    /// 本地化配置资产的 Editor-only 加载器。
    /// </summary>
    internal static class LocalizationSettingsEditorLoader
    {
        public const string AssetPath = LocalizationPath.SettingsAssetPath;

        public static LocalizationSettingsAsset LoadOrCreate()
        {
            // 本地化设置页面可从菜单直接打开，不能依赖用户先浏览其他设置页面。
            // 先补齐框架全局设置，保持所有 Project Settings 模块的初始化契约一致。
            GlobalSettingsEditorLoader.LoadOrCreate();
            EnsureParentDirectoryExists(AssetPath);

            var asset = AssetDatabase.LoadAssetAtPath<LocalizationSettingsAsset>(AssetPath);
            if (asset != null)
                return asset;

            asset = ScriptableObject.CreateInstance<LocalizationSettingsAsset>();
            AssetDatabase.CreateAsset(asset, AssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return asset;
        }

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
