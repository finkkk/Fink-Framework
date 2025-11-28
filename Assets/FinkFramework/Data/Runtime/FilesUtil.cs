using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FinkFramework.Config;
using FinkFramework.Utils;
using UnityEngine;

namespace FinkFramework.Data.Runtime
{
    /// <summary>
    /// 文件工具类（基于 Odin + AES）
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
        public static T LoadDefaultData<T>(string relativePath = null, bool decrypt = true)
        {
            try
            {
                if (string.IsNullOrEmpty(relativePath))
                    relativePath = FindRelativePath<T>();

                if (string.IsNullOrEmpty(relativePath))
                    return default;
                
                relativePath = EnsureDataPrefix(relativePath);
                string fullPath = BuildFullPath(Application.streamingAssetsPath, relativePath,true);
                
                if (!File.Exists(fullPath))
                {
                    LogUtil.Warn("FilesUtil", $"默认数据文件不存在：{fullPath}");
                    return default;
                }

                T data = DataUtil.Load<T>(fullPath, decrypt);
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
        public static T LoadLocalData<T>(string relativePath = null, bool decrypt = true)
        {
            try
            {
                if (string.IsNullOrEmpty(relativePath))
                    relativePath = FindRelativePath<T>(); // 优先查 PersistentDataPath

                if (string.IsNullOrEmpty(relativePath))
                    return default;
                relativePath = EnsureDataPrefix(relativePath);
                EnsureLocalFileExists(relativePath);
                string fullPath = BuildFullPath(Application.persistentDataPath, relativePath,true);

                if (!File.Exists(fullPath))
                {
                    LogUtil.Warn("FilesUtil", $"本地数据文件不存在：{fullPath}");
                    return default;
                }

                T data = DataUtil.Load<T>(fullPath, decrypt);
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
        public static void SaveLocalData<T>(T data,string relativePath = null, bool encrypt = true)
        {
            try
            {
                if (string.IsNullOrEmpty(relativePath))
                    relativePath = FindRelativePath<T>();

                if (string.IsNullOrEmpty(relativePath))
                {
                    LogUtil.Warn("FilesUtil", $"未找到类型 {typeof(T).Name} 对应的文件路径，保存失败。");
                    return;
                }
                relativePath = EnsureDataPrefix(relativePath);
                string fullPath = BuildFullPath(Application.persistentDataPath, relativePath,true);
                EnsureDirectory(Path.GetDirectoryName(fullPath));
                DataUtil.Save(fullPath, data, encrypt);
                LogUtil.Success("FilesUtil", $"已保存本地数据：{relativePath}");
            }
            catch (Exception ex)
            {
                LogUtil.Error("FilesUtil", $"保存本地数据失败：{relativePath} → {ex.Message}");
            }
        }
        #endregion
        
        #region 路径工具
        
        /// <summary>
        /// 标准化路径字符串：
        /// 1. 统一分隔符为 '/';
        /// 2. 去除多余空格;
        /// 3. 保留绝对路径前缀（不误删）
        /// </summary>
        public static string NormalizePath(string path)
        {
            path = TextsUtil.NormalizePunctuation(path);
            if (string.IsNullOrEmpty(path)) return string.Empty;

            // 统一分隔符
            path = path.Trim().Replace('\\', '/');

            // 只有当不是完整绝对磁盘路径且以“/”开头的时候（即仅当有/开头的相对路径的时候），才去掉前导斜杠
            if (path.StartsWith("/") && !Path.IsPathRooted(path))
                path = path[1..];
            return path;
        }
        
        /// <summary>
        /// 确保路径以 "Data/" 为开头；
        /// 若未包含该前缀，则自动补全。
        /// </summary>
        private static string EnsureDataPrefix(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return "Data/Unknown";

            // 标准化路径符号
            relativePath = NormalizePath(relativePath).TrimStart('/');
    
            // 自动补全
            return relativePath.StartsWith("data/", StringComparison.OrdinalIgnoreCase)
                ? relativePath
                : "Data/" + relativePath;
        }
        
        /// <summary>
        /// 拼接创建完整路径（包括自定义后缀名）
        /// </summary>
        /// <param name="basePath">基础路径</param>
        /// <param name="relativePath">相对路径</param>
        /// <param name="isAddExtension">是否自动填充拓展名 默认为否</param>
        /// <returns></returns>
        public static string BuildFullPath(string basePath, string relativePath, bool isAddExtension = false)
        {
            string normalized = NormalizePath(relativePath);
            string withoutExt = Path.ChangeExtension(normalized, null);
            return NormalizePath(Path.Combine(basePath, isAddExtension ? (NormalizePath(withoutExt) + GlobalConfig.ENCRYPTED_FILE_EXTENSION) : NormalizePath(withoutExt)));
        }

        /// <summary>
        /// 确保路径文件夹存在
        /// </summary>
        /// <param name="path">路径</param>
        public static void EnsureDirectory(string path)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
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
                    EnsureDirectory(persistentPath);
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
        /// <returns>返回相对路径（如 Player/PlayerData），找不到返回 null</returns>
        public static string FindRelativePath<T>()
        {
            Type type = typeof(T);
            if (cache.TryGetValue(type, out string cached))
                return cached;

            string className = type.Name;
            string searchName = className.EndsWith("Container")
                ? className[..^9]
                : className;

            // 搜索根目录（persistentDataPath）
            string defaultRoot = Path.Combine(Application.streamingAssetsPath, "Data");
            string searchRoot = NormalizePath(defaultRoot);
            if (!Directory.Exists(searchRoot))
            {
                LogUtil.Warn($"搜索目录不存在：{searchRoot}");
                return null;
            }

            // 搜索所有 加密后缀 文件
            var files = Directory.GetFiles(searchRoot, "*" + GlobalConfig.ENCRYPTED_FILE_EXTENSION, SearchOption.AllDirectories);
            var match = files.FirstOrDefault(f =>
                Path.GetFileNameWithoutExtension(f)
                    .Equals(searchName, StringComparison.OrdinalIgnoreCase));

            if (match == null)
            {
                LogUtil.Warn($"未找到与类型 {className} 匹配的文件。");
                return null;
            }

            // ----------- 以 streamingAssetsPath 为锚点截取 -----------
            string normalized = NormalizePath(match);
            string root = NormalizePath(Application.streamingAssetsPath);

            if (!normalized.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                LogUtil.Warn($"文件路径不在 StreamingAssets 下：{normalized}");
                return null;
            }

            // 去掉 StreamingAssets/ 前缀
            string relativeToStreaming = normalized[(root.Length + 1)..];

            // 再去掉扩展名
            string relNoExt = Path.ChangeExtension(relativeToStreaming, null);

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
            LogUtil.Info("已清空自动路径缓存。");
        }
 
        #endregion
    }
}