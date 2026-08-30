using System;
using System.IO;
using UnityEngine;
using FinkFramework.Runtime.Environments;
using FinkFramework.Runtime.Settings.Loaders;
using FinkFramework.Runtime.Utils;

namespace FinkFramework.Runtime.Data
{
    /// <summary>
    /// 数据管线的统一路径访问工具。
    /// C# 生成路径根据 GlobalSettings（EnvironmentState）自动判断使用内部 / 外部模式。
    ///
    /// 路径约定：
    /// - Excel 源数据：       项目根目录/FinkFramework_Data/DataTables
    /// - C# 内部默认路径：    Assets/Scripts/Data/AutoGen/DataClass
    /// - C# 外部默认路径：    项目根目录/FinkFramework_Data/AutoGen/DataClass
    /// - JSON 编辑器快照：    项目根目录/FinkFramework_Data/AutoExport/DataJson
    /// - JSON 运行时文件：    Assets/StreamingAssets/FinkFramework_Data/DataJson
    /// - Binary 运行时文件：  Assets/StreamingAssets/FinkFramework_Data/DataBinary
    ///
    /// Json 模式直接使用 StreamingAssets 下的 JSON；Binary 模式会额外生成 Binary 文件。
    /// </summary>
    public static class DataPipelinePath
    {
        private const string DefaultInternalCSharpOutputPath = "Assets/Scripts/Data/AutoGen/DataClass";
        private const string DefaultExternalCSharpOutputPath = "FinkFramework_Data/AutoGen/DataClass";
        private static string _lastInvalidCSharpConfiguration;

        // 兼容旧 API：外部 C# 根目录。最终输出请使用 CSharpRoot。
        public static readonly string ExternalCSharpRoot = Path.Combine(ProjectRoot, "FinkFramework_Data/AutoGen");
        
        // 外部数据根目录（JSON / Binary 路径）
        public static readonly string ExternalAutoExportRoot = Path.Combine(ProjectRoot, "FinkFramework_Data/AutoExport");

        // 兼容旧 API：内部 C# 根目录。最终输出请使用 CSharpRoot。
        public static readonly string InternalCSharpRoot = Path.Combine(Application.dataPath, "Scripts/Data/AutoGen");
        
        // 内部数据根目录（JSON / Binary 路径）
        public static readonly string InternalStreamingRoot = Path.Combine(Application.streamingAssetsPath, "FinkFramework_Data");

        // 工程根目录（项目路径）
        public static string ProjectRoot
        {
            get
            {
                string path = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                PathUtil.EnsureDirectory(path);
                return PathUtil.NormalizePath(path);
            }
        }
        
        /// <summary>
        /// Excel 根目录（始终固定外部）
        /// </summary>
        public static string ExcelRoot
        {
            get
            {
                string path = Path.Combine(ProjectRoot, "FinkFramework_Data/DataTables");
                PathUtil.EnsureDirectory(path);
                return PathUtil.NormalizePath(path);
            }
        }

        /// <summary>
        /// 获取 C# 数据类默认输出目录（项目相对路径）。
        /// </summary>
        /// <param name="isInternal">true 表示 Assets 内部路径，false 表示项目外部路径。</param>
        public static string GetDefaultCSharpOutputPath(bool isInternal)
        {
            return isInternal
                ? DefaultInternalCSharpOutputPath
                : DefaultExternalCSharpOutputPath;
        }

        /// <summary>
        /// 获取当前模式下 C# 数据类的最终绝对输出目录，并确保目录存在。
        /// </summary>
        public static string CSharpRoot
        {
            get
            {
                var settings = GlobalSettingsRuntimeLoader.Current;
                bool useExternal = settings.CSharpPathMode == EnvironmentState.CSharpOutputPathMode.External;
                bool useCustom = useExternal
                    ? settings.UseCustomExternalCSharpOutputPath
                    : settings.UseCustomInternalCSharpOutputPath;
                string configuredPath = null;
                if (useCustom)
                {
                    configuredPath = useExternal
                        ? settings.ExternalCSharpOutputPath
                        : settings.InternalCSharpOutputPath;
                }
                string defaultPath = GetDefaultCSharpOutputPath(!useExternal);

                return ResolveCSharpOutputPath(configuredPath, defaultPath, !useExternal);
            }
        }

        /// <summary>
        /// 校验 C# 输出路径。路径必须是项目相对路径；内部路径必须位于 Assets 内部。
        /// </summary>
        /// <param name="configuredPath">待校验的项目相对路径。</param>
        /// <param name="isInternal">true 表示校验内部路径，false 表示校验外部路径。</param>
        /// <param name="error">校验失败时返回原因。</param>
        public static bool TryValidateCSharpOutputPath(
            string configuredPath,
            bool isInternal,
            out string error)
        {
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(configuredPath))
            {
                error = "输出路径不能为空。";
                return false;
            }

            // PathUtil 会将普通字符串的前导 '/' 视为多余字符；这里必须先检查原始输入，
            // 否则类似 /outside 的绝对路径可能在规范化后被误当成项目相对路径。
            string rawPath = configuredPath.Trim().Replace('\\', '/');
            if (Path.IsPathRooted(rawPath) || rawPath.StartsWith("/", StringComparison.Ordinal))
            {
                error = "请使用项目相对路径，不要填写绝对路径。";
                return false;
            }

            string normalizedPath = PathUtil.NormalizePath(rawPath);

            if (string.IsNullOrEmpty(normalizedPath))
            {
                error = "输出路径不能为空。";
                return false;
            }

            if (isInternal && string.Equals(
                    normalizedPath.TrimEnd('/'),
                    "Assets",
                    StringComparison.OrdinalIgnoreCase))
            {
                error = "内部 C# 输出路径请至少填写 Assets 后面的一个子目录。";
                return false;
            }

            string projectRoot = Path.GetFullPath(ProjectRoot);
            string candidate = Path.GetFullPath(Path.Combine(
                projectRoot,
                normalizedPath.Replace('/', Path.DirectorySeparatorChar)));

            if (!IsSameOrInside(projectRoot, candidate))
            {
                error = "输出路径必须位于当前 Unity 项目目录内。";
                return false;
            }

            bool insideAssets = IsSameOrInside(Application.dataPath, candidate);
            if (isInternal && !insideAssets)
            {
                error = "内部 C# 输出路径必须位于 Assets 目录内，否则 Unity 不会自动编译生成的代码。";
                return false;
            }

            if (!isInternal && insideAssets)
            {
                error = "外部 C# 输出路径不能位于 Assets 目录内，请使用项目根目录下的其他目录。";
                return false;
            }

            return true;
        }

        private static string ResolveCSharpOutputPath(
            string configuredPath,
            string defaultPath,
            bool isInternal)
        {
            string outputPath = string.IsNullOrWhiteSpace(configuredPath)
                ? defaultPath
                : configuredPath;

            if (!TryValidateCSharpOutputPath(outputPath, isInternal, out string error))
            {
                string invalidConfiguration = $"{isInternal}:{outputPath}:{error}";
                if (!string.Equals(_lastInvalidCSharpConfiguration, invalidConfiguration, StringComparison.Ordinal))
                {
                    _lastInvalidCSharpConfiguration = invalidConfiguration;
                    LogUtil.Error("DataPipelinePath", $"C# 输出路径无效：{error} 将回退到默认路径：{defaultPath}");
                }
                outputPath = defaultPath;
            }
            else
            {
                _lastInvalidCSharpConfiguration = string.Empty;
            }

            string absolutePath = Path.Combine(
                ProjectRoot,
                PathUtil.NormalizePath(outputPath).Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(absolutePath);
            return PathUtil.NormalizePath(absolutePath);
        }

        private static bool IsSameOrInside(string root, string candidate)
        {
            string normalizedRoot = Path.GetFullPath(root)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string normalizedCandidate = Path.GetFullPath(candidate);

            return normalizedCandidate.Equals(normalizedRoot, StringComparison.OrdinalIgnoreCase) ||
                   normalizedCandidate.StartsWith(
                       normalizedRoot + Path.DirectorySeparatorChar,
                       StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// JSON 输出目录（自动根据运行时模式决定 Internal / External）
        /// </summary>
        public static string JsonRoot
        {
            get
            {
                bool useInternal = GlobalSettingsRuntimeLoader.Current.CurrentDataLoadMode == EnvironmentState.DataLoadMode.Json;

                string baseRoot = useInternal
                    ? InternalStreamingRoot                   // 运行时读取 → 必须内部
                    : ExternalAutoExportRoot;                 // Binary 模式 → 永远外部 JSON

                string path = Path.Combine(baseRoot, "DataJson");
                PathUtil.EnsureDirectory(path);
                return PathUtil.NormalizePath(path);
            }
        }
        
        /// <summary>
        /// 二进制数据输出目录（仅 Binary 模式使用）
        /// </summary>
        public static string BinaryRoot
        {
            get
            {
                // JSON 模式完全不需要二进制路径，也不应生成目录
                if (GlobalSettingsRuntimeLoader.Current.CurrentDataLoadMode == EnvironmentState.DataLoadMode.Json)
                {
                    return string.Empty;
                }

                // Binary 模式才会开启二进制路径
                string baseRoot = InternalStreamingRoot;

                string path = Path.Combine(baseRoot, "DataBinary");
                PathUtil.EnsureDirectory(path);
                return PathUtil.NormalizePath(path);
            }
        }
    }
}
