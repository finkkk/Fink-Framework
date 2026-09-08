using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FinkFramework.Runtime.Localization;
using FinkFramework.Runtime.Utils;
using UnityEditor;
using UnityEngine;

namespace FinkFramework.Editor.Modules.Localization.UI
{
    /// <summary>
    /// 本地化语言表的 Excel 交换功能：负责语言表的导入和导出。
    /// </summary>
    public sealed partial class LocalizationWindow
    {
        #region Excel 导入与导出

        /// <summary>
        /// 显示语言表 Excel 导入/导出操作菜单。
        /// </summary>
        private void ShowExcelMenu()
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("导入当前分类"), false, ImportExcel);
            menu.AddItem(new GUIContent("导入全部分类"), false, ImportAllExcel);
            menu.AddItem(new GUIContent("导出全部语言表"), false, ExportExcel);
            menu.ShowAsContext();
        }

        /// <summary>
        /// 统一说明 Excel 内容校验失败时的处理结果。
        /// 读取阶段尚未修改编辑器数据或磁盘 JSON，因此必须明确告知用户本次导入被拦截。
        /// </summary>
        private static string BuildExcelImportBlockedMessage(string detail)
        {
            return "检测到 Excel 内容错误，本次导入已被禁止。\n\n"
                + (detail ?? "未提供具体错误信息")
                + "\n\n当前编辑器数据和磁盘 JSON 均未修改。请修复 Excel 后重新导入。";
        }

        /// <summary>
        /// 统一说明 Excel 应用阶段失败的处理结果。
        /// 该阶段可能已经完成部分磁盘写入，所以不承诺“完全未修改”。
        /// </summary>
        private static string BuildExcelImportFailureMessage(string detail)
        {
            return "Excel 导入未完成。\n\n"
                + (detail ?? "未提供具体错误信息")
                + "\n\n请检查错误内容，确认语言表文件状态后再重试。";
        }
        
        /// <summary>
        /// 将单语言 Excel 内容导入当前分类。
        /// </summary>
        private void ImportExcel()
        {
            string category = GetSelectedCategory();
            if (string.IsNullOrEmpty(category))
            {
                EditorUtility.DisplayDialog("导入 Excel", "当前没有可导入的分类。", "确定");
                return;
            }
        
            string excelPath = EditorUtility.OpenFilePanel(
                "导入本地化 Excel",
                LocalizationPath.ProjectRoot,
                "xlsx");
            if (string.IsNullOrEmpty(excelPath) || !ConfirmDiscardChanges())
                return;
        
            if (!LocalizationExcelExchange.TryReadCategory(
                    excelPath,
                    category,
                    locales,
                    out LocalizationExcelExchange.ImportResult importResult,
                    out string readMessage))
            {
                EditorUtility.DisplayDialog(
                    "导入 Excel 已阻止",
                    BuildExcelImportBlockedMessage(readMessage),
                    "确定");
                return;
            }
        
            var existingKeys = new HashSet<string>(keys, StringComparer.OrdinalIgnoreCase);
            int addedKeyCount = importResult.Keys.Count(key => !existingKeys.Contains(key));
            int updatedValueCount = 0;
            int unchangedValueCount = 0;
            var changeDetails = new List<LocalizationDiffPreviewWindow.Section.Change>();
            foreach (KeyValuePair<string, Dictionary<string, string>> localePair in importResult.ValuesByLocale)
            {
                valuesByLocale.TryGetValue(localePair.Key, out Dictionary<string, string> currentValues);
                foreach (KeyValuePair<string, string> valuePair in localePair.Value)
                {
                    if (currentValues != null
                        && currentValues.TryGetValue(valuePair.Key, out string currentValue)
                        && string.Equals(currentValue, valuePair.Value, StringComparison.Ordinal))
                        unchangedValueCount++;
                    else
                    {
                        updatedValueCount++;
                        if (changeDetails.Count < LocalizationExcelExchange.MaxPreviewChangeCount)
                        {
                            string oldValue = currentValues != null
                                && currentValues.TryGetValue(valuePair.Key, out string existingValue)
                                ? existingValue
                                : string.Empty;
                            changeDetails.Add(
                                new LocalizationDiffPreviewWindow.Section.Change(
                                    $"{localePair.Key} / {valuePair.Key}",
                                    FormatDiffValue(oldValue),
                                    FormatDiffValue(valuePair.Value)));
                        }
                    }
                }
            }
        
            var summaryItems = new List<string>
            {
                $"新增 Key：{addedKeyCount} 个",
                $"更新翻译：{updatedValueCount} 个",
                $"未变化翻译：{unchangedValueCount} 个",
                "确认后会载入当前编辑器，之后需要点击“保存并同步运行时副本”才会写回 JSON。"
            };
            if (changeDetails.Count < updatedValueCount)
                summaryItems.Add($"已列出前 {changeDetails.Count} 个翻译变化示例，其余变化已省略。");
        
            LocalizationDiffPreviewWindow.Open(
                "Excel 导入预览",
                $"分类：{category}    预览 Excel 内容与当前编辑器内容的差异。",
                new[]
                {
                    new LocalizationDiffPreviewWindow.Section(
                        "导入统计",
                        LocalizationDiffPreviewWindow.SectionType.Summary,
                        summaryItems),
                    LocalizationDiffPreviewWindow.Section.CreateChanges(
                        $"翻译变化示例（最多显示 {LocalizationExcelExchange.MaxPreviewChangeCount} 条）",
                        LocalizationDiffPreviewWindow.SectionType.Changed,
                        changeDetails)
                },
                "应用导入",
                () => ApplyExcelImport(importResult, category));
        }
        
        private void ApplyExcelImport(
            LocalizationExcelExchange.ImportResult importResult,
            string category)
        {
            var existingKeys = new HashSet<string>(keys, StringComparer.OrdinalIgnoreCase);
            int addedKeyCount = importResult.Keys.Count(key => !existingKeys.Contains(key));
            int updatedValueCount = 0;
            RecordUndoState();
            foreach (string key in importResult.Keys)
            {
                if (existingKeys.Add(key))
                    keys.Add(key);
            }
        
            foreach (KeyValuePair<string, Dictionary<string, string>> localePair in importResult.ValuesByLocale)
            {
                if (!valuesByLocale.TryGetValue(localePair.Key, out Dictionary<string, string> currentValues))
                {
                    currentValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    valuesByLocale[localePair.Key] = currentValues;
                }
        
                foreach (KeyValuePair<string, string> valuePair in localePair.Value)
                {
                    if (!currentValues.TryGetValue(valuePair.Key, out string currentValue)
                        || !string.Equals(currentValue, valuePair.Value, StringComparison.Ordinal))
                        updatedValueCount++;
                    currentValues[valuePair.Key] = valuePair.Value;
                }
            }
        
            keys.Sort(StringComparer.Ordinal);
            hasPendingChanges = true;
            LogUtil.Info(
                "LocalizationExcelTool",
                $"已将 Excel 导入当前编辑器：分类 {category}，新增 Key {addedKeyCount} 个，" +
                $"更新翻译 {updatedValueCount} 个。");
            ShowNotification(new GUIContent("Excel 已载入，请保存并同步运行时副本"));
            Repaint();
        }
        
        /// <summary>
        /// 将包含多语言列的 Excel 内容导入当前分类。
        /// </summary>
        private void ImportAllExcel()
        {
            IReadOnlyList<string> categoryNames = LocalizationEditorDataUtility.GetCategories(settings);
            if (categoryNames.Count == 0)
            {
                EditorUtility.DisplayDialog("导入全部 Excel", "当前没有配置可导入的分类。", "确定");
                return;
            }
        
            if (!ConfirmDiscardChanges())
                return;
        
            if (HasExternalFileChanges())
            {
                if (!EditorUtility.DisplayDialog(
                        "语言表已在外部修改",
                        "当前分类的语言表已被外部修改。继续批量导入会以磁盘文件为基准，是否重新载入当前分类后继续？",
                        "重新载入并继续",
                        "取消"))
                    return;
        
                LoadSelectedCategory();
            }
        
            string excelPath = EditorUtility.OpenFilePanel(
                "导入全部本地化 Excel",
                LocalizationPath.ProjectRoot,
                "xlsx");
            if (string.IsNullOrEmpty(excelPath))
                return;
        
            if (!LocalizationExcelExchange.TryReadWorkbook(
                    excelPath,
                    categoryNames,
                    locales,
                    out LocalizationExcelExchange.WorkbookImportResult workbook,
                    out string readMessage))
            {
                EditorUtility.DisplayDialog(
                    "导入全部 Excel 已阻止",
                    BuildExcelImportBlockedMessage(readMessage),
                    "确定");
                return;
            }
        
            if (!LocalizationExcelExchange.TryBuildImportSummary(
                    settings,
                    workbook,
                    out LocalizationExcelExchange.ImportSummary summary,
                    out string summaryMessage))
            {
                EditorUtility.DisplayDialog(
                    "导入全部 Excel 已阻止",
                    BuildExcelImportBlockedMessage(summaryMessage),
                    "确定");
                return;
            }
        
            var summaryItems = new List<string>
            {
                readMessage,
                $"新增 Key：{summary.AddedKeyCount} 个",
                $"更新翻译：{summary.UpdatedValueCount} 个",
                $"未变化翻译：{summary.UnchangedValueCount} 个",
                "确认后会直接写回已匹配分类的 JSON。",
                "Excel 中没有出现的旧 Key 会保留。"
            };
            if (summary.ChangeDetails.Count > 0)
            {
                var changeDetails = summary.ChangeDetails
                    .Select(detail => new LocalizationDiffPreviewWindow.Section.Change(
                        $"{detail.Category} / {detail.Locale} / {detail.Key}",
                        detail.Before,
                        detail.After))
                    .ToList();
                summaryItems.Add(summary.UpdatedValueCount > summary.ChangeDetails.Count
                    ? $"已列出前 {summary.ChangeDetails.Count} 个翻译变化示例，其余变化已省略。"
                    : $"已列出全部 {summary.ChangeDetails.Count} 个翻译变化。");
        
                if (workbook.MissingCategories.Count > 0)
                    summaryItems.Add($"未找到的配置分类：{string.Join("、", workbook.MissingCategories)}");
                if (workbook.UnknownSheets.Count > 0)
                    summaryItems.Add($"未匹配的 Sheet：{string.Join("、", workbook.UnknownSheets)}");
        
                var sections = new List<LocalizationDiffPreviewWindow.Section>
                {
                    new LocalizationDiffPreviewWindow.Section(
                        "导入统计",
                        LocalizationDiffPreviewWindow.SectionType.Summary,
                        summaryItems),
                    LocalizationDiffPreviewWindow.Section.CreateChanges(
                        $"翻译变化示例（最多显示 {LocalizationExcelExchange.MaxPreviewChangeCount} 条）",
                        LocalizationDiffPreviewWindow.SectionType.Changed,
                        changeDetails)
                };
                LocalizationDiffPreviewWindow.Open(
                    "全部 Excel 导入预览",
                    "预览整个工作簿与当前磁盘 JSON 的差异。",
                    sections,
                    "确认导入",
                    () => ApplyAllExcelImport(workbook));
                return;
            }
        
            if (workbook.MissingCategories.Count > 0)
                summaryItems.Add($"未找到的配置分类：{string.Join("、", workbook.MissingCategories)}");
            if (workbook.UnknownSheets.Count > 0)
                summaryItems.Add($"未匹配的 Sheet：{string.Join("、", workbook.UnknownSheets)}");
        
            LocalizationDiffPreviewWindow.Open(
                "全部 Excel 导入预览",
                "预览整个工作簿与当前磁盘 JSON 的差异。",
                new[]
                {
                    new LocalizationDiffPreviewWindow.Section(
                        "导入统计",
                        LocalizationDiffPreviewWindow.SectionType.Summary,
                        summaryItems)
                },
                "确认导入",
                () => ApplyAllExcelImport(workbook));
        }
        
        private void ApplyAllExcelImport(
            LocalizationExcelExchange.WorkbookImportResult workbook)
        {
            if (!LocalizationExcelExchange.TryApplyWorkbook(
                    settings,
                    workbook,
                    out LocalizationExcelExchange.ApplyResult applyResult,
                    out string applyMessage))
            {
                EditorUtility.DisplayDialog(
                    "Excel 导入未完成",
                    BuildExcelImportFailureMessage(applyMessage),
                    "确定");
                return;
            }
        
            AssetDatabase.Refresh();
            LoadSelectedCategory();
            ShowNotification(new GUIContent("全部语言表已导入"));
            EditorUtility.DisplayDialog(
                "导入完成",
                applyMessage + "\n\n" +
                string.Join("\n", applyResult.CategoryDetails) +
                "\n\n语言表已导入，Key 会直接从 JSON 读取。",
                "确定");
        }
        
        /// <summary>
        /// 导出当前分类的多语言内容到 Excel。
        /// </summary>
        private void ExportExcel()
        {
            if (hasPendingChanges
                && !EditorUtility.DisplayDialog(
                    "导出本地化 Excel",
                    "当前编辑器有未保存修改。导出全部语言表将使用磁盘上已保存的 JSON，是否继续？",
                    "继续导出",
                    "取消"))
                return;
        
            string excelPath = EditorUtility.SaveFilePanel(
                "导出本地化 Excel",
                LocalizationPath.ProjectRoot,
                "Localization.xlsx",
                "xlsx");
            if (string.IsNullOrEmpty(excelPath))
                return;
        
            if (File.Exists(excelPath)
                && !EditorUtility.DisplayDialog(
                    "确认覆盖 Excel",
                    $"文件已经存在，是否覆盖？\n\n{excelPath}",
                    "覆盖",
                    "取消"))
                return;
        
            if (!LocalizationExcelExchange.TryExport(settings, excelPath, out string message))
            {
                EditorUtility.DisplayDialog("导出 Excel 失败", message, "确定");
                return;
            }
        
            ShowNotification(new GUIContent("Excel 导出完成"));
            EditorUtility.DisplayDialog("导出完成", message, "确定");
        }
        
        #endregion
    }
}
