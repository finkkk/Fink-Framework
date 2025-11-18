using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ExcelDataReader;
using Framework.Utils;
using UnityEngine;

namespace Framework.Data.Editor
{
    /// <summary>
    /// 数据 QA 验证工具
    /// ------------------------------------------------------------
    /// 作用：
    /// - 扫描所有 Excel 表格；
    /// - 检查字段名合法性、类型可解析性；
    /// - 尝试解析所有单元格，捕获并打印详细错误；
    /// - 输出整体通过率统计；
    /// ------------------------------------------------------------
    /// 可直接由 DataToolWindow 调用。
    /// </summary>
    public static class DataQATool
    {
        private static int successCount;
        private static int warningCount;
        private static int errorCount;

        public static void ValidateAllData()
        {
            string sourceRoot = Path.Combine(Application.dataPath, "Data");
            if (!Directory.Exists(sourceRoot))
            {
                LogUtil.Error("DataQATool", $"源目录不存在: {sourceRoot}");
                return;
            }

            var excelFiles = Directory.EnumerateFiles(sourceRoot, "*.xlsx", SearchOption.AllDirectories).ToArray();
            if (excelFiles.Length == 0)
            {
                LogUtil.Warn("DataQATool", $"没有找到任何可验证的文件: {sourceRoot}");
                return;
            }

            successCount = 0;
            warningCount = 0;
            errorCount = 0;
            LogUtil.Info("DataQATool", $"开始执行数据验证 ({excelFiles.Length} 张表)...");

            foreach (var excelPath in excelFiles)
                ValidateExcel(excelPath);

            // === 最终统计 ===
            int total = excelFiles.Length;
            int passed = successCount;
            int failed = total - passed;

            string summary =
                $"共 <color=#FFFFFF>{total}</color> 张表｜" +
                $"通过 <color=#00C853>{passed}</color>｜" +
                $"警告 <color=#FFB300>{warningCount}</color>｜" +
                $"错误 <color=#FF5252>{errorCount}</color>｜" +
                $"未通过 <color=#FF7043>{failed}</color>";

            if (failed == 0)
                LogUtil.Success("DataQATool", $"全部通过｜{summary}");
            else
                LogUtil.Warn("DataQATool", $"QA未通过｜{summary}");
        }

        private static void ValidateExcel(string excelPath)
        {
            bool hasError = false;
            bool hasWarn = false;
            string tableName = Path.GetFileNameWithoutExtension(excelPath);

            try
            {
                using var stream = File.Open(excelPath, FileMode.Open, FileAccess.Read);
                using var reader = ExcelReaderFactory.CreateReader(stream);

                reader.Read(); // 字段名
                var fieldNames = Enumerable.Range(0, reader.FieldCount)
                    .Select(i => reader.GetString(i)?.Trim() ?? "")
                    .ToArray();

                reader.Read(); // 字段类型
                var fieldTypes = Enumerable.Range(0, reader.FieldCount)
                    .Select(i => reader.GetString(i)?.Trim() ?? "string")
                    .ToArray();

                reader.Read(); // 跳过描述行

                HashSet<string> nameSet = new();
                for (int i = 0; i < fieldNames.Length; i++)
                {
                    string name = fieldNames[i];
                    string typeName = fieldTypes[i];

                    if (string.IsNullOrEmpty(name))
                    {
                        LogUtil.Error("DataQATool", $"[{tableName}] 第 {i + 1} 列字段名为空！");
                        hasError = true;
                        continue;
                    }

                    if (!Regex.IsMatch(name, @"^[a-zA-Z_][a-zA-Z0-9_]*$"))
                    {
                        LogUtil.Warn("DataQATool", $"[{tableName}] 字段名 '{name}' 含非法字符");
                        hasWarn = true;
                    }

                    if (!nameSet.Add(name))
                    {
                        LogUtil.Error("DataQATool", $"[{tableName}] 字段名重复：{name}");
                        hasError = true;
                    }

                    // 让 DataParseTool 作为最终权威
                    try
                    {
                        // 尝试解析一个空值，验证类型结构是否可被支持
                        _ = DataParseTool.ConvertValue(null, typeName, name, tableName);
                    }
                    catch
                    {
                        LogUtil.Error("DataQATool", $"[{tableName}] 字段 '{name}' 类型不受支持：{typeName}");
                        hasError = true;
                    }
                }

                int rowIndex = 5;
                while (reader.Read())
                {
                    bool isEmpty = true;
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        if (reader.GetValue(i) != null && !string.IsNullOrWhiteSpace(reader.GetValue(i).ToString()))
                        {
                            isEmpty = false;
                            break;
                        }
                    }

                    if (isEmpty)
                    {
                        rowIndex++;
                        continue;
                    }

                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        if (string.IsNullOrEmpty(fieldNames[i])) continue;
                        object raw = reader.GetValue(i);

                        try
                        {
                            _ = DataParseTool.ConvertValue(raw, fieldTypes[i], fieldNames[i], tableName);
                        }
                        catch (Exception ex)
                        {
                            LogUtil.Error("DataQATool",
                                $"[{tableName}] 第 {rowIndex} 行 字段 '{fieldNames[i]}' ({fieldTypes[i]}) 验证失败: {ex.Message}");
                            hasError = true;
                        }
                    }

                    rowIndex++;
                }

                // === 表格结果 ===
                if (!hasError && !hasWarn)
                {
                    successCount++;
                    LogUtil.Success("DataQATool", $"[{tableName}] ✔ 验证通过");
                }
                else
                {
                    if (hasWarn) warningCount++;
                    if (hasError) errorCount++;
                    LogUtil.Warn("DataQATool", $"[{tableName}] ✗ 存在警告或错误");
                }
            }
            catch (Exception ex)
            {
                errorCount++;
                LogUtil.Error("DataQATool", $"[{tableName}] 验证失败: {ex.Message}");
            }
        }
    }
}
