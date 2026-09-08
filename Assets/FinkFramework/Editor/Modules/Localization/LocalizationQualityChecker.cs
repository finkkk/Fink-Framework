using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using FinkFramework.Runtime.Environments;
using FinkFramework.Runtime.Localization;
using FinkFramework.Runtime.Utils;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_TEXTMESHPRO
using TMPro;
#endif
#if ENABLE_UGUI
using UnityEngine.UI;
#endif

namespace FinkFramework.Editor.Modules.Localization
{
    /// <summary>
    /// 本地化语言表的只读质量检查器，结果输出到 Unity Console。
    /// </summary>
    public static class LocalizationQualityChecker
    {
        private const string LogModule = "LocalizationQATool";
        private const int MaxLoggedIssuesPerType = 20;
        private const string FrameworkLocalizationRuntimePath =
            "Assets/FinkFramework/Runtime/Localization/";
        private static readonly Regex LiteralLocalizationCallPattern = new Regex(
            @"\bLocalizationManager\s*\.\s*(?:Get|TryGet|Format)(?:Async)?\s*\(\s*""(?<key>(?:\\.|[^""\\])*)""",
            RegexOptions.Compiled);
        private static readonly Regex DynamicLocalizationCallPattern = new Regex(
            @"\bLocalizationManager\s*\.\s*(?:Get|TryGet|Format)(?:Async)?\s*\(\s*(?<argument>[^,\)\r\n]+)",
            RegexOptions.Compiled);
        private static readonly Regex StrictKeyPattern = new Regex(
            @"^[a-z0-9_]+(?:\.[a-z0-9_]+)+$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private enum IssueSeverity
        {
            Error,
            Warning
        }

        private sealed class Issue
        {
            public IssueSeverity Severity;
            public string Message;
            public string Location;
        }

        private sealed class ProjectReferenceScanResult
        {
            public int SceneCount;
            public int PrefabCount;
            public int ComponentCount;
            public int ScriptCount;
            public int ScriptReferenceCount;
            public int DynamicScriptReferenceCount;
            public int DataTableReferenceCount;
            public int StaticTextCount;
            public int UnsupportedLocalizedComponentCount;
            public int ResourceComponentCount;
            public readonly HashSet<string> UsedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public readonly Dictionary<string, List<string>> BindingLocations =
                new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 扫描工程中对指定 Key 的本地化引用，供编辑器重命名提示使用。
        /// </summary>
        public static IReadOnlyList<string> FindKeyReferences(
            LocalizationSettingsAsset settings,
            string key)
        {
            var references = new List<string>();
            if (settings == null || string.IsNullOrWhiteSpace(key))
                return references;

            var allKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { key.Trim() };
            var result = new ProjectReferenceScanResult();
            var issues = new List<Issue>();
            ScanProjectReferences(
                allKeys,
                LoadValidAssetKeys(),
                result,
                issues,
                false);

            if (result.BindingLocations.TryGetValue(key.Trim(), out List<string> locations))
                references.AddRange(locations);

            return references;
        }

        /// <summary>
        /// 加载当前配置并执行本地化质量检查。
        /// </summary>
        public static void Run()
        {
            Run(LocalizationSettingsEditorLoader.LoadOrCreate(), false);
        }

        /// <summary>
        /// 使用指定配置执行本地化质量检查。
        /// </summary>
        public static void Run(LocalizationSettingsAsset settings)
        {
            Run(settings, false);
        }

        /// <summary>
        /// 使用指定配置执行本地化质量检查，并选择是否扫描未接入本地化的静态文本。
        /// </summary>
        public static void Run(
            LocalizationSettingsAsset settings,
            bool scanUnlocalizedStaticText)
        {
            if (settings == null)
            {
                LogUtil.Error(LogModule, "LocalizationSettingsAsset 不存在。");
                return;
            }

            if (!LocalizationDataSyncUtility.PrepareSourceDataRoot(
                    out string prepareMessage))
            {
                LogUtil.Error(LogModule, prepareMessage);
                return;
            }

            var issues = new List<Issue>();
            List<LocaleInfo> locales = GetLocales(settings, issues);
            List<string> categories = GetCategories(settings, issues);
            string configuredDefaultLocale = GetConfiguredLocaleCode(settings, "defaultLocale");
            string defaultLocale = LocaleInfo.TryNormalize(
                configuredDefaultLocale,
                out string normalizedDefaultLocale)
                ? normalizedDefaultLocale
                : null;

            if (!string.IsNullOrEmpty(defaultLocale)
                && !locales.Any(locale => string.Equals(
                    locale.Code,
                    defaultLocale,
                    StringComparison.OrdinalIgnoreCase)))
            {
                AddIssue(
                    issues,
                    IssueSeverity.Error,
                    $"默认语言未包含在支持语言列表中：{defaultLocale}",
                    "本地化设置");
            }

            LogUtil.Info(
                LogModule,
                $"开始执行{(scanUnlocalizedStaticText ? "完整" : "快速")}本地化质量检查" +
                $"（{categories.Count} 个分类，{locales.Count} 种语言）...");

            if (locales.Count == 0)
                AddIssue(issues, IssueSeverity.Error, "没有配置任何支持语言。", "本地化设置");

            if (categories.Count == 0)
                AddIssue(issues, IssueSeverity.Error, "没有配置任何语言表分类。", "本地化设置");

            HashSet<string> assetKeys = CheckAssetTable(
                locales,
                defaultLocale,
                issues);

            if (locales.Count == 0 || categories.Count == 0)
            {
                LogIssues(issues);
                LogSummary(issues);
                return;
            }

            string dataRoot = LocalizationPath.GetSourceDataRoot();
            if (!Directory.Exists(dataRoot))
            {
                AddIssue(
                    issues,
                    IssueSeverity.Warning,
                    "翻译数据目录不存在，请先在本地化配置中点击“保存并应用配置”创建目录和语言表。",
                    dataRoot);
                LogIssues(issues);
                LogSummary(issues);
                return;
            }

            var globalKeyOwners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var allKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string category in categories)
            {
                CheckCategory(
                    settings,
                    dataRoot,
                    category,
                    locales,
                    defaultLocale,
                    globalKeyOwners,
                    allKeys,
                    issues);
            }

            CheckUnconfiguredCategoryDirectories(dataRoot, categories, issues);

            var referenceScan = new ProjectReferenceScanResult();
            ScanProjectReferences(
                allKeys,
                assetKeys,
                referenceScan,
                issues,
                scanUnlocalizedStaticText);
            LogUnusedKeys(allKeys, referenceScan.UsedKeys, issues);

            LogIssues(issues);
            LogSummary(issues);
        }

        private static void CheckCategory(
            LocalizationSettingsAsset settings,
            string dataRoot,
            string category,
            IReadOnlyList<LocaleInfo> locales,
            string defaultLocale,
            IDictionary<string, string> globalKeyOwners,
            ISet<string> allKeys,
            ICollection<Issue> issues)
        {
            if (!LocalizationPath.IsSafeCategory(category))
            {
                AddIssue(issues, IssueSeverity.Error, "分类名称不安全，不能包含路径分隔符。", category);
                return;
            }

            string categoryDirectory = LocalizationPath.GetCategoryDirectory(dataRoot, category);
            if (!Directory.Exists(categoryDirectory))
            {
                AddIssue(
                    issues,
                    IssueSeverity.Warning,
                    "分类目录不存在，当前分类没有可扫描的语言表。",
                    categoryDirectory);
                return;
            }

            CheckPhysicalLanguageFiles(
                categoryDirectory,
                locales,
                settings.JsonFileNamePattern,
                issues);

            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var valuesByLocale = new Dictionary<string, Dictionary<string, string>>(
                StringComparer.OrdinalIgnoreCase);

            foreach (LocaleInfo locale in locales)
            {
                var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                valuesByLocale[locale.Code] = values;

                string filePath = LocalizationPath.GetLanguageFilePath(
                    dataRoot,
                    category,
                    locale.Code,
                    settings.JsonFileNamePattern);
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                {
                    AddIssue(
                        issues,
                        IsDefaultLocale(locale, defaultLocale)
                            ? IssueSeverity.Error
                            : IssueSeverity.Warning,
                        IsDefaultLocale(locale, defaultLocale)
                            ? "默认语言表文件缺失。"
                            : "目标语言表文件缺失。",
                        $"{category} / {locale.Code}\n{filePath}");
                    continue;
                }

                try
                {
                    JObject document = JObject.Parse(
                        ReadUtf8Text(filePath).TrimStart('\uFEFF'),
                        new JsonLoadSettings
                        {
                            DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error
                        });

                    foreach (JProperty property in document.Properties())
                    {
                        if (!ValidateKeyNaming(category, property.Name, filePath, issues))
                        {
                            if (string.IsNullOrWhiteSpace(property.Name))
                                continue;
                        }

                        string key = LocalizationKeyUtility.BuildFullKey(
                            category,
                            property.Name);
                        if (string.IsNullOrWhiteSpace(key))
                        {
                            AddIssue(issues, IssueSeverity.Error, "发现空 Key。", filePath);
                            continue;
                        }

                        if (property.Value.Type != JTokenType.String)
                        {
                            AddIssue(
                                issues,
                                IssueSeverity.Error,
                                $"Key 的值必须是字符串：{key}",
                                filePath);
                            continue;
                        }

                        string value = property.Value.Value<string>() ?? string.Empty;
                        if (!values.TryAdd(key, value))
                        {
                            AddIssue(
                                issues,
                                IssueSeverity.Error,
                                $"同一语言表中存在重复完整 Key：{key}",
                                filePath);
                            continue;
                        }

                        keys.Add(key);

                        string globalKey = locale.Code + "\u001f" + key;
                        if (globalKeyOwners.TryGetValue(globalKey, out string ownerCategory)
                            && !string.Equals(ownerCategory, category, StringComparison.Ordinal))
                        {
                            AddIssue(
                                issues,
                                IssueSeverity.Error,
                                $"同一语言的 Key 在多个分类中重复：{key}",
                                $"{ownerCategory} / {category} / {locale.Code}");
                        }
                        else
                        {
                            globalKeyOwners[globalKey] = category;
                        }
                    }
                }
                catch (DecoderFallbackException)
                {
                    AddIssue(
                        issues,
                        IssueSeverity.Error,
                        "语言表文件不是有效的 UTF-8 编码。",
                        filePath);
                }
                catch (Exception exception)
                {
                    AddIssue(
                        issues,
                        IssueSeverity.Error,
                        $"JSON 格式无效：{exception.Message}",
                        filePath);
                }
            }

            List<string> sortedKeys = keys.OrderBy(key => key, StringComparer.Ordinal).ToList();
            allKeys.UnionWith(sortedKeys);
            foreach (string key in sortedKeys)
            {
                foreach (LocaleInfo locale in locales)
                {
                    Dictionary<string, string> values = valuesByLocale[locale.Code];
                    if (!values.TryGetValue(key, out string value))
                    {
                        AddIssue(
                            issues,
                            IsDefaultLocale(locale, defaultLocale)
                                ? IssueSeverity.Error
                                : IssueSeverity.Warning,
                            IsDefaultLocale(locale, defaultLocale)
                                ? $"默认语言缺少 Key：{key}"
                                : $"目标语言缺少翻译 Key：{key}",
                            $"{category} / {locale.Code}");
                    }
                    else if (string.IsNullOrWhiteSpace(value))
                    {
                        AddIssue(
                            issues,
                            IsDefaultLocale(locale, defaultLocale)
                                ? IssueSeverity.Error
                                : IssueSeverity.Warning,
                            IsDefaultLocale(locale, defaultLocale)
                                ? $"默认语言翻译内容为空：{key}"
                                : $"目标语言翻译内容为空：{key}",
                            $"{category} / {locale.Code}");
                    }
                }
            }

            if (!LocalizationPlaceholderValidator.Validate(
                    locales,
                    sortedKeys,
                    valuesByLocale,
                    out string placeholderMessage))
            {
                AddIssue(
                    issues,
                    IssueSeverity.Error,
                    placeholderMessage.Replace("发现占位符问题：", "占位符校验失败：").Trim(),
                    category);
            }

        }

        private static bool ValidateKeyNaming(
            string category,
            string rawKey,
            string filePath,
            ICollection<Issue> issues)
        {
            if (string.IsNullOrWhiteSpace(rawKey))
            {
                AddIssue(issues, IssueSeverity.Error, "发现空 Key。", filePath);
                return false;
            }

            string key = rawKey.Trim();
            string expectedPrefix = category.Trim().ToLowerInvariant() + ".";
            bool valid = true;
            if (!LocalizationKeyUtility.IsFullKeyForCategory(category, key))
            {
                AddIssue(
                    issues,
                    IssueSeverity.Error,
                    $"Key 必须使用完整主分类前缀：{expectedPrefix}…（实际：{key}）",
                    filePath);
                valid = false;
            }
            else
            {
                string canonicalKey = LocalizationKeyUtility.BuildFullKey(category, key);
                if (!string.Equals(key, canonicalKey, StringComparison.Ordinal))
                {
                    AddIssue(
                        issues,
                        IssueSeverity.Error,
                        $"Key 的主分类前缀必须使用小写格式：{expectedPrefix}…（实际：{key}）",
                        filePath);
                    valid = false;
                }
            }

            if (!StrictKeyPattern.IsMatch(key))
            {
                AddIssue(
                    issues,
                    IssueSeverity.Error,
                    $"Key 命名不符合规范，只允许小写英文、数字、下划线和点号：{key}",
                    filePath);
                valid = false;
            }

            return valid;
        }

        private static void CheckPhysicalLanguageFiles(
            string categoryDirectory,
            IReadOnlyList<LocaleInfo> locales,
            string fileNamePattern,
            ICollection<Issue> issues)
        {
            var expectedFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (LocaleInfo locale in locales)
            {
                string expectedName = LocalizationPath.GetLanguageFileName(
                    locale.Code,
                    fileNamePattern);
                expectedFiles[expectedName] = locale.Code;
            }

            string[] physicalFiles = Directory.GetFiles(
                categoryDirectory,
                "*",
                SearchOption.TopDirectoryOnly);
            foreach (string physicalFile in physicalFiles)
            {
                string fileName = Path.GetFileName(physicalFile);
                if (fileName.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    AddIssue(
                        issues,
                        IssueSeverity.Warning,
                        "分类目录中存在非 JSON 语言文件。",
                        physicalFile);
                    continue;
                }

                if (!expectedFiles.TryGetValue(fileName, out string configuredLocale))
                {
                    try
                    {
                        // 即使文件是多余文件，也要检查其字节编码，避免错误被静默忽略。
                        ReadUtf8Text(physicalFile);
                    }
                    catch (DecoderFallbackException)
                    {
                        AddIssue(
                            issues,
                            IssueSeverity.Error,
                            "语言表文件不是有效的 UTF-8 编码。",
                            physicalFile);
                    }

                    if (!LocaleInfo.TryFromFileName(fileName, out string physicalLocale))
                    {
                        AddIssue(
                            issues,
                            IssueSeverity.Error,
                            "语言文件名中的语言代码无效，且不符合当前文件名规则。",
                            physicalFile);
                    }
                    else
                    {
                        AddIssue(
                            issues,
                            IssueSeverity.Warning,
                            $"发现未配置的多余语言文件：{physicalLocale}。",
                            physicalFile);
                    }

                    continue;
                }

                string expectedName = LocalizationPath.GetLanguageFileName(
                    configuredLocale,
                    fileNamePattern);
                if (!string.Equals(fileName, expectedName, StringComparison.Ordinal))
                {
                    AddIssue(
                        issues,
                        IssueSeverity.Error,
                        $"语言文件名大小写或格式不符合规范，应为：{expectedName}。",
                        physicalFile);
                }
            }

            foreach (string childDirectory in Directory.GetDirectories(
                categoryDirectory,
                "*",
                SearchOption.TopDirectoryOnly))
            {
                AddIssue(
                    issues,
                    IssueSeverity.Warning,
                    "分类目录中存在未参与本地化加载的子目录。",
                    childDirectory);
            }
        }

        private static void CheckUnconfiguredCategoryDirectories(
            string dataRoot,
            IReadOnlyCollection<string> configuredCategories,
            ICollection<Issue> issues)
        {
            var configured = new HashSet<string>(
                configuredCategories,
                StringComparer.OrdinalIgnoreCase);
            foreach (string directory in Directory.GetDirectories(
                dataRoot,
                "*",
                SearchOption.TopDirectoryOnly))
            {
                string category = Path.GetFileName(directory);
                if (!configured.Contains(category))
                {
                    AddIssue(
                        issues,
                        IssueSeverity.Warning,
                        "发现未配置的多余本地化分类目录。",
                        directory);
                }
            }
        }

        private static HashSet<string> CheckAssetTable(
            IReadOnlyList<LocaleInfo> locales,
            string defaultLocale,
            ICollection<Issue> issues)
        {
            var validKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            LocalizationAssetTable catalog = AssetDatabase.LoadAssetAtPath<LocalizationAssetTable>(
                LocalizationPath.AssetTablePath);
            if (catalog == null || catalog.Entries == null)
                return validKeys;

            var keySet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var configuredLocaleCodes = new HashSet<string>(
                locales.Select(locale => locale.Code),
                StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                LocalizationAssetEntry entry = catalog.Entries[i];
                string location = $"资源本地化表 / 第 {i + 1} 行";
                if (entry == null)
                {
                    AddIssue(issues, IssueSeverity.Error, "资源条目为空。", location);
                    continue;
                }

                string key = entry.Key?.Trim();
                if (string.IsNullOrEmpty(key))
                {
                    AddIssue(issues, IssueSeverity.Error, "资源 Key 不能为空。", location);
                }
                else if (!StrictKeyPattern.IsMatch(key))
                {
                    AddIssue(
                        issues,
                        IssueSeverity.Error,
                        $"资源 Key 不符合完整 Key 规则：{key}",
                        location);
                }
                else if (!keySet.Add(key))
                {
                    AddIssue(issues, IssueSeverity.Error, $"资源 Key 重复：{key}", location);
                }
                else
                {
                    validKeys.Add(key);
                }

                var localeCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (entry.LocaleValues != null)
                {
                    foreach (LocalizationAssetLocaleValue value in entry.LocaleValues)
                    {
                        if (value == null)
                        {
                            AddIssue(issues, IssueSeverity.Error, "资源语言项为空。", location);
                            continue;
                        }

                        if (!LocaleInfo.TryNormalize(value.LocaleCode, out string normalizedLocale)
                            || !configuredLocaleCodes.Contains(normalizedLocale))
                        {
                            AddIssue(
                                issues,
                                IssueSeverity.Warning,
                                $"资源表包含未配置的语言项：{value.LocaleCode}",
                                location);
                            continue;
                        }

                        if (!localeCodes.Add(normalizedLocale))
                        {
                            AddIssue(
                                issues,
                                IssueSeverity.Error,
                                $"同一资源 Key 存在重复语言项：{normalizedLocale}",
                                location);
                            continue;
                        }

                        if (value.Asset != null)
                        {
                            string typeError = GetAssetValidationError(entry.AssetType, value.Asset);
                            if (!string.IsNullOrEmpty(typeError))
                            {
                                AddIssue(
                                    issues,
                                    IssueSeverity.Error,
                                    $"语言 {normalizedLocale} 的资源类型错误：{typeError}",
                                    location);
                            }
                        }
                    }
                }

                foreach (LocaleInfo locale in locales)
                {
                    UnityEngine.Object asset = GetCatalogAsset(entry, locale.Code);
                    if (asset != null)
                        continue;

                    bool isDefault = IsDefaultLocale(locale, defaultLocale);
                    AddIssue(
                        issues,
                        isDefault ? IssueSeverity.Error : IssueSeverity.Warning,
                        isDefault ? "默认语言资源缺失。" : "目标语言资源缺失。",
                        $"{location} / {key} / {locale.Code}");
                }
            }

            return validKeys;
        }

        private static HashSet<string> LoadValidAssetKeys()
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            LocalizationAssetTable catalog = AssetDatabase.LoadAssetAtPath<LocalizationAssetTable>(
                LocalizationPath.AssetTablePath);
            if (catalog?.Entries == null)
                return result;

            foreach (LocalizationAssetEntry entry in catalog.Entries)
            {
                if (entry != null
                    && !string.IsNullOrWhiteSpace(entry.Key)
                    && StrictKeyPattern.IsMatch(entry.Key.Trim()))
                {
                    result.Add(entry.Key.Trim());
                }
            }

            return result;
        }

        private static UnityEngine.Object GetCatalogAsset(
            LocalizationAssetEntry entry,
            string localeCode)
        {
            if (entry?.LocaleValues == null)
                return null;

            foreach (LocalizationAssetLocaleValue value in entry.LocaleValues)
            {
                if (value != null
                    && string.Equals(value.LocaleCode, localeCode, StringComparison.OrdinalIgnoreCase))
                    return value.Asset;
            }

            return null;
        }

        private static string GetAssetValidationError(
            LocalizationAssetType assetType,
            UnityEngine.Object asset)
        {
            switch (assetType)
            {
                case LocalizationAssetType.Sprite:
                    return asset is Sprite ? null : "需要 Sprite。";
                case LocalizationAssetType.AudioClip:
                    return asset is AudioClip ? null : "需要 AudioClip。";
                case LocalizationAssetType.Font:
                    return asset is Font ? null : "需要 Font。";
                case LocalizationAssetType.TMPFontAsset:
#if ENABLE_TEXTMESHPRO
                    return asset is TMP_FontAsset ? null : "需要 TMP Font Asset。";
#else
                    return "当前项目未安装 TextMeshPro，无法使用 TMP Font Asset。";
#endif
                case LocalizationAssetType.Prefab:
                    if (!(asset is GameObject))
                        return "需要 Prefab 资源。";
                    return PrefabUtility.GetPrefabAssetType(asset) == PrefabAssetType.NotAPrefab
                        ? "需要项目中的 Prefab 资产，不能使用场景对象。"
                        : null;
                case LocalizationAssetType.ScriptableObject:
                    return asset is ScriptableObject ? null : "需要 ScriptableObject。";
                case LocalizationAssetType.Other:
                    return null;
                default:
                    return "资源类型未知。";
            }
        }

        private static void ScanProjectReferences(
            ISet<string> allKeys,
            ISet<string> assetKeys,
            ProjectReferenceScanResult result,
            ICollection<Issue> issues,
            bool scanUnlocalizedStaticText)
        {
            LogUtil.Info(LogModule, "开始扫描场景和 Prefab 中的本地化组件引用...");

            ScanPrefabs(
                allKeys,
                assetKeys,
                result,
                issues,
                scanUnlocalizedStaticText);
            ScanScenes(
                allKeys,
                assetKeys,
                result,
                issues,
                scanUnlocalizedStaticText);
            ScanScripts(allKeys, result, issues);
            ScanDataTableReferences(allKeys, result, issues);

            string staticTextSummary = scanUnlocalizedStaticText
                ? $"未接入本地化的静态文本 {result.StaticTextCount} 个"
                : "静态文本接入扫描已关闭";
            LogUtil.Info(
                LogModule,
                $"工程引用扫描完成：场景 {result.SceneCount} 个，Prefab {result.PrefabCount} 个，" +
                $"FinkLocalizedText {result.ComponentCount} 个，资源本地化组件 {result.ResourceComponentCount} 个，" +
                $"脚本 {result.ScriptCount} 个，" +
                $"脚本 Key 引用 {result.ScriptReferenceCount} 个，" +
                $"动态脚本 Key 引用 {result.DynamicScriptReferenceCount} 个，" +
                $"数据表 Key 引用 {result.DataTableReferenceCount} 个，" +
                $"{staticTextSummary}，" +
                $"不支持的本地化组件 {result.UnsupportedLocalizedComponentCount} 个。");

            foreach (KeyValuePair<string, List<string>> pair in result.BindingLocations)
            {
                if (pair.Value.Count < 2)
                    continue;

                AddIssue(
                    issues,
                    IssueSeverity.Warning,
                    $"Key 被多个 FinkLocalizedText 绑定：{pair.Key}",
                    string.Join("\n", pair.Value));
            }
        }

        private static void ScanPrefabs(
            ISet<string> allKeys,
            ISet<string> assetKeys,
            ProjectReferenceScanResult result,
            ICollection<Issue> issues,
            bool scanUnlocalizedStaticText)
        {
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
            foreach (string guid in prefabGuids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (prefab == null)
                    continue;

                result.PrefabCount++;
                ScanGameObjectHierarchy(
                    prefab,
                    $"Prefab：{assetPath}",
                    allKeys,
                    assetKeys,
                    result,
                    issues,
                    scanUnlocalizedStaticText);
            }
        }

        private static void ScanScenes(
            ISet<string> allKeys,
            ISet<string> assetKeys,
            ProjectReferenceScanResult result,
            ICollection<Issue> issues,
            bool scanUnlocalizedStaticText)
        {
            string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });
            SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();

            try
            {
                foreach (string guid in sceneGuids)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    Scene scene = SceneManager.GetSceneByPath(assetPath);
                    bool openedByScan = !scene.IsValid() || !scene.isLoaded;

                    if (openedByScan)
                        scene = EditorSceneManager.OpenScene(assetPath, OpenSceneMode.Additive);

                    if (!scene.IsValid() || !scene.isLoaded)
                    {
                        AddIssue(
                            issues,
                            IssueSeverity.Error,
                            "无法打开场景进行本地化引用扫描。",
                            assetPath);
                        continue;
                    }

                    result.SceneCount++;
                    foreach (GameObject root in scene.GetRootGameObjects())
                    {
                        ScanGameObjectHierarchy(
                            root,
                            $"Scene：{assetPath}",
                            allKeys,
                            assetKeys,
                            result,
                            issues,
                            scanUnlocalizedStaticText);
                    }

                    if (openedByScan)
                        EditorSceneManager.CloseScene(scene, true);
                }
            }
            catch (Exception exception)
            {
                AddIssue(
                    issues,
                    IssueSeverity.Error,
                    $"扫描场景时发生异常：{exception.Message}",
                    "场景扫描");
            }
            finally
            {
                try
                {
                    EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
                }
                catch (Exception exception)
                {
                    AddIssue(
                        issues,
                        IssueSeverity.Error,
                        $"恢复扫描前的场景布局失败：{exception.Message}",
                        "场景扫描");
                }
            }
        }

        private static void ScanScripts(
            ISet<string> allKeys,
            ProjectReferenceScanResult result,
            ICollection<Issue> issues)
        {
            string[] scriptGuids = AssetDatabase.FindAssets("t:Script", new[] { "Assets" });

            foreach (string guid in scriptGuids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(assetPath)
                    || !assetPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                    continue;

                string absolutePath = Path.Combine(LocalizationPath.ProjectRoot, assetPath);
                string source;
                try
                {
                    source = File.ReadAllText(absolutePath, Encoding.UTF8);
                }
                catch (Exception exception)
                {
                    AddIssue(
                        issues,
                        IssueSeverity.Warning,
                        $"脚本读取失败，已跳过静态 Key 扫描：{exception.Message}",
                        assetPath);
                    continue;
                }

                result.ScriptCount++;
                ScanScriptPattern(
                    source,
                    assetPath,
                    LiteralLocalizationCallPattern,
                    allKeys,
                    result,
                    issues);
                if (!assetPath.StartsWith(
                        FrameworkLocalizationRuntimePath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    ScanDynamicScriptPattern(source, assetPath, result, issues);
                }
            }
        }

        private static void ScanDynamicScriptPattern(
            string source,
            string assetPath,
            ProjectReferenceScanResult result,
            ICollection<Issue> issues)
        {
            foreach (Match match in DynamicLocalizationCallPattern.Matches(source))
            {
                if (IsInsideCommentOrString(source, match.Index))
                    continue;

                string argument = match.Groups["argument"].Value.Trim();
                if (argument.StartsWith("\"", StringComparison.Ordinal)
                    || argument.StartsWith("@\"", StringComparison.Ordinal))
                    continue;

                result.DynamicScriptReferenceCount++;
                AddIssue(
                    issues,
                    IssueSeverity.Warning,
                    $"脚本使用动态本地化 Key，QA 无法静态确认其是否存在：{argument}",
                    $"脚本：{assetPath}:{GetLineNumber(source, match.Index)}");
            }
        }

        private static void ScanDataTableReferences(
            ISet<string> allKeys,
            ProjectReferenceScanResult result,
            ICollection<Issue> issues)
        {
            string projectRoot = LocalizationPath.ProjectRoot;
            string[] dataRoots =
            {
                Path.Combine(projectRoot, "FinkFramework_Data", "AutoExport", "DataJson"),
                Path.Combine(Application.streamingAssetsPath, "FinkFramework_Data", "DataJson")
            };
            var scannedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string dataRoot in dataRoots)
            {
                if (!Directory.Exists(dataRoot))
                    continue;

                string[] files = Directory.GetFiles(
                    dataRoot,
                    "*.json",
                    SearchOption.AllDirectories);
                foreach (string file in files)
                {
                    string normalizedFile = NormalizePath(file);
                    if (!scannedFiles.Add(normalizedFile))
                        continue;

                    try
                    {
                        JObject document = JObject.Parse(
                            ReadUtf8Text(file).TrimStart('\uFEFF'),
                            new JsonLoadSettings
                            {
                                DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error
                            });
                        ScanDataToken(document, file, allKeys, result, issues);
                    }
                    catch (DecoderFallbackException)
                    {
                        AddIssue(
                            issues,
                            IssueSeverity.Error,
                            "数据表 JSON 不是有效的 UTF-8 编码。",
                            file);
                    }
                    catch (Exception exception)
                    {
                        AddIssue(
                            issues,
                            IssueSeverity.Warning,
                            $"数据表 JSON 无法解析，已跳过 Key 引用扫描：{exception.Message}",
                            file);
                    }
                }
            }
        }

        private static void ScanDataToken(
            JToken token,
            string file,
            ISet<string> allKeys,
            ProjectReferenceScanResult result,
            ICollection<Issue> issues)
        {
            if (token == null)
                return;

            if (token is JObject document)
            {
                foreach (JProperty property in document.Properties())
                {
                    RegisterDataTableKey(
                        property.Name,
                        $"数据表：{file}{GetJsonTokenLocation(property)}",
                        allKeys,
                        result,
                        issues);
                    ScanDataToken(property.Value, file, allKeys, result, issues);
                }

                return;
            }

            if (token is JArray array)
            {
                foreach (JToken item in array)
                    ScanDataToken(item, file, allKeys, result, issues);

                return;
            }

            if (token.Type == JTokenType.String)
            {
                RegisterDataTableKey(
                    token.Value<string>(),
                    $"数据表：{file}{GetJsonTokenLocation(token)}",
                    allKeys,
                    result,
                    issues);
            }
        }

        private static void RegisterDataTableKey(
            string value,
            string location,
            ISet<string> allKeys,
            ProjectReferenceScanResult result,
            ICollection<Issue> issues)
        {
            if (string.IsNullOrWhiteSpace(value) || !allKeys.Contains(value.Trim()))
                return;

            result.DataTableReferenceCount++;
            RegisterKeyReference(value.Trim(), location, allKeys, result, issues);
        }

        private static string GetJsonTokenLocation(JToken token)
        {
            return string.IsNullOrEmpty(token?.Path) ? string.Empty : $" / {token.Path}";
        }

        private static void ScanScriptPattern(
            string source,
            string assetPath,
            Regex pattern,
            ISet<string> allKeys,
            ProjectReferenceScanResult result,
            ICollection<Issue> issues)
        {
            foreach (Match match in pattern.Matches(source))
            {
                if (IsInsideCommentOrString(source, match.Index))
                    continue;

                string key = UnescapeCSharpString(match.Groups["key"].Value);

                result.ScriptReferenceCount++;
                RegisterKeyReference(
                    key,
                    $"脚本：{assetPath}:{GetLineNumber(source, match.Index)}",
                    allKeys,
                    result,
                    issues);
            }
        }

        private static void RegisterKeyReference(
            string key,
            string location,
            ISet<string> allKeys,
            ProjectReferenceScanResult result,
            ICollection<Issue> issues)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                AddIssue(issues, IssueSeverity.Error, "脚本本地化调用使用了空 Key。", location);
                return;
            }

            key = key.Trim();
            if (!allKeys.Contains(key))
            {
                AddIssue(issues, IssueSeverity.Error, $"脚本引用了不存在的 Key：{key}", location);
                return;
            }

            result.UsedKeys.Add(key);
            if (!result.BindingLocations.TryGetValue(key, out List<string> locations))
            {
                locations = new List<string>();
                result.BindingLocations.Add(key, locations);
            }

            locations.Add(location);
        }

        private static string UnescapeCSharpString(string value)
        {
            return Regex.Unescape(value ?? string.Empty);
        }

        private static int GetLineNumber(string source, int index)
        {
            int line = 1;
            for (int i = 0; i < index && i < source.Length; i++)
            {
                if (source[i] == '\n')
                    line++;
            }

            return line;
        }

        private static bool IsInsideCommentOrString(string source, int index)
        {
            bool inBlockComment = false;
            bool inString = false;
            bool inChar = false;
            bool verbatimString = false;

            for (int i = 0; i < index && i < source.Length; i++)
            {
                char current = source[i];
                char next = i + 1 < source.Length ? source[i + 1] : '\0';

                if (inBlockComment)
                {
                    if (current == '*' && next == '/')
                    {
                        inBlockComment = false;
                        i++;
                    }

                    continue;
                }

                if (inString)
                {
                    if (verbatimString)
                    {
                        if (current == '"')
                        {
                            if (next == '"')
                                i++;
                            else
                            {
                                inString = false;
                                verbatimString = false;
                            }
                        }
                    }
                    else if (current == '\\')
                    {
                        i++;
                    }
                    else if (current == '"')
                    {
                        inString = false;
                    }

                    continue;
                }

                if (inChar)
                {
                    if (current == '\\')
                        i++;
                    else if (current == '\'')
                        inChar = false;

                    continue;
                }

                if (current == '/' && next == '/')
                {
                    i += 2;
                    while (i < index && i < source.Length && source[i] != '\n')
                        i++;

                    if (i >= index)
                        return true;

                    continue;
                }

                if (current == '/' && next == '*')
                {
                    inBlockComment = true;
                    i++;
                    continue;
                }

                if (current == '"')
                {
                    inString = true;
                    verbatimString = i > 0 && source[i - 1] == '@';
                    continue;
                }

                if (current == '\'')
                    inChar = true;
            }

            return inBlockComment || inString || inChar;
        }

        private static string NormalizePath(string path)
        {
            return string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/');
        }

        private static void ScanGameObjectHierarchy(
            GameObject root,
            string assetLabel,
            ISet<string> allKeys,
            ISet<string> assetKeys,
            ProjectReferenceScanResult result,
            ICollection<Issue> issues,
            bool scanUnlocalizedStaticText)
        {
            FinkLocalizedText[] components = root.GetComponentsInChildren<FinkLocalizedText>(true);
            foreach (FinkLocalizedText component in components)
            {
                result.ComponentCount++;
                string location = $"{assetLabel}\n对象：{GetTransformPath(component.transform)}";
                string key = component.Key == null ? string.Empty : component.Key.Trim();

                if (!HasSupportedTextTarget(component.gameObject))
                {
                    result.UnsupportedLocalizedComponentCount++;
                    AddIssue(
                        issues,
                        IssueSeverity.Error,
                        "FinkLocalizedText 没有绑定受支持的 Text 或 TMP 文本组件。",
                        location);
                }

                if (string.IsNullOrEmpty(key))
                {
                    AddIssue(issues, IssueSeverity.Warning, "FinkLocalizedText 没有绑定 Key。", location);
                    continue;
                }

                if (!allKeys.Contains(key))
                {
                    AddIssue(
                        issues,
                        IssueSeverity.Error,
                        $"FinkLocalizedText 绑定了不存在的 Key：{key}",
                        location);
                    continue;
                }

                result.UsedKeys.Add(key);
                if (!result.BindingLocations.TryGetValue(key, out List<string> locations))
                {
                    locations = new List<string>();
                    result.BindingLocations.Add(key, locations);
                }

                locations.Add(location);
            }

            FinkLocalizedAsset[] assetComponents =
                root.GetComponentsInChildren<FinkLocalizedAsset>(true);
            foreach (FinkLocalizedAsset component in assetComponents)
            {
                result.ResourceComponentCount++;
                string location = $"{assetLabel}\n对象：{GetTransformPath(component.transform)}";
                string key = component.Key == null ? string.Empty : component.Key.Trim();

                if (string.IsNullOrEmpty(key))
                {
                    AddIssue(issues, IssueSeverity.Warning, "FinkLocalizedAsset 没有绑定 Key。", location);
                    continue;
                }

                if (!assetKeys.Contains(key))
                {
                    AddIssue(
                        issues,
                        IssueSeverity.Error,
                        $"FinkLocalizedAsset 绑定了不存在的资源 Key：{key}",
                        location);
                }
            }

            if (scanUnlocalizedStaticText)
                ScanStaticTextComponents(root, assetLabel, result, issues);
        }

        private static bool HasSupportedTextTarget(GameObject gameObject)
        {
            if (gameObject == null)
                return false;

#if ENABLE_UGUI
            if (EnvironmentState.AutoUGUI && gameObject.GetComponent<Text>() != null)
                return true;
#endif
#if ENABLE_TEXTMESHPRO
            if (EnvironmentState.AutoTMP && gameObject.GetComponent<TMP_Text>() != null)
                return true;
#endif
            return false;
        }

        private static void ScanStaticTextComponents(
            GameObject root,
            string assetLabel,
            ProjectReferenceScanResult result,
            ICollection<Issue> issues)
        {
#if ENABLE_UGUI
            if (EnvironmentState.AutoUGUI)
            {
                Text[] legacyTexts = root.GetComponentsInChildren<Text>(true);
                foreach (Text text in legacyTexts)
                {
                    if (text == null || string.IsNullOrWhiteSpace(text.text))
                        continue;

                    if (HasLocalizationBindingComponent(text.gameObject))
                        continue;

                    AddStaticTextIssue(text, "UnityEngine.UI.Text", assetLabel, result, issues);
                }
            }
#endif

#if ENABLE_TEXTMESHPRO
            if (EnvironmentState.AutoTMP)
            {
                TMP_Text[] tmpTexts = root.GetComponentsInChildren<TMP_Text>(true);
                foreach (TMP_Text text in tmpTexts)
                {
                    if (text == null || string.IsNullOrWhiteSpace(text.text))
                        continue;

                    if (HasLocalizationBindingComponent(text.gameObject))
                        continue;

                    AddStaticTextIssue(text, "TMPro.TMP_Text", assetLabel, result, issues);
                }
            }
#endif
        }

        /// <summary>
        /// 同一对象只要已挂接任一本地化绑定组件，就不再重复报告“静态文本未接入”。
        /// 具体组件是否缺少 Key 会由组件绑定检查单独给出一次警告。
        /// </summary>
        private static bool HasLocalizationBindingComponent(GameObject gameObject)
        {
            return gameObject != null
                && (gameObject.GetComponent<FinkLocalizedText>() != null
                    || gameObject.GetComponent<FinkLocalizedAsset>() != null);
        }

        private static void AddStaticTextIssue(
            Component component,
            string componentType,
            string assetLabel,
            ProjectReferenceScanResult result,
            ICollection<Issue> issues)
        {
            result.StaticTextCount++;
            AddIssue(
                issues,
                IssueSeverity.Warning,
                $"发现未接入本地化的静态文本组件：{componentType}",
                $"{assetLabel}\n对象：{GetTransformPath(component.transform)}\n文本：{GetComponentText(component)}");
        }

        private static string GetComponentText(Component component)
        {
#if ENABLE_UGUI
            if (component is Text legacyText)
                return legacyText.text;
#endif
#if ENABLE_TEXTMESHPRO
            if (component is TMP_Text tmpText)
                return tmpText.text;
#endif
            return string.Empty;
        }

        private static string GetTransformPath(Transform transform)
        {
            if (transform == null)
                return "<未知对象>";

            var names = new List<string>();
            Transform current = transform;
            while (current != null)
            {
                names.Add(current.name);
                current = current.parent;
            }

            names.Reverse();
            return string.Join("/", names);
        }

        private static void LogUnusedKeys(ISet<string> allKeys, ISet<string> usedKeys, ICollection<Issue> issues)
        {
            foreach (string key in allKeys.OrderBy(value => value, StringComparer.Ordinal))
            {
                if (!usedKeys.Contains(key))
                {
                    AddIssue(
                        issues,
                        IssueSeverity.Warning,
                        $"语言表 Key 未被工程中已识别的本地化引用使用：{key}",
                        "语言表");
                }
            }
        }

        private static List<LocaleInfo> GetLocales(
            LocalizationSettingsAsset settings,
            ICollection<Issue> issues)
        {
            var result = new List<LocaleInfo>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (LocaleInfo locale in settings.SupportedLocales ?? Array.Empty<LocaleInfo>())
            {
                if (locale == null)
                {
                    AddIssue(
                        issues,
                        IssueSeverity.Error,
                        "支持语言列表中存在空语言项。",
                        "本地化设置 / 支持语言");
                    continue;
                }

                if (!LocaleInfo.TryNormalize(locale.Code, out string normalizedCode))
                {
                    AddIssue(
                        issues,
                        IssueSeverity.Error,
                        $"语言代码不是合法的 BCP 47 格式：{locale.Code}",
                        "本地化设置 / 支持语言");
                    continue;
                }

                if (!seen.Add(normalizedCode))
                {
                    AddIssue(
                        issues,
                        IssueSeverity.Error,
                        $"支持语言列表中存在重复语言：{normalizedCode}",
                        "本地化设置 / 支持语言");
                    continue;
                }

                result.Add(new LocaleInfo(normalizedCode, locale.DisplayName));
            }

            ValidateDefaultLocale(settings, "defaultLocale", "默认语言", issues);
            ValidateDefaultLocale(settings, "defaultFallbackLocale", "默认 Fallback 语言", issues);
            return result;
        }

        private static List<string> GetCategories(
            LocalizationSettingsAsset settings,
            ICollection<Issue> issues)
        {
            var result = new List<string>();
            if (settings.Categories == null)
                return result;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string rawCategory in settings.Categories)
            {
                string category = rawCategory?.Trim();
                if (string.IsNullOrWhiteSpace(category))
                {
                    AddIssue(
                        issues,
                        IssueSeverity.Error,
                        "主分类名称不能为空。",
                        "本地化设置 / 主分类");
                    continue;
                }

                if (!LocalizationPath.IsSafeCategory(category))
                {
                    AddIssue(
                        issues,
                        IssueSeverity.Error,
                        $"主分类名称不安全：{category}",
                        "本地化设置 / 主分类");
                    continue;
                }

                if (!seen.Add(category))
                {
                    AddIssue(
                        issues,
                        IssueSeverity.Error,
                        $"主分类列表中存在重复分类：{category}",
                        "本地化设置 / 主分类");
                    continue;
                }

                result.Add(category);
            }

            return result;
        }

        private static void ValidateDefaultLocale(
            LocalizationSettingsAsset settings,
            string propertyName,
            string label,
            ICollection<Issue> issues)
        {
            string value = GetConfiguredLocaleCode(settings, propertyName, issues, label);

            if (!LocaleInfo.TryNormalize(value, out _))
            {
                AddIssue(
                    issues,
                    IssueSeverity.Error,
                    $"{label}不是合法的 BCP 47 语言代码：{value}",
                    "本地化设置");
            }
        }

        private static string GetConfiguredLocaleCode(
            LocalizationSettingsAsset settings,
            string propertyName,
            ICollection<Issue> issues = null,
            string label = null)
        {
            try
            {
                SerializedObject serializedSettings = new SerializedObject(settings);
                serializedSettings.UpdateIfRequiredOrScript();
                SerializedProperty property = serializedSettings.FindProperty(propertyName);
                return property?.stringValue;
            }
            catch (Exception exception)
            {
                if (issues != null)
                {
                    AddIssue(
                        issues,
                        IssueSeverity.Error,
                        $"无法读取{label}配置：{exception.Message}",
                        "本地化设置");
                }

                return null;
            }
        }

        private static bool IsDefaultLocale(LocaleInfo locale, string defaultLocale)
        {
            return locale != null
                && !string.IsNullOrEmpty(defaultLocale)
                && string.Equals(
                    locale.Code,
                    defaultLocale,
                    StringComparison.OrdinalIgnoreCase);
        }

        private static string ReadUtf8Text(string filePath)
        {
            byte[] bytes = File.ReadAllBytes(filePath);
            return new UTF8Encoding(false, true).GetString(bytes);
        }

        private static void AddIssue(
            ICollection<Issue> issues,
            IssueSeverity severity,
            string message,
            string location)
        {
            issues.Add(new Issue
            {
                Severity = severity,
                Message = message,
                Location = location
            });
        }

        private static void LogIssues(IEnumerable<Issue> issues)
        {
            var loggedCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            var suppressedCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            var typeLabels = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (Issue issue in issues)
            {
                string typeLabel = GetIssueTypeLabel(issue.Message);
                string typeKey = $"{issue.Severity}|{typeLabel}";
                typeLabels[typeKey] = typeLabel;
                loggedCounts.TryGetValue(typeKey, out int loggedCount);
                if (loggedCount >= MaxLoggedIssuesPerType)
                {
                    suppressedCounts.TryGetValue(typeKey, out int suppressedCount);
                    suppressedCounts[typeKey] = suppressedCount + 1;
                    continue;
                }

                loggedCounts[typeKey] = loggedCount + 1;
                string output = $"{issue.Message}\n位置：{issue.Location}";
                if (issue.Severity == IssueSeverity.Error)
                    LogUtil.Error(LogModule, output);
                else
                    LogUtil.Warn(LogModule, output);
            }

            foreach (KeyValuePair<string, int> pair in suppressedCounts)
            {
                LogUtil.Warn(
                    LogModule,
                    $"“{typeLabels[pair.Key]}”同类问题超过 {MaxLoggedIssuesPerType} 条，" +
                    $"另有 {pair.Value} 条未逐项输出；请根据 QA 汇总继续检查。");
            }
        }

        /// <summary>
        /// 去掉消息中随 Key、语言或对象变化的详情，得到稳定的问题类型名称。
        /// </summary>
        private static string GetIssueTypeLabel(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return "未分类问题";

            int separatorIndex = message.IndexOf('：');
            return separatorIndex > 0
                ? message.Substring(0, separatorIndex).Trim()
                : message.Trim();
        }

        private static void LogSummary(IReadOnlyCollection<Issue> issues)
        {
            int errorCount = issues.Count(issue => issue.Severity == IssueSeverity.Error);
            int warningCount = issues.Count(issue => issue.Severity == IssueSeverity.Warning);
            if (issues.Count == 0)
            {
                LogUtil.Success(LogModule, "全部通过｜未发现问题。");
                return;
            }

            LogUtil.Warn(
                LogModule,
                $"QA未通过｜错误 {errorCount} 个，警告 {warningCount} 个。");
        }
    }
}
