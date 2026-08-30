using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using FinkFramework.Runtime.Environments;
using FinkFramework.Runtime.Settings.Loaders;
using FinkFramework.Runtime.Utils;
using UnityEngine;
using UnityEngine.Networking;

namespace FinkFramework.Runtime.Data
{
    /// <summary>
    /// 数据文件读写工具。
    ///
    /// 默认数据位于 StreamingAssets：
    /// - Editor / Standalone：通常是普通文件路径，可使用 System.IO；
    /// - Android / iOS：可能是 jar:file:// 或其他 URI，必须使用 UnityWebRequest 异步读取。
    ///
    /// 本地数据位于 persistentDataPath，始终使用 System.IO 读写。
    /// </summary>
    public static class DataFilesUtil
    {
        private const string ManifestRelativePath = "FinkFramework_Data/data-manifest.json";

        private static readonly Dictionary<Type, string> pathCache = new();
        private static DataManifest manifestCache;
        private static bool manifestLoadAttempted;

        #region 默认数据

        /// <summary>
        /// 同步读取默认数据。
        /// Android / iOS 的 StreamingAssets 是 URI，移动端请使用 LoadDefaultDataAsync。
        /// </summary>
        public static T LoadDefaultData<T>(string relativePath = null)
        {
            if (IsStreamingAssetsUri)
            {
                LogUtil.Warn("DataFilesUtil", "移动端 StreamingAssets 需要异步读取，请使用 LoadDefaultDataAsync。");
                return default;
            }

            try
            {
                relativePath ??= FindRelativePath<T>();
                if (string.IsNullOrEmpty(relativePath))
                    return default;

                string extension = GetRuntimeExtension();
                string fullPath = BuildFullPath(Application.streamingAssetsPath, relativePath, extension);
                if (!File.Exists(fullPath))
                {
                    LogUtil.Warn("DataFilesUtil", $"默认数据文件不存在：{fullPath}");
                    return default;
                }

                return DataUtil.Load<T>(fullPath);
            }
            catch (Exception ex)
            {
                LogUtil.Error("DataFilesUtil", $"读取默认数据失败：{relativePath} → {ex.Message}");
                return default;
            }
        }

        /// <summary>
        /// 跨平台异步读取默认数据，支持 Android / iOS StreamingAssets URI。
        /// </summary>
        public static async UniTask<T> LoadDefaultDataAsync<T>(
            string relativePath = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                DataManifestEntry manifestEntry = null;
                if (string.IsNullOrEmpty(relativePath))
                {
                    // 编辑器/桌面保留目录扫描回退；只有 URI 型 StreamingAssets 才依赖 manifest。
                    manifestEntry = IsStreamingAssetsUri
                        ? await FindManifestEntryAsync<T>(cancellationToken)
                        : null;
                    relativePath = manifestEntry?.relativePath;
                    relativePath ??= FindRelativePath<T>();
                }

                if (string.IsNullOrEmpty(relativePath))
                    return default;

                // 使用清单中的实际扩展名，避免运行时设置与已打包数据不一致时读错文件。
                string extension = manifestEntry?.extension ?? GetRuntimeExtension();
                string relativeFile = BuildRelativeFilePath(relativePath, extension);
                byte[] bytes = await ReadStreamingBytesAsync(relativeFile, cancellationToken);
                return DataUtil.LoadFromBytes<T>(bytes, extension);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                LogUtil.Error("DataFilesUtil", $"异步读取默认数据失败：{relativePath} → {ex.Message}");
                return default;
            }
        }

        #endregion

        #region 本地数据

        /// <summary>
        /// 同步读取本地数据。
        /// 移动端首次从 StreamingAssets 初始化时请使用 LoadLocalDataAsync。
        /// </summary>
        public static T LoadLocalData<T>(string relativePath = null)
        {
            if (IsStreamingAssetsUri && string.IsNullOrEmpty(relativePath))
            {
                LogUtil.Warn("DataFilesUtil", "移动端无法同步扫描 StreamingAssets，请使用 LoadLocalDataAsync。");
                return default;
            }

            try
            {
                relativePath ??= FindRelativePath<T>();
                if (string.IsNullOrEmpty(relativePath))
                    return default;

                EnsureLocalFileExists(relativePath);
                string extension = GetRuntimeExtension();
                string fullPath = BuildFullPath(Application.persistentDataPath, relativePath, extension);
                if (!File.Exists(fullPath))
                {
                    LogUtil.Warn("DataFilesUtil", $"本地数据文件不存在：{fullPath}");
                    return default;
                }

                return DataUtil.Load<T>(fullPath);
            }
            catch (Exception ex)
            {
                LogUtil.Error("DataFilesUtil", $"读取本地数据失败：{relativePath} → {ex.Message}");
                return default;
            }
        }

        /// <summary>
        /// 跨平台异步读取本地数据。
        /// 如果 persistentDataPath 中不存在，会通过跨平台读取器初始化默认文件。
        /// </summary>
        public static async UniTask<T> LoadLocalDataAsync<T>(
            string relativePath = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                relativePath ??= await FindRelativePathAsync<T>(cancellationToken);
                if (string.IsNullOrEmpty(relativePath))
                    return default;

                string extension = GetRuntimeExtension();
                string fullPath = BuildFullPath(Application.persistentDataPath, relativePath, extension);
                await EnsureLocalFileExistsAsync(relativePath, extension, cancellationToken);

                if (!File.Exists(fullPath))
                {
                    LogUtil.Warn("DataFilesUtil", $"本地数据文件不存在：{fullPath}");
                    return default;
                }

                byte[] bytes = await File.ReadAllBytesAsync(fullPath);
                return DataUtil.LoadFromBytes<T>(bytes, extension);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                LogUtil.Error("DataFilesUtil", $"异步读取本地数据失败：{relativePath} → {ex.Message}");
                return default;
            }
        }

        /// <summary>
        /// 覆盖保存本地数据。persistentDataPath 是可写目录，移动端可以直接使用。
        /// </summary>
        public static void SaveLocalData<T>(T data, string relativePath = null)
        {
            try
            {
                relativePath ??= FindRelativePath<T>();
                if (string.IsNullOrEmpty(relativePath))
                {
                    LogUtil.Warn("DataFilesUtil", $"未找到类型 {typeof(T).Name} 对应的文件路径，保存失败。");
                    return;
                }

                string extension = GetRuntimeExtension();
                string fullPath = BuildFullPath(Application.persistentDataPath, relativePath, extension);
                PathUtil.EnsureDirectory(fullPath);
                DataUtil.Save(fullPath, data);
                LogUtil.Success("DataFilesUtil", $"已保存本地数据：{relativePath}");
            }
            catch (Exception ex)
            {
                LogUtil.Error("DataFilesUtil", $"保存本地数据失败：{relativePath} → {ex.Message}");
            }
        }

        #endregion

        #region 路径与读取器

        private static bool IsStreamingAssetsUri =>
            Application.streamingAssetsPath.IndexOf("://", StringComparison.Ordinal) >= 0;

        private static string GetRuntimeExtension()
        {
            return GlobalSettingsRuntimeLoader.Current.CurrentDataLoadMode == EnvironmentState.DataLoadMode.Json
                ? ".json"
                : GlobalSettingsRuntimeLoader.Current.EncryptedExtension;
        }

        /// <summary>
        /// 拼接完整本地路径，并替换为指定扩展名。
        /// </summary>
        public static string BuildFullPath(string basePath, string relativePath, string extension = null)
        {
            if (string.IsNullOrEmpty(basePath) || string.IsNullOrEmpty(relativePath))
                return string.Empty;

            string normalized = PathUtil.NormalizePath(relativePath);
            string withoutExtension = Path.ChangeExtension(normalized, null);
            string finalPath = extension != null ? withoutExtension + extension : withoutExtension;
            return PathUtil.NormalizePath(Path.Combine(basePath, finalPath));
        }

        private static string BuildRelativeFilePath(string relativePath, string extension)
        {
            string normalized = PathUtil.NormalizePath(relativePath);
            string withoutExtension = Path.ChangeExtension(normalized, null);
            return PathUtil.NormalizePath(withoutExtension + extension);
        }

        private static string BuildStreamingUri(string relativePath)
        {
            string root = Application.streamingAssetsPath.TrimEnd('/', '\\');
            string relative = PathUtil.NormalizePath(relativePath).TrimStart('/');
            return root + "/" + relative;
        }

        private static async UniTask<byte[]> ReadStreamingBytesAsync(
            string relativePath,
            CancellationToken cancellationToken)
        {
            string fullPath = BuildStreamingUri(relativePath);

            if (!IsStreamingAssetsUri)
                return await File.ReadAllBytesAsync(fullPath);

            using UnityWebRequest request = UnityWebRequest.Get(fullPath);
            await request.SendWebRequest().ToUniTask(cancellationToken: cancellationToken);

            if (request.result != UnityWebRequest.Result.Success)
                throw new IOException($"StreamingAssets 读取失败：{fullPath} → {request.error}");

            return request.downloadHandler.data;
        }

        private static void EnsureLocalFileExists(string relativePath)
        {
            if (IsStreamingAssetsUri)
            {
                LogUtil.Warn("DataFilesUtil", "移动端不能同步复制 StreamingAssets，请使用 LoadLocalDataAsync。");
                return;
            }

            string extension = GetRuntimeExtension();
            string persistentPath = BuildFullPath(Application.persistentDataPath, relativePath, extension);
            string streamingPath = BuildFullPath(Application.streamingAssetsPath, relativePath, extension);
            if (File.Exists(persistentPath) || !File.Exists(streamingPath))
                return;

            PathUtil.EnsureDirectory(persistentPath);
            File.Copy(streamingPath, persistentPath);
        }

        private static async UniTask EnsureLocalFileExistsAsync(
            string relativePath,
            string extension,
            CancellationToken cancellationToken)
        {
            string persistentPath = BuildFullPath(Application.persistentDataPath, relativePath, extension);
            if (File.Exists(persistentPath))
                return;

            string relativeFile = BuildRelativeFilePath(relativePath, extension);
            byte[] bytes = await ReadStreamingBytesAsync(relativeFile, cancellationToken);
            PathUtil.EnsureDirectory(persistentPath);
            File.WriteAllBytes(persistentPath, bytes);
            LogUtil.Info("DataFilesUtil", $"已初始化本地数据：{persistentPath}");
        }

        #endregion

        #region 运行时清单与路径查找

        /// <summary>
        /// 编辑器 / 桌面环境下保留目录扫描能力，方便开发期直接运行。
        /// Android / iOS 请使用异步版本读取 manifest。
        /// </summary>
        public static string FindRelativePath<T>()
        {
            Type type = typeof(T);
            if (pathCache.TryGetValue(type, out string cached))
                return cached;

            if (IsStreamingAssetsUri)
            {
                LogUtil.Warn("DataFilesUtil", "移动端不能同步扫描 StreamingAssets，请使用异步数据 API。");
                return null;
            }

            string searchName = GetDataKey(type);
            var settings = GlobalSettingsRuntimeLoader.Current;
            bool useJson = settings.CurrentDataLoadMode == EnvironmentState.DataLoadMode.Json;
            string folder = useJson
                ? "FinkFramework_Data/DataJson"
                : "FinkFramework_Data/DataBinary";
            string extension = useJson ? ".json" : settings.EncryptedExtension;
            if (string.IsNullOrEmpty(extension))
                return null;

            string streamingRoot = PathUtil.NormalizePath(Application.streamingAssetsPath);
            string root = Path.Combine(streamingRoot, folder);
            if (!Directory.Exists(root))
                return null;

            // 只扫描当前运行模式对应的目录，避免 JSON 模式误选到旧的 Binary 文件。
            string match = Directory
                .GetFiles(root, "*" + extension, SearchOption.AllDirectories)
                .FirstOrDefault(file => Path.GetFileNameWithoutExtension(file)
                    .Equals(searchName, StringComparison.OrdinalIgnoreCase));

            if (match == null)
                return null;

            string normalized = PathUtil.NormalizePath(match);
            string relative = normalized[(streamingRoot.Length + 1)..];
            pathCache[type] = Path.ChangeExtension(relative, null);
            return pathCache[type];

        }

        /// <summary>
        /// Android / iOS 通过 manifest 查找数据路径，避免遍历 APK 内目录。
        /// </summary>
        public static async UniTask<string> FindRelativePathAsync<T>(
            CancellationToken cancellationToken = default)
        {
            Type type = typeof(T);
            if (pathCache.TryGetValue(type, out string cached))
                return cached;

            if (!IsStreamingAssetsUri)
                return FindRelativePath<T>();

            DataManifestEntry entry = await FindManifestEntryAsync<T>(cancellationToken);
            return entry?.relativePath;
        }

        private static async UniTask<DataManifestEntry> FindManifestEntryAsync<T>(
            CancellationToken cancellationToken)
        {
            Type type = typeof(T);
            DataManifest manifest = await LoadManifestAsync(cancellationToken);
            if (manifest?.entries == null)
                return null;

            string key = GetDataKey(type);
            DataManifestEntry entry = manifest.entries.FirstOrDefault(item =>
                string.Equals(item.key, key, StringComparison.OrdinalIgnoreCase));

            if (entry == null || string.IsNullOrEmpty(entry.relativePath))
            {
                LogUtil.Warn("DataFilesUtil", $"数据清单中未找到：{key}");
                return null;
            }

            pathCache[type] = entry.relativePath;
            return entry;
        }

        private static async UniTask<DataManifest> LoadManifestAsync(CancellationToken cancellationToken)
        {
            if (manifestLoadAttempted)
                return manifestCache;

            try
            {
                byte[] bytes = await ReadStreamingBytesAsync(ManifestRelativePath, cancellationToken);
                // UnityWebRequest 返回原始字节；兼容旧版本生成的 UTF-8 BOM 文件。
                string json = Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF');
                manifestCache = JsonUtility.FromJson<DataManifest>(json);

                if (manifestCache?.entries == null)
                    throw new InvalidDataException("运行时数据清单内容为空或格式无效。");

                // 只有成功解析后才标记，网络/解析失败可以通过“重新读取”重试。
                manifestLoadAttempted = true;
                return manifestCache;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                LogUtil.Error("DataFilesUtil", $"读取运行时数据清单失败：{ex.Message}");
                return null;
            }
        }

        private static string GetDataKey(Type type)
        {
            const string suffix = "Container";
            return type.Name.EndsWith(suffix, StringComparison.Ordinal)
                ? type.Name[..^suffix.Length]
                : type.Name;
        }

        public static void ClearCache()
        {
            pathCache.Clear();
            manifestCache = null;
            manifestLoadAttempted = false;
        }

        #endregion
    }
}
