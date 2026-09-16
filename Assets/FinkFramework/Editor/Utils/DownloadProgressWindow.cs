using System;
using System.Globalization;
using System.IO;
using FinkFramework.Runtime.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace FinkFramework.Editor.Utils
{
    /// <summary>
    /// Fink Framework 更新包下载窗口。
    /// 下载过程独立于版本检查，避免用户只能从 Console 判断是否仍在下载。
    /// </summary>
    internal sealed class DownloadProgressWindow : EditorWindow
    {
        private const int DownloadTimeoutSeconds = 600;

        private static DownloadProgressWindow _window;
        private static UnityWebRequest _request;
        private static string _packageUrl;
        private static string _packagePath;
        private static string _latestVersion;
        private static string _status;
        private static string _error;
        private static bool _isDownloading;
        private static bool _downloadFinished;
        private static long _downloadedBytes;
        private static long _totalBytes;
        private static float _progress;

        internal static void Start(string packageUrl, string packagePath, string latestVersion)
        {
            if (string.IsNullOrWhiteSpace(packageUrl))
            {
                LogUtil.Error("框架更新", "更新包下载地址为空。");
                return;
            }

            _packageUrl = packageUrl;
            _packagePath = packagePath;
            _latestVersion = latestVersion;

            _window = GetWindow<DownloadProgressWindow>(true, "Fink Framework 更新下载", true);
            _window.minSize = new Vector2(480, 220);
            _window.maxSize = new Vector2(760, 300);
            _window.BeginDownload();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("Fink Framework 更新下载");
        }

        private void OnDisable()
        {
            EditorApplication.update -= RepaintWhileDownloading;

            // 用户关闭窗口时停止网络请求，避免窗口关闭后仍在后台下载。
            if (_window == this && _request != null)
                CancelDownload("用户取消了下载。");

            if (_window == this)
                _window = null;
        }

        private void OnGUI()
        {
            GUILayout.Space(16);
            GUILayout.Label("Fink Framework 更新下载", EditorStyles.boldLabel);
            GUILayout.Label($"目标版本：v{_latestVersion}", EditorStyles.miniLabel);
            GUILayout.Space(10);

            if (_isDownloading)
            {
                DrawDownloadingState();
            }
            else if (!string.IsNullOrEmpty(_error))
            {
                DrawErrorState();
            }
            else if (_downloadFinished)
            {
                EditorGUILayout.HelpBox("下载完成，正在准备导入更新包…", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("准备下载更新包…", MessageType.Info);
            }

            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (_isDownloading)
            {
                if (GUILayout.Button("取消下载", GUILayout.Width(110), GUILayout.Height(28)))
                    CancelDownload("用户取消了下载。");
            }
            else if (!string.IsNullOrEmpty(_error))
            {
                if (GUILayout.Button("重试下载", GUILayout.Width(110), GUILayout.Height(28)))
                    BeginDownload();

                GUILayout.Space(8);
                if (GUILayout.Button("关闭", GUILayout.Width(80), GUILayout.Height(28)))
                    Close();
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(12);
        }

        private static void DrawDownloadingState()
        {
            string progressText = _totalBytes > 0
                ? $"{_progress:P0}    {FormatBytes(_downloadedBytes)} / {FormatBytes(_totalBytes)}"
                : $"已下载 {FormatBytes(_downloadedBytes)}";

            Rect progressRect = EditorGUILayout.GetControlRect(false, 22);
            EditorGUI.ProgressBar(progressRect, _progress, progressText);
            GUILayout.Space(8);
            EditorGUILayout.LabelField(_status ?? "正在下载…", EditorStyles.wordWrappedLabel);
        }

        private static void DrawErrorState()
        {
            EditorGUILayout.HelpBox("更新包下载失败。", MessageType.Error);
            GUILayout.Space(4);
            EditorGUILayout.LabelField(_error, EditorStyles.wordWrappedLabel);
        }

        private void BeginDownload()
        {
            if (_request != null)
                CancelDownload("重新开始下载。");

            _error = null;
            _status = "正在连接下载服务器…";
            _isDownloading = true;
            _downloadFinished = false;
            _downloadedBytes = 0;
            _totalBytes = 0;
            _progress = 0f;

            try
            {
                _request = UnityWebRequest.Get(_packageUrl);
                _request.downloadHandler = new DownloadHandlerBuffer();
                _request.redirectLimit = 10;
                _request.timeout = DownloadTimeoutSeconds;
                _request.SetRequestHeader("User-Agent", "Fink-Framework-UpdateChecker");

                UnityWebRequestAsyncOperation operation = _request.SendWebRequest();
                operation.completed += OnDownloadCompleted;
                EditorApplication.update -= RepaintWhileDownloading;
                EditorApplication.update += RepaintWhileDownloading;

                LogUtil.Info("框架更新", "开始下载更新包…");
                RepaintWindow();
            }
            catch (Exception ex)
            {
                if (_request != null)
                {
                    _request.Dispose();
                    _request = null;
                }
                HandleDownloadFailure("启动下载请求失败：" + ex.Message);
            }
        }

        private static void RepaintWhileDownloading()
        {
            if (_request == null)
                return;

            _downloadedBytes = (long)_request.downloadedBytes;
            _totalBytes = ReadContentLength(_request);
            _progress = _totalBytes > 0
                ? Mathf.Clamp01((float)_downloadedBytes / _totalBytes)
                : Mathf.Max(0f, _request.downloadProgress);

            if (_request.downloadedBytes > 0)
                _status = "正在下载更新包…";

            RepaintWindow();
        }

        private static void OnDownloadCompleted(AsyncOperation operation)
        {
            EditorApplication.update -= RepaintWhileDownloading;

            UnityWebRequest request = _request;
            _request = null;

            if (request == null)
                return;

            _downloadedBytes = (long)request.downloadedBytes;
            _totalBytes = ReadContentLength(request);

            if (request.result != UnityWebRequest.Result.Success)
            {
                string error = BuildRequestError(request);
                request.Dispose();
                HandleDownloadFailure(error);
                return;
            }

            try
            {
                byte[] package = request.downloadHandler?.data;
                request.Dispose();

                if (package == null || package.Length == 0)
                    throw new InvalidDataException("服务器返回的更新包为空。");

                File.WriteAllBytes(_packagePath, package);
                _downloadedBytes = package.Length;
                _totalBytes = package.Length;
                _progress = 1f;
                _status = "下载完成，正在准备导入更新包…";
                _isDownloading = false;
                _downloadFinished = true;

                LogUtil.Info("框架更新", $"更新包下载完成：{FormatBytes(package.Length)}。");
                RepaintWindow();
                EditorApplication.delayCall += StartPackageImport;
            }
            catch (Exception ex)
            {
                request.Dispose();
                HandleDownloadFailure("保存更新包失败：" + ex.Message);
            }
        }

        private static void StartPackageImport()
        {
            if (_window != null)
                _window.Close();

            FrameworkPackageUpdater.Start(_packagePath);
        }

        private static void HandleDownloadFailure(string error)
        {
            EditorApplication.update -= RepaintWhileDownloading;
            _isDownloading = false;
            _downloadFinished = false;
            _error = error;
            FrameworkPackageUpdater.DeleteDownloadedPackage(_packagePath);
            LogUtil.Error("框架更新", "下载更新包失败：" + error);
            RepaintWindow();
        }

        private static void CancelDownload(string message)
        {
            EditorApplication.update -= RepaintWhileDownloading;

            if (_request != null)
            {
                _request.Abort();
                _request.Dispose();
                _request = null;
            }

            _isDownloading = false;
            _downloadFinished = false;
            _error = message;
            FrameworkPackageUpdater.DeleteDownloadedPackage(_packagePath);
            LogUtil.Warn("框架更新", message);
            RepaintWindow();
        }

        private static string BuildRequestError(UnityWebRequest request)
        {
            string status = request.responseCode > 0
                ? $"HTTP {request.responseCode}"
                : "未收到 HTTP 状态码";
            string detail = string.IsNullOrWhiteSpace(request.error) ? "未知网络错误" : request.error;
            return $"{status}：{detail}\n地址：{request.url}";
        }

        private static long ReadContentLength(UnityWebRequest request)
        {
            string value = request.GetResponseHeader("Content-Length");
            return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long length)
                ? length
                : 0;
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024)
                return bytes + " B";
            if (bytes < 1024 * 1024)
                return $"{bytes / 1024f:0.0} KB";
            return $"{bytes / (1024f * 1024f):0.00} MB";
        }

        private static void RepaintWindow()
        {
            if (_window != null)
                _window.Repaint();
        }
    }
}
