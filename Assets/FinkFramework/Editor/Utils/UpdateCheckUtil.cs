using System;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;
using FinkFramework.Runtime.Environments;
using FinkFramework.Runtime.Settings;
using FinkFramework.Runtime.Utils;
using Unity.Plastic.Newtonsoft.Json.Linq;
using UnityEditor;

namespace FinkFramework.Editor.Utils
{
    public static class UpdateCheckUtil
    {
        // 使用你自己的服务器 JSON 文件
        private const string VersionUrl = "https://finkkk.cn/upload/version.json";

        private const string LastCheckKey = "FinkFramework_LastUpdateCheck";
        
        private static readonly HttpClient Client = new()
        {
            Timeout = TimeSpan.FromSeconds(5)
        };

        private static bool _checking;

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
                if (!GlobalSettings.TryGet(out var settings))
                    return; // 首次导入时不报错，直接跳过检查

                // 开关：关闭则不检查
                if (!settings.EnableUpdateCheck && !isManual)
                    return;
                
                // 自动检查才需要检查间隔
                if (!isManual)
                {
                    string last = EditorPrefs.GetString(LastCheckKey, "");
                    if (DateTime.TryParse(last, out DateTime lastTime) &&
                        (DateTime.Now - lastTime).TotalDays < settings.UpdateCheckIntervalDays)
                        return;

                    // 自动检查才更新 lastCheck
                    EditorPrefs.SetString(LastCheckKey, DateTime.Now.ToString(CultureInfo.InvariantCulture));
                }
                
                if (isManual)
                {
                    LogUtil.Info("版本检查", "开始检查 Fink Framework 更新...");
                }
                
                if (!Client.DefaultRequestHeaders.UserAgent.ToString().Contains("Fink-Framework"))
                {
                    Client.DefaultRequestHeaders.UserAgent.ParseAdd("Fink-Framework");
                }

                // 从你的服务器下载 version.json
                string json = await Client.GetStringAsync(VersionUrl);
                var data = JObject.Parse(json);

                string latestVersion = data["latest"]?.ToString();
                const string currentVersion = EnvironmentState.FrameworkVersion;

                if (latestVersion == currentVersion && isManual)
                {
                    LogUtil.Success("版本检查", $"当前版本已是最新版本：{currentVersion}");
                    return;
                }
                // 有新版本 → 提示
                if (!string.IsNullOrEmpty(latestVersion) && latestVersion != currentVersion)
                {
                    LogUtil.Warn(
                        "版本更新检查",
                        $"Fink Framework 有新版本！当前：{currentVersion} → 最新：{latestVersion}\n" +
                        $"更新地址：https://github.com/finkkk/Fink-Framework/releases"
                    );
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
                LogUtil.Error("UpdateCheck", ex.Message);
            }
            finally
            {
                _checking = false;
            }
        }
    }
}
