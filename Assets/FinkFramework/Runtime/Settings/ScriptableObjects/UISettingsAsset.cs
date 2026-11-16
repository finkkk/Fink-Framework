using FinkFramework.Runtime.Environments;
using UnityEngine;

namespace FinkFramework.Runtime.Settings.ScriptableObjects
{
    /// <summary>UI 主画布、面板加载来源和生成目录配置；资源后端通过参数显式传入。</summary>
    [CreateAssetMenu(fileName = "UISettingsAsset", menuName = "Fink Framework/Settings/UI Settings")]
    public sealed class UISettingsAsset : ScriptableObject
    {
        #region ===== UI配置 =====

        [Header("Main Surface 渲染模式")]
        [Tooltip("只控制框架默认 Main Surface。场景中的世界空间 UI 请使用独立 Canvas + UISurfaceRoot。")]
        public EnvironmentState.UIMode CurrentUIMode = EnvironmentState.UIMode.Auto;

        [Header("使用自定义 Main Canvas")]
        [Tooltip("开启后使用指定预制体的缩放与排序等设置；渲染模式仍由 Main Surface 渲染模式决定。")]
        public bool UseCustomMainCanvas = false;

        [Tooltip("预制体根对象必须带有 Canvas；已有层节点会复用，缺失层节点由框架补齐。")]
        public GameObject CustomMainCanvasPrefab;

        [Header("Main Canvas 屏幕缩放模式")]
        public UnityEngine.UI.CanvasScaler.ScaleMode UICanvasScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;

        [Tooltip("UI 的设计分辨率，仅在随屏幕尺寸缩放时生效。")]
        public Vector2 UIReferenceResolution = new(1920f, 1080f);

        public UnityEngine.UI.CanvasScaler.ScreenMatchMode UIScreenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;

        [Range(0f, 1f)]
        [Tooltip("0 匹配宽度，1 匹配高度；仅在 MatchWidthOrHeight 模式生效。")]
        public float UIMatchWidthOrHeight = 0f;

        [Min(0.01f)]
        [Tooltip("仅在固定像素尺寸模式生效。")]
        public float UIScaleFactor = 1f;

        [Min(0.01f)]
        public float UIReferencePixelsPerUnit = 100f;

        public UnityEngine.UI.CanvasScaler.Unit UIPhysicalUnit =
            UnityEngine.UI.CanvasScaler.Unit.Points;

        [Min(1f)]
        public float UIFallbackScreenDPI = 96f;

        [Min(1f)]
        public float UIDefaultSpriteDPI = 96f;

        [Header("Main Canvas 渲染排序")]
        [Tooltip("Sorting Layer 的 ID，只应用于框架默认 Main Canvas。")]
        public int UICanvasSortingLayerId = 0;

        public int UICanvasSortingOrder = 0;

        [Min(0.01f)]
        [Tooltip("仅在 Screen Space - Camera 模式生效，必须位于 UI 相机裁剪范围内。")]
        public float UICanvasPlaneDistance = 100f;

        [Tooltip("仅在屏幕空间模式生效。")]
        public bool UICanvasPixelPerfect = false;

        [Header("按输入设备自动管理导航交互")]
        [Tooltip("开启后，设备输入检测管理器会在 PC 键鼠或触摸输入时关闭导航交互、在手柄输入时重新开启。"
                 + "此功能依赖设备输入检测已启用；关闭此项后不再自动切换，导航交互始终保持开启。")]
        public bool EnableAutoNavigationInteractionByInputDevice = true;

        [Header("UI 面板加载")]
        [Tooltip("Resources：使用原有面板目录；跟随资源后端：按面板类名加载；自定义：由运行时地址解析器提供路径。")]
        public FinkFramework.Runtime.UI.UIPanelLoadSource UIPanelLoadSource =
            FinkFramework.Runtime.UI.UIPanelLoadSource.Resources;

        [Tooltip("跟随 AssetBundle 后端时使用的 UI 包名，路径为 ab://包名/面板类名。包名不能包含斜杠。")]
        public string UIPanelAssetBundleName = "ui";

        [Tooltip("Addressables 面板自动加入的资源组。组不存在时由生成器创建。")]
        public string UIPanelAddressablesGroup = "UI Panels";

        [Tooltip("非 Resources 面板的 Assets 下相对输出目录。切换来源不会移动已有资源。")]
        public string UIPanelExternalPrefabOutputRelativePath = DefaultUIPanelExternalPrefabOutputRelativePath;

        public const string DefaultUIPanelExternalPrefabOutputRelativePath = "GameAssets/UI/Panels";

        /// <summary>根据显式传入的资源后端判断面板是否走 Resources。</summary>
        /// <param name="backend">当前选中的资源后端。</param>
        /// <returns>面板使用 Resources 加载时返回 true。</returns>
        public bool UsesResources(EnvironmentState.ResourceBackendType backend) =>
            UIPanelLoadSource == FinkFramework.Runtime.UI.UIPanelLoadSource.Resources
            || UIPanelLoadSource == FinkFramework.Runtime.UI.UIPanelLoadSource.FollowResourceBackend
                && backend == EnvironmentState.ResourceBackendType.None;

        /// <summary>校验当前来源使用的生成目录；资源后端由调用方提供。</summary>
        /// <param name="backend">当前资源后端。</param>
        /// <param name="path">解析出的 Assets 路径。</param>
        /// <param name="error">失败原因。</param>
        /// <returns>输出目录有效时返回 true。</returns>
        public bool TryGetUIPanelPrefabOutputPath(EnvironmentState.ResourceBackendType backend, out string path, out string error)
        {
            path = string.Empty;
            if (UsesResources(backend))
            {
                if (!TryNormalizeUIPanelPrefabOutputRelativePath(UIPanelPrefabOutputRelativePath,
                        out string relative, out error))
                    return false;
                path = "Assets/" + relative;
                return true;
            }

            if (!TryNormalizeUIPanelExternalPrefabOutputRelativePath(UIPanelExternalPrefabOutputRelativePath,
                    out string external, out error))
                return false;
            path = "Assets/" + external;
            return true;
        }

        public static bool TryNormalizeUIPanelExternalPrefabOutputRelativePath(
            string value, out string normalized, out string error)
        {
            string raw = value?.Trim().Replace('\\', '/');
            if (raw != null && raw.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
                raw = raw.Substring("Assets/".Length);
            if (!TryNormalizeRelativeFolderPath(raw, DefaultUIPanelExternalPrefabOutputRelativePath,
                    "非 Resources 预制体输出目录", out normalized, out error))
                return false;
            foreach (string segment in normalized.Split('/'))
            {
                if (string.Equals(segment, "Resources", System.StringComparison.OrdinalIgnoreCase)
                    || string.Equals(segment, "Editor", System.StringComparison.OrdinalIgnoreCase)
                    || string.Equals(segment, "StreamingAssets", System.StringComparison.OrdinalIgnoreCase))
                {
                    error = "非 Resources 面板目录不能位于 Resources、Editor 或 StreamingAssets 下。";
                    return false;
                }
            }
            return true;
        }

        [Header("UI 面板脚本输出目录")]
        [Tooltip("全局脚本根目录后的相对目录，例如 UI/Panels。")]
        public string UIPanelScriptOutputSuffix = DefaultUIPanelScriptOutputSuffix;

        [Header("UI 面板预制体输出目录")]
        [Tooltip("Assets 后的相对目录，例如 Resources/UI/Panels。必须位于 Resources 目录下。")]
        public string UIPanelPrefabOutputRelativePath = DefaultUIPanelPrefabOutputRelativePath;

        public const string DefaultUIPanelScriptOutputSuffix = "UI/Panels";
        public const string DefaultUIPanelPrefabOutputRelativePath = "Resources/UI/Panels";

        /// <summary>
        /// 返回全局脚本根目录后的 UI 面板目录。支持从全局脚本根目录下的完整 Assets 路径提取后缀。
        /// </summary>
        public static string NormalizeUIPanelScriptOutputSuffix(string value, string scriptRootDirectory)
        {
            return TryNormalizeUIPanelScriptOutputSuffix(
                    value,
                    scriptRootDirectory,
                    out string normalized,
                    out _)
                ? normalized
                : DefaultUIPanelScriptOutputSuffix;
        }

        public static bool TryNormalizeUIPanelScriptOutputSuffix(
            string value,
            string scriptRootDirectory,
            out string normalized,
            out string error)
        {
            string raw = string.IsNullOrWhiteSpace(value)
                ? DefaultUIPanelScriptOutputSuffix
                : value.Trim().Replace('\\', '/').Trim('/');
            string scriptRoot = GlobalSettingsAsset.NormalizeScriptRootDirectory(scriptRootDirectory);
            string legacyPrefix = $"Assets/{scriptRoot}";

            if (raw.StartsWith(legacyPrefix + "/", System.StringComparison.OrdinalIgnoreCase))
                raw = raw.Substring(legacyPrefix.Length + 1);
            else if (raw.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
            {
                normalized = string.Empty;
                error = $"脚本输出目录固定从 {legacyPrefix}/ 开始，输入框中只填写后续目录。";
                return false;
            }

            return TryNormalizeRelativeFolderPath(
                raw,
                DefaultUIPanelScriptOutputSuffix,
                "脚本输出目录",
                out normalized,
                out error);
        }

        /// <summary>
        /// 返回 Assets 后的 UI 预制体相对目录。支持从全局脚本根目录下的完整 Assets 路径提取后缀。
        /// </summary>
        public static string NormalizeUIPanelPrefabOutputRelativePath(string value)
        {
            return TryNormalizeUIPanelPrefabOutputRelativePath(value, out string normalized, out _)
                ? normalized
                : DefaultUIPanelPrefabOutputRelativePath;
        }

        public static bool TryNormalizeUIPanelPrefabOutputRelativePath(
            string value,
            out string normalized,
            out string error)
        {
            string raw = string.IsNullOrWhiteSpace(value)
                ? DefaultUIPanelPrefabOutputRelativePath
                : value.Trim().Replace('\\', '/').Trim('/');
            if (raw.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
                raw = raw.Substring("Assets/".Length);

            if (!TryNormalizeRelativeFolderPath(
                    raw,
                    DefaultUIPanelPrefabOutputRelativePath,
                    "预制体输出目录",
                    out normalized,
                    out error))
                return false;

            string[] segments = normalized.Split('/');
            int resourcesIndex = FindResourcesSegmentIndex(segments);
            if (resourcesIndex < 0 || resourcesIndex == segments.Length - 1)
            {
                error = $"预制体输出目录必须位于 Resources 的子目录中，例如：{DefaultUIPanelPrefabOutputRelativePath}";
                return false;
            }

            return true;
        }

        /// <summary>
        /// 返回可用于 AssetDatabase 和文件系统 API 的完整 UI 脚本输出目录。
        /// </summary>
        public static string GetUIPanelScriptOutputPath(string scriptRootDirectory, string outputSuffix)
        {
            string scriptRoot = GlobalSettingsAsset.NormalizeScriptRootDirectory(scriptRootDirectory);
            string suffix = NormalizeUIPanelScriptOutputSuffix(outputSuffix, scriptRoot);
            return $"Assets/{scriptRoot}/{suffix}";
        }

        /// <summary>
        /// 返回可用于 AssetDatabase 和文件系统 API 的完整 UI 预制体输出目录。
        /// </summary>
        public static string GetUIPanelPrefabOutputPath(string relativePath)
        {
            return $"Assets/{NormalizeUIPanelPrefabOutputRelativePath(relativePath)}";
        }

        /// <summary>
        /// 把预制体目录转换为 Resources.Load 使用的相对路径。
        /// </summary>
        public static string GetUIPanelResourcesPath(string relativePath)
        {
            string normalized = NormalizeUIPanelPrefabOutputRelativePath(relativePath);
            string[] segments = normalized.Split('/');
            int resourcesIndex = FindResourcesSegmentIndex(segments);
            return string.Join("/", segments, resourcesIndex + 1, segments.Length - resourcesIndex - 1);
        }

        private static bool TryNormalizeRelativeFolderPath(
            string value,
            string defaultValue,
            string displayName,
            out string normalized,
            out string error)
        {
            normalized = string.Empty;
            error = string.Empty;

            string raw = string.IsNullOrWhiteSpace(value)
                ? defaultValue
                : value.Trim().Replace('\\', '/').Trim('/');

            if (System.IO.Path.IsPathRooted(raw))
            {
                error = $"{displayName}只能填写相对目录，例如：{defaultValue}";
                return false;
            }

            string[] segments = raw.Split('/');
            for (int i = 0; i < segments.Length; i++)
            {
                string segment = segments[i].Trim();
                if (string.IsNullOrWhiteSpace(segment) || segment == "." || segment == "..")
                {
                    error = $"{displayName}不能包含空目录、. 或 ..。";
                    return false;
                }

                if (segment.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0)
                {
                    error = $"{displayName}包含非法目录名：{segment}";
                    return false;
                }

                segments[i] = segment;
            }

            normalized = string.Join("/", segments);
            return true;
        }

        private static int FindResourcesSegmentIndex(string[] segments)
        {
            for (int i = 0; i < segments.Length; i++)
            {
                if (string.Equals(segments[i], "Resources", System.StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }

        #endregion
    }
}
