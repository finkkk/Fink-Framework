using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using FinkFramework.Runtime.Utils;
using UnityEngine.Networking;

namespace FinkFramework.Runtime.Localization
{
    /// <summary>
    /// 一份已经读取完成的本地化语言表文本。
    /// </summary>
    public sealed class LocalizationDataFile
    {
        /// <summary>
        /// 创建一次已完成读取的语言文件结果。
        /// </summary>
        public LocalizationDataFile(string sourcePath, string json)
        {
            SourcePath = sourcePath;
            Json = json;
        }

        /// <summary>
        /// 实际读取来源，可以是本地绝对路径或 StreamingAssets URI。
        /// </summary>
        public string SourcePath { get; }

        /// <summary>
        /// 读取到的原始 JSON 文本。解析由 LocalizationDatabase 负责。
        /// </summary>
        public string Json { get; }
    }

    /// <summary>
    /// 本地化模块使用的固定文件数据源，只负责读取文本，不负责解析和缓存语言表。
    /// 桌面平台使用文件 API，Android 等平台使用 StreamingAssets 的本地 URI。
    /// </summary>
    public sealed class LocalizationFileDataSource
    {
        /// <summary>同步读取本地文件形式的清单；URI 清单必须使用异步接口。</summary>
        public bool TryReadManifest(string manifestPath, out string json)
        {
            json = null;
            if (string.IsNullOrWhiteSpace(manifestPath) || IsUri(manifestPath) || !File.Exists(manifestPath))
                return false;

            try
            {
                json = File.ReadAllText(manifestPath);
                return true;
            }
            catch (Exception exception)
            {
                LogUtil.Error(
                    "Localization",
                    $"读取本地化清单失败：{manifestPath} → {exception.Message}");
                return false;
            }
        }

        /// <summary>
        /// 异步读取本地或 StreamingAssets URI 形式的清单。
        /// </summary>
        public async UniTask<string> ReadManifestAsync(
            string manifestPath,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(manifestPath))
                return null;

            if (IsUri(manifestPath))
            {
                try
                {
                    using UnityWebRequest request = UnityWebRequest.Get(manifestPath);
                    await request.SendWebRequest().ToUniTask(
                        cancellationToken: cancellationToken);
                    if (request.result == UnityWebRequest.Result.Success)
                        return request.downloadHandler.text;

                    LogUtil.Warn(
                        "Localization",
                        $"读取本地化清单失败：{manifestPath} → {request.error}");
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    LogUtil.Warn(
                        "Localization",
                        $"读取本地化清单异常：{manifestPath} → {exception.Message}");
                }

                return null;
            }

            if (!File.Exists(manifestPath))
                return null;

            try
            {
                string json = await File.ReadAllTextAsync(manifestPath, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                return json;
            }
            catch (FileNotFoundException)
            {
                return null;
            }
            catch (DirectoryNotFoundException)
            {
                return null;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                LogUtil.Warn(
                    "Localization",
                    $"异步读取本地化清单失败：{manifestPath} → {exception.Message}");
                return null;
            }
        }

        /// <summary>
        /// 从候选根目录同步读取一张语言表。读取失败时继续尝试下一个候选根目录。
        /// </summary>
        public bool TryRead(
            IEnumerable<string> candidateRoots,
            string category,
            string localeCode,
            string fileNamePattern,
            out LocalizationDataFile file)
        {
            file = null;
            if (candidateRoots == null)
                return false;

            foreach (string root in candidateRoots)
            {
                if (IsUri(root))
                    continue;

                string candidate = LocalizationPath.GetLanguageFilePath(
                    root,
                    category,
                    localeCode,
                    fileNamePattern);
                if (string.IsNullOrEmpty(candidate) || !File.Exists(candidate))
                    continue;

                try
                {
                    file = new LocalizationDataFile(candidate, File.ReadAllText(candidate));
                    return true;
                }
                catch (Exception exception)
                {
                    LogUtil.Error(
                        "Localization",
                        $"读取语言文件失败：{candidate} → {exception.Message}");
                    // 当前候选根目录读取失败时继续尝试其他候选根目录。
                }
            }

            return false;
        }

        /// <summary>
        /// 异步读取一张语言表，支持本地文件和 StreamingAssets URI。
        /// </summary>
        public async UniTask<LocalizationDataFile> ReadAsync(
            IEnumerable<string> candidateRoots,
            string category,
            string localeCode,
            string fileNamePattern,
            CancellationToken cancellationToken = default)
        {
            if (candidateRoots == null)
                return null;

            foreach (string root in candidateRoots)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string candidate = LocalizationPath.GetLanguageFilePath(
                    root,
                    category,
                    localeCode,
                    fileNamePattern);
                if (string.IsNullOrEmpty(candidate))
                    continue;

                if (IsUri(root))
                {
                    try
                    {
                        using UnityWebRequest request = UnityWebRequest.Get(candidate);
                        await request.SendWebRequest().ToUniTask(
                            cancellationToken: cancellationToken);
                        if (request.result == UnityWebRequest.Result.Success)
                            return new LocalizationDataFile(candidate, request.downloadHandler.text);

                        LogUtil.Warn(
                            "Localization",
                            $"读取 StreamingAssets URI 文件失败：{candidate} → {request.error}");
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception exception)
                    {
                        LogUtil.Warn(
                            "Localization",
                            $"读取 StreamingAssets URI 文件异常：{candidate} → {exception.Message}");
                    }

                    continue;
                }

                if (!File.Exists(candidate))
                    continue;

                try
                {
                    string json = await File.ReadAllTextAsync(candidate, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    return new LocalizationDataFile(candidate, json);
                }
                catch (FileNotFoundException)
                {
                    // 文件可能在扫描后被删除，继续尝试下一个候选根目录。
                }
                catch (DirectoryNotFoundException)
                {
                    // 目录可能在扫描后被删除，继续尝试下一个候选根目录。
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    LogUtil.Error(
                        "Localization",
                        $"异步读取语言文件失败：{candidate} → {exception.Message}");
                    // 当前候选根目录失败时继续尝试下一个候选根目录。
                }
            }

            return null;
        }

        /// <summary>
        /// 判断路径是否为需要 UnityWebRequest 访问的 URI。
        /// </summary>
        private static bool IsUri(string path)
        {
            return !string.IsNullOrEmpty(path)
                && path.IndexOf("://", StringComparison.Ordinal) >= 0;
        }
    }
}
