using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ExcelDataReader;
using FinkFramework.Runtime.Data;
using FinkFramework.Runtime.Environments;
using FinkFramework.Runtime.Settings.Loaders;
using FinkFramework.Runtime.Utils;
using UnityEditor;

namespace FinkFramework.Editor.Modules.Data
{
    /// <summary>
    /// 数据导出工具（DataExportTool）。
    ///
    /// 1. 扫描项目根目录/FinkFramework_Data/DataTables 下的 Excel；
    /// 2. 将表格内容转换为容器对象并生成 JSON 快照；
    /// 3. Binary 模式下额外生成运行时 Binary 文件；
    /// 4. 由 DataManifestTool 生成运行时文件清单。
    /// </summary>
    public class DataExportTool
    {
        #region === 主流程 ===
        /// <summary>
        /// 一键读取全部数据并导出文件
        /// </summary>
        public static (int success, int total) ExportAllData(bool silent = false)
        {
            // 原始数据源目录 表格放置位置：项目根目录/FinkFramework_Data/DataTables
            string sourceRoot = DataPipelinePath.ExcelRoot;
            
            // 先确认 Excel 源目录存在；旧输出文件由数据处理流程统一管理。
            if (!Directory.Exists(sourceRoot))
            {
                LogUtil.Error("DataExportTool", $"源目录不存在: {sourceRoot}");
                return (0,0);
            }
            // 搜索所有允许导出的文件
            var validFiles = Directory
                .EnumerateFiles(sourceRoot, "*.xlsx", SearchOption.AllDirectories)
                .OrderBy(PathUtil.NormalizePath, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (validFiles.Length == 0)
            {
                LogUtil.Warn("DataExportTool", $"没有找到任何可存储的文件: {sourceRoot}");
                return (0,0);
            }
            // 成功完成导出的文件数量。
            int successCount = 0;
            foreach (var excelPath in validFiles)
            {
                if (ExportData(excelPath, sourceRoot))
                    successCount++;
            }

            if (successCount != validFiles.Length)
            {
                LogUtil.Error("DataExportTool", "存在导出失败的数据表，不生成运行时数据清单。");
                return (successCount, validFiles.Length);
            }

            // Android 无法通过 Directory 遍历 APK 内的 StreamingAssets，
            // 因此导表完成后生成运行时数据清单。
            if (!DataManifestTool.Generate())
            {
                LogUtil.Error("DataExportTool", "运行时数据清单生成失败，导出流程终止。");
                return (0, validFiles.Length);
            }

            // 刷新资源数据库
            AssetDatabase.Refresh();
            if (!silent)
                LogUtil.Success("DataExportTool", $"全部数据导出完成 ({successCount}/{validFiles.Length})");
            DataFilesUtil.ClearCache();
            return (successCount,validFiles.Length);
        }
        #endregion
        
        #region === 核心导出逻辑 ===
        /// <summary>
        /// 读取Excel数据并导出加密数据存储
        /// </summary>
        /// <param name="excelPath">表格文件所在路径</param>
        /// <param name="sourceRoot">数据文件根目录</param>
        private static bool ExportData(string excelPath, string sourceRoot)
        {
            bool hasError = false;
            string tableName = Path.GetFileNameWithoutExtension(excelPath);

             try
            {
                // ========== 1. 查找类型 ==========
                Type dataType = DataUtil.FindType(TextsUtil.ToPascalCase(tableName));
                Type containerType = DataUtil.FindType(TextsUtil.ToPascalCase(tableName + "Container"));

                if (dataType == null || containerType == null)
                {
                    LogUtil.Warn("DataExportTool", $"[{tableName}] 数据结构类尚未生成。请先执行【数据工具面板 → 仅生成数据文件】再导出。");
                    return false;
                }

                // ========== 2. 读取 Excel ==========
                var listType = typeof(List<>).MakeGenericType(dataType);
                var listInstance = Activator.CreateInstance(listType);
                var addMethod = listType.GetMethod("Add");

                using var stream = File.Open(excelPath, FileMode.Open, FileAccess.Read);
                using var reader = ExcelReaderFactory.CreateReader(stream);
                // 读取表格前三行数据信息
                var header = ExcelReaderTool.ReadHeader(reader);
                var fieldNames = header.fieldNames;
                var fieldTypes = header.fieldTypes;

                var check = ExcelReaderTool.CheckFieldDefinitions(
                    tableName, fieldNames, fieldTypes
                );
                var skipColumn = check.skipColumn;

                // === 数据行 ===
                int rowIndex = 4;
                while (reader.Read())
                {
                    bool isEmpty = true;
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        var v = reader.GetValue(i);
                        if (v != null && !string.IsNullOrWhiteSpace(v.ToString()))
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

                    var rowObj = Activator.CreateInstance(dataType);

                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        // 跳过未生成类型的列
                        if (skipColumn[i]) continue;
                        
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
                                LogUtil.Error("DataParseTool", err);
                        }

                        if (pr.warnings.Count > 0)
                        {
                            foreach (var w in pr.warnings)
                                LogUtil.Warn("DataParseTool", w);
                        }
                        
                        try
                        {
                            dataType.GetField(fieldNames[i])?.SetValue(rowObj, pr.value);
                        }
                        catch (Exception e)
                        {
                            hasError = true;
                            LogUtil.Error("DataExportTool", $"{tableName}.{fieldNames[i]} 赋值失败 ← 值='{pr.value}'：{e.Message}");
                        }
                    }
                    addMethod?.Invoke(listInstance, new[] { rowObj });
                    rowIndex++;
                }

                // 解析失败时不写出不完整的数据，避免生成看似成功但内容损坏的文件。
                if (hasError)
                    return false;

                // ========== 3. 创建容器 ==========
                var container = Activator.CreateInstance(containerType);
                containerType.GetField("items")?.SetValue(container, listInstance);
                
                // ========== 4. 永远导出 JSON ==========
                string relativePath = PathUtil.NormalizePath(Path.GetRelativePath(sourceRoot, excelPath));
                string jsonPath = DataFilesUtil.BuildFullPath(DataPipelinePath.JsonRoot, relativePath, ".json");
                if (!JsonExportTool.TryExportJson(container, jsonPath))
                    return false;
                
                // ========== 5. 处理二进制数据的输出 ==========
                if (GlobalSettingsRuntimeLoader.Current.CurrentDataLoadMode == EnvironmentState.DataLoadMode.Binary)
                {
                    string targetRoot = DataPipelinePath.BinaryRoot;
                    // 使用 streamingAssetsPath 作为 root（Binary 模式）
                    string binaryFullPath = DataFilesUtil.BuildFullPath(targetRoot, relativePath, GlobalSettingsRuntimeLoader.Current.EncryptedExtension);
                    if (!BinaryExportTool.TryExportBinary(container, binaryFullPath))
                        return false;
                }
                
                return !hasError;
            }
            catch (Exception ex)
            {
                LogUtil.Error("DataExportTool", $"导出失败：{excelPath} → {ex.Message}");
                return false;
            }
        }
        #endregion
    }
}
