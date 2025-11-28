#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FinkFramework.Data.Runtime;
using FinkFramework.Utils;
using UnityEditor;
using UnityEngine;

namespace FinkFramework.Data.Editor
{
    /// <summary>
    /// 数据自动生成工具
    /// 用于从 <c>项目根目录/DataTables</c> 目录下的 Excel 文件自动生成对应的 C# 数据类定义文件。
    /// 功能说明：
    /// 1. 递归扫描所有 Excel 文件；
    /// 3. 自动生成类文件并保存至 <c>Assets/Scripts/Data/AutoGen</c>；
    /// 4. 自动刷新 Unity 资源数据库；
    /// 注意事项：
    /// - 不会覆盖非自动生成的文件；
    /// </summary>
    public static class DataGenTool
    {
        #region 字段定义

        private static readonly string SOURCE_DIR = Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..")), "DataTables");
        private static readonly string OUTPUT_DIR = Path.Combine(Application.dataPath, "Scripts/Data/AutoGen");
        private static readonly string CLASS_ROOT = Path.Combine(OUTPUT_DIR, "DataClass");
        private static readonly string JSON_ROOT  = Path.Combine(OUTPUT_DIR, "DataJson");
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

            public string Template;
        }

        #endregion

        #region 主入口

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
            FilesUtil.EnsureDirectory(OUTPUT_DIR);

            // ---------- 搜索所有表格 ----------
            var excelFiles = Directory
                .EnumerateFiles(SOURCE_DIR, "*.*", SearchOption.AllDirectories)
                .Where(f => ALLOWED_EXTS.Contains(Path.GetExtension(f).ToLower()))
                .ToArray();

            if (excelFiles.Length == 0)
            {
                LogUtil.Warn("DataGenTool", "没有找到任何 xlsx 文件。");
                return (0, 0);
            }

            // ---------- 记录所有应存在的新类名 ----------
            HashSet<string> expectedClassFiles = new();
            foreach (var excelPath in excelFiles)
            {
                string name = Path.GetFileNameWithoutExtension(excelPath);
                string className = TextsUtil.ToPascalCase(name);
                expectedClassFiles.Add($"{className}.cs");
                expectedClassFiles.Add($"{className}Container.cs");
            }
            
            // 生成前仅清空一次 JSON 根目录
            if (Directory.Exists(JSON_ROOT))
                Directory.Delete(JSON_ROOT, true);
            Directory.CreateDirectory(JSON_ROOT);

            successCount = 0;
            totalCount = excelFiles.Length;

            // ---------- 生成并覆盖 ----------
            foreach (var excelPath in excelFiles)
            {
                try
                {
                    GenerateDataFile(excelPath);
                    successCount++;
                }
                catch (Exception ex)
                {
                    LogUtil.Error("DataGenTool", $"生成失败：{Path.GetFileName(excelPath)}\n{ex.Message}");
                }
            }

            // ---------- 清理已失效的旧文件 ----------
            var allFiles = Directory.GetFiles(OUTPUT_DIR, "*.cs", SearchOption.AllDirectories);
            int removedCount = 0;
            foreach (var file in allFiles)
            {
                string fileName = Path.GetFileName(file);
                if (!expectedClassFiles.Contains(fileName))
                {
                    File.Delete(file);
                    removedCount++;
                }
            }
            if (removedCount > 0)
                LogUtil.Info("DataGenTool", $"已清理无效旧文件：{removedCount} 个");

            // ---------- 刷新资源 ----------
            AssetDatabase.Refresh();

            if (!silent)
            {
                string summary1 = $"部分数据生成失败！状态: {successCount}/{totalCount}";
                string summary2 = $"数据文件生成完成！状态: {successCount}/{totalCount}";
                EditorPrefs.SetString("Fink_LastGenResult", successCount != totalCount ? summary1 : summary2);
            }

            return (successCount, totalCount);
        }
        
        /// <summary>
        /// 防止因为编译导致打印代码生成完成的信息丢失
        /// </summary>
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
                LogUtil.Success("DataGenTool", msg);
            }
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

            // STEP 4：生成 JSON 模板
            GenerateJsonTemplate(meta);  
        }
        
        #endregion
        
        #region Step 1：解析 Excel 表头

        private static ExcelMeta ParseExcelMeta(string excelPath)
        {
              // ---------- 1. 获取类名 ----------
            string fileName = Path.GetFileNameWithoutExtension(excelPath);
            string className = TextsUtil.ToPascalCase(fileName);
            
            // ---------- 2. 读取模板 ----------
            var templateAsset = Resources.Load<TextAsset>("Data/template_data");
            if (!templateAsset)
            {
                LogUtil.Error("DataGenTool", "未找到模板文件：Resources/Data/template_data.txt");
            }
            string template = templateAsset.text;

            // ---------- 3. 打开 Excel 文件 ----------
            using var stream = File.Open(excelPath, FileMode.Open, FileAccess.Read);
            using var reader = ExcelDataReader.ExcelReaderFactory.CreateReader(stream);

            // ---------- 4.  读取表格前三行 ----------
            var header = ExcelReaderTool.ReadHeader(reader);

            string[] fieldNames = header.fieldNames;
            string[] fieldTypes = header.fieldTypes;
            string[] fieldDescs = header.fieldDescs;
            
            // ---------- 5. 输出到目标路径 ----------
            string jsonOutputDir = GetJsonOutputDir(excelPath);
            string classOutputDir = GetClassOutputDir(excelPath);
            // 确保两个输出目录都存在
            Directory.CreateDirectory(classOutputDir);
            Directory.CreateDirectory(jsonOutputDir);
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
            string template     = meta.Template;
            
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
            string code = template
                .Replace("{Usings}", usings)
                .Replace("{DateTime}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
                .Replace("{SourceFile}", Path.GetFileName(excelPath))
                .Replace("{Namespace}", namespaceSuffix)
                .Replace("{ClassName}", className)
                .Replace("{Fields}", fieldBuilder.ToString().TrimEnd());

            // ---------- 输出到目标路径 ----------
            string classOutputDir = GetClassOutputDir(excelPath);
            string outputPath = Path.Combine(classOutputDir, $"{className}.cs");
            File.WriteAllText(outputPath, code, Encoding.UTF8);
        }

        #endregion
        
        #region Step 3：生成 Container 类

        private static void GenerateContainerClass(ExcelMeta meta)
        {
            var templateAsset = Resources.Load<TextAsset>("Data/template_container");
            if (!templateAsset)
            {
                LogUtil.Error("DataGenTool", "未找到模板文件：Resources/Data/template_container.txt");
                return;
            }
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
            
            // ---------- 3. 替换模板变量 ----------
            string containerCode = templateAsset.text
                .Replace("{DateTime}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
                .Replace("{SourceFile}", Path.GetFileName(excelPath))
                .Replace("{Namespace}", namespaceSuffix)
                .Replace("{ClassName}", className);

            // ---------- 4. 写入文件 ----------
            string containerPath = Path.Combine(classOutputDir, $"{className}Container.cs");
            File.WriteAllText(containerPath, containerCode, Encoding.UTF8);
        }

        #endregion
        
        #region Step 4：Json 模板生成

        /// <summary>
        /// 生成对应 JSON 模板文件（用于示例）
        /// </summary>
        private static void GenerateJsonTemplate(ExcelMeta meta)
        {
            try
            {
                // ---------- 1. 创建 JSON 目录 ----------
                string className   = meta.ClassName;
                string excelPath   = meta.ExcelPath;
                // 1. 获取目录
                string jsonDir = GetJsonOutputDir(excelPath);
                // 2. 建目录（不会删 root，不会相互覆盖）
                Directory.CreateDirectory(jsonDir);
                // 3. 生成 xxx.json（确保是文件，不是文件夹）
                string jsonPath = Path.Combine(jsonDir, $"{className}.json");
                
                string[] fieldNames = meta.FieldNames;
                string[] fieldTypes = meta.FieldTypes;
                
                // ---------- 2. 构造 JSON 数据 ----------
                var jsonObj = new Dictionary<string, object>();
                for (int i = 0; i < fieldNames.Length; i++)
                {
                    if (string.IsNullOrEmpty(fieldNames[i])) continue;
                    Type? t = DataUtil.FindType(fieldTypes[i]);
                    jsonObj[fieldNames[i]] = BuildJsonSkeleton(t);
                }
                var wrapper = new { items = new[] { jsonObj } };
                string json = Newtonsoft.Json.JsonConvert.SerializeObject(wrapper, Newtonsoft.Json.Formatting.Indented);
                
                // ---------- 3. 写入 JSON 文件 ----------
       
                File.WriteAllText(jsonPath, json, Encoding.UTF8);
                LogUtil.Info("DataGenTool", $"已生成 JSON 模板：{jsonPath}");
            }
            catch (Exception ex)
            {
                LogUtil.Warn("DataGenTool", $"生成 JSON 模板失败：{ex.Message}");
            }
        }
        
        private static object BuildJsonSkeleton(Type? t, string fieldName = "")
        {
            if (t == null)
            {
                LogUtil.Error("DataGenTool", $"字段 '{fieldName}' 类型不存在，无法生成 JSON 模板");
                return null!;
            }
            
            // === 1. 基础类型 ===
            if (t == typeof(int)) return 0;
            if (t == typeof(float)) return 0f;
            if (t == typeof(double)) return 0.0;
            if (t == typeof(long)) return 0L;
            if (t == typeof(bool)) return false;
            if (t == typeof(string)) return "";
            if (t == typeof(short)) return (short)0;
            if (t == typeof(ushort)) return (ushort)0;
            if (t == typeof(byte)) return (byte)0;
            if (t == typeof(sbyte)) return (sbyte)0;
            if (t == typeof(decimal)) return 0m;
            if (t == typeof(char)) return "";
            if (t == typeof(DateTime)) return "2000-01-01";
            
            // === 2. Unity 结构体 ===
            if (t == typeof(Vector2)) return new { x = 0, y = 0 };
            if (t == typeof(Vector3)) return new { x = 0, y = 0, z = 0 };
            if (t == typeof(Vector4)) return new { x = 0, y = 0, z = 0, w = 0 };
            if (t == typeof(Color)) return new { r = 1, g = 1, b = 1, a = 1 };
            if (t == typeof(Matrix4x4))
            {
                return new
                {
                    m00 = 1, m01 = 0, m02 = 0, m03 = 0,
                    m10 = 0, m11 = 1, m12 = 0, m13 = 0,
                    m20 = 0, m21 = 0, m22 = 1, m23 = 0,
                    m30 = 0, m31 = 0, m32 = 0, m33 = 1
                };
            }

            // === 3. 数组 ===
            if (t is { IsArray: true })
            {
                Type? elemType = t.GetElementType();
                return new[] { BuildJsonSkeleton(elemType, fieldName) };
            }

            // === 4. List<T> ===
            if (t is { IsGenericType: true } && t.GetGenericTypeDefinition() == typeof(List<>))
            {
                Type elemType = t.GetGenericArguments()[0];
                return new[] { BuildJsonSkeleton(elemType, fieldName) };
            }

            // === 5. Dictionary<K,V> ===
            if (t is { IsGenericType: true } && t.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                Type keyT = t.GetGenericArguments()[0];
                Type valT = t.GetGenericArguments()[1];
                string sampleKey =
                    keyT == typeof(string) ? "Key" :
                    keyT == typeof(int) ? "1" :
                    keyT == typeof(long) ? "1" :
                    keyT == typeof(short) ? "1" :
                    keyT == typeof(ushort) ? "1" :
                    keyT == typeof(byte) ? "1" :
                    keyT == typeof(sbyte) ? "1" :
                    keyT.IsEnum ? Enum.GetNames(keyT).First() :
                    "SampleKey";
                return new Dictionary<string, object>
                {
                    [sampleKey] = BuildJsonSkeleton(valT, sampleKey)
                };
            }

            // === 6. 自定义类（用反射递归字段） ===
            if (t.IsClass || t is { IsValueType: true, IsPrimitive: false })
            {
                var obj = new Dictionary<string, object?>();
                foreach (var f in t.GetFields())
                    obj[f.Name] = BuildJsonSkeleton(f.FieldType, f.Name);
                return obj;
            }

            // === 7. 兜底 ===
            LogUtil.Warn("DataGenTool", $"未知类型 '{t.FullName}'，生成空结构");
            return null!;
        }

        #endregion

        #region 工具方法

        /// <summary>
        /// 基于变量类型判断是否需要添加引用命名空间
        /// </summary>
        /// <param name="fieldTypes">字段类型</param>
        /// <returns></returns>
        private static string CollectRequiredUsings(string[] fieldTypes)
        {
            bool needUnity = false;
            bool needGeneric = false;

            foreach (var type in fieldTypes)
            {
                if (string.IsNullOrEmpty(type)) continue;

                if (type.Contains("Vector", StringComparison.OrdinalIgnoreCase) ||
                    type.Equals("Color", StringComparison.OrdinalIgnoreCase))
                    needUnity = true;

                if (type.Contains("Dictionary") || type.Contains("List") || type.EndsWith("[]"))
                    needGeneric = true;
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("using System;");
            if (needGeneric) sb.AppendLine("using System.Collections.Generic;");
            if (needUnity) sb.AppendLine("using UnityEngine;");
            sb.AppendLine(); // 空一行
            return sb.ToString();
        }
        
        /// <summary>
        /// 获取数据代码输出路径
        /// </summary>
        private static string GetClassOutputDir(string excelPath)
        {
            return Path.Combine(CLASS_ROOT, GetRelativePath(excelPath));
        }

        /// <summary>
        /// 获取Json文件输出路径
        /// </summary>
        private static string GetJsonOutputDir(string excelPath)
        {
            // 返回最终 json 应该写入的目标文件夹
            string relative = GetRelativePath(excelPath);
            return Path.Combine(JSON_ROOT, relative);
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