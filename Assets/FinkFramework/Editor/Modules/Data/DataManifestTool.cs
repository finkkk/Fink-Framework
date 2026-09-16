using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using FinkFramework.Runtime.Data;
using FinkFramework.Runtime.Environments;
using FinkFramework.Runtime.Settings.Loaders;
using FinkFramework.Runtime.Utils;
using UnityEngine;

namespace FinkFramework.Editor.Modules.Data
{
    /// <summary>
    /// 生成运行时数据清单，避免 Android 下遍历 StreamingAssets 目录。
    /// 只在编辑器中生成清单；运行时由 DataFilesUtil 读取清单并定位数据文件。
    /// </summary>
    internal static class DataManifestTool
    {
        private const string ManifestFileName = "data-manifest.json";

        public static bool Generate()
        {
            try
            {
                string streamingRoot = PathUtil.NormalizePath(Application.streamingAssetsPath);
                string dataRoot = GlobalSettingsRuntimeLoader.Current.CurrentDataLoadMode == EnvironmentState.DataLoadMode.Binary
                    ? DataPipelinePath.BinaryRoot
                    : DataPipelinePath.JsonRoot;

                if (string.IsNullOrEmpty(dataRoot) || !Directory.Exists(dataRoot))
                {
                    LogUtil.Error("DataManifestTool", $"运行时数据目录不存在，无法生成清单：{dataRoot}");
                    return false;
                }

                string extension = GlobalSettingsRuntimeLoader.Current.CurrentDataLoadMode == EnvironmentState.DataLoadMode.Binary
                    ? GlobalSettingsRuntimeLoader.Current.EncryptedExtension
                    : ".json";
                if (string.IsNullOrEmpty(extension))
                {
                    LogUtil.Error("DataManifestTool", "当前 Binary 模式未配置有效的数据文件扩展名。请检查全局数据设置。");
                    return false;
                }

                var files = Directory
                    .EnumerateFiles(dataRoot, "*" + extension, SearchOption.AllDirectories)
                    // 固定输出顺序，避免文件系统遍历顺序变化导致清单产生无意义差异。
                    .OrderBy(PathUtil.NormalizePath, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                var manifest = new DataManifest();
                var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (string file in files)
                {
                    string key = GetDataKey(Path.GetFileNameWithoutExtension(file));

                    if (string.IsNullOrEmpty(key) || !keys.Add(key))
                    {
                        LogUtil.Error("DataManifestTool", $"发现空 key 或同名数据表，无法生成唯一运行时路径：{key}");
                        return false;
                    }

                    string relative = PathUtil.NormalizePath(Path.GetRelativePath(streamingRoot, file));
                    manifest.entries.Add(new DataManifestEntry
                    {
                        key = key,
                        relativePath = Path.ChangeExtension(relative, null),
                        extension = Path.GetExtension(file).ToLowerInvariant(),
                        format = extension.Equals(".json", StringComparison.OrdinalIgnoreCase) ? "json" : "binary",
                        size = new FileInfo(file).Length,
                        hash = ComputeHash(file)
                    });
                }

                string manifestPath = Path.Combine(
                    Application.streamingAssetsPath,
                    "FinkFramework_Data",
                    ManifestFileName
                );

                Directory.CreateDirectory(Path.GetDirectoryName(manifestPath) ?? string.Empty);
                string json = JsonUtility.ToJson(manifest, true);
                // 不写 BOM，避免移动端通过 UnityWebRequest 读取后交给 JsonUtility 时解析失败。
                File.WriteAllText(manifestPath, json, new UTF8Encoding(false));

                LogUtil.Success("DataManifestTool", $"运行时数据清单生成完成：{manifestPath}（{files.Length} 个文件）");
                return true;
            }
            catch (Exception ex)
            {
                LogUtil.Error("DataManifestTool", $"生成运行时数据清单失败：{ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 将数据文件名转换为运行时清单使用的统一 key。
        /// 生成器、清单和构建校验必须使用同一套命名规则。
        /// </summary>
        internal static string GetDataKey(string fileName)
        {
            string rawKey = fileName.EndsWith("Container", StringComparison.OrdinalIgnoreCase)
                ? fileName[..^9]
                : fileName;
            return TextsUtil.ToPascalCase(rawKey);
        }

        private static string ComputeHash(string path)
        {
            using var sha = SHA256.Create();
            using var stream = File.OpenRead(path);
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
