using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FinkFramework.Runtime.Localization;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace FinkFramework.Editor.Modules.Localization
{
    /// <summary>
    /// 生成和校验本地化运行时清单。
    /// </summary>
    internal static class LocalizationManifestTool
    {
        public static bool Generate(
            LocalizationSettingsAsset settings,
            out string message)
        {
            if (!TryBuildManifest(settings, false, out LocalizationManifest manifest, out message))
                return false;

            try
            {
                string runtimeRoot = LocalizationPath.GetStreamingDataRoot();
                Directory.CreateDirectory(runtimeRoot);
                string manifestPath = LocalizationPath.GetManifestPath(runtimeRoot);
                string json = JsonUtility.ToJson(manifest, true) + "\n";
                LocalizationEditorFileUtil.WriteUtf8TextAtomic(manifestPath, json);
                message =
                    $"本地化运行时清单已生成：{manifestPath}（{manifest.files.Count} 个文件，" +
                    $"{manifest.keys.Count} 个 Key）";
                return true;
            }
            catch (Exception exception)
            {
                message = $"写入本地化运行时清单失败：{exception.Message}";
                return false;
            }
        }

        public static bool Validate(
            LocalizationSettingsAsset settings,
            out string message)
        {
            if (settings == null)
            {
                message = "LocalizationSettingsAsset 为空。";
                return false;
            }

            settings.NormalizeConfiguration();
            if (settings.Categories == null || settings.Categories.Count == 0)
            {
                message = "本地化配置没有主分类，无法进行构建。";
                return false;
            }

            if (settings.SupportedLocales == null || settings.SupportedLocales.Count == 0)
            {
                message = "本地化配置没有支持语言，无法进行构建。";
                return false;
            }

            if (!TryBuildManifest(settings, true, out LocalizationManifest expected, out message))
                return false;

            string manifestPath = LocalizationPath.GetManifestPath(
                LocalizationPath.GetStreamingDataRoot());
            if (!File.Exists(manifestPath))
            {
                message =
                    $"缺少本地化运行时清单：{manifestPath}。请保存配置或重新生成语言表。";
                return false;
            }

            LocalizationManifest actual;
            try
            {
                actual = JsonUtility.FromJson<LocalizationManifest>(
                    File.ReadAllText(manifestPath, Encoding.UTF8).TrimStart('\uFEFF'));
            }
            catch (Exception exception)
            {
                message = $"本地化运行时清单无法解析：{exception.Message}";
                return false;
            }

            if (!AreFileSetsEquivalent(expected, actual, out message))
                return false;

            message =
                $"本地化运行时文件集合和 Key 索引校验通过：{expected.files.Count} 个文件。";
            return true;
        }

        /// <summary>
        /// 从源语言表构建内存 Manifest；可选地要求配置中的每个文件都存在。
        /// </summary>
        private static bool TryBuildManifest(
            LocalizationSettingsAsset settings,
            bool requireAllFiles,
            out LocalizationManifest manifest,
            out string message)
        {
            manifest = new LocalizationManifest();
            message = null;
            if (settings == null)
            {
                message = "LocalizationSettingsAsset 为空。";
                return false;
            }

            settings.NormalizeConfiguration();
            if (settings.Categories == null || settings.SupportedLocales == null)
            {
                message = "本地化分类或支持语言列表为空。";
                return false;
            }

            string dataRoot = LocalizationPath.GetSourceDataRoot();
            // Key 在 Inspector、运行时 Manifest 和资源表中统一按不区分大小写处理。
            var keyCategories = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var errors = new List<string>();

            foreach (string category in settings.Categories)
            {
                if (!LocalizationPath.IsSafeCategory(category))
                {
                    errors.Add($"无效分类：{category}");
                    continue;
                }

                foreach (LocaleInfo locale in settings.SupportedLocales)
                {
                    if (locale == null || !LocaleInfo.TryNormalize(locale.Code, out string normalizedLocale))
                    {
                        errors.Add($"无效语言：{locale?.Code}");
                        continue;
                    }

                    string filePath = LocalizationPath.GetLanguageFilePath(
                        dataRoot,
                        category,
                        normalizedLocale,
                        settings.JsonFileNamePattern);
                    if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                    {
                        if (requireAllFiles)
                            errors.Add($"缺少语言文件：{category}/{normalizedLocale}");
                        continue;
                    }

                    if (!TryReadEntries(filePath, category, keyCategories, out List<string> keys, out string error))
                    {
                        errors.Add(error);
                        continue;
                    }

                    manifest.files.Add(new LocalizationManifestFile
                    {
                        category = category,
                        locale = normalizedLocale,
                        relativePath = NormalizeRelativePath(
                            Path.GetRelativePath(dataRoot, filePath)),
                        keys = keys
                    });
                }
            }

            if (errors.Count > 0)
            {
                message = BuildErrors(errors);
                return false;
            }

            manifest.files = manifest.files
                .OrderBy(item => item.category, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.locale, StringComparer.OrdinalIgnoreCase)
                .ToList();
            manifest.keys = keyCategories
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new LocalizationManifestKey
                {
                    key = pair.Key,
                    category = pair.Value
                })
                .ToList();
            return true;
        }

        /// <summary>
        /// 读取单个语言文件，校验 JSON 类型、完整 Key 以及跨分类重复。
        /// </summary>
        private static bool TryReadEntries(
            string filePath,
            string category,
            IDictionary<string, string> keyCategories,
            out List<string> keys,
            out string error)
        {
            keys = new List<string>();
            error = null;
            JObject document;
            try
            {
                document = JObject.Parse(
                    File.ReadAllText(filePath, Encoding.UTF8).TrimStart('\uFEFF'),
                    new JsonLoadSettings
                    {
                        DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error
                    });
            }
            catch (Exception exception)
            {
                error = $"语言文件无法解析：{filePath} → {exception.Message}";
                return false;
            }

            var fileKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (JProperty property in document.Properties())
            {
                if (string.IsNullOrWhiteSpace(property.Name))
                {
                    error = $"语言文件包含空 Key：{filePath}";
                    return false;
                }

                if (property.Value.Type != JTokenType.String)
                {
                    error = $"语言文件的 Value 必须是字符串：{filePath} / {property.Name}";
                    return false;
                }

                string fullKey = LocalizationKeyUtility.BuildFullKey(
                    category,
                    property.Name);
                if (string.IsNullOrWhiteSpace(fullKey))
                {
                    error = $"语言文件的 Key 无法补充分类前缀：{filePath} / {property.Name}";
                    return false;
                }

                if (!fileKeys.Add(fullKey))
                {
                    error = $"语言文件包含重复 Key：{filePath} / {fullKey}";
                    return false;
                }

                if (keyCategories.TryGetValue(fullKey, out string existingCategory)
                    && !string.Equals(existingCategory, category, StringComparison.OrdinalIgnoreCase))
                {
                    error =
                        $"Key 同时存在于多个分类：{fullKey}（{existingCategory}、{category}）";
                    return false;
                }

                keyCategories[fullKey] = category;
                keys.Add(fullKey);
            }

            keys.Sort(StringComparer.Ordinal);
            return true;
        }

        /// <summary>
        /// 比较两个 Manifest 的文件身份和 Key 签名，不受文件排列顺序影响。
        /// </summary>
        private static bool AreFileSetsEquivalent(
            LocalizationManifest expected,
            LocalizationManifest actual,
            out string message)
        {
            message = null;
            if (actual == null || actual.version != expected.version)
            {
                message = "本地化运行时清单版本不匹配。请重新生成清单。";
                return false;
            }

            var expectedFiles = expected.files ?? new List<LocalizationManifestFile>();
            var actualFiles = actual.files ?? new List<LocalizationManifestFile>();
            var expectedFileMap = BuildFileMap(expectedFiles);
            var actualFileMap = BuildFileMap(actualFiles);
            if (expectedFileMap == null
                || actualFileMap == null
                || expectedFileMap.Count != actualFileMap.Count
                || expectedFileMap.Any(pair =>
                    !actualFileMap.TryGetValue(pair.Key, out string actualKeys)
                    || !string.Equals(pair.Value, actualKeys, StringComparison.Ordinal)))
            {
                message = "本地化运行时清单的文件或 Key 索引与源目录不一致。请保存并同步运行时副本。";
                return false;
            }

            return true;
        }

        /// <summary>
        /// 将 Manifest 文件列表转换为唯一身份到排序 Key 签名的映射。
        /// </summary>
        private static Dictionary<string, string> BuildFileMap(
            IEnumerable<LocalizationManifestFile> files)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (LocalizationManifestFile file in files ?? Enumerable.Empty<LocalizationManifestFile>())
            {
                string identity = BuildFileIdentity(file);
                if (string.IsNullOrEmpty(identity) || result.ContainsKey(identity))
                    return null;

                string keySignature = string.Join(
                    "\u001f",
                    (file.keys ?? new List<string>())
                    .Select(key => key ?? string.Empty)
                    .OrderBy(key => key, StringComparer.Ordinal));
                result.Add(identity, keySignature);
            }

            return result;
        }

        /// <summary>
        /// 构造分类、语言和相对路径组成的文件唯一标识。
        /// </summary>
        private static string BuildFileIdentity(LocalizationManifestFile file)
        {
            if (file == null)
                return string.Empty;

            return $"{file.category}\n{file.locale}\n{file.relativePath}";
        }

        private static string NormalizeRelativePath(string path)
        {
            return path.Replace(Path.DirectorySeparatorChar, '/')
                .Replace(Path.AltDirectorySeparatorChar, '/');
        }

        private static string BuildErrors(IReadOnlyList<string> errors)
        {
            var builder = new StringBuilder();
            builder.AppendLine("本地化运行时清单生成/校验失败：");
            for (int i = 0; i < errors.Count && i < 12; i++)
                builder.AppendLine($"- {errors[i]}");

            if (errors.Count > 12)
                builder.AppendLine($"- 其余 {errors.Count - 12} 个问题已省略。 ");

            return builder.ToString().TrimEnd();
        }
    }
}
