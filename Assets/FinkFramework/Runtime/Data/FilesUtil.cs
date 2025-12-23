using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FinkFramework.Runtime.Environments;
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
                string fullPath = BuildFullPath(Application.streamingAssetsPath, relativePath, GlobalSettingsRuntimeLoader.Current.CurrentDataLoadMode == EnvironmentState.DataLoadMode.Json
                    ? ".json"
                    : GlobalSettingsRuntimeLoader.Current.EncryptedExtension);
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
                string fullPath = BuildFullPath(Application.persistentDataPath, relativePath,GlobalSettingsRuntimeLoader.Current.CurrentDataLoadMode == EnvironmentState.DataLoadMode.Json
                    ? ".json"
                    : GlobalSettingsRuntimeLoader.Current.EncryptedExtension);

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
                string fullPath = BuildFullPath(Application.persistentDataPath, relativePath,GlobalSettingsRuntimeLoader.Current.CurrentDataLoadMode == EnvironmentState.DataLoadMode.Json
                    ? ".json"
                    : GlobalSettingsRuntimeLoader.Current.EncryptedExtension);
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
        public static string BuildFullPath(string basePath, string relativePath, string extension = null)
        {
            string normalized = PathUtil.NormalizePath(relativePath);
            string withoutExt = Path.ChangeExtension(normalized, null);
            string finalPath = extension != null ? withoutExt + extension : withoutExt;
            return PathUtil.NormalizePath(Path.Combine(basePath, finalPath));
            
        }
        
        /// <summary>
        /// 如果 PersistentDataPath 下没有指定文件，则从 StreamingAssets 拷贝一份默认文件到 PersistentDataPath
        /// </summary>
        /// <param name="relativePath">相对路径</param>
        private static void EnsureLocalFileExists(string relativePath)
        {
            string ext = GlobalSettingsRuntimeLoader.Current.CurrentDataLoadMode == EnvironmentState.DataLoadMode.Json
                ? ".json"
                : GlobalSettingsRuntimeLoader.Current.EncryptedExtension;

            string persistentPath = BuildFullPath(Application.persistentDataPath, relativePath, ext);
            string streamingPath  = BuildFullPath(Application.streamingAssetsPath, relativePath, ext);
            if (!File.Exists(persistentPath))
            {
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
            
            // StreamingAssets 根目录（只算一次）
            string streamingRoot = PathUtil.NormalizePath(Application.streamingAssetsPath);
            
            // 搜索目录：Binary 优先，Json 兜底
            string[] folders =
            {
                "FinkFramework_Data/DataBinary",
                "FinkFramework_Data/DataJson"
            };

            // 搜索扩展名：加密后缀优先，其次 json
            List<string> extList = new();

            if (!string.IsNullOrEmpty(GlobalSettingsRuntimeLoader.Current.EncryptedExtension))
                extList.Add(GlobalSettingsRuntimeLoader.Current.EncryptedExtension);

            extList.Add(".json");

            string[] exts = extList.ToArray();
            
            
            foreach (var folder in folders)
            {
                string root = PathUtil.NormalizePath(
                    Path.Combine(streamingRoot, folder)
                );

                if (!Directory.Exists(root))
                    continue;

                foreach (var ext in exts)
                {
                    var files = Directory.GetFiles(root, "*" + ext, SearchOption.AllDirectories);

                    var match = files.FirstOrDefault(f =>
                        Path.GetFileNameWithoutExtension(f)
                            .Equals(searchName, StringComparison.OrdinalIgnoreCase));

                    if (match == null)
                        continue;

                    // 计算相对于 StreamingAssets 的路径
                    string normalized = PathUtil.NormalizePath(match);

                    if (!normalized.StartsWith(streamingRoot, StringComparison.OrdinalIgnoreCase))
                        continue;

                    // 去掉 StreamingAssets/ 前缀
                    string relativeToStreaming = normalized[(streamingRoot.Length + 1)..];

                    // 去掉扩展名
                    string relNoExt = Path.ChangeExtension(relativeToStreaming, null);

                    // 写缓存
                    cache[type] = relNoExt;

                    return relNoExt;
                }
            }

            // 没找到
            return null;
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