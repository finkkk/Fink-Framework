using System;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;
using FinkFramework.Runtime.Environments;
using FinkFramework.Runtime.Settings.Loaders;
using FinkFramework.Runtime.Utils;
using Unity.Plastic.Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace FinkFramework.Editor.Utils
{
    public static class UpdateCheckUtil
    {
        // GitHub 最新正式 Release 接口。
        private const string VersionUrl = "https://api.github.com/repos/finkkk/Fink-Framework/releases/latest";
        private const string ReleasesUrl = "https://github.com/finkkk/Fink-Framework/releases";

        // EditorPrefs 是全局的，必须按项目隔离，否则一个项目的检查记录会影响其他项目。
        private static string LastCheckKey =>
            $"FinkFramework_LastUpdateCheck:{Application.dataPath}";
        
        private static readonly HttpClient Client = CreateHttpClient();

        private static bool _checking;
        // 编辑器启动后的自动检查尚未结束时，保留用户主动点击的意图，不能静默丢弃。
        private static bool _manualCheckQueued;
        private static bool _forceUpdateQueued;

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(5)
            };

            client.DefaultRequestHeaders.UserAgent.ParseAdd("Fink-Framework-UpdateChecker");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            return client;
        }

        // 自动检查（InitializeOnLoad）
        [InitializeOnLoadMethod]
        private static void CheckUpdateOnLoad() => _ = CheckUpdateAsync(false, false);
        
        /// <summary>
        /// 手动触发更新检查：忽略自动检查间隔，也不会改写自动检查时间。
        /// 若已有自动检查在执行，则在其结束后补做一次手动检查。
        /// </summary>
        public static void CheckUpdateManual()
        {
            if (_checking)
            {
                _manualCheckQueued = true;
                ShowManualCheckNotification("已有检查正在进行，完成后将重新检查。");
                return;
            }

            ShowManualCheckNotification("正在检查 Fink Framework 更新…");
            _ = CheckUpdateAsync(true, false);
        }
        
        /// <summary>
        /// 获取 GitHub 最新正式版并强制重新安装，不比较本地版本号。
        /// 即使当前版本已经是最新版本，也会继续下载并覆盖导入。
        /// </summary>
        public static void ForceUpdateLatest()
        {
            if (_checking)
            {
                _forceUpdateQueued = true;
                ShowManualCheckNotification("已有更新检查正在进行，完成后将执行强制重装。");
                return;
            }

            bool confirmed = EditorUtility.DisplayDialog(
                "Fink Framework 强制重装",
                "此操作会从 GitHub 获取最新正式版，并覆盖 Assets/FinkFramework 内的框架源码、Editor 工具、内置资源和插件文件。\n\n" +
                "即使当前版本已经是最新版本，也会重新下载并导入。对这些文件的本地修改将会丢失；框架配置、数据文件和项目生成文件不会被修改。\n\n" +
                "更新前会自动创建备份，更新失败将尝试恢复。",
                "继续获取最新版",
                "取消");
            if (!confirmed)
                return;

            ShowManualCheckNotification("正在获取 GitHub 最新正式版…");
            _ = CheckUpdateAsync(true, true);
        }
        
        private static async Task CheckUpdateAsync(bool isManual, bool forceUpdate)
        {
            if (_checking) return;
            _checking = true;
            
            try
            {
                if (!GlobalSettingsRuntimeLoader.TryGet(out var settings) && !isManual)
                    return; // 自动检查依赖配置；手动检查即使首次导入也应可用。

                // 开关：关闭则不检查
                if (!isManual && (settings == null || !settings.EnableUpdateCheck))
                    return;
                
                // 自动检查才需要检查间隔
                if (!isManual)
                {
                    string last = EditorPrefs.GetString(LastCheckKey, "");
                    if (DateTime.TryParse(
                            last,
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.AssumeLocal,
                            out DateTime lastTime) &&
                        (DateTime.Now - lastTime).TotalDays < settings.UpdateCheckIntervalDays)
                        return;

                    // 自动检查才更新 lastCheck
                    EditorPrefs.SetString(LastCheckKey, DateTime.Now.ToString(CultureInfo.InvariantCulture));
                }
                
                if (isManual)
                {
                    LogUtil.Info("版本检查", forceUpdate
                        ? "开始获取 GitHub 最新正式版…"
                        : "开始检查 Fink Framework 更新...");
                }
                
                // GitHub 返回 Release 元数据，版本号位于 tag_name，例如 v0.3.9。
                string json = await Client.GetStringAsync(VersionUrl);
                var data = JObject.Parse(json);

                string latestVersion = NormalizeVersion(data["tag_name"]?.ToString());
                string releaseUrl = data["html_url"]?.ToString() ?? ReleasesUrl;
                string currentVersion = NormalizeVersion(EnvironmentState.FrameworkVersion);

                if (!TryParseVersion(latestVersion, out var latest))
                {
                    if (isManual)
                        LogUtil.Error("版本检查", $"GitHub 版本号格式无法解析：{latestVersion}");
                    return;
                }

                if (!TryParseVersion(currentVersion, out var current))
                {
                    if (isManual)
                        LogUtil.Error("版本检查", $"本地版本号格式无法解析：{currentVersion}");
                    return;
                }

                // 本地版本高于 GitHub 版本时，通常代表本地开发版，禁止强制覆盖。
                if (forceUpdate && current.CompareTo(latest) > 0)
                {
                    string message =
                        $"检测到本地版本高于 GitHub 最新正式版：本地 {currentVersion}，GitHub {latestVersion}。\n\n" +
                        "当前版本可能是本地开发版，已取消强制重装，以免覆盖本地开发代码。";
                    LogUtil.Warn("框架强制重装", message);
                    EditorApplication.delayCall += () => EditorUtility.DisplayDialog(
                        "强制重装已取消", message, "确定");
                    return;
                }

                if (!forceUpdate)
                {
                    int comparison = latest.CompareTo(current);
                    if (comparison <= 0)
                    {
                        if (isManual)
                        {
                            string message = comparison == 0
                                ? $"当前版本已是最新版本：{currentVersion}"
                                : $"当前版本高于 GitHub 最新正式版，可能是本地开发版本：本地 {currentVersion}，GitHub {latestVersion}";
                            LogUtil.Success("版本检查", message);
                        }

                        return;
                    }
                }

                string packageUrl = FindPackageUrl(data, latestVersion);
                if (string.IsNullOrEmpty(packageUrl))
                {
                    LogUtil.Warn(forceUpdate ? "框架强制重装" : "版本更新检查",
                        $"发现新版本 {latestVersion}，但该 Release 未找到可下载的 unitypackage。\n更新地址：{releaseUrl}");
                    return;
                }

                if (isManual)
                {
                    EditorApplication.delayCall += () => ShowUpdateDialog(
                        currentVersion, latestVersion, packageUrl, forceUpdate);
                }
                else
                {
                    LogUtil.Warn("版本更新检查",
                        $"Fink Framework 有新版本！当前：{currentVersion} → 最新：{latestVersion}\n请通过“立即检查更新”确认并安装。");
                }
            }
            catch (TaskCanceledException)
            {
                if (isManual)
                {
                    LogUtil.Error(
                        "版本检查",
                        "检查更新超时，请检查网络连接或稍后再试。"
                    );
                }
            }
            catch (Exception ex)
            {
                if (isManual)
                    LogUtil.Error("版本检查", ex.Message);
            }
            finally
            {
                _checking = false;

                if (_forceUpdateQueued)
                {
                    _forceUpdateQueued = false;
                    EditorApplication.delayCall += ForceUpdateLatest;
                }

                if (_manualCheckQueued)
                {
                    _manualCheckQueued = false;
                    // 不在当前 async continuation 中递归启动，避免状态切换与 Editor GUI 事件交错。
                    EditorApplication.delayCall += CheckUpdateManual;
                }
            }
        }

        /// <summary>
        /// 为触发检查的编辑器窗口提供即时反馈；没有可用窗口时仅保留控制台日志。
        /// </summary>
        private static void ShowManualCheckNotification(string message)
        {
            EditorWindow.focusedWindow?.ShowNotification(new GUIContent(message));
        }

        private static string FindPackageUrl(JObject release, string version)
        {
            JArray assets = release["assets"] as JArray;
            if (assets == null) return null;

            const string packagePrefix = "FinkFramework";
            const string packageExtension = ".unitypackage";
            string expectedVersion = NormalizeVersion(version);

            foreach (JToken asset in assets)
            {
                string name = asset["name"]?.ToString();
                if (string.IsNullOrEmpty(name) ||
                    !name.StartsWith(packagePrefix, StringComparison.OrdinalIgnoreCase) ||
                    !name.EndsWith(packageExtension, StringComparison.OrdinalIgnoreCase))
                    continue;

                string assetVersion = name.Substring(
                        packagePrefix.Length,
                        name.Length - packagePrefix.Length - packageExtension.Length)
                    .TrimStart('.', '-', '_');

                if (string.Equals(NormalizeVersion(assetVersion), expectedVersion, StringComparison.OrdinalIgnoreCase))
                    return asset["browser_download_url"]?.ToString();
            }
            return null;
        }

        private static void ShowUpdateDialog(
            string currentVersion, string latestVersion, string packageUrl, bool forceUpdate)
        {
            if (forceUpdate)
            {
                // 强制重装已经在 ForceUpdateLatest 中完成风险确认，这里直接进入下载。
                DownloadAndUpdate(latestVersion, packageUrl);
                return;
            }

            bool confirmed = EditorUtility.DisplayDialog("Fink Framework 版本更新",
                $"发现新版本：{currentVersion} → {latestVersion}\n\n" +
                "更新会覆盖 Assets/FinkFramework 内的框架源码、Editor 工具、内置资源和插件文件。对这些文件的本地修改将会丢失；框架配置、数据文件和项目生成文件不会被修改。\n\n" +
                "更新前会自动创建备份，更新失败将尝试恢复。",
                "立即更新", "暂不更新");
            if (confirmed) DownloadAndUpdate(latestVersion, packageUrl);
        }

        private static void DownloadAndUpdate(string latestVersion, string packageUrl)
        {
            const string packagePath = "Library/FinkFrameworkUpdate.unitypackage";
            DownloadProgressWindow.Start(packageUrl, packagePath, latestVersion);
        }

        private static string NormalizeVersion(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string normalized = value.Trim();
            return normalized.StartsWith("v", StringComparison.OrdinalIgnoreCase)
                ? normalized[1..]
                : normalized;
        }

        private static bool TryParseVersion(string value, out Version version)
        {
            version = null;
            string normalized = NormalizeVersion(value);
            string[] parts = normalized.Split('.');

            if (parts.Length < 2 || parts.Length > 4)
                return false;

            int[] components = new int[4];
            for (int i = 0; i < parts.Length; i++)
            {
                if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out components[i]) ||
                    components[i] < 0)
                    return false;
            }

            version = new Version(components[0], components[1], components[2], components[3]);
            return true;
        }
    }
}
