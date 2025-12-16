using System.IO;
using UnityEngine;
using FinkFramework.Runtime.Environments;
using FinkFramework.Runtime.Settings;
using FinkFramework.Runtime.Utils;

namespace FinkFramework.Runtime.Data
{
    /// <summary>
    /// 数据管线的统一路径访问工具。
    /// 所有生成路径根据 GlobalSettings（EnvironmentState） 自动判断使用内部 / 外部模式。
    ///
    /// 读表路径：（始终不变）
    /// Excel 数据源 → FinkFramework_Data/DataTables 
    /// 
    /// 外部路径：
    ///   C#     → FinkFramework_Data/AutoGen//Data/DataClass/
    ///   JSON   → FinkFramework_Data/AutoExport/DataJson （Binary 模式才有此目录；Json 模式自动选择内部路径不导出此目录）
    ///   Binary → FinkFramework_Data/AutoExport/DataBinary
    /// 
    /// 内部路径：
    ///   C#     → Assets/Scripts/Data/AutoGen/DataClass
    ///   JSON   → Assets/StreamingAssets/FinkFramework_Data/DataJson   (只有Json 模式自动选择内部路径才有此目录)
    ///   Binary → Assets/StreamingAssets/FinkFramework_Data/DataBinary
    /// </summary>
    public static class DataPipelinePath
    {
        // 外部数据根目录（C#）
        public static readonly string ExternalCSharpRoot = Path.Combine(ProjectRoot, "FinkFramework_Data/AutoGen");
        
        // 外部数据根目录（JSON / Binary 路径）
        public static readonly string ExternalAutoExportRoot = Path.Combine(ProjectRoot, "FinkFramework_Data/AutoExport");

        // 内部数据根目录（C#）
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
        /// C# 数据类输出目录
        /// </summary>
        public static string CSharpRoot
        {
            get
            {
                string baseRoot = GlobalSettings.Current.CSharpUseExternal
                    ? ExternalCSharpRoot
                    : InternalCSharpRoot;

                string path = Path.Combine(baseRoot, "DataClass");
                PathUtil.EnsureDirectory(path);
                return PathUtil.NormalizePath(path);
            }
        }

        /// <summary>
        /// JSON 输出目录（自动根据运行时模式决定 Internal / External）
        /// </summary>
        public static string JsonRoot
        {
            get
            {
                bool useInternal = GlobalSettings.Current.CurrentDataLoadMode == EnvironmentState.DataLoadMode.Json;

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
                if (GlobalSettings.Current.CurrentDataLoadMode == EnvironmentState.DataLoadMode.Json)
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
