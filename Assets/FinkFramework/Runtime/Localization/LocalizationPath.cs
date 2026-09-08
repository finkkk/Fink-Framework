using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace FinkFramework.Runtime.Localization
{
    /// <summary>
    /// 本地化模块的统一路径访问工具。
    ///
    /// 运行时语言文件固定约定为：
    /// - Assets/StreamingAssets/FinkFramework_Data/Localization/{Category}/{locale}.json
    /// - Assets/StreamingAssets/FinkFramework_Data/localization-manifest.json
    /// </summary>
    public static class LocalizationPath
    {
        // 这是运行时数据目录的固定相对路径，不从项目配置读取。
        public const string DefaultDataRoot = "FinkFramework_Data/Localization";
        // 这是编辑器维护的项目内源文件目录，不属于运行时资源目录。
        public const string DefaultSourceDataRoot = "FinkFramework_Data/Localization";
        public const string ManifestFileName = "localization-manifest.json";
        // 本地化配置和资源表属于项目数据，不属于可更新的框架目录；路径固定且不开放配置。
        public const string SettingsAssetPath =
            "Assets/FinkFramework_Assets/Resources/FinkFramework/Settings/Localization/LocalizationSettingsAsset.asset";
        public const string SettingsResourcesPath =
            "FinkFramework/Settings/Localization/LocalizationSettingsAsset";
        public const string AssetTablePath =
            "Assets/FinkFramework_Assets/Resources/FinkFramework/Localization/LocalizationAssetTable.asset";
        public const string AssetTableResourcesPath =
            "FinkFramework/Localization/LocalizationAssetTable";

        /// <summary>
        /// Unity 项目根目录。
        /// </summary>
        public static string ProjectRoot => NormalizeFileSystemPath(Path.GetFullPath(Path.Combine(Application.dataPath, "..")));

        /// <summary>
        /// 获取固定的 StreamingAssets 数据根目录。
        /// URI 形式的 StreamingAssets 路径会原样保留，供上层决定使用文件 API 还是 UnityWebRequest。
        /// </summary>
        public static string GetStreamingDataRoot()
        {
            return CombinePath(Application.streamingAssetsPath, DefaultDataRoot);
        }

        /// <summary>
        /// 获取编辑器维护的本地化源文件目录。
        /// 源文件位于项目根目录，便于版本控制；StreamingAssets 目录只保存自动生成的运行时副本。
        /// </summary>
        public static string GetSourceDataRoot()
        {
            return CombinePath(ProjectRoot, DefaultSourceDataRoot);
        }

        /// <summary>
        /// 返回固定的运行时语言表根目录。
        /// </summary>
        public static IEnumerable<string> GetCandidateRoots(LocalizationSettingsAsset settings)
        {
            if (settings == null)
                yield break;

            yield return GetStreamingDataRoot();
        }

        /// <summary>
        /// 获取分类目录。分类只能是单层目录名，避免路径穿越和隐式嵌套。
        /// </summary>
        public static string GetCategoryDirectory(string root, string category)
        {
            if (string.IsNullOrWhiteSpace(root) || !IsSafeCategory(category))
                return null;

            return CombinePath(root, category.Trim());
        }

        /// <summary>
        /// 获取运行时本地化清单路径。
        /// 清单位于 Localization 数据目录上一级，和其他 Fink 数据清单并列。
        /// </summary>
        public static string GetManifestPath(string root)
        {
            if (string.IsNullOrWhiteSpace(root))
                return null;

            string parentRoot = GetParentPath(root);
            return string.IsNullOrWhiteSpace(parentRoot)
                ? null
                : CombinePath(parentRoot, ManifestFileName);
        }

        /// <summary>
        /// 获取指定语言的 JSON 文件路径。
        /// </summary>
        public static string GetLanguageFilePath(
            string root,
            string category,
            string localeCode,
            string fileNamePattern)
        {
            if (!LocaleInfo.TryNormalize(localeCode, out string normalizedLocale))
                return null;

            string categoryDirectory = GetCategoryDirectory(root, category);
            if (categoryDirectory == null)
                return null;

            string fileName = GetLanguageFileName(normalizedLocale, fileNamePattern);
            return CombinePath(categoryDirectory, fileName);
        }

        /// <summary>
        /// 根据配置格式生成语言文件名。
        /// </summary>
        public static string GetLanguageFileName(string localeCode, string fileNamePattern)
        {
            string normalizedLocale = LocaleInfo.Normalize(localeCode);
            string localeFilePart = normalizedLocale.Replace('-', '_').ToLowerInvariant();

            // 没有 {0} 时所有语言都会得到同一个文件名，后续写入会互相覆盖；
            // 直接回退到默认格式比接受一个看似合法但不可用的配置更安全。
            if (string.IsNullOrWhiteSpace(fileNamePattern)
                || fileNamePattern.IndexOf("{0}", StringComparison.Ordinal) < 0)
                return LocaleInfo.ToFileName(normalizedLocale);

            try
            {
                string fileName = string.Format(fileNamePattern, localeFilePart);
                return IsSafeFileName(fileName)
                    ? fileName
                    : LocaleInfo.ToFileName(normalizedLocale);
            }
            catch (FormatException)
            {
                return LocaleInfo.ToFileName(normalizedLocale);
            }
        }

        /// <summary>
        /// 将项目绝对路径转换为编辑器中更易读的显示路径。
        /// Assets 下的路径保留 Assets 前缀，项目根目录下的外部路径使用“项目根目录/”前缀。
        /// </summary>
        public static string ToProjectDisplayPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            string normalizedPath = NormalizeFileSystemPath(path);
            string normalizedProjectRoot = ProjectRoot.TrimEnd('/');
            if (string.Equals(normalizedPath, normalizedProjectRoot, StringComparison.OrdinalIgnoreCase))
                return "项目根目录";

            string projectPrefix = normalizedProjectRoot + "/";
            if (!normalizedPath.StartsWith(projectPrefix, StringComparison.OrdinalIgnoreCase))
                return normalizedPath;

            string relativePath = normalizedPath.Substring(projectPrefix.Length);
            if (relativePath.StartsWith("Assets/StreamingAssets/", StringComparison.OrdinalIgnoreCase))
                return relativePath.Substring("Assets/".Length);

            return relativePath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)
                ? relativePath
                : "项目根目录/" + relativePath;
        }

        public static bool IsSafeCategory(string category)
        {
            string value = category?.Trim();
            return !string.IsNullOrEmpty(value)
                && value != "."
                && value != ".."
                && value.IndexOfAny(new[] { '/', '\\' }) < 0
                && value.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
        }

        /// <summary>
        /// 校验文件名是否为单层安全文件名，防止配置生成越界路径。
        /// </summary>
        private static bool IsSafeFileName(string fileName)
        {
            string value = fileName?.Trim();
            return !string.IsNullOrEmpty(value)
                && value != "."
                && value != ".."
                && value.IndexOfAny(new[] { '/', '\\' }) < 0
                && value.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
        }

        /// <summary>
        /// 拼接文件系统路径或 URI 路径，并统一分隔符格式。
        /// </summary>
        private static string CombinePath(string first, string second)
        {
            if (IsUri(first))
                return first.TrimEnd('/', '\\') + "/" + second.Trim('/', '\\');

            return NormalizeFileSystemPath(Path.GetFullPath(Path.Combine(first, second)));
        }

        /// <summary>
        /// 获取文件系统路径或 URI 的父目录。
        /// </summary>
        private static string GetParentPath(string path)
        {
            if (IsUri(path))
            {
                string normalized = path.TrimEnd('/', '\\');
                int separatorIndex = normalized.LastIndexOf('/');
                int schemeEndIndex = normalized.IndexOf("://", StringComparison.Ordinal) + 2;
                return separatorIndex <= schemeEndIndex
                    ? null
                    : normalized.Substring(0, separatorIndex);
            }

            DirectoryInfo parent = Directory.GetParent(path);
            return parent?.FullName;
        }

        /// <summary>
        /// 将 Windows 和 Unix 分隔符统一为编辑器显示使用的正斜杠。
        /// </summary>
        private static string NormalizeFileSystemPath(string path)
        {
            return path.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
        }

        /// <summary>
        /// 判断路径是否包含 URI 协议前缀。
        /// </summary>
        private static bool IsUri(string path)
        {
            return !string.IsNullOrWhiteSpace(path)
                && path.IndexOf("://", StringComparison.Ordinal) >= 0;
        }
    }
}
