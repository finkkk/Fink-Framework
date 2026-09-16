using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using FinkFramework.Editor.Modules.Data;
using FinkFramework.Runtime.Data;
using FinkFramework.Runtime.Environments;
using FinkFramework.Runtime.Settings.Loaders;
using FinkFramework.Runtime.Utils;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace FinkFramework.Editor.Utils
{
    /// <summary>
    /// 构建前校验运行时数据是否已经生成、清单是否完整且未过期。
    /// </summary>
    internal sealed class DataBuildCheckUtil : IPreprocessBuildWithReport
    {
        public int callbackOrder => 1;

        public void OnPreprocessBuild(BuildReport report)
        {
            string sourceRoot = DataPipelinePath.ExcelRoot;
            string[] excelFiles = Directory.Exists(sourceRoot)
                ? Directory.GetFiles(sourceRoot, "*.xlsx", SearchOption.AllDirectories)
                : Array.Empty<string>();

            // 没有数据表时允许框架作为纯功能模块使用。
            if (excelFiles.Length == 0)
                return;

            string manifestPath = Path.Combine(
                Application.streamingAssetsPath,
                "FinkFramework_Data",
                "data-manifest.json"
            );

            if (!File.Exists(manifestPath))
            {
                throw new BuildFailedException(
                    "构建失败：缺少运行时数据清单 data-manifest.json。\n" +
                    "请先执行 Fink Framework → Data Pipeline → 一键处理数据。"
                );
            }

            DataManifest manifest;
            try
            {
                string json = File.ReadAllText(manifestPath, Encoding.UTF8).TrimStart('\uFEFF');
                manifest = JsonUtility.FromJson<DataManifest>(json);
            }
            catch (Exception ex)
            {
                throw new BuildFailedException($"构建失败：运行时数据清单无法解析。{ex.Message}");
            }

            if (manifest?.entries == null || manifest.entries.Count == 0)
                throw new BuildFailedException("构建失败：运行时数据清单为空，请重新执行一键处理数据。");

            string expectedExtension = GlobalSettingsRuntimeLoader.Current.CurrentDataLoadMode == EnvironmentState.DataLoadMode.Json
                ? ".json"
                : GlobalSettingsRuntimeLoader.Current.EncryptedExtension;

            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (DataManifestEntry entry in manifest.entries)
            {
                if (string.IsNullOrEmpty(entry.key) || !keys.Add(entry.key))
                    throw new BuildFailedException($"构建失败：运行时数据清单存在重复或空 key：{entry.key}");

                if (!string.Equals(entry.extension, expectedExtension, StringComparison.OrdinalIgnoreCase))
                    throw new BuildFailedException(
                        $"构建失败：数据清单格式与当前数据模式不一致：{entry.key} ({entry.extension})"
                    );

                string filePath = Path.Combine(
                    Application.streamingAssetsPath,
                    entry.relativePath + entry.extension
                );

                if (!File.Exists(filePath))
                    throw new BuildFailedException($"构建失败：清单引用的数据文件不存在：{filePath}");

                FileInfo info = new(filePath);
                if (info.Length != entry.size || !string.Equals(ComputeHash(filePath), entry.hash, StringComparison.OrdinalIgnoreCase))
                {
                    throw new BuildFailedException(
                        $"构建失败：数据文件与清单不一致：{entry.key}。请重新执行一键处理数据。"
                    );
                }
            }

            var expectedKeys = new HashSet<string>(
                excelFiles.Select(path => DataManifestTool.GetDataKey(
                    Path.GetFileNameWithoutExtension(path))),
                StringComparer.OrdinalIgnoreCase
            );

            if (!expectedKeys.SetEquals(keys))
            {
                throw new BuildFailedException(
                    "构建失败：Excel 数据表与运行时数据清单不一致，请重新执行一键处理数据。"
                );
            }

            LogUtil.Info("DataBuildCheckUtil", $"运行时数据校验通过：{keys.Count} 张表。");
        }

        private static string ComputeHash(string path)
        {
            using var sha = SHA256.Create();
            using var stream = File.OpenRead(path);
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
