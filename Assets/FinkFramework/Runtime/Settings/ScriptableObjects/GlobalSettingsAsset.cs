using FinkFramework.Runtime.Localization;
using UnityEngine;

namespace FinkFramework.Runtime.Settings.ScriptableObjects
{
    /// <summary>框架基础配置和模块配置入口；模块详细参数由各自的 SO 保存。</summary>
    public class GlobalSettingsAsset : ScriptableObject
    {
        #region ===== 框架配置 =====

        [Header("是否启用版本更新检查")]
        [Tooltip("若为 false，则编辑器不会自动检查 GitHub 是否有新版本。")]
        public bool EnableUpdateCheck = true;

        [Header("版本检查间隔（天）")]
        [Tooltip("编辑器多久检查一次更新。默认 1 天检查一次。")]
        public int UpdateCheckIntervalDays = 1;


        [Header("是否强制关闭 XR")]
        [Tooltip("是否强制关闭 XR（比自动检测优先级更高）。若为 true，则即使已安装 XRI 也会禁用 XR 相关系统。若为 false，则按自动检测结果处理。")]
        public bool ForceDisableXR = false;

        [Header("是否强制关闭 新输入系统")]
        [Tooltip("是否强制关闭 新输入系统（比自动检测优先级更高）。若为 true，则即使已安装 Input System 也会禁用 Input System 相关系统。若为 false，则按自动检测结果处理。")]
        public bool ForceDisableNewInputSystem = false;

        [Header("是否强制关闭 URP")]
        [Tooltip("是否强制关闭 URP（比自动检测优先级更高）。若为 true，则即使已安装 URP 也会禁用 URP 相关系统。若为 false，则按自动检测结果处理。")]
        public bool ForceDisableURP = false;

        [Header("是否启用 编辑器加载 打包检测")]
        [Tooltip("若为 true，则在构建前扫描 C# 脚本，若存在 editor:// 路径，将阻止打包。")]
        public bool EnableEditorUrlCheck = true;

        [Header("是否启用 音频配置 打包检测")]
        [Tooltip("构建前检查音频 Mixer 和输出分组。仅控制构建检查，不改变音频模块开关或运行时配置校验。")]
        public bool EnableAudioBuildCheck = true;

        [Header("全局脚本目录")]
        [Tooltip("所有框架脚本生成器使用的 Assets 下相对目录。固定以 Assets/ 开头，默认填写 Scripts，最终路径为 Assets/Scripts。")]
        public string ScriptRootDirectory = DefaultScriptRootDirectory;

        /// <summary>
        /// 全局脚本根目录的默认值。该目录是 Assets 后面的相对部分。
        /// </summary>
        public const string DefaultScriptRootDirectory = "Scripts";

        /// <summary>
        /// 将配置中的脚本根目录规范化为 Assets 后的相对路径。
        /// </summary>
        public static string NormalizeScriptRootDirectory(string value)
        {
            return TryNormalizeScriptRootDirectory(value, out string normalized, out _)
                ? normalized
                : DefaultScriptRootDirectory;
        }

        /// <summary>
        /// 校验并规范化全局脚本根目录。
        /// </summary>
        public static bool TryNormalizeScriptRootDirectory(
            string value,
            out string normalized,
            out string error)
        {
            normalized = string.Empty;
            error = string.Empty;

            string raw = (value ?? string.Empty).Trim().Replace('\\', '/');
            if (raw.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
                raw = raw.Substring("Assets/".Length);
            else if (string.Equals(raw, "Assets", System.StringComparison.OrdinalIgnoreCase))
                raw = string.Empty;

            raw = raw.Trim('/');
            if (string.IsNullOrWhiteSpace(raw))
            {
                error = "脚本根目录不能为空，请填写 Assets/ 后面的目录，例如 Scripts。";
                return false;
            }

            if (System.IO.Path.IsPathRooted(raw) || raw.StartsWith("/", System.StringComparison.Ordinal))
            {
                error = "这里只能填写 Assets/ 后面的相对目录，不能填写绝对路径。";
                return false;
            }

            string[] segments = raw.Split('/');
            for (int i = 0; i < segments.Length; i++)
            {
                string segment = segments[i].Trim();
                if (string.IsNullOrWhiteSpace(segment) || segment == "." || segment == "..")
                {
                    error = "脚本根目录不能包含空目录、. 或 ..。";
                    return false;
                }

                if (segment.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0)
                {
                    error = $"脚本根目录包含非法目录名：{segment}";
                    return false;
                }

                segments[i] = segment;
            }

            normalized = string.Join("/", segments);
            return true;
        }

        #endregion

        #region ===== 模块配置引用 =====

        [Header("数据管线配置")]
        public DataSettingsAsset DataSettings;

        [Header("存档配置")]
        public SaveSettingsAsset SaveSettings;

        [Header("输入配置")]
        public InputSettingsAsset InputSettings;

        [Header("UI配置")]
        public UISettingsAsset UISettings;

        [Header("资源后端配置")]
        public ResourceBackendSettingsAsset ResourceBackendSettings;

        [Header("音频配置")]
        public AudioSettingsAsset AudioSettings;

        [Header("本地化配置")]
        public LocalizationSettingsAsset LocalizationSettings;

        #endregion
    }
}
