using System;

namespace FinkFramework.Runtime.Localization
{
    /// <summary>
    /// 本地化 Key 的分类前缀工具。
    ///
    /// 配置中的分类名称可以保留编辑器显示大小写，例如 UI；
    /// 写入运行时数据时，分类前缀统一使用小写，例如 ui.main_menu.start_game。
    /// </summary>
    public static class LocalizationKeyUtility
    {
        /// <summary>
        /// 将编辑器中的分类内 Key 转换为完整运行时 Key。
        /// 已经带有当前分类前缀的 Key 不会重复添加前缀。
        /// </summary>
        public static string BuildFullKey(string category, string key)
        {
            if (string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(key))
                return null;

            string prefix = GetCategoryPrefix(category);
            string trimmedKey = key.Trim();
            if (string.Equals(trimmedKey, prefix, StringComparison.OrdinalIgnoreCase))
                return prefix;

            string prefixWithSeparator = prefix + ".";
            if (trimmedKey.StartsWith(prefixWithSeparator, StringComparison.OrdinalIgnoreCase))
                return prefix + trimmedKey.Substring(prefix.Length);

            return prefixWithSeparator + trimmedKey;
        }

        /// <summary>
        /// 将完整 Key 转换为当前分类下的编辑器 Key。
        /// 兼容尚未带分类前缀的旧 Key。
        /// </summary>
        public static string GetCategoryRelativeKey(string category, string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return string.Empty;

            string trimmedKey = key.Trim();
            if (string.IsNullOrWhiteSpace(category))
                return trimmedKey;

            string prefix = GetCategoryPrefix(category);
            if (string.Equals(trimmedKey, prefix, StringComparison.OrdinalIgnoreCase))
                return string.Empty;

            string prefixWithSeparator = prefix + ".";
            return trimmedKey.StartsWith(prefixWithSeparator, StringComparison.OrdinalIgnoreCase)
                ? trimmedKey.Substring(prefixWithSeparator.Length)
                : trimmedKey;
        }

        /// <summary>
        /// 判断 Key 是否已经带有指定分类的完整前缀。
        /// </summary>
        public static bool IsFullKeyForCategory(string category, string key)
        {
            if (string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(key))
                return false;

            string prefix = GetCategoryPrefix(category);
            string trimmedKey = key.Trim();
            return string.Equals(trimmedKey, prefix, StringComparison.OrdinalIgnoreCase)
                || trimmedKey.StartsWith(prefix + ".", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 获取用于运行时完整 Key 的规范化分类前缀。
        /// </summary>
        private static string GetCategoryPrefix(string category)
        {
            return category.Trim().ToLowerInvariant();
        }
    }
}
