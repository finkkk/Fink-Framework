using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FinkFramework.Runtime.Localization;
using FinkFramework.Runtime.Utils;
using UnityEditor;

namespace FinkFramework.Editor.Modules.Localization
{
    /// <summary>
    /// 将项目内本地化源文件同步为 StreamingAssets 运行时副本。
    /// </summary>
    internal static class LocalizationDataSyncUtility
    {
        /// <summary>
        /// 源语言表发生保存、删除或同步后通知编辑器组件刷新 Key 目录。
        /// </summary>
        public static event Action SourceDataChanged;

        /// <summary>
        /// 同步全部 JSON，并重新生成运行时 Manifest。
        /// </summary>
        public static bool Sync(
            LocalizationSettingsAsset settings,
            out string message,
            bool refreshAssetDatabase = true)
        {
            message = null;
            if (settings == null)
            {
                message = "LocalizationSettingsAsset 为空。";
                return false;
            }

            string sourceRoot = LocalizationPath.GetSourceDataRoot();
            string runtimeRoot = LocalizationPath.GetStreamingDataRoot();
            try
            {
                if (!PrepareSourceDataRoot(out string prepareMessage))
                {
                    message = prepareMessage;
                    return false;
                }

                MirrorJsonFiles(sourceRoot, runtimeRoot);

                if (!LocalizationManifestTool.Generate(settings, out string manifestMessage))
                {
                    message = manifestMessage;
                    return false;
                }

                if (refreshAssetDatabase)
                    AssetDatabase.Refresh();

                message =
                    $"本地化源文件已同步到运行时副本。\n源目录：{sourceRoot}\n运行时副本：{runtimeRoot}";
                return true;
            }
            catch (Exception exception)
            {
                message =
                    $"本地化源文件同步失败：{exception.Message}\n源目录：{sourceRoot}\n运行时副本：{runtimeRoot}";
                return false;
            }
            finally
            {
                // 同步成功或失败都刷新编辑器 Key 目录；源文件可能已经在同步前写入。
                NotifySourceDataChanged();
            }
        }

        /// <summary>
        /// 通知编辑器本地化源文件已变化。事件异常不会影响保存流程。
        /// </summary>
        public static void NotifySourceDataChanged()
        {
            Action handlers = SourceDataChanged;
            if (handlers == null)
                return;

            Delegate[] invocationList = handlers.GetInvocationList();
            for (int i = 0; i < invocationList.Length; i++)
            {
                if (!(invocationList[i] is Action handler))
                    continue;

                try
                {
                    handler();
                }
                catch (Exception exception)
                {
                    LogUtil.Warn("Localization", $"刷新本地化 Key 目录失败：{exception.Message}");
                }
            }
        }

        /// <summary>
        /// 确保本地化源目录存在。
        /// </summary>
        public static bool PrepareSourceDataRoot(out string message)
        {
            string sourceRoot = LocalizationPath.GetSourceDataRoot();
            try
            {
                Directory.CreateDirectory(sourceRoot);
                message = null;
                return true;
            }
            catch (Exception exception)
            {
                message =
                    $"创建本地化源文件目录失败：{exception.Message}\n目录：{sourceRoot}";
                return false;
            }
        }

        /// <summary>
        /// 校验源文件、运行时 JSON 和 Manifest 的文件集合及 Key 索引一致。
        /// </summary>
        public static bool ValidateSynchronizedCopy(
            LocalizationSettingsAsset settings,
            out string message)
        {
            if (settings == null)
            {
                message = "LocalizationSettingsAsset 为空。";
                return false;
            }

            string sourceRoot = LocalizationPath.GetSourceDataRoot();
            string runtimeRoot = LocalizationPath.GetStreamingDataRoot();
            if (!Directory.Exists(sourceRoot))
            {
                message = $"缺少本地化源文件目录：{sourceRoot}";
                return false;
            }

            if (!Directory.Exists(runtimeRoot))
            {
                message = $"缺少本地化运行时副本目录：{runtimeRoot}";
                return false;
            }

            HashSet<string> sourceFiles = GetRelativeJsonFiles(sourceRoot);
            HashSet<string> runtimeFiles = GetRelativeJsonFiles(runtimeRoot);
            if (!sourceFiles.SetEquals(runtimeFiles))
            {
                string missing = string.Join(", ", sourceFiles.Except(runtimeFiles).Take(8));
                string extra = string.Join(", ", runtimeFiles.Except(sourceFiles).Take(8));
                message =
                    "本地化运行时副本文件集合与源目录不一致。" +
                    (string.IsNullOrEmpty(missing) ? string.Empty : $" 缺少：{missing}。") +
                    (string.IsNullOrEmpty(extra) ? string.Empty : $" 多余：{extra}。");
                return false;
            }

            if (!LocalizationManifestTool.Validate(settings, out message))
                return false;

            message = $"本地化源文件与运行时副本校验通过：{sourceFiles.Count} 个 JSON 文件。";
            return true;
        }

        /// <summary>
        /// 将源目录中的 JSON 原子复制到运行时目录，并删除运行时目录中的过期 JSON。
        /// </summary>
        private static void MirrorJsonFiles(string sourceRoot, string runtimeRoot)
        {
            Directory.CreateDirectory(runtimeRoot);
            HashSet<string> sourceFiles = GetRelativeJsonFiles(sourceRoot);
            HashSet<string> runtimeFiles = GetRelativeJsonFiles(runtimeRoot);

            foreach (string relativePath in sourceFiles)
            {
                string sourcePath = Path.Combine(sourceRoot, relativePath);
                string runtimePath = Path.Combine(runtimeRoot, relativePath);
                string directory = Path.GetDirectoryName(runtimePath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                // JSON 运行时副本也采用原子替换，避免 Unity/编辑器中断时留下半截文件。
                LocalizationEditorFileUtil.WriteUtf8TextAtomic(
                    runtimePath,
                    File.ReadAllText(sourcePath));
            }

            // 运行时目录是生成副本，只清理其中不再存在于源目录的 JSON。
            foreach (string staleRelativePath in runtimeFiles.Except(sourceFiles).ToArray())
                File.Delete(Path.Combine(runtimeRoot, staleRelativePath));
        }

        /// <summary>
        /// 获取目录下所有 JSON 的相对路径集合，用于同步和一致性校验。
        /// </summary>
        private static HashSet<string> GetRelativeJsonFiles(string root)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!Directory.Exists(root))
                return result;

            foreach (string filePath in Directory.GetFiles(root, "*.json", SearchOption.AllDirectories))
            {
                string relativePath = Path.GetRelativePath(root, filePath)
                    .Replace(Path.DirectorySeparatorChar, '/')
                    .Replace(Path.AltDirectorySeparatorChar, '/');
                result.Add(relativePath);
            }

            return result;
        }

    }
}
