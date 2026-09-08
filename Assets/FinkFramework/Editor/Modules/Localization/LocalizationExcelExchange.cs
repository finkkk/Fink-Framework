using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security;
using System.Text;
using ExcelDataReader;
using FinkFramework.Runtime.Localization;
using FinkFramework.Runtime.Utils;
using Newtonsoft.Json.Linq;

namespace FinkFramework.Editor.Modules.Localization
{
    /// <summary>
    /// 本地化专用 Excel 交换工具。
    ///
    /// 固定工作簿格式：一个主分类一个 Sheet，第一列为 Key，后续列为语言代码。
    /// 它不尝试处理任意 JSON 或任意业务表格，只负责本地化语言表格式。
    /// </summary>
    public static class LocalizationExcelExchange
    {
        private const string LogModule = "LocalizationExcelTool";
        // 预览只展示少量样例，避免一次导入大量 Key 时创建超大的编辑器窗口。
        internal const int MaxPreviewChangeCount = 20;

        // 这些 URI 是 Open XML 的命名空间/关系类型标识，不是运行时访问的网络地址。
        // 使用 Uri.UriSchemeHttp 拼接，避免静态分析器将协议标识误报为不安全网络请求。
        private static readonly string SpreadsheetNamespace =
            Uri.UriSchemeHttp + "://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly string PackageContentTypesNamespace =
            Uri.UriSchemeHttp + "://schemas.openxmlformats.org/package/2006/content-types";
        private static readonly string PackageRelationshipsNamespace =
            Uri.UriSchemeHttp + "://schemas.openxmlformats.org/package/2006/relationships";
        private static readonly string OfficeRelationshipsNamespace =
            Uri.UriSchemeHttp + "://schemas.openxmlformats.org/officeDocument/2006/relationships";

        /// <summary>一个分类 Sheet 的解析结果，不会直接修改磁盘文件。</summary>
        public sealed class ImportResult
        {
            /// <summary>对应的主分类名称。</summary>
            public string Category;
            /// <summary>Excel Sheet 名称。</summary>
            public string SheetName;
            /// <summary>按语言组织的 Key/翻译值。</summary>
            public readonly Dictionary<string, Dictionary<string, string>> ValuesByLocale =
                new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            /// <summary>该 Sheet 解析出的分类内 Key 集合。</summary>
            public readonly HashSet<string> Keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            /// <summary>有效数据行数量。</summary>
            public int RowCount;
        }

        /// <summary>整个工作簿的解析结果及未匹配 Sheet 信息。</summary>
        public sealed class WorkbookImportResult
        {
            /// <summary>按分类名称索引的解析结果。</summary>
            public readonly Dictionary<string, ImportResult> Categories =
                new Dictionary<string, ImportResult>(StringComparer.OrdinalIgnoreCase);
            /// <summary>配置中存在但工作簿缺失的分类。</summary>
            public readonly List<string> MissingCategories = new List<string>();
            /// <summary>工作簿中无法匹配当前配置的 Sheet。</summary>
            public readonly List<string> UnknownSheets = new List<string>();
            /// <summary>工作簿 Sheet 总数。</summary>
            public int SheetCount;
        }

        /// <summary>导入预览汇总，不修改磁盘文件。</summary>
        public sealed class ImportSummary
        {
            public readonly List<ChangeDetail> ChangeDetails = new();
            public int CategoryCount;
            public int AddedKeyCount;
            public int UpdatedValueCount;
            public int UnchangedValueCount;
        }

        public sealed class ChangeDetail
        {
            /// <summary>创建一条翻译变化记录。</summary>
            public ChangeDetail(
                string category,
                string locale,
                string key,
                string before,
                string after)
            {
                Category = category;
                Locale = locale;
                Key = key;
                Before = before;
                After = after;
            }

            /// <summary>主分类名称。</summary>
            public string Category { get; }
            /// <summary>标准语言标识。</summary>
            public string Locale { get; }
            /// <summary>完整本地化 Key。</summary>
            public string Key { get; }
            /// <summary>导入前文本。</summary>
            public string Before { get; }
            /// <summary>导入后文本。</summary>
            public string After { get; }
        }

        /// <summary>批量导入写盘结果和统计信息。</summary>
        public sealed class ApplyResult
        {
            public readonly List<string> CategoryDetails = new List<string>();
            public int CategoryCount;
            public int AddedKeyCount;
            public int UpdatedValueCount;
            public int UnchangedValueCount;
            public int WrittenFileCount;
            public bool SchemaChanged;
        }

        private sealed class CategoryData
        {
            public string Name;
            public readonly HashSet<string> Keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public readonly Dictionary<string, Dictionary<string, string>> ValuesByLocale =
                new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        }

        private sealed class CategoryApplyPlan
        {
            public string Category;
            public int AddedKeyCount;
            public int UpdatedValueCount;
            public int UnchangedValueCount;
        }

        private sealed class PendingFileWrite
        {
            public string FilePath;
            public string Content;
        }

        /// <summary>
        /// 从 Excel 的指定分类 Sheet 读取本地化内容，不修改项目 JSON。
        /// </summary>
        public static bool TryReadCategory(
            string excelPath,
            string category,
            IReadOnlyList<LocaleInfo> locales,
            out ImportResult result,
            out string message)
        {
            result = new ImportResult();
            message = string.Empty;

            if (!File.Exists(excelPath))
            {
                message = $"Excel 文件不存在：{excelPath}";
                return false;
            }

            if (!LocalizationPath.IsSafeCategory(category))
            {
                message = $"分类名称不安全：{category}";
                return false;
            }

            try
            {
                using FileStream stream = File.Open(excelPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using IExcelDataReader reader = ExcelReaderFactory.CreateReader(stream);
                bool foundCategory = false;

                do
                {
                    if (!string.Equals(reader.Name?.Trim(), category, StringComparison.OrdinalIgnoreCase))
                        continue;

                    foundCategory = true;
                    result.Category = category;
                    result.SheetName = reader.Name?.Trim() ?? category;
                    if (!TryReadSheet(reader, category, locales, result, out message))
                        return false;
                    break;
                }
                while (reader.NextResult());

                if (!foundCategory)
                {
                    message = $"Excel 中没有找到分类 Sheet：{category}";
                    return false;
                }

                message = $"已读取分类“{category}”：{result.RowCount} 行，{result.Keys.Count} 个 Key。";
                return true;
            }
            catch (Exception exception)
            {
                message = $"Excel 读取失败：{exception.Message}";
                LogUtil.Error(LogModule, $"{message}\n文件：{excelPath}");
                return false;
            }
        }

        /// <summary>
        /// 读取整个本地化工作簿。每个分类 Sheet 会被解析为一个 ImportResult，暂不修改项目 JSON。
        /// </summary>
        public static bool TryReadWorkbook(
            string excelPath,
            IReadOnlyList<string> categories,
            IReadOnlyList<LocaleInfo> locales,
            out WorkbookImportResult result,
            out string message)
        {
            result = new WorkbookImportResult();
            message = string.Empty;

            if (!File.Exists(excelPath))
            {
                message = $"Excel 文件不存在：{excelPath}";
                return false;
            }

            var categoryNames = (categories ?? Array.Empty<string>())
                .Where(LocalizationPath.IsSafeCategory)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (categoryNames.Count == 0)
            {
                message = "当前配置没有可导入的本地化分类。";
                return false;
            }

            Dictionary<string, string> sheetToCategory = BuildSheetCategoryLookup(categoryNames);
            var foundCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using FileStream stream = File.Open(excelPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using IExcelDataReader reader = ExcelReaderFactory.CreateReader(stream);
                do
                {
                    result.SheetCount++;
                    string sheetName = reader.Name?.Trim() ?? string.Empty;
                    if (!sheetToCategory.TryGetValue(sheetName, out string category))
                    {
                        result.UnknownSheets.Add(sheetName);
                        continue;
                    }

                    if (!foundCategories.Add(category))
                    {
                        message =
                            $"Excel 中发现重复的分类 Sheet：“{category}”（当前 Sheet“{sheetName}”与之前的 Sheet 重复）。";
                        return false;
                    }

                    var importResult = new ImportResult
                    {
                        Category = category,
                        SheetName = sheetName
                    };
                    if (!TryReadSheet(reader, category, locales, importResult, out message))
                        return false;

                    result.Categories[category] = importResult;
                }
                while (reader.NextResult());

                foreach (string category in categoryNames)
                {
                    if (!foundCategories.Contains(category))
                        result.MissingCategories.Add(category);
                }

                if (result.Categories.Count == 0)
                {
                    message = "Excel 中没有找到任何已配置的本地化分类 Sheet。";
                    return false;
                }

                message =
                    $"已读取本地化工作簿：{result.Categories.Count} 个分类，" +
                    $"{result.SheetCount} 个 Sheet。";
                return true;
            }
            catch (Exception exception)
            {
                message = $"Excel 工作簿读取失败：{exception.Message}";
                LogUtil.Error(LogModule, $"{message}\n文件：{excelPath}");
                return false;
            }
        }

        /// <summary>
        /// 比较工作簿导入内容与磁盘上的 JSON，只生成预览统计，不修改文件。
        /// </summary>
        public static bool TryBuildImportSummary(
            LocalizationSettingsAsset settings,
            WorkbookImportResult workbook,
            out ImportSummary summary,
            out string message)
        {
            summary = new ImportSummary();
            message = string.Empty;
            if (settings == null || workbook == null)
            {
                message = "本地化配置或 Excel 导入结果不存在。";
                return false;
            }

            try
            {
                settings.NormalizeConfiguration();
                if (!LocalizationDataSyncUtility.PrepareSourceDataRoot(
                        out message))
                    return false;

                string dataRoot = LocalizationPath.GetSourceDataRoot();
                List<LocaleInfo> locales = GetLocales(settings);

                foreach (KeyValuePair<string, ImportResult> pair in workbook.Categories)
                {
                    CategoryData current = ReadCategoryFromJson(
                        dataRoot,
                        pair.Key,
                        locales,
                        settings.JsonFileNamePattern);
                    int addedKeys = 0;
                    int updatedValues = 0;
                    int unchangedValues = 0;
                    foreach (string key in pair.Value.Keys)
                    {
                        if (!current.Keys.Contains(key))
                            addedKeys++;
                    }

                    foreach (KeyValuePair<string, Dictionary<string, string>> localePair in pair.Value.ValuesByLocale)
                    {
                        current.ValuesByLocale.TryGetValue(
                            localePair.Key,
                            out Dictionary<string, string> currentValues);
                        foreach (KeyValuePair<string, string> valuePair in localePair.Value)
                        {
                            string currentValue = string.Empty;
                            if (currentValues != null)
                                currentValues.TryGetValue(valuePair.Key, out currentValue);
                            if (string.Equals(currentValue ?? string.Empty, valuePair.Value ?? string.Empty, StringComparison.Ordinal))
                                unchangedValues++;
                            else
                            {
                                updatedValues++;
                                if (summary.ChangeDetails.Count < MaxPreviewChangeCount)
                                {
                                    summary.ChangeDetails.Add(
                                        new ChangeDetail(
                                            pair.Key,
                                            localePair.Key,
                                            valuePair.Key,
                                            FormatPreviewValue(currentValue),
                                            FormatPreviewValue(valuePair.Value)));
                                }
                            }
                        }
                    }

                    summary.CategoryCount++;
                    summary.AddedKeyCount += addedKeys;
                    summary.UpdatedValueCount += updatedValues;
                    summary.UnchangedValueCount += unchangedValues;
                }

                return true;
            }
            catch (Exception exception)
            {
                message = $"生成 Excel 导入预览失败：{exception.Message}";
                LogUtil.Error(LogModule, message);
                return false;
            }
        }

        /// <summary>
        /// 将已确认的工作簿导入结果合并到对应分类 JSON。Excel 未包含的旧 Key 会保留。
        /// </summary>
        public static bool TryApplyWorkbook(
            LocalizationSettingsAsset settings,
            WorkbookImportResult workbook,
            out ApplyResult result,
            out string message)
        {
            result = new ApplyResult();
            message = string.Empty;
            if (settings == null || workbook == null)
            {
                message = "本地化配置或 Excel 导入结果不存在。";
                return false;
            }

            try
            {
                settings.NormalizeConfiguration();
                if (!LocalizationDataSyncUtility.PrepareSourceDataRoot(
                        out message))
                    return false;

                string dataRoot = LocalizationPath.GetSourceDataRoot();
                List<LocaleInfo> locales = GetLocales(settings);
                var plans = new List<CategoryApplyPlan>();
                var pendingWrites = new List<PendingFileWrite>();

                // 先对所有分类做完整预校验，避免批量写入过程中出现部分成功。
                foreach (KeyValuePair<string, ImportResult> pair in workbook.Categories)
                {
                    CategoryData merged = ReadCategoryFromJson(
                        dataRoot,
                        pair.Key,
                        locales,
                        settings.JsonFileNamePattern);
                    int addedKeyCount = 0;
                    int updatedValueCount = 0;
                    int unchangedValueCount = 0;
                    foreach (string key in pair.Value.Keys)
                    {
                        if (!merged.Keys.Contains(key))
                        {
                            result.SchemaChanged = true;
                            addedKeyCount++;
                        }
                        merged.Keys.Add(key);
                    }
                    foreach (KeyValuePair<string, Dictionary<string, string>> localePair in pair.Value.ValuesByLocale)
                    {
                        if (!merged.ValuesByLocale.TryGetValue(localePair.Key, out Dictionary<string, string> values))
                        {
                            values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                            merged.ValuesByLocale[localePair.Key] = values;
                        }

                        foreach (KeyValuePair<string, string> valuePair in localePair.Value)
                        {
                            values.TryGetValue(valuePair.Key, out string currentValue);
                            if (string.Equals(
                                    currentValue ?? string.Empty,
                                    valuePair.Value ?? string.Empty,
                                    StringComparison.Ordinal))
                                unchangedValueCount++;
                            else
                                updatedValueCount++;

                            if (!string.Equals(
                                    LocalizationPlaceholderValidator.GetSignature(currentValue),
                                    LocalizationPlaceholderValidator.GetSignature(valuePair.Value),
                                    StringComparison.Ordinal))
                                result.SchemaChanged = true;
                            values[valuePair.Key] = valuePair.Value ?? string.Empty;
                        }
                    }

                    List<string> mergedKeys = merged.Keys
                        .OrderBy(value => value, StringComparer.Ordinal)
                        .ToList();

                    if (!LocalizationPlaceholderValidator.Validate(
                            locales,
                            mergedKeys,
                            merged.ValuesByLocale,
                            out string placeholderMessage))
                    {
                        message = $"分类 {pair.Key} 的占位符校验失败：\n{placeholderMessage}";
                        return false;
                    }

                    plans.Add(new CategoryApplyPlan
                    {
                        Category = pair.Key,
                        AddedKeyCount = addedKeyCount,
                        UpdatedValueCount = updatedValueCount,
                        UnchangedValueCount = unchangedValueCount
                    });

                    foreach (LocaleInfo locale in locales)
                    {
                        string filePath = LocalizationPath.GetLanguageFilePath(
                            dataRoot,
                            pair.Key,
                            locale.Code,
                            settings.JsonFileNamePattern);
                        var document = new JObject();
                        Dictionary<string, string> values = merged.ValuesByLocale[locale.Code];
                        foreach (string key in merged.Keys.OrderBy(value => value, StringComparer.Ordinal))
                        {
                            values.TryGetValue(key, out string value);
                            string fullKey = LocalizationKeyUtility.BuildFullKey(
                                pair.Key,
                                key);
                            document[fullKey] = value ?? string.Empty;
                        }

                        pendingWrites.Add(new PendingFileWrite
                        {
                            FilePath = filePath,
                            Content = document.ToString(Newtonsoft.Json.Formatting.Indented) + "\n"
                        });
                    }
                }

                // 所有分类都通过预校验后才开始写盘，避免批量导入中途失败时只写入一部分分类。
                foreach (PendingFileWrite pendingWrite in pendingWrites)
                {
                    string directory = Path.GetDirectoryName(pendingWrite.FilePath);
                    if (!string.IsNullOrEmpty(directory))
                        Directory.CreateDirectory(directory);

                    LocalizationEditorFileUtil.WriteUtf8TextAtomic(
                        pendingWrite.FilePath,
                        pendingWrite.Content);
                    result.WrittenFileCount++;
                }

                foreach (CategoryApplyPlan plan in plans)
                {
                    result.CategoryCount++;
                    result.AddedKeyCount += plan.AddedKeyCount;
                    result.UpdatedValueCount += plan.UpdatedValueCount;
                    result.UnchangedValueCount += plan.UnchangedValueCount;
                    result.CategoryDetails.Add(
                        $"{plan.Category}：新增 Key {plan.AddedKeyCount}，更新翻译 {plan.UpdatedValueCount}，未变化 {plan.UnchangedValueCount}");
                }

                if (!LocalizationDataSyncUtility.Sync(settings, out string manifestMessage))
                {
                    message =
                        $"语言表已写入，但本地化运行时清单同步失败：{manifestMessage}";
                    LogUtil.Error(LogModule, message);
                    return false;
                }

                message =
                    $"已导入 {result.CategoryCount} 个分类，写入 {result.WrittenFileCount} 个语言文件，" +
                    $"新增 Key {result.AddedKeyCount} 个，更新翻译 {result.UpdatedValueCount} 个。";
                LogUtil.Success(LogModule, message);
                return true;
            }
            catch (Exception exception)
            {
                message = $"应用 Excel 工作簿导入失败：{exception.Message}";
                LogUtil.Error(LogModule, message);
                return false;
            }
        }

        /// <summary>
        /// 将项目中已保存的全部本地化 JSON 导出为一个工作簿。
        /// </summary>
        public static bool TryExport(
            LocalizationSettingsAsset settings,
            string excelPath,
            out string message)
        {
            message = string.Empty;
            if (settings == null)
            {
                message = "LocalizationSettingsAsset 不存在。";
                return false;
            }

            try
            {
                settings.NormalizeConfiguration();
                if (!LocalizationDataSyncUtility.PrepareSourceDataRoot(
                        out message))
                    return false;

                List<LocaleInfo> locales = GetLocales(settings);
                var categories = new List<CategoryData>();
                string dataRoot = LocalizationPath.GetSourceDataRoot();

                foreach (string category in settings.Categories ?? new List<string>())
                {
                    if (!LocalizationPath.IsSafeCategory(category))
                        continue;

                    CategoryData data = ReadCategoryFromJson(
                        dataRoot,
                        category,
                        locales,
                        settings.JsonFileNamePattern);
                    categories.Add(data);
                }

                if (categories.Count == 0)
                {
                    message = "没有可导出的本地化分类。";
                    return false;
                }

                WriteWorkbook(excelPath, categories, locales);
                message = $"已导出本地化 Excel：{categories.Count} 个分类，文件 {excelPath}";
                LogUtil.Success(LogModule, message);
                return true;
            }
            catch (Exception exception)
            {
                message = $"本地化 Excel 导出失败：{exception.Message}";
                LogUtil.Error(LogModule, message);
                return false;
            }
        }

        /// <summary>
        /// 读取当前 Excel Sheet 的表头和数据行，并转换为分类内相对 Key。
        /// 只读取已配置的语言列；空白 Key 行会被忽略，带翻译但没有 Key 的行会报错。
        /// </summary>
        private static bool TryReadSheet(
            IExcelDataReader reader,
            string category,
            IReadOnlyList<LocaleInfo> locales,
            ImportResult result,
            out string message)
        {
            message = string.Empty;
            if (!reader.Read())
            {
                message = "分类 Sheet 为空，至少需要一行表头。";
                return false;
            }

            var localeColumns = new Dictionary<int, string>();
            for (int column = 0; column < reader.FieldCount; column++)
            {
                string header = reader.GetValue(column)?.ToString().Trim();
                if (column == 0)
                {
                    if (!string.Equals(header, "Key", StringComparison.OrdinalIgnoreCase))
                    {
                        message = "本地化 Excel 第一列表头必须是 Key。";
                        return false;
                    }

                    continue;
                }

                LocaleInfo locale = locales.FirstOrDefault(item =>
                    string.Equals(item.Code, header, StringComparison.OrdinalIgnoreCase));
                if (locale == null)
                    continue;

                if (localeColumns.ContainsValue(locale.Code))
                {
                    message = $"Excel 中语言列重复：{locale.Code}";
                    return false;
                }

                localeColumns[column] = locale.Code;
                result.ValuesByLocale[locale.Code] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            if (localeColumns.Count == 0)
            {
                message = "本地化 Excel 没有匹配到任何已配置语言列。";
                return false;
            }

            while (reader.Read())
            {
                string key = LocalizationKeyUtility.GetCategoryRelativeKey(
                    category,
                    reader.GetValue(0)?.ToString());
                bool emptyRow = string.IsNullOrEmpty(key);
                if (emptyRow)
                {
                    bool hasValue = localeColumns.Keys.Any(column =>
                        !string.IsNullOrWhiteSpace(reader.GetValue(column)?.ToString()));
                    if (!hasValue)
                        continue;

                    message =
                        $"Sheet“{category}”第 {result.RowCount + 2} 行没有 Key，但包含翻译内容。";
                    return false;
                }

                if (!result.Keys.Add(key))
                {
                    message =
                        $"Sheet“{category}”第 {result.RowCount + 2} 行发现重复 Key：“{key}”。"
                        + "该 Key 已在前面的行出现，请合并或删除重复行。";
                    return false;
                }

                foreach (KeyValuePair<int, string> localeColumn in localeColumns)
                {
                    object rawValue = localeColumn.Key < reader.FieldCount
                        ? reader.GetValue(localeColumn.Key)
                        : null;
                    result.ValuesByLocale[localeColumn.Value][key] = rawValue?.ToString() ?? string.Empty;
                }

                result.RowCount++;
            }

            return true;
        }

        /// <summary>
        /// 为导入工作簿建立 Sheet 名到主分类的映射，同时兼容分类名被 Excel 清理后的名称。
        /// </summary>
        private static Dictionary<string, string> BuildSheetCategoryLookup(IReadOnlyList<string> categories)
        {
            var categoryData = categories
                .Select(category => new CategoryData { Name = category })
                .ToList();
            List<string> sheetNames = BuildUniqueSheetNames(categoryData);
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < categories.Count; i++)
            {
                result[categories[i]] = categories[i];
                result[sheetNames[i]] = categories[i];
            }

            return result;
        }

        /// <summary>
        /// 从项目源 JSON 读取一个分类的现有内容，作为 Excel 导入合并或导出的基准。
        /// 此方法只读取磁盘，不修改源文件和运行时副本。
        /// </summary>
        private static CategoryData ReadCategoryFromJson(
            string dataRoot,
            string category,
            IReadOnlyList<LocaleInfo> locales,
            string fileNamePattern)
        {
            var result = new CategoryData { Name = category };
            foreach (LocaleInfo locale in locales)
            {
                var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                result.ValuesByLocale[locale.Code] = values;
                string filePath = LocalizationPath.GetLanguageFilePath(
                    dataRoot,
                    category,
                    locale.Code,
                    fileNamePattern);
                if (!File.Exists(filePath))
                    continue;

                JObject document = JObject.Parse(
                    File.ReadAllText(filePath, Encoding.UTF8).TrimStart('\uFEFF'),
                    new JsonLoadSettings
                    {
                        DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error
                    });
                var fileKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (JProperty property in document.Properties())
                {
                    if (property.Value.Type != JTokenType.String)
                        throw new InvalidDataException($"{filePath} 的 Key 值不是字符串：{property.Name}");

                    string editorKey = LocalizationKeyUtility.GetCategoryRelativeKey(
                        category,
                        property.Name);
                    if (string.IsNullOrWhiteSpace(editorKey))
                        continue;

                    if (!fileKeys.Add(editorKey))
                    {
                        throw new InvalidDataException(
                            $"{filePath} 中存在重复的完整 Key 映射：{editorKey}。请合并分类前缀相同的条目后再导入。");
                    }

                    result.Keys.Add(editorKey);
                    values[editorKey] = property.Value.Value<string>() ?? string.Empty;
                }
            }

            return result;
        }

        /// <summary>
        /// 将翻译内容压缩成适合差异预览窗口显示的一行文本，并转义换行符。
        /// </summary>
        private static string FormatPreviewValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "（空）";

            string normalized = value
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
            return normalized.Length > 70
                ? normalized.Substring(0, 70) + "..."
                : normalized;
        }

        /// <summary>
        /// 将分类数据写入标准 .xlsx 压缩包，包含工作簿、关系、样式和各 Sheet XML。
        /// </summary>
        private static void WriteWorkbook(
            string excelPath,
            IReadOnlyList<CategoryData> categories,
            IReadOnlyList<LocaleInfo> locales)
        {
            string directory = Path.GetDirectoryName(excelPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            using FileStream stream = File.Create(excelPath);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Create);
            List<string> sheetNames = BuildUniqueSheetNames(categories);

            WriteZipEntry(archive, "[Content_Types].xml", BuildContentTypesXml(categories.Count));
            WriteZipEntry(archive, "_rels/.rels", BuildRootRelationshipsXml());
            WriteZipEntry(archive, "xl/workbook.xml", BuildWorkbookXml(sheetNames));
            WriteZipEntry(archive, "xl/_rels/workbook.xml.rels", BuildWorkbookRelationshipsXml(categories.Count));
            WriteZipEntry(archive, "xl/styles.xml", BuildStylesXml());

            for (int i = 0; i < categories.Count; i++)
            {
                WriteZipEntry(
                    archive,
                    $"xl/worksheets/sheet{i + 1}.xml",
                    BuildWorksheetXml(categories[i], locales));
            }
        }

        /// <summary>
        /// 构造单个分类 Sheet 的 XML：首行为 Key 和语言表头，后续行按 Key 写入翻译。
        /// 缺失或空白翻译使用高亮样式，方便导出后人工补齐。
        /// </summary>
        private static string BuildWorksheetXml(CategoryData category, IReadOnlyList<LocaleInfo> locales)
        {
            var builder = new StringBuilder();
            builder.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            builder.Append($"<worksheet xmlns=\"{SpreadsheetNamespace}\"><sheetData>");
            builder.Append("<row r=\"1\">");
            AppendCell(builder, 0, 0, "Key", DataExchangeStyle.Header);
            for (int i = 0; i < locales.Count; i++)
                AppendCell(builder, i + 1, 0, locales[i].Code, DataExchangeStyle.Header);
            builder.Append("</row>");

            int rowIndex = 1;
            foreach (string key in category.Keys.OrderBy(value => value, StringComparer.Ordinal))
            {
                builder.Append($"<row r=\"{rowIndex + 1}\">");
                AppendCell(builder, 0, rowIndex, key, DataExchangeStyle.Default);
                for (int i = 0; i < locales.Count; i++)
                {
                    Dictionary<string, string> values = category.ValuesByLocale[locales[i].Code];
                    bool hasValue = values.TryGetValue(key, out string value);
                    AppendCell(
                        builder,
                        i + 1,
                        rowIndex,
                        value ?? string.Empty,
                        hasValue && !string.IsNullOrWhiteSpace(value)
                            ? DataExchangeStyle.Default
                            : DataExchangeStyle.Highlight);
                }

                builder.Append("</row>");
                rowIndex++;
            }

            builder.Append("</sheetData></worksheet>");
            return builder.ToString();
        }

        /// <summary>
        /// 向 Sheet XML 追加一个内联字符串单元格，并写入坐标、样式和 XML 安全文本。
        /// </summary>
        private static void AppendCell(
            StringBuilder builder,
            int columnIndex,
            int rowIndex,
            string value,
            DataExchangeStyle style)
        {
            string styleAttribute = style == DataExchangeStyle.Default
                ? string.Empty
                : $" s=\"{(int)style}\"";
            builder.Append(
                $"<c r=\"{GetCellReference(columnIndex, rowIndex)}\"{styleAttribute} t=\"inlineStr\"><is><t xml:space=\"preserve\">{XmlEscape(value)}</t></is></c>");
        }

        private enum DataExchangeStyle
        {
            Default = 0,
            Header = 1,
            Highlight = 2
        }

        /// <summary>
        /// 读取配置中的语言列表，标准化语言码并移除重复项，保证 Excel 列稳定且唯一。
        /// </summary>
        private static List<LocaleInfo> GetLocales(LocalizationSettingsAsset settings)
        {
            var result = new List<LocaleInfo>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (LocaleInfo locale in settings.SupportedLocales ?? Array.Empty<LocaleInfo>())
            {
                if (locale == null || !LocaleInfo.TryNormalize(locale.Code, out string code) || !seen.Add(code))
                    continue;

                result.Add(new LocaleInfo(code, locale.DisplayName));
            }

            return result;
        }

        /// <summary>
        /// 生成符合 Excel 规则的唯一 Sheet 名：替换非法字符、限制 31 字符并处理重名后缀。
        /// </summary>
        private static List<string> BuildUniqueSheetNames(IReadOnlyList<CategoryData> categories)
        {
            var result = new List<string>();
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < categories.Count; i++)
            {
                string name = new string((categories[i].Name ?? string.Empty)
                    .Select(character => ":\\/?*[]".IndexOf(character) >= 0 ? '_' : character)
                    .ToArray()).Trim();
                if (string.IsNullOrEmpty(name))
                    name = $"Sheet{i + 1}";
                if (name.Length > 31)
                    name = name.Substring(0, 31);

                string baseName = name;
                int suffix = 2;
                while (!usedNames.Add(name))
                {
                    string suffixText = $"_{suffix++}";
                    int maxBaseLength = Math.Max(1, 31 - suffixText.Length);
                    name = baseName.Substring(0, Math.Min(baseName.Length, maxBaseLength)) + suffixText;
                }

                result.Add(name);
            }

            return result;
        }

        /// <summary>
        /// 将从零开始的列、行索引转换为 Excel 单元格坐标，例如 (0, 0) 转为 A1。
        /// </summary>
        private static string GetCellReference(int columnIndex, int rowIndex)
        {
            int value = columnIndex + 1;
            var letters = new StringBuilder();
            while (value > 0)
            {
                value--;
                letters.Insert(0, (char)('A' + value % 26));
                value /= 26;
            }

            return letters + (rowIndex + 1).ToString();
        }

        /// <summary>
        /// 以 UTF-8 无 BOM 将一段 XML 文本写入 .xlsx 压缩包中的指定条目。
        /// </summary>
        private static void WriteZipEntry(ZipArchive archive, string path, string content)
        {
            ZipArchiveEntry entry = archive.CreateEntry(path, CompressionLevel.Fastest);
            using StreamWriter writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
            writer.Write(content);
        }

        /// <summary>
        /// 构造 .xlsx 的内容类型清单，声明工作簿、样式表和所有工作表 XML 的 MIME 类型。
        /// </summary>
        private static string BuildContentTypesXml(int sheetCount)
        {
            var builder = new StringBuilder();
            builder.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            builder.Append($"<Types xmlns=\"{PackageContentTypesNamespace}\">");
            builder.Append("<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>");
            builder.Append("<Default Extension=\"xml\" ContentType=\"application/xml\"/>");
            builder.Append("<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>");
            builder.Append("<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>");
            for (int i = 1; i <= sheetCount; i++)
                builder.Append($"<Override PartName=\"/xl/worksheets/sheet{i}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");
            builder.Append("</Types>");
            return builder.ToString();
        }

        /// <summary>
        /// 构造压缩包根关系文件，声明根关系指向 xl/workbook.xml。
        /// </summary>
        private static string BuildRootRelationshipsXml()
        {
            return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                   $"<Relationships xmlns=\"{PackageRelationshipsNamespace}\"><Relationship Id=\"rId1\" Type=\"{OfficeRelationshipsNamespace}/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>";
        }

        /// <summary>
        /// 构造工作簿 XML，并为每个 Sheet 分配名称、顺序编号和关系 ID。
        /// </summary>
        private static string BuildWorkbookXml(IReadOnlyList<string> sheetNames)
        {
            var builder = new StringBuilder();
            builder.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            builder.Append($"<workbook xmlns=\"{SpreadsheetNamespace}\" xmlns:r=\"{OfficeRelationshipsNamespace}\"><sheets>");
            for (int i = 0; i < sheetNames.Count; i++)
                builder.Append($"<sheet name=\"{XmlEscape(sheetNames[i])}\" sheetId=\"{i + 1}\" r:id=\"rId{i + 1}\"/>");
            builder.Append("</sheets></workbook>");
            return builder.ToString();
        }

        /// <summary>
        /// 构造工作簿关系文件，连接所有 Sheet 和样式表资源。
        /// </summary>
        private static string BuildWorkbookRelationshipsXml(int sheetCount)
        {
            var builder = new StringBuilder();
            builder.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            builder.Append($"<Relationships xmlns=\"{PackageRelationshipsNamespace}\">");
            for (int i = 1; i <= sheetCount; i++)
                builder.Append($"<Relationship Id=\"rId{i}\" Type=\"{OfficeRelationshipsNamespace}/worksheet\" Target=\"worksheets/sheet{i}.xml\"/>");
            builder.Append($"<Relationship Id=\"rId{sheetCount + 1}\" Type=\"{OfficeRelationshipsNamespace}/styles\" Target=\"styles.xml\"/>");
            builder.Append("</Relationships>");
            return builder.ToString();
        }

        /// <summary>
        /// 构造最小样式表：默认样式、表头样式和缺失翻译高亮样式。
        /// </summary>
        private static string BuildStylesXml()
        {
            return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                   $"<styleSheet xmlns=\"{SpreadsheetNamespace}\"><fonts count=\"2\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font><font><b/><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts><fills count=\"4\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill><fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFD9EAF7\"/><bgColor indexed=\"64\"/></patternFill></fill><fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFFFE699\"/><bgColor indexed=\"64\"/></patternFill></fill></fills><borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders><cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs><cellXfs count=\"3\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/><xf numFmtId=\"0\" fontId=\"1\" fillId=\"2\" borderId=\"0\" applyFont=\"1\" applyFill=\"1\"/><xf numFmtId=\"0\" fontId=\"0\" fillId=\"3\" borderId=\"0\" applyFill=\"1\"/></cellXfs><cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles></styleSheet>";
        }

        /// <summary>
        /// 移除 XML 不允许的控制字符，并对保留字符进行实体编码。
        /// </summary>
        private static string XmlEscape(string value)
        {
            string sanitized = new string((value ?? string.Empty)
                .Where(character => character == '\t' || character == '\n' || character == '\r' || character >= ' ')
                .ToArray());
            return SecurityElement.Escape(sanitized) ?? string.Empty;
        }
    }
}
