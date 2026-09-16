using FinkFramework.Editor.Modules.Localization;
using FinkFramework.Runtime.Localization;
using FinkFramework.Runtime.Utils;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace FinkFramework.Editor.Utils
{
    /// <summary>
    /// 构建前校验本地化配置、语言文件和运行时清单，避免 Player 中缺少翻译数据。
    /// </summary>
    internal sealed class LocalizationBuildCheckUtil : IPreprocessBuildWithReport
    {
        public int callbackOrder => 2;

        public void OnPreprocessBuild(BuildReport report)
        {
            LocalizationSettingsAsset settings = AssetDatabase.LoadAssetAtPath<LocalizationSettingsAsset>(
                LocalizationPath.SettingsAssetPath);
            if (settings == null || !settings.EnableLocalization)
            {
                LogUtil.Info(
                    "LocalizationBuildCheckUtil",
                    settings == null
                        ? "未找到本地化配置，跳过本地化构建校验。"
                        : "本地化模块已关闭，跳过本地化构建校验。");
                return;
            }

            if (!LocalizationDataSyncUtility.Sync(settings, out string syncMessage, false))
                throw new BuildFailedException("构建前本地化同步失败：" + syncMessage);

            if (LocalizationDataSyncUtility.ValidateSynchronizedCopy(settings, out string message))
            {
                LogUtil.Info("LocalizationBuildCheckUtil", message);
                return;
            }

            throw new BuildFailedException("构建失败：" + message);
        }
    }
}
