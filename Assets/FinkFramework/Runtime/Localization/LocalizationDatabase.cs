using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace FinkFramework.Runtime.Localization
{
    /// <summary>
    /// 按“语言 + 分类”保存本地化表的内存数据库。
    /// </summary>
    public sealed class LocalizationDatabase
    {
        private sealed class LocalizationTable
        {
            public Dictionary<string, string> Entries;
        }

        private readonly Dictionary<string, Dictionary<string, LocalizationTable>> tablesByLocale =
            new Dictionary<string, Dictionary<string, LocalizationTable>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Dictionary<string, string>> entriesByLocale =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        private string activeLocale;
        private string fallbackLocale;
        private int entryCount;
        private int tableCount;

        /// <summary>
        /// 当前数据库中所有已加载表的文本数量。
        /// </summary>
        public int Count => entryCount;

        /// <summary>
        /// 当前数据库中已加载的语言表数量。
        /// </summary>
        public int TableCount => tableCount;

        /// <summary>
        /// 设置默认查询语言和 Fallback 语言。
        /// </summary>
        public void SetLocaleContext(string localeCode, string fallbackLocaleCode)
        {
            activeLocale = LocaleInfo.TryNormalize(localeCode, out string normalizedLocale)
                ? normalizedLocale
                : null;
            fallbackLocale = LocaleInfo.TryNormalize(fallbackLocaleCode, out string normalizedFallback)
                ? normalizedFallback
                : null;
        }

        /// <summary>
        /// 使用当前语言上下文查询一个 Key。
        /// </summary>
        public bool TryGet(string key, out string value)
        {
            return TryGet(activeLocale, fallbackLocale, key, out value);
        }

        /// <summary>
        /// 使用指定语言和 Fallback 语言查询一个 Key。
        /// </summary>
        public bool TryGet(
            string localeCode,
            string fallbackLocaleCode,
            string key,
            out string value)
        {
            return TryGet(
                localeCode,
                fallbackLocaleCode,
                key,
                out value,
                out _);
        }

        /// <summary>
        /// 使用指定语言和 Fallback 语言查询一个 Key，并返回是否使用了 Fallback。
        /// </summary>
        public bool TryGet(
            string localeCode,
            string fallbackLocaleCode,
            string key,
            out string value,
            out bool usedFallback)
        {
            value = null;
            usedFallback = false;
            if (string.IsNullOrWhiteSpace(key))
                return false;

            key = key.Trim();

            if (TryGetFromLocale(localeCode, key, out value))
                return true;

            if (string.Equals(localeCode, fallbackLocaleCode, StringComparison.OrdinalIgnoreCase))
                return false;

            if (!TryGetFromLocale(fallbackLocaleCode, key, out value))
                return false;

            usedFallback = true;
            return true;
        }

        /// <summary>
        /// 判断指定语言表是否已加载。
        /// </summary>
        public bool IsTableLoaded(string localeCode, string category)
        {
            return ContainsTable(localeCode, category);
        }

        /// <summary>
        /// 解析并添加一张语言表。解析成功后才会提交到数据库。
        /// 同一语言内的 Key 不能跨分类重复，不同语言可以拥有相同 Key。
        /// </summary>
        public bool TryAddJson(
            string json,
            string sourceName,
            string localeCode,
            string category,
            out string error)
        {
            error = null;

            if (!LocaleInfo.TryNormalize(localeCode, out string normalizedLocale))
            {
                error = $"语言标识无效：{localeCode}";
                return false;
            }

            if (!LocalizationPath.IsSafeCategory(category))
            {
                error = $"分类名称无效：{category}";
                return false;
            }

            string sourceLabel = string.IsNullOrWhiteSpace(sourceName)
                ? $"{normalizedLocale}/{category.Trim()}"
                : sourceName.Trim();

            if (ContainsTable(normalizedLocale, category))
            {
                error = $"语言表已加载：{sourceLabel}";
                return false;
            }

            if (!TryParseJson(json, out Dictionary<string, string> parsedEntries, out error))
            {
                error = $"{sourceLabel}：{error}";
                return false;
            }

            // 语言表允许兼容读取分类内相对 Key，但内存数据库统一保存完整 Key，
            // 避免相对 Key 与完整 Key 同时存在时产生两套不可互查的数据。
            // 编辑器 Key 选择和 Manifest 索引均按不区分大小写处理，运行时也必须拒绝大小写变体。
            var pendingEntries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, string> entry in parsedEntries)
            {
                string fullKey = LocalizationKeyUtility.BuildFullKey(category, entry.Key);
                if (string.IsNullOrWhiteSpace(fullKey))
                {
                    error = $"Key 无法补充分类前缀：{entry.Key}";
                    return false;
                }

                if (!pendingEntries.TryAdd(fullKey, entry.Value))
                {
                    error = $"检测到重复 Key：{fullKey}";
                    return false;
                }
            }

            if (tablesByLocale.TryGetValue(normalizedLocale, out Dictionary<string, LocalizationTable> localeTables))
            {
                foreach (string key in pendingEntries.Keys)
                {
                    foreach (LocalizationTable table in localeTables.Values)
                    {
                        if (table.Entries.ContainsKey(key))
                        {
                            error = $"检测到重复 Key：{key}";
                            return false;
                        }
                    }
                }
            }
            else
            {
                localeTables = new Dictionary<string, LocalizationTable>(StringComparer.OrdinalIgnoreCase);
            }

            if (!entriesByLocale.TryGetValue(
                    normalizedLocale,
                    out Dictionary<string, string> localeEntries))
            {
                localeEntries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            localeTables.Add(category.Trim(), new LocalizationTable
            {
                Entries = pendingEntries
            });
            foreach (KeyValuePair<string, string> entry in pendingEntries)
                localeEntries.Add(entry.Key, entry.Value);

            tablesByLocale[normalizedLocale] = localeTables;
            entriesByLocale[normalizedLocale] = localeEntries;
            tableCount++;
            entryCount += pendingEntries.Count;
            return true;
        }

        /// <summary>
        /// 兼容旧调用方式。新代码应传入语言和分类，以便支持独立卸载。
        /// </summary>
        public bool TryAddJson(string json, string sourceName, out string error)
        {
            if (string.IsNullOrEmpty(activeLocale))
            {
                error = "未设置语言上下文，无法使用旧版语言表加载接口。";
                return false;
            }

            return TryAddJson(json, sourceName, activeLocale, "__legacy", out error);
        }

        /// <summary>
        /// 卸载指定语言下的一个分类。
        /// </summary>
        public bool RemoveTable(string localeCode, string category)
        {
            if (!LocaleInfo.TryNormalize(localeCode, out string normalizedLocale)
                || !LocalizationPath.IsSafeCategory(category)
                || !tablesByLocale.TryGetValue(normalizedLocale, out Dictionary<string, LocalizationTable> localeTables))
                return false;

            string normalizedCategory = category.Trim();
            if (!localeTables.Remove(normalizedCategory, out LocalizationTable removedTable))
                return false;

            if (entriesByLocale.TryGetValue(
                    normalizedLocale,
                    out Dictionary<string, string> localeEntries))
            {
                // 同一语言内禁止重复 Key，因此可以直接移除该分类的条目。
                foreach (string key in removedTable.Entries.Keys)
                    localeEntries.Remove(key);
            }

            tableCount--;
            entryCount -= removedTable.Entries.Count;
            if (localeTables.Count == 0)
            {
                tablesByLocale.Remove(normalizedLocale);
                entriesByLocale.Remove(normalizedLocale);
            }

            return true;
        }

        /// <summary>
        /// 从数据库中移除指定语言的全部分类。
        /// </summary>
        public int RemoveLocale(string localeCode)
        {
            if (!LocaleInfo.TryNormalize(localeCode, out string normalizedLocale))
                return 0;

            if (!tablesByLocale.TryGetValue(normalizedLocale, out Dictionary<string, LocalizationTable> localeTables))
                return 0;

            int removedCount = localeTables.Count;
            int removedEntryCount = entriesByLocale.TryGetValue(
                normalizedLocale,
                out Dictionary<string, string> localeEntries)
                ? localeEntries.Count
                : CountEntries(localeTables);
            tablesByLocale.Remove(normalizedLocale);
            entriesByLocale.Remove(normalizedLocale);
            tableCount -= removedCount;
            entryCount -= removedEntryCount;
            return removedCount;
        }

        /// <summary>
        /// 清空所有已加载文本和语言表。
        /// </summary>
        public void Clear()
        {
            tablesByLocale.Clear();
            entriesByLocale.Clear();
            entryCount = 0;
            tableCount = 0;
            activeLocale = null;
            fallbackLocale = null;
        }

        /// <summary>
        /// 统计一组语言表中的文本条目数量，用于维护数据库计数器。
        /// </summary>
        private static int CountEntries(
            IReadOnlyDictionary<string, LocalizationTable> localeTables)
        {
            int count = 0;
            foreach (LocalizationTable table in localeTables.Values)
                count += table.Entries.Count;
            return count;
        }

        /// <summary>
        /// 只从指定语言的已加载条目中查询，不执行 Fallback。
        /// </summary>
        private bool TryGetFromLocale(string localeCode, string key, out string value)
        {
            value = null;
            if (string.IsNullOrWhiteSpace(key)
                || !LocaleInfo.TryNormalize(localeCode, out string normalizedLocale)
                || !entriesByLocale.TryGetValue(normalizedLocale, out Dictionary<string, string> localeEntries))
                return false;

            return localeEntries.TryGetValue(key.Trim(), out value);
        }

        /// <summary>
        /// 判断指定语言和分类是否已经提交到数据库。
        /// </summary>
        private bool ContainsTable(string localeCode, string category)
        {
            if (!LocaleInfo.TryNormalize(localeCode, out string normalizedLocale)
                || !LocalizationPath.IsSafeCategory(category)
                || !tablesByLocale.TryGetValue(normalizedLocale, out Dictionary<string, LocalizationTable> localeTables))
                return false;

            return localeTables.ContainsKey(category.Trim());
        }

        /// <summary>
        /// 解析一张 JSON 语言表，并在提交前校验 Key 与 Value 类型。
        /// </summary>
        private static bool TryParseJson(
            string json,
            out Dictionary<string, string> entries,
            out string error)
        {
            entries = null;
            error = null;

            if (string.IsNullOrWhiteSpace(json))
            {
                error = "JSON 内容为空。";
                return false;
            }

            JObject root;
            try
            {
                root = JObject.Parse(
                    json.TrimStart('\uFEFF'),
                    new JsonLoadSettings
                    {
                        DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error
                    });
            }
            catch (Exception exception)
            {
                error = $"JSON 格式错误：{exception.Message}";
                return false;
            }

            var pendingEntries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (JProperty property in root.Properties())
            {
                if (string.IsNullOrWhiteSpace(property.Name))
                {
                    error = "Key 不能为空。";
                    return false;
                }

                if (property.Value.Type != JTokenType.String)
                {
                    error = $"Key '{property.Name}' 的 Value 必须是字符串。";
                    return false;
                }

                if (!pendingEntries.TryAdd(property.Name, property.Value.Value<string>() ?? string.Empty))
                {
                    error = $"检测到重复 Key：{property.Name}";
                    return false;
                }
            }

            entries = pendingEntries;
            return true;
        }
    }
}
