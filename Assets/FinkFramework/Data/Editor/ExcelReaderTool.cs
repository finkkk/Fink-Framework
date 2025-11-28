using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ExcelDataReader;
using FinkFramework.Utils;

namespace FinkFramework.Data.Editor
{
    /// <summary>
    /// Excel 通用读取工具类
    /// ----------------------------------------------------------------
    /// 主要职责：
    /// 1. 读取 Excel 表头（字段名 / 字段类型 / 描述行）
    /// 2. 构建跳列标记 skipColumn（字段类型无法解析的列自动跳过）
    /// 3. 遍历 Excel 数据行
    /// </summary>
    public class ExcelReaderTool
    {
        /// <summary>
        /// 表头结构（字段名 / 字段类型 / 字段描述）
        /// </summary>
        public class ExcelHeader
        {
            public string[] fieldNames;
            public string[] fieldTypes;
            public string[] fieldDescs;
        }
        
        /// <summary>
        /// 读取 Excel 的前三行表头：字段名 / 字段类型 / 字段描述
        /// </summary>
        public static ExcelHeader ReadHeader(IExcelDataReader reader)
        {
            // === 读取字段名 ===
            reader.Read();
            var names = Enumerable.Range(0, reader.FieldCount)
                .Select(i => reader.GetString(i)?.Trim() ?? "")
                .ToArray();
            // === 读取字段类型 ===
            reader.Read();
            var types = Enumerable.Range(0, reader.FieldCount)
                .Select(i => reader.GetString(i)?.Trim() ?? "string")
                .ToArray();
            // === 读取描述行内容 ===
            string[] descs = new string[reader.FieldCount];
            bool hasDesc = reader.Read();
            if (hasDesc)
            {
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    object v = reader.GetValue(i);
                    descs[i] = v?.ToString().Trim() ?? "";
                }
            }
            else
            {
                // 若第三行缺失，则全部设为空
                for (int i = 0; i < reader.FieldCount; i++)
                    descs[i] = "";
            }

            return new ExcelHeader
            {
                fieldNames = names,
                fieldTypes = types,
                fieldDescs = descs
            };
        }
        
        /// <summary>
        /// 表格内容检查返回数据
        /// </summary>
        ///
        public class FieldCheckResult
        {
            public bool[] skipColumn;
            public bool hasError;
            public bool hasWarn;
        }
        /// <summary>
        /// 表格内容检查
        /// </summary>
        public static FieldCheckResult CheckFieldDefinitions(string tableName, string[] fieldNames, string[] fieldTypes)
        {
            FieldCheckResult result = new FieldCheckResult
            {
                skipColumn = new bool[fieldNames.Length],
                hasError = false,
                hasWarn = false
            };
            // ==== 表格内容检查 ====
            HashSet<string> nameSet = new();
            for (int i = 0; i < fieldNames.Length; i++)
            {
                string name = fieldNames[i];
                string typeName = fieldTypes[i];
                // 字段名为空
                if (string.IsNullOrEmpty(name))
                {
                    LogUtil.Error("DataQATool", $"[{tableName}] 第 {i + 1} 列字段名为空！");
                    result.hasError = true;
                    continue;
                }

                // 字段名非法
                if (!Regex.IsMatch(name, @"^[a-zA-Z_][a-zA-Z0-9_]*$"))
                {
                    LogUtil.Warn("DataQATool", $"[{tableName}] 字段名 '{name}' 含非法字符（例如特殊符号或中文）");
                    result.hasWarn = true;
                }

                // 字段名重复
                if (!nameSet.Add(name))
                {
                    LogUtil.Error("DataQATool", $"[{tableName}] 字段名重复：{name}");
                    result.hasError = true;
                    continue;
                }

                // 类型未生成 → 智能跳过并给出警告
                var resolvedType = DataParseTool.FindTypeCached(typeName);
                if (resolvedType == null)
                {
                    LogUtil.Warn("DataQATool",
                        $"[{tableName}] 字段 '{name}' (第2行第{i + 1}列) 类型 '{typeName}' 无法解析。" +
                        $"可能原因：字段类型中存在未知数据类。" +
                        $"请先执行【数据工具面板 → 仅生成数据文件】再进行 QA。" +
                        $"此列将跳过 QA 检查。");

                    result.hasWarn = true;
                    // 当类型无法找到时自动跳过该类型所在列的数据解析
                    result.skipColumn[i] = true;
                }
            }
            return result;
        }
    }
}