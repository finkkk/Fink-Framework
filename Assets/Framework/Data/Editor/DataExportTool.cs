using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ExcelDataReader;
using Framework.Data.Runtime;
using Framework.Utils;
using UnityEditor;
using UnityEngine;

namespace Framework.Data.Editor
{
    /// <summary>
    /// 数据导出工具（DataExportTool）
    /// ------------------------------------------------------------
    /// 功能：
    /// 1. 扫描 项目根目录/DataTables 下的全部 Excel；
    /// 2. 将表格内容读取为容器对象；
    /// 3. 使用 Odin + AES 生成加密数据文件；
    /// ------------------------------------------------------------
    /// 默认导出至 StreamingAssets/Data
    /// 可通过 DataHandleTool 一键执行。
    /// </summary>
    public class DataExportTool
    {
        #region === 主流程 ===
        /// <summary>
        /// 一键加密全部数据并生成文件
        /// </summary>
        public static (int success, int total) EncryptAllData(bool silent = false)
        {
            // 原始数据源目录：项目根目录/DataTables
            string sourceRoot = Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..")), "DataTables");
            // 加密数据输出目录：StreamingAssets/Data
            string targetRoot = Path.Combine(Application.streamingAssetsPath, "Data");
            
            // 清理旧加密目录 只清理 StreamingAssets 下旧加密文件
            if (!Directory.Exists(sourceRoot))
            {
                LogUtil.Error("DataExportTool", $"源目录不存在: {sourceRoot}");
                return (0,0);
            }
            FilesUtil.EnsureDirectory(targetRoot);
            // 搜索所有允许导出的文件
            var validFiles = Directory
                .EnumerateFiles(sourceRoot, "*.xlsx", SearchOption.AllDirectories)
                .ToArray();

            if (validFiles.Length == 0)
            {
                LogUtil.Warn("DataExportTool", $"没有找到任何可存储的文件: {sourceRoot}");
                return (0,0);
            }
            // 成功执行加密存储的文件数量
            int successCount = 0;
            foreach (var excelPath in validFiles)
            {
                if (ExportExcel(excelPath, sourceRoot, targetRoot))
                    successCount++;
            }
            // 刷新资源数据库
            AssetDatabase.Refresh();
            if (!silent)
                LogUtil.Success("DataExportTool", $"全部数据导出完成 ({successCount}/{validFiles.Length})");
            FilesUtil.ClearCache();
            return (successCount,validFiles.Length);
        }
        
        /// <summary>
        /// 清空加密存储的全部数据（仅清空 Data 文件夹）
        /// </summary>
        public static void ClearEncryptData()
        {
            string persistentPath = Path.Combine(Application.persistentDataPath, "Data");
            string streamingPath = Path.Combine(Application.streamingAssetsPath, "Data");
            try
            {
                void SafeDelete(string path)
                {
                    if (Directory.Exists(path))
                    {
                        Directory.Delete(path, true);
                        Directory.CreateDirectory(path);
                        LogUtil.Info("DataExportTool", $"已清空目录: {path}");
                    }
                    else
                    {
                        Directory.CreateDirectory(path);
                        LogUtil.Warn("DataExportTool", $"未发现目录，已自动创建: {path}");
                    }
                }
                SafeDelete(persistentPath);
                #if UNITY_EDITOR
                // 仅在编辑器环境清空 StreamingAssets
                SafeDelete(streamingPath);
                #endif
                LogUtil.Success("DataExportTool", "已清空所有加密数据目录！");
                AssetDatabase.Refresh();
            }
            catch (Exception ex)
            {
                LogUtil.Error("DataExportTool", $"清空加密数据失败: {ex.Message}");
            }
        }
        #endregion
        
        #region === 核心导出逻辑 ===
        /// <summary>
        /// 读取Excel数据并加密存储
        /// </summary>
        /// <param name="excelPath">表格文件所在路径</param>
        /// <param name="sourceRoot">数据文件根目录</param>
        /// <param name="targetRoot">目标根目录</param>
        private static bool ExportExcel(string excelPath, string sourceRoot, string targetRoot)
        {
            bool hasError = false;
            string tableName = Path.GetFileNameWithoutExtension(excelPath);
            // 获取相对路径
            string relativePath = FilesUtil.NormalizePath(Path.GetRelativePath(sourceRoot, excelPath));
            // 绝对输出路径
            string targetPath = FilesUtil.BuildFullPath(targetRoot, relativePath,true);

             try
            {
                // 查找数据类 / 容器类
                Type dataType = DataUtil.FindType(TextsUtil.ToPascalCase(tableName));
                Type containerType = DataUtil.FindType(TextsUtil.ToPascalCase(tableName + "Container"));

                if (dataType == null || containerType == null)
                {
                    LogUtil.Warn("DataExportTool",
                        $"[{tableName}] 数据结构类尚未生成。请先执行【数据工具面板 → 仅生成数据文件】再导出。");
                    return false;
                }

                // === 创建表数据列表 ===
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
                    tableName,
                    header.fieldNames,
                    header.fieldTypes
                );

                var skipColumn = check.skipColumn;
                hasError |= check.hasError;

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

                // === 创建容器 ===
                var container = Activator.CreateInstance(containerType);
                containerType.GetField("items")?.SetValue(container, listInstance);

                FilesUtil.EnsureDirectory(targetPath);

                if (File.Exists(targetPath))
                    File.Delete(targetPath);

                // === Odin + AES 保存 ===
                DataUtil.Save(targetPath, container, encrypt: true);

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