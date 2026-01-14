using System;
using System.IO;
using System.Linq;
using ExcelDataReader;
using FinkFramework.Runtime.Data;
using FinkFramework.Runtime.Utils;

namespace FinkFramework.Editor.Modules.Data
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
        #region === 全局统计 ===
        private static int successCount; // 完全通过的表
        private static int warningCount; // 含警告的表
        private static int errorCount;   // 含错误的表
        #endregion
        
        #region === 入口：验证所有 Excel ===
        
        /// <summary>
        /// 扫描 DataTables 目录，验证所有 Excel 文件的数据合法性。
        /// 输出最终 QA 汇总统计（成功 / 警告 / 错误）。
        /// </summary>
        public static void ValidateAllData()
        {
            string sourceRoot = DataPipelinePath.ExcelRoot;
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
        #endregion
        
        #region === 内部：单个表格验证 ===
        /// <summary>
        /// 对单个 Excel 文件执行：
        /// 1. 字段名合法性检查
        /// 2. 字段类型结构可解析性检查（使用伪造数据）
        /// 3. 每个单元格实际数据解析检查
        /// </summary>
        private static void ValidateExcel(string excelPath)
        {
            bool hasError = false;
            bool hasWarn = false;
            string tableName = Path.GetFileNameWithoutExtension(excelPath);

            try
            {
                using var stream = File.Open(excelPath, FileMode.Open, FileAccess.Read);
                using var reader = ExcelReaderFactory.CreateReader(stream);
                // 读取表格前三行数据信息
                var header = ExcelReaderTool.ReadHeader(reader);
                var fieldNames = header.fieldNames;
                var fieldTypes = header.fieldTypes;

                
                var check = ExcelReaderTool.CheckFieldDefinitions(
                    tableName,
                    header.fieldNames,
                    header.fieldTypes
                );

                var skipColumn = check.skipColumn;
                hasError |= check.hasError;
                hasWarn |= check.hasWarn;
                // 尝试基于变量类型填入一个伪造数值去测试解析，验证类型结构是否可被支持
                for (int i = 0; i < fieldNames.Length; i++)
                {
                    string name = fieldNames[i];
                    string typeName = fieldTypes[i];
                    
                    var testValue = GenerateTestValueForType(typeName);
                    string pos = $"第2行第{i+1}列";
                    var pr = DataParseTool.ConvertValue(
                        testValue,
                        typeName,
                        $"变量类型检查-变量名:{name} (坐标:{pos})",
                        tableName
                    );
                    if (pr.errors.Count > 0)
                    {
                        hasError = true;
                        foreach (var err in pr.errors)
                            LogUtil.Error("DataQATool", err);
                    }

                    if (pr.warnings.Count > 0)
                    {
                        hasWarn = true;
                        foreach (var w in pr.warnings)
                            LogUtil.Warn("DataQATool", w);
                    }
                }
                // ==== 按行读取并检查数据 ====
                int rowIndex = 4;
                while (reader.Read())
                {
                    bool isEmpty = true;
                    // 判断本行是否为空行（跳过）
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
                    // 遍历每列
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        if (skipColumn[i]) continue;   // 跳过未生成类型的整列
                        
                        if (string.IsNullOrEmpty(fieldNames[i])) continue;
                        object raw = reader.GetValue(i);

                        string pos = $"第{rowIndex}行第{i+1}列";
                        var pr = DataParseTool.ConvertValue(
                            raw,
                            fieldTypes[i],
                            $"变量数据解析-变量名:{fieldNames[i]} (坐标:{pos})",
                            tableName
                        );

                        if (pr.errors.Count > 0)
                        {
                            hasError = true;
                            foreach (var err in pr.errors)
                                LogUtil.Error("DataQATool", err);
                        }

                        if (pr.warnings.Count > 0)
                        {
                            hasWarn = true;
                            foreach (var w in pr.warnings)
                                LogUtil.Warn("DataQATool", w);
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
                    LogUtil.Warn("DataQATool", $"[{tableName}] ✗ 存在警告或错误！不予通过");
                }
            }
            catch (Exception ex)
            {
                errorCount++;

                // 文件被占用（通常是 Excel 正在打开）
                if (ex is IOException && ex.Message.Contains("Sharing violation"))
                {
                    LogUtil.Error("DataQATool", 
                        $"[{tableName}] 验证失败：无法读取 Excel 文件，因为该文件可能正在被 Excel 或其他程序打开。\n" +
                        $"请关闭所有打开 {tableName}.xlsx 的窗口后再试一次。");

                    return;
                }

                // 其他未知错误
                LogUtil.Error("DataQATool", $"[{tableName}] 验证失败: {ex.Message}");
            }
        }
        #endregion

        #region === 内部：生成测试数据 === 
        
        /// <summary>
        /// 为 QA 生成一个“可用于测试类型结构”的伪造数据
        /// 比直接传 null 更严谨，会模拟 JSON 或数组结构。
        /// </summary>
        private static object GenerateTestValueForType(string type)
        {
            type = type.Trim();

            // 基础类型
            switch (type)
            {
                case "string": return "";
                case "int":
                case "float":
                case "double":
                case "long":
                    return "0";
                case "bool": return "false";
                case "char": return "a";
                case "decimal":
                case "short":
                case "ushort":
                case "byte":
                case "sbyte":
                    return "0";
                case "DateTime": return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            }

            // 泛型：List<T>
            if (type.StartsWith("List<"))
                return "[]";   // 最小合法列表

            // 数组 T[]
            if (type.EndsWith("[]"))
                return "[]";   // 和 List 一致即可

            // 字典 Dictionary<K,V>
            if (type.StartsWith("Dictionary<"))
                return "{}";   // 最小合法字典

            switch (type)
            {
                // Vector2 / Vector3 / Vector4
                case "Vector2":
                    return "(0,0)";
                case "Vector3":
                    return "(0,0,0)";
                case "Vector4":
                    return "(0,0,0,0)";
                // Color
                case "Color":
                    return "(1,1,1,1)";
            }

            // ===== enum 类型专用测试值 =====
            Type t = DataParseTool.FindTypeCached(type);
            if (t is { IsEnum: true })
            {
                // 使用 enum 的第一个合法值作为测试
                return Enum.GetNames(t).FirstOrDefault() ?? "";
            }

            // 其他自定义类 —— 用空 JSON 结构测试
            return "{}";
        }
        
        #endregion
    }
}
