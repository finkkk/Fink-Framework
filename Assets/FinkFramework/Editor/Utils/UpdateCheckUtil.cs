using System;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;
using FinkFramework.Runtime.Environments;
using FinkFramework.Runtime.Settings.Loaders;
using FinkFramework.Runtime.Utils;
using Unity.Plastic.Newtonsoft.Json.Linq;
using UnityEditor;

namespace FinkFramework.Editor.Utils
{
    public static class UpdateCheckUtil
    {
        // GitHub 最新正式 Release 接口。
        private const string VersionUrl = "https://api.github.com/repos/finkkk/Fink-Framework/releases/latest";
        private const string ReleasesUrl = "https://github.com/finkkk/Fink-Framework/releases";

        private const string LastCheckKey = "FinkFramework_LastUpdateCheck";
        
        private static readonly HttpClient Client = CreateHttpClient();

        private static bool _checking;

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
        private static void CheckUpdateOnLoad() => _ = CheckUpdateAsync(false);
        
        // 手动触发：不需要检查间隔、不写入 EditorPrefs
        public static void CheckUpdateManual() => _ = CheckUpdateAsync(true);
        
        private static async Task CheckUpdateAsync(bool isManual)
        {
            if (_checking) return;
            _checking = true;
            
            try
            {
                if (!GlobalSettingsRuntimeLoader.TryGet(out var settings))
                    return; // 首次导入时不报错，直接跳过检查

                // 开关：关闭则不检查
                if (!settings.EnableUpdateCheck && !isManual)
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
                    LogUtil.Info("版本检查", "开始检查 Fink Framework 更新...");
                }
                
                // GitHub 返回 Release 元数据，版本号位于 tag_name，例如 v0.3.9。
                string json = await Client.GetStringAsync(VersionUrl);
                var data = JObject.Parse(json);

                string latestVersion = NormalizeVersion(data["tag_name"]?.ToString());
                string releaseUrl = data["html_url"]?.ToString() ?? ReleasesUrl;
                string currentVersion = NormalizeVersion(EnvironmentState.FrameworkVersion);

                if (!TryParseVersion(currentVersion, out var current) ||
                    !TryParseVersion(latestVersion, out var latest))
                {
                    if (isManual)
                        LogUtil.Error("版本检查", $"版本号格式无法解析：本地 {currentVersion}，GitHub {latestVersion}");
                    return;
                }

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

                string packageUrl = FindPackageUrl(data, latestVersion);
                if (string.IsNullOrEmpty(packageUrl))
                {
                    LogUtil.Warn("版本更新检查",
                        $"发现新版本 {latestVersion}，但该 Release 未提供 FinkFramework-v{latestVersion}.unitypackage。\n更新地址：{releaseUrl}");
                    return;
                }

                if (isManual)
                {
                    EditorApplication.delayCall += () => ShowUpdateDialog(currentVersion, latestVersion, packageUrl);
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
            }
        }

        private static string FindPackageUrl(JObject release, string version)
        {
            string expectedName = $"FinkFramework-v{version}.unitypackage";
            JArray assets = release["assets"] as JArray;
            if (assets == null) return null;
            foreach (JToken asset in assets)
            {
                string name = asset["name"]?.ToString();
                if (string.Equals(name, expectedName, StringComparison.OrdinalIgnoreCase))
                    return asset["browser_download_url"]?.ToString();
            }
            return null;
        }

        private static void ShowUpdateDialog(string currentVersion, string latestVersion, string packageUrl)
        {
            bool confirmed = EditorUtility.DisplayDialog("Fink Framework 版本更新",
                $"发现新版本：{currentVersion} → {latestVersion}\n\n" +
                "更新会覆盖 Assets/FinkFramework 内的框架源码、Editor 工具、内置资源和插件文件。对这些文件的本地修改将会丢失；框架配置、数据文件和项目生成文件不会被修改。\n\n" +
                "更新前会自动创建备份，更新失败将尝试恢复。",
                "立即更新", "暂不更新");
            if (confirmed) _ = DownloadAndUpdateAsync(packageUrl, latestVersion);
        }

        private static async Task DownloadAndUpdateAsync(string packageUrl, string version)
        {
            const string packagePath = "Library/FinkFrameworkUpdate.unitypackage";
            try
            {
                LogUtil.Info("框架更新", "正在下载更新包...");
                using var downloadClient = CreateHttpClient();
                downloadClient.Timeout = TimeSpan.FromMinutes(10);
                byte[] package = await downloadClient.GetByteArrayAsync(packageUrl);
                if (package == null || package.Length == 0) throw new InvalidOperationException("下载的更新包为空。");
                await System.IO.File.WriteAllBytesAsync(packagePath, package);
                EditorApplication.delayCall += () => FrameworkPackageUpdater.Start(packagePath, version);
            }
            catch (Exception ex)
            {
                FrameworkPackageUpdater.DeleteDownloadedPackage(packagePath);
                LogUtil.Error("框架更新", "下载更新包失败：" + ex.Message);
            }
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
