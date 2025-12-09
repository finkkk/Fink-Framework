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

        [InitializeOnLoadMethod]
        private static void CheckUpdate() => _ = CheckUpdateAsync();
        
        private static async Task CheckUpdateAsync()
        {
            var settings = GlobalSettings.Current;

            // 开关：关闭则不检查
            if (!settings.EnableUpdateCheck)
                return;

            // 是否到了检查日期
            var lastTicks = EditorPrefs.GetString(LastCheckKey, "");
            if (DateTime.TryParse(lastTicks, out DateTime lastTime))
            {
                if ((DateTime.Now - lastTime).TotalDays < settings.UpdateCheckIntervalDays)
                    return; // 未到检查时间
            }

            // 记录本次检查时间
            EditorPrefs.SetString(LastCheckKey, DateTime.Now.ToString(CultureInfo.InvariantCulture));

            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "Fink-Framework");

                // 从你的服务器下载 version.json
                string json = await client.GetStringAsync(VersionUrl);
                var data = JObject.Parse(json);

                string latestVersion = data["latest"]?.ToString();
                const string currentVersion = EnvironmentState.FrameworkVersion;

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
            catch (Exception ex)
            {
                LogUtil.Error("UpdateCheck", ex.Message);
            }
        }
    }
}
