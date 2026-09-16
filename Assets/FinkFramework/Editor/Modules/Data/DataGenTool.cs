#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FinkFramework.Runtime.Data;
using FinkFramework.Runtime.Environments;
using FinkFramework.Runtime.Settings.Loaders;
using FinkFramework.Runtime.Utils;
using UnityEditor;
using UnityEngine;

namespace FinkFramework.Editor.Modules.Data
{
    /// <summary>
    /// 数据自动生成工具。
    /// 用于从 <c>项目根目录/FinkFramework_Data/DataTables</c> 目录下的 Excel 文件自动生成对应的 C# 数据类定义文件。
    /// 功能说明：
    /// 1. 递归扫描所有 Excel 文件；
    /// 2. 自动生成类文件并保存至当前配置的 C# 输出目录；
    /// 3. 自动刷新 Unity 资源数据库；
    /// 注意事项：
    /// - 目标路径中的同名类会在每次生成时更新；旧路径清理不会删除手动修改过的文件。
    /// </summary>
    public static class DataGenTool
    {
        #region 字段定义

        private static string SOURCE_DIR => DataPipelinePath.ExcelRoot;
        private static string CLASS_ROOT => DataPipelinePath.CSharpRoot;
        private static readonly string[] ALLOWED_EXTS = { ".xlsx" };
        private static int totalCount;
        private static int successCount;
        
        public struct ExcelMeta
        {
            public string ClassName;
            public string ExcelPath;

            public string[] FieldNames;
            public string[] FieldTypes;
            public string[] FieldDescs;

            public string? Template;
        }

        #endregion

        #region 主入口

        /// <summary>
        /// 确认当前 C# 输出路径是否可以使用。
        /// 一键处理流程会在清理导出数据前调用，避免用户取消时先破坏现有输出。
        /// </summary>
        public static bool EnsureOutputPathReady()
        {
            return DataGenerationManifestTool.EnsureOutputPathConfirmed(CLASS_ROOT);
        }

        /// <summary>
        /// 数据生成工具主入口
        /// 遍历所有表格 并分别执行 自动生成数据类 数据容器类 Json文件 
        /// </summary>
        public static (int success, int total) GenerateAllData(bool silent = false)
        {
            if (!Directory.Exists(SOURCE_DIR))
            {
                LogUtil.Error("DataGenTool", $"数据源目录不存在：{SOURCE_DIR}");
                return (0, 0);
            }

            // ---------- 搜索所有表格 ----------
            var excelFiles = Directory
                .EnumerateFiles(SOURCE_DIR, "*.*", SearchOption.AllDirectories)
                .Where(f => ALLOWED_EXTS.Contains(
                    Path.GetExtension(f),
                    StringComparer.OrdinalIgnoreCase))
                .OrderBy(PathUtil.NormalizePath, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (excelFiles.Length == 0)
            {
                LogUtil.Warn("DataGenTool", "没有找到任何 xlsx 文件。");
                return (0, 0);
            }

            string classRoot = CLASS_ROOT;
            if (!DataGenerationManifestTool.EnsureOutputPathConfirmed(classRoot))
                return (0, excelFiles.Length);

            bool hasPreviousManifest = DataGenerationManifestTool.TryLoad(out DataGenerationManifest previousManifest);

            successCount = 0;
            totalCount = excelFiles.Length;
            var generatedFiles = new List<string>();

            // ---------- 生成并覆盖 ----------
            foreach (var excelPath in excelFiles)
            {
                try
                {
                    GenerateDataFile(excelPath);
                    generatedFiles.AddRange(GetGeneratedFilePaths(excelPath));
                    successCount++;
                }
                catch (Exception ex)
                {
                    LogUtil.Error("DataGenTool", $"生成失败：{Path.GetFileName(excelPath)}\n{ex.Message}");
                }
            }

            if (successCount != totalCount)
                return (successCount, totalCount);

            if (!DataGenerationManifestTool.TryWrite(classRoot, generatedFiles, out string manifestError))
            {
                LogUtil.Error("DataGenTool", $"C# 生成清单写入失败，已停止后续导出：{manifestError}");
                return (0, totalCount);
            }

            // 新代码和清单都成功后，再清理旧输出目录或当前目录中的安全文件。
            if (hasPreviousManifest)
                DataGenerationManifestTool.CleanupPreviousOutput(previousManifest, classRoot, generatedFiles);

            bool isInternalOutput =
                GlobalSettingsRuntimeLoader.Current.CSharpPathMode == EnvironmentState.CSharpOutputPathMode.Internal;

            // 外部目录不受 Unity 资源数据库管理，无需刷新；内部目录需要刷新以触发脚本编译。
            if (isInternalOutput)
                AssetDatabase.Refresh();

            
            if (!silent)
            {
                if (isInternalOutput)
                {
                    // 内部输出 → 写 EditorPrefs 用于编译后再打印
                    string summary1 = $"部分数据生成失败！状态: {successCount}/{totalCount}";
                    string summary2 = $"数据文件生成完成！状态: {successCount}/{totalCount}";
                    EditorPrefs.SetString("Fink_LastGenResult", successCount != totalCount ? summary1 : summary2);
                }
                else
                {
                    // 外部输出 → 不走 EditorPrefs，直接打印
                    if (successCount != totalCount)
                        LogUtil.Error("DataGenTool", $"部分数据生成失败！状态: {successCount}/{totalCount}");
                    else
                        LogUtil.Success("DataGenTool", $"数据文件生成完成！状态: {successCount}/{totalCount}");
                }
            }

            return (successCount, totalCount);
        }
        
        #endregion

        #region 单表生成函数
        
        /// <summary>
        /// 从单个 xlsx 文件生成 C# 数据类 数据容器类 和 Json文件
        /// </summary>
        private static void GenerateDataFile(string excelPath)
        {
            
            // STEP 1：解析表头
            ExcelMeta meta = ParseExcelMeta(excelPath);

            // STEP 2：生成 C# 数据类
            GenerateCSharpClass(meta);

            // STEP 3：生成容器类
            GenerateContainerClass(meta);
        }
        
        #endregion
        
        #region Step 1：解析 Excel 表头

        private static ExcelMeta ParseExcelMeta(string excelPath)
        {
              // ---------- 1. 获取类名 ----------
            string fileName = Path.GetFileNameWithoutExtension(excelPath);
            string className = TextsUtil.ToPascalCase(fileName);
            
            // ---------- 2. 读取模板 ----------
            var templateAsset = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/FinkFramework/Editor/EditorResources/Data/template_data.txt");
            if (!templateAsset)
                throw new FileNotFoundException("未找到模板文件：Assets/FinkFramework/Editor/EditorResources/Data/template_data.txt");
            string? template = templateAsset?.text;

            // ---------- 3. 打开 Excel 文件 ----------
            using var stream = File.Open(excelPath, FileMode.Open, FileAccess.Read);
            using var reader = ExcelDataReader.ExcelReaderFactory.CreateReader(stream);

            // ---------- 4.  读取表格前三行 ----------
            var header = ExcelReaderTool.ReadHeader(reader);

            string[] fieldNames = header.fieldNames;
            string[] fieldTypes = header.fieldTypes;
            string[] fieldDescs = header.fieldDescs;
            
            // ---------- 5. 输出到目标路径 ----------
            GetClassOutputDir(excelPath);
            ExcelMeta meta = new ExcelMeta
            {
                ExcelPath = excelPath,
                ClassName = className,
                FieldNames = fieldNames,
                FieldTypes = fieldTypes,
                FieldDescs = fieldDescs,
                Template = template
            };
            return meta;
        }

        #endregion
        
        #region Step 2：生成 C# 类

        private static void GenerateCSharpClass(ExcelMeta meta)
        {
            string className    = meta.ClassName;
            string excelPath    = meta.ExcelPath;
            string[] fieldNames = meta.FieldNames;
            string[] fieldTypes = meta.FieldTypes;
            string[] fieldDescs = meta.FieldDescs;
            string? template     = meta.Template;
            
            // ---------- 拼接字段字符串 ----------
            StringBuilder fieldBuilder = new();
            for (int i = 0; i < fieldNames.Length; i++)
            {
                if (string.IsNullOrEmpty(fieldNames[i])) continue;
                string desc = string.IsNullOrEmpty(fieldDescs[i]) ? fieldNames[i] : fieldDescs[i];
                fieldBuilder.AppendLine($"        /// <summary>{desc}</summary>");
                fieldBuilder.AppendLine($"        public {fieldTypes[i]} {fieldNames[i]};");
                fieldBuilder.AppendLine();
            }
            // ---------- 命名空间 ----------
            // 计算相对于 CLASS_ROOT 的路径（从 DataClass 后开始）
            string relativePath = Path.GetRelativePath(CLASS_ROOT, GetClassOutputDir(excelPath));
            // 替换为命名空间格式
            string namespaceSuffix;
            if (relativePath == "." || string.IsNullOrEmpty(relativePath))
            {
                namespaceSuffix = "Data.AutoGen.DataClass";
            }
            else
            {
                namespaceSuffix = "Data.AutoGen.DataClass." + relativePath.Replace(Path.DirectorySeparatorChar, '.');
            }
            string usings = CollectRequiredUsings(fieldTypes);
            // ---------- 替换模板变量 ----------
            string? code = template?.Replace("{Usings}", usings)
                .Replace("{DateTime}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
                .Replace("{SourceFile}", Path.GetFileName(excelPath))
                .Replace("{Namespace}", namespaceSuffix)
                .Replace("{ClassName}", className)
                .Replace("{Fields}", fieldBuilder.ToString().TrimEnd());

            // ---------- 输出到目标路径 ----------
            string classOutputDir = GetClassOutputDir(excelPath);
            Directory.CreateDirectory(classOutputDir); 
            string outputPath = Path.Combine(classOutputDir, $"{className}.cs");
            code = DataGenerationManifestTool.AddGeneratedFileMarker(
                TextsUtil.NormalizeLineEndings(code ?? string.Empty));
            File.WriteAllText(outputPath, code, Encoding.UTF8);
        }

        #endregion
        
        #region Step 3：生成 Container 类

        private static void GenerateContainerClass(ExcelMeta meta)
        {
            var templateAsset = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/FinkFramework/Editor/EditorResources/Data/template_container.txt");
            if (!templateAsset)
                throw new FileNotFoundException("未找到模板文件：Assets/FinkFramework/Editor/EditorResources/Data/template_container.txt");
            string className  = meta.ClassName;
            string excelPath  = meta.ExcelPath;
            
            // ---------- 1. 计算命名空间 ----------
            string relativePath = Path.GetRelativePath(CLASS_ROOT, GetClassOutputDir(excelPath));

            string namespaceSuffix;
            if (relativePath == "." || string.IsNullOrEmpty(relativePath))
            {
                namespaceSuffix = "Data.AutoGen.DataClass";
            }
            else
            {
                namespaceSuffix = "Data.AutoGen.DataClass." + relativePath.Replace(Path.DirectorySeparatorChar, '.');
            }
            
            // ---------- 2. 输出路径 ----------
            string classOutputDir = GetClassOutputDir(excelPath);
            Directory.CreateDirectory(classOutputDir); 
            
            // ---------- 3. 替换模板变量 ----------
            string? containerCode = templateAsset?.text
                .Replace("{DateTime}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
                .Replace("{SourceFile}", Path.GetFileName(excelPath))
                .Replace("{Namespace}", namespaceSuffix)
                .Replace("{ClassName}", className);

            // ---------- 4. 写入文件 ----------
            string containerPath = Path.Combine(classOutputDir, $"{className}Container.cs");
            containerCode = DataGenerationManifestTool.AddGeneratedFileMarker(
                TextsUtil.NormalizeLineEndings(containerCode ?? string.Empty));
            File.WriteAllText(containerPath, containerCode, Encoding.UTF8);
        }

        #endregion

        #region 自动收集命名空间

        /// <summary>
        /// 基于变量类型判断是否需要添加引用命名空间
        /// </summary>
        /// <param name="fieldTypes">字段类型</param>
        /// <returns></returns>
        private static string CollectRequiredUsings(string[] fieldTypes)
        {
            var namespaces = new HashSet<string> { "System" };

            foreach (var typeName in fieldTypes)
            {
                if (string.IsNullOrEmpty(typeName))
                    continue;

                // 解析真实类型（处理 List<T> / Dictionary<K,V> / T[]）
                CollectNamespaceRecursive(typeName, namespaces);
            }

            StringBuilder sb = new StringBuilder();
            foreach (var ns in namespaces.OrderBy(n => n))
            {
                sb.AppendLine($"using {ns};");
            }
            sb.AppendLine();
            return sb.ToString();
        }

        private static void CollectNamespaceRecursive(string typeName, HashSet<string> namespaces)
        {
            while (true)
            {
                // 拆泛型 List<T>, Dictionary<K,V>
                if (typeName.StartsWith("List<") || typeName.StartsWith("Dictionary<"))
                {
                    var inner = DataUtil.SplitGenericArgs(typeName.Substring(typeName.IndexOf('<') + 1).TrimEnd('>'));

                    foreach (var var in inner) CollectNamespaceRecursive(var.Trim(), namespaces);

                    namespaces.Add("System.Collections.Generic");
                    return;
                }

                // 数组
                if (typeName.EndsWith("[]"))
                {
                    typeName = typeName.Substring(0, typeName.Length - 2);
                    continue;
                }

                // 基础类型 → 不需要 using
                if (IsPrimitive(typeName)) return;

                // 解析真实 Type
                Type t = DataUtil.FindType(typeName);
                if (t == null) return;

                if (!string.IsNullOrEmpty(t.Namespace)) namespaces.Add(t.Namespace);
                break;
            }
        }

        private static bool IsPrimitive(string type)
        {
            return type switch
            {
                "int" or "float" or "double" or "bool" or "string" or "long" or "short" or "byte" or "decimal" or "char" or "DateTime" => true,
                _ => false
            };
        }

        #endregion

        #region 工具方法
        
        [InitializeOnLoadMethod]
        private static void DataGenToolLogger()
        {
            string msg = EditorPrefs.GetString("Fink_LastGenResult", "");
            if (!string.IsNullOrEmpty(msg) && !EditorApplication.isCompiling)
            {
                EditorPrefs.DeleteKey("Fink_LastGenResult");
                if (msg.Contains("失败"))
                {
                    LogUtil.Error("DataGenTool", msg);
                }
                else
                {
                    LogUtil.Success("DataGenTool", msg);
                }
            }
        }
        
        /// <summary>
        /// 获取数据代码输出路径
        /// </summary>
        private static string GetClassOutputDir(string excelPath)
        {
            return Path.Combine(CLASS_ROOT, GetRelativePath(excelPath));
        }

        private static IEnumerable<string> GetGeneratedFilePaths(string excelPath)
        {
            string outputDirectory = GetClassOutputDir(excelPath);
            string className = TextsUtil.ToPascalCase(Path.GetFileNameWithoutExtension(excelPath));

            yield return Path.Combine(outputDirectory, $"{className}.cs");
            yield return Path.Combine(outputDirectory, $"{className}Container.cs");
        }
        
        /// <summary>
        /// 获取相对路径（作为子目录）
        /// </summary>
        private static string GetRelativePath(string excelPath)
        {
            string relative = Path.GetRelativePath(SOURCE_DIR, excelPath);
            string? dir = Path.GetDirectoryName(relative);

            // Excel 直接放在根目录：返回 ""，否则返回子目录
            return dir?.Replace("\\", "/") ?? "";
        }
        
        #endregion

    }
}
