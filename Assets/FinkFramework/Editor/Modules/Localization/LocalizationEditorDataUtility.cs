using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FinkFramework.Runtime.Localization;

namespace FinkFramework.Editor.Modules.Localization
{
    /// <summary>
    /// 编辑器读取本地化源数据时使用的共享辅助方法。
    /// </summary>
    internal static class LocalizationEditorDataUtility
    {
        /// <summary>
        /// 返回配置中的分类；配置尚未保存分类时，从源数据目录的一级子目录自动发现分类。
        /// </summary>
        public static IReadOnlyList<string> GetCategories(LocalizationSettingsAsset settings)
        {
            if (settings?.Categories is { Count: > 0 })
                return settings.Categories;

            return DiscoverCategories();
        }

        /// <summary>
        /// 扫描源数据目录中的一级分类目录，不读取或修改配置资产。
        /// </summary>
        public static IReadOnlyList<string> DiscoverCategories()
        {
            string sourceRoot = LocalizationPath.GetSourceDataRoot();
            if (!Directory.Exists(sourceRoot))
                return Array.Empty<string>();

            return Directory.GetDirectories(sourceRoot, "*", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName)
                .Where(LocalizationPath.IsSafeCategory)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
