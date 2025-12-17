using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FinkFramework.Runtime.Environments;
using FinkFramework.Runtime.Settings;
using FinkFramework.Runtime.Settings.Loaders;
using FinkFramework.Runtime.Utils;
using UnityEngine;

namespace FinkFramework.Runtime.Data
{
    /// <summary>
    /// 文件工具类
    /// ------------------------------------------------------------
    /// - LoadDefault：只读默认数据（配置、模板等），随安装包发布；
    /// - LoadLocal：  可读可写（存档、设置等），玩家持久化数据；
    /// - 默认自动加密;可不传路径，自动根据类名匹配对应文件;调试时可传 decrypt: false 读取明文。
    /// </summary>
    public static class FilesUtil
    {
        #region 数据 读写
        /// <summary>
        /// 读取默认数据（只读 只从 StreamingAssets 读取）
        /// 可不传路径，会自动根据类名匹配文件。
        /// </summary>
        public static T LoadDefaultData<T>(string relativePath = null)
        {
            try
            {
                // 自动查找路径
                if (string.IsNullOrEmpty(relativePath))
                    relativePath = FindRelativePath<T>();
                // 拼接完整 StreamingAssets 路径（自动选择 .json / .fink）
                string fullPath = BuildFullPath(Application.streamingAssetsPath, relativePath, true);
                if (!File.Exists(fullPath))
                {
                    LogUtil.Warn("FilesUtil", $"默认数据文件不存在：{fullPath}");
                    return default;
                }
                // 读取（DataUtil 会根据扩展名自动选择 JSON 或 Binary 序列化方式）
                T data = DataUtil.Load<T>(fullPath);
                return data;
            }
            catch (Exception ex)
            {
                LogUtil.Error("FilesUtil", $"读取默认数据失败：{relativePath} → {ex.Message}");
                return default;
            }
        }

        /// <summary>
        /// 读取本地数据（可读可写 适合存档等）
        /// 若本地文件不存在，则从 StreamingAssets 拷贝默认文件。
        /// </summary>
        public static T LoadLocalData<T>(string relativePath = null)
        {
            try
            {
                // 自动查找
                if (string.IsNullOrEmpty(relativePath))
                    relativePath = FindRelativePath<T>(); // 优先查 PersistentDataPath

                if (string.IsNullOrEmpty(relativePath))
                    return default;
                // 如果本地不存在 → 自动复制默认文件（根据 JSON/Binary 模式）
                EnsureLocalFileExists(relativePath);
                // 拼接 PersistentDataPath 完整路径（自动补 .json 或 .fink 后缀）
                string fullPath = BuildFullPath(Application.persistentDataPath, relativePath,true);

                if (!File.Exists(fullPath))
                {
                    LogUtil.Warn("FilesUtil", $"本地数据文件不存在：{fullPath}");
                    return default;
                }
                // 自动根据扩展名加载 JSON 或 Binary
                T data = DataUtil.Load<T>(fullPath);
                return data;
            }
            catch (Exception ex)
            {
                LogUtil.Error("FilesUtil", $"读取本地数据失败：{relativePath} → {ex.Message}");
                return default;
            }
        }

        /// <summary>
        /// 覆盖保存本地数据（可读可写）
        /// 可不传路径，会自动匹配类型对应文件。
        /// </summary>
        public static void SaveLocalData<T>(T data,string relativePath = null)
        {
            try
            {
                // 自动匹配类型对应路径
                if (string.IsNullOrEmpty(relativePath))
                    relativePath = FindRelativePath<T>();

                if (string.IsNullOrEmpty(relativePath))
                {
                    LogUtil.Warn("FilesUtil", $"未找到类型 {typeof(T).Name} 对应的文件路径，保存失败。");
                    return;
                }
                // 构造最终保存路径（根据数据源模式自动附加 .json / .fink）
                string fullPath = BuildFullPath(Application.persistentDataPath, relativePath,true);
                // 创建目录
                PathUtil.EnsureDirectory(Path.GetDirectoryName(fullPath));
                // 保存数据（DataUtil.Save 内部根据扩展名选择 JSON 或 Binary）
                DataUtil.Save(fullPath, data);
                LogUtil.Success("FilesUtil", $"已保存本地数据（模式：{GlobalSettingsRuntimeLoader.Current.CurrentDataLoadMode}）：{relativePath}");
            }
            catch (Exception ex)
            {
                LogUtil.Error("FilesUtil", $"保存本地数据失败：{relativePath} → {ex.Message}");
            }
        }
        #endregion
        
        #region 路径工具
        
        /// <summary>
        /// 拼接创建完整路径（包括自定义后缀名）
        /// </summary>
        /// <param name="basePath">基础路径</param>
        /// <param name="relativePath">相对路径</param>
        /// <param name="isAddExtension">是否自动填充拓展名 默认为否</param>
        /// <returns></returns>
        public static string BuildFullPath(string basePath, string relativePath, bool isAddExtension = false)
        {
            string normalized = PathUtil.NormalizePath(relativePath);
            string withoutExt = Path.ChangeExtension(normalized, null);
            // 根据当前数据源模式返回扩展名
            string ext = GlobalSettingsRuntimeLoader.Current.CurrentDataLoadMode == EnvironmentState.DataLoadMode.Json ? ".json" : GlobalSettingsRuntimeLoader.Current.EncryptedExtension;
            return PathUtil.NormalizePath(Path.Combine(basePath, isAddExtension ? PathUtil.NormalizePath(withoutExt) + ext : PathUtil.NormalizePath(withoutExt)));
        }
        
        /// <summary>
        /// 如果 PersistentDataPath 下没有指定文件，则从 StreamingAssets 拷贝一份默认文件到 PersistentDataPath
        /// </summary>
        /// <param name="relativePath">相对路径</param>
        private static void EnsureLocalFileExists(string relativePath)
        {
            string persistentPath = BuildFullPath(Application.persistentDataPath, relativePath,true);
            if (!File.Exists(persistentPath))
            {
                string streamingPath = BuildFullPath(Application.streamingAssetsPath, relativePath,true);
                if (File.Exists(streamingPath))
                {
                    PathUtil.EnsureDirectory(persistentPath);
                    File.Copy(streamingPath, persistentPath);
                    LogUtil.Info("FilesUtil", $"已初始化文件：{persistentPath}");
                }
                else
                {
                    LogUtil.Warn("FilesUtil", $"初始化失败，StreamingAssets 下未找到默认文件：{streamingPath}");
                }
            }
        }

        #endregion

        #region 遍历查找文件
        
        // 缓存字典，避免反复扫描
        private static readonly Dictionary<Type, string> cache = new();

        /// <summary>
        /// 根据类型自动匹配相对路径（不带扩展名）
        /// </summary>
        /// <typeparam name="T">目标数据类型，如 PlayerDataContainer</typeparam>
        /// <returns>返回相对路径（如 Data/Player/PlayerData），找不到返回 null</returns>
        public static string FindRelativePath<T>()
        {
            Type type = typeof(T);
            // 缓存命中
            if (cache.TryGetValue(type, out string cached))
                return cached;

            string className = type.Name;
            string searchName = className.EndsWith("Container")
                ? className[..^9]
                : className;
            
            // 根据数据源模式决定扩展名
            string ext = GlobalSettingsRuntimeLoader.Current.CurrentDataLoadMode == EnvironmentState.DataLoadMode.Json
                ? ".json"
                : GlobalSettingsRuntimeLoader.Current.EncryptedExtension;
            
            // 根据模式决定搜索根目录
            // Binary → StreamingAssets/DataBinary/
            // JSON → StreamingAssets/DataJson/
            string folder = GlobalSettingsRuntimeLoader.Current.CurrentDataLoadMode == EnvironmentState.DataLoadMode.Json
                ? "FinkFramework_Data/DataJson"
                : "FinkFramework_Data/DataBinary";
            string searchRoot = PathUtil.NormalizePath(Path.Combine(Application.streamingAssetsPath, folder));
            if (!Directory.Exists(searchRoot))
            {
                LogUtil.Warn($"搜索目录不存在：{searchRoot}");
                return null;
            }
            // 搜索所有符合扩展名的文件
            var files = Directory.GetFiles(searchRoot, "*" + ext, SearchOption.AllDirectories);

            var match = files.FirstOrDefault(f =>
                Path.GetFileNameWithoutExtension(f)
                    .Equals(searchName, StringComparison.OrdinalIgnoreCase));

            if (match == null)
            {
                LogUtil.Warn($"未找到与类型 {className} 匹配的文件（扩展名：{ext}）。");
                return null;
            }

            // ----------- 计算相对路径（以 StreamingAssets 为锚点） -----------
            string normalized = PathUtil.NormalizePath(match);
            string root = PathUtil.NormalizePath(Application.streamingAssetsPath);

            if (!normalized.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                LogUtil.Warn($"文件路径不在 StreamingAssets 下：{normalized}");
                return null;
            }

            // 去掉 StreamingAssets/
            string relativeToStreaming = normalized[(root.Length + 1)..];

            // 再去掉扩展名
            string relNoExt = Path.ChangeExtension(relativeToStreaming, null);

            // 缓存
            cache[type] = relNoExt;
            
            LogUtil.Info($"[{className}] 匹配到文件：{relNoExt}");
            return relNoExt;
        }

        /// <summary>
        /// 清除路径缓存（用于重新生成数据后）
        /// </summary>
        public static void ClearCache()
        {
            cache.Clear();
        }
 
        #endregion
    }
}