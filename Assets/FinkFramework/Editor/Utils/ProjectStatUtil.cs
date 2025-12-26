#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FinkFramework.Runtime.Utils;
using UnityEditor;
using UnityEngine;

namespace FinkFramework.Editor.Utils
{
    /// <summary>
    /// 项目统计工具（Editor-only）
    /// </summary>
    public static class ProjectStatUtil
    {
        public class StatOptions
        {
            #region ===== 项目统计选项 =====

            /// <summary>
            /// 是否启用代码统计（C# / Shader 行数）
            /// </summary>
            public bool countCode;

            /// <summary>
            /// 是否仅统计指定脚本目录（而非整个 Assets）
            /// </summary>
            public bool onlyTargetScriptFolder;

            /// <summary>
            /// 指定脚本统计目录（相对于 Assets，例如 "Scripts"）
            /// </summary>
            public string scriptFolderPath;

            /// <summary>
            /// 是否统计 Shader 行数
            /// </summary>
            public bool countShader;

            /// <summary>
            /// 是否统计材质资源
            /// </summary>
            public bool countMaterial;

            /// <summary>
            /// 是否统计模型资源（fbx / obj / glb）
            /// </summary>
            public bool countModel;

            /// <summary>
            /// 是否统计音频资源
            /// </summary>
            public bool countAudio;

            /// <summary>
            /// 是否统计 Prefab 资源
            /// </summary>
            public bool countPrefab;

            /// <summary>
            /// 是否统计场景资源
            /// </summary>
            public bool countScene;

            /// <summary>
            /// 是否统计 Addressables 资源组
            /// </summary>
            public bool countAddressables;

            /// <summary>
            /// 是否统计 AssetBundle 资源
            /// </summary>
            public bool countAssetBundle;

            /// <summary>
            /// 是否统计图片 / 纹理资源
            /// </summary>
            public bool countTexture;

            #endregion

            #region ===== 数据归档（总开关） =====

            /// <summary>
            /// 是否启用数据归档功能
            /// 启用后才允许执行统计数据导出 / 源码导出
            /// </summary>
            public bool enableArchive;

            #endregion

            #region ===== 统计数据归档 =====

            /// <summary>
            /// 是否导出项目统计文本
            /// </summary>
            public bool exportStatReport;

            /// <summary>
            /// 统计数据导出目录（绝对路径）
            /// 导出文件名将自动按时间生成
            /// 例如：ProjectStat_yyyyMMdd_HHmmss.txt
            /// </summary>
            public string statExportDir;

            #endregion
            
            #region ===== 源码归档导出 =====

            /// <summary>
            /// 是否导出项目源码文本
            /// </summary>
            public bool exportSourceCode;

            /// <summary>
            /// 参与源码导出的文件夹路径列表（相对于 Assets）
            /// 例如："Scripts"、"FinkFramework"
            /// </summary>
            public List<string> sourceCodeFolders = new();

            /// <summary>
            /// 是否包含 Editor 目录下的代码
            /// </summary>
            public bool includeEditor;

            /// <summary>
            /// 是否在每个源码块前写入文件路径说明
            /// </summary>
            public bool addFilePathHeader;

            /// <summary>
            /// 源码文本导出目录（绝对路径）
            /// 导出文件名将自动按时间生成
            /// 例如：SourceCode_yyyyMMdd_HHmmss.txt
            /// </summary>
            public string sourceExportDir;

            #endregion
        }


        #region 数据统计部分
        
        public static string GenerateReport(StatOptions options)
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("========== 项目统计数据 ==========\n");
            sb.AppendLine($"[统计时间]\n{System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();
            
            // 一次性枚举
            var allFiles = Directory.GetFiles(
                Application.dataPath,
                "*.*",
                SearchOption.AllDirectories
            );

            if (options.countCode)
                AppendCodeStats(sb, options, allFiles);

            AppendAssetStats(sb, options,allFiles);

            sb.AppendLine("==============================");
            return sb.ToString();
        }

        private static void AppendCodeStats(StringBuilder sb, StatOptions options, string[] allFiles)
        {
            if (!options.countCode)
                return;

            sb.AppendLine("[代码统计]");

            string codeRoot = options.onlyTargetScriptFolder
                ? Path.Combine(Application.dataPath, options.scriptFolderPath)
                : Application.dataPath;

            int csLines = 0;
            int shaderLines = 0;

            foreach (var file in allFiles)
            {
                var normalizedFile = PathUtil.NormalizePath(file);
                var normalizedRoot = PathUtil.NormalizePath(codeRoot);

                if (!normalizedFile.StartsWith(normalizedRoot))
                    continue;

                if (file.EndsWith(".cs"))
                    csLines += File.ReadLines(file).Count();
                else if (options.countShader && file.EndsWith(".shader"))
                    shaderLines += File.ReadLines(file).Count();
            }

            sb.AppendLine($"  C# 行数：{csLines}");

            if (options.countShader)
                sb.AppendLine($"  Shader 行数：{shaderLines}");

            sb.AppendLine();
        }

        private static void AppendAssetStats(StringBuilder sb, StatOptions options, string[] allFiles)
        {
            bool hasAssetContent =
                options.countMaterial ||
                options.countModel ||
                options.countAudio ||
                options.countPrefab ||
                options.countScene ||
                options.countAddressables ||
                options.countAssetBundle ||
                options.countTexture ;

            if (!hasAssetContent)
                return;

            sb.AppendLine("[资产统计]");

            if (options.countMaterial)
                sb.AppendLine($"  材质：{allFiles.Count(f => f.EndsWith(".mat"))}");

            if (options.countModel)
                sb.AppendLine($"  模型：{allFiles.Count(f => f.EndsWith(".fbx") || f.EndsWith(".obj") || f.EndsWith(".glb"))}");

            if (options.countAudio)
                sb.AppendLine($"  音频：{allFiles.Count(f => f.EndsWith(".wav") || f.EndsWith(".mp3") || f.EndsWith(".ogg"))}");

            if (options.countPrefab)
                sb.AppendLine($"  Prefab：{allFiles.Count(f => f.EndsWith(".prefab"))}");

            if (options.countScene)
                sb.AppendLine($"  场景：{allFiles.Count(f => f.EndsWith(".unity"))}");
            
            if (options.countTexture) 
                sb.AppendLine($"  图片/纹理：{allFiles.Count(f => f.EndsWith(".png") || f.EndsWith(".jpg") || f.EndsWith(".jpeg") || f.EndsWith(".tga") || f.EndsWith(".psd") || f.EndsWith(".exr") || f.EndsWith(".hdr"))}");
            
            if (options.countAddressables)
            {
                string[] guids = AssetDatabase.FindAssets("t:AddressableAssetGroup");
                sb.AppendLine($"  Addressables 组：{guids.Length}");
            }

            if (options.countAssetBundle)
            {
                int abCount = allFiles.Count(f =>
                {
                    string assetPath = "Assets" + PathUtil.NormalizePath(f).Replace(PathUtil.NormalizePath(Application.dataPath), "");
                    var importer = AssetImporter.GetAtPath(assetPath);
                    return importer && !string.IsNullOrEmpty(importer.assetBundleName);
                });

                sb.AppendLine($"  AssetBundle 资源：{abCount}");
            }

            sb.AppendLine();
        }
        
        #endregion
        
        #region 源码导出部分

        public static void ExportCode(StatOptions options, string outputPath)
        {
            // 总开关 + 子模块开关
            if (!options.enableArchive || !options.exportSourceCode)
                return;

            if (options.sourceCodeFolders == null || options.sourceCodeFolders.Count == 0)
            {
                return;
            }

            var csFiles = CollectCsFiles(options);

            if (csFiles.Count == 0)
            {
                LogUtil.Warn("ProjectStatUtil", "已跳过执行源码归档导出：未发现符合条件的 C# 文件。");
                return;
            }

            ExportAsPlainText(csFiles, options, outputPath);
        }
        
        private static List<string> CollectCsFiles(StatOptions options)
        {
            var allCs = Directory.GetFiles(
                Application.dataPath,
                "*.cs",
                SearchOption.AllDirectories
            );

            var result = new List<string>();

            foreach (var file in allCs)
            {
                var normalized = PathUtil.NormalizePath(file);

                if (!IsInSourceFolders(normalized, options.sourceCodeFolders))
                    continue;

                if (!options.includeEditor && normalized.Contains("/Editor/"))
                    continue;

                result.Add(file);
            }

            return result;
        }
        
        private static bool IsInSourceFolders(string file, List<string> folders)
        {
            foreach (var folder in folders)
            {
                var full = PathUtil.NormalizePath(
                    Path.Combine(Application.dataPath, folder)
                );

                if (file.StartsWith(full + "/") || file == full)
                    return true;
            }

            return false;
        }
        
        private static void ExportAsPlainText(List<string> files, StatOptions options, string outputPath)
        {
            var sb = new StringBuilder();
            files.Sort(System.StringComparer.Ordinal);
            foreach (var file in files)
            {
                if (options.addFilePathHeader)
                {
                    var assetPath = "Assets" + file.Replace(Application.dataPath, "").Replace("\\", "/");
                    sb.AppendLine("====================================");
                    sb.AppendLine($"文件: {assetPath}");
                    sb.AppendLine("====================================");
                    sb.AppendLine();
                }

                sb.AppendLine(File.ReadAllText(file));
                sb.AppendLine();
                sb.AppendLine();
            }
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? string.Empty);
            File.WriteAllText(outputPath, sb.ToString(), new UTF8Encoding(false));
        }
        
        #endregion

        #region 归档编排部分

        public static void Archive(StatOptions options)
        {
            if (!options.enableArchive)
                return;

            string timeStamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");

            // ===== 统计数据归档 =====
            if (options.exportStatReport)
            {
                if (string.IsNullOrEmpty(options.statExportDir))
                {
                    LogUtil.Warn("ProjectStatUtil", "已跳过统计数据归档：未指定统计导出目录。");
                }
                else
                {
                    string statPath = Path.Combine(options.statExportDir, $"ProjectStat_{timeStamp}.txt");

                    Directory.CreateDirectory(
                        Path.GetDirectoryName(statPath) ?? string.Empty
                    );

                    File.WriteAllText(statPath, GenerateReport(options), new UTF8Encoding(false));
                }
            }

            // ===== 源码归档 =====
            if (options.exportSourceCode)
            {
                if (string.IsNullOrEmpty(options.sourceExportDir))
                {
                    LogUtil.Warn("ProjectStatUtil", "已跳过源码归档：未指定源码导出目录。");
                }
                else
                {
                    string sourcePath = Path.Combine(
                        options.sourceExportDir,
                        $"SourceCode_{timeStamp}.txt"
                    );

                    ExportCode(options, sourcePath);
                }
            }
            
            AssetDatabase.Refresh();
        }

        #endregion
    }
}
#endif
