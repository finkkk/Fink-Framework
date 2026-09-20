using FinkFramework.Runtime.Environments;
using FinkFramework.Runtime.Utils;
using FinkFramework.Runtime.Input;
using UnityEngine;
using UnityEngine.Serialization;

namespace FinkFramework.Runtime.Settings.ScriptableObjects
{
    /// <summary>
    /// 全局配置 SO 文件（唯一存在）
    /// </summary>
    public class GlobalSettingsAsset : ScriptableObject
    {
        /// <summary>存档系统默认使用的二进制文件后缀。</summary>
        public const string DefaultSaveBinaryExtension = ".sav";

        #region ===== 框架配置 =====

        [Header("是否启用版本更新检查")]
        [Tooltip("若为 false，则编辑器不会自动检查 GitHub 是否有新版本。")]
        public bool EnableUpdateCheck = true;

        [Header("版本检查间隔（天）")]
        [Tooltip("编辑器多久检查一次更新。默认 0 天检查一次。")]
        public int UpdateCheckIntervalDays = 1;
        
        [Header("是否启用音效模块")]
        [Tooltip("若为 false，则音效模块将完全禁用：不会初始化 AudioManager，不加载音频资源，也不会播放任何音乐或音效。")]
        public bool EnableAudioModule = true;

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

        #region ===== 输入配置 =====

        [Header("启用设备输入检测")]
        [Tooltip("关闭后，框架不再判断当前主要输入设备，所有订阅者都会回到 Unknown 状态。")]
        public bool EnableDeviceDetection = true;

        [Header("鼠标移动视为键鼠输入")]
        [Tooltip("关闭后，鼠标按键仍会被识别为键鼠输入，但单纯移动鼠标不会改变当前主要输入设备。")]
        public bool TreatMouseMovementAsKeyboardMouseInput = true;

        [Header("鼠标移动检测阈值（像素）")]
        [Tooltip("单帧鼠标移动距离达到此值时，才视为键鼠输入。较大值可减少轻微抖动造成的设备切换。")]
        [Min(0f)]
        public float MouseMovementDetectionThreshold = DefaultMouseMovementDetectionThreshold;

        public const float DefaultMouseMovementDetectionThreshold = 2f;

        [Header("绑定冲突处理模式")]
        [Tooltip("Warning 允许冲突绑定并由设置界面标红提示；Strict 拒绝冲突绑定。")]
        public InputConflictMode InputConflictMode = InputConflictMode.Warning;

        #endregion
        
        #region ===== UI配置 =====
        
        [Header("Main Surface 渲染模式")]
        [Tooltip("只控制框架默认 Main Surface。场景中的世界空间 UI 请使用独立 Canvas + UISurfaceRoot。")]
        public EnvironmentState.UIMode CurrentUIMode = EnvironmentState.UIMode.Auto;

        [Header("按输入设备自动管理导航交互")]
        [Tooltip("开启后，设备输入检测管理器会在 PC 键鼠或触摸输入时关闭导航交互、在手柄输入时重新开启。"
                 + "此功能依赖设备输入检测已启用；关闭此项后不再自动切换，导航交互始终保持开启。")]
        public bool EnableAutoNavigationInteractionByInputDevice = true;

        [Header("UI 面板脚本输出目录")]
        [Tooltip("全局脚本根目录后的相对目录，例如 UI/Panels。")]
        [FormerlySerializedAs("UIPanelScriptOutputPath")]
        public string UIPanelScriptOutputSuffix = DefaultUIPanelScriptOutputSuffix;

        [Header("UI 面板预制体输出目录")]
        [Tooltip("Assets 后的相对目录，例如 Resources/UI/Panels。必须位于 Resources 目录下。")]
        [FormerlySerializedAs("UIPanelPrefabOutputPath")]
        public string UIPanelPrefabOutputRelativePath = DefaultUIPanelPrefabOutputRelativePath;

        public const string DefaultUIPanelScriptOutputSuffix = "UI/Panels";
        public const string DefaultUIPanelPrefabOutputRelativePath = "Resources/UI/Panels";

        /// <summary>
        /// 返回全局脚本根目录后的 UI 面板目录。兼容迁移旧版保存的完整 Assets 路径。
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
            string scriptRoot = NormalizeScriptRootDirectory(scriptRootDirectory);
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
        /// 返回 Assets 后的 UI 预制体相对目录。兼容迁移旧版保存的完整 Assets 路径。
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
            string scriptRoot = NormalizeScriptRootDirectory(scriptRootDirectory);
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
        
        #region ===== 数据配置 =====
        
        [Header("运行时数据加载模式")]
        [Tooltip("Binary：使用加密二进制作为运行时数据源(Json依然导出)  Json：只使用 JSON 作为运行时数据源(二进制不导出)")]
        public EnvironmentState.DataLoadMode CurrentDataLoadMode = EnvironmentState.DataLoadMode.Binary;

        [Header("C# 数据类路径模式")]
        [Tooltip("选择生成的 C# 数据类输出到 Assets 内部还是项目外部目录。")]
        [FormerlySerializedAs("CSharpUseExternal")]
        public EnvironmentState.CSharpOutputPathMode CSharpPathMode =
            EnvironmentState.CSharpOutputPathMode.Internal;

        /// <summary>
        /// 兼容旧代码的访问方式。新代码请使用 CSharpPathMode。
        /// </summary>
        public bool CSharpUseExternal
        {
            get => CSharpPathMode == EnvironmentState.CSharpOutputPathMode.External;
            set => CSharpPathMode = value
                ? EnvironmentState.CSharpOutputPathMode.External
                : EnvironmentState.CSharpOutputPathMode.Internal;
        }

        [Header("启用自定义内部 C# 路径")]
        [Tooltip("关闭后使用内部默认路径；再次启用时保留之前填写的路径。")]
        public bool UseCustomInternalCSharpOutputPath = false;

        [Header("启用自定义外部 C# 路径")]
        [Tooltip("关闭后使用外部默认路径；再次启用时保留之前填写的路径。")]
        public bool UseCustomExternalCSharpOutputPath = false;

        [Header("内部 C# 输出后缀路径")]
        [Tooltip("全局脚本根目录后的模块相对路径。未启用自定义路径时使用 Data/AutoGen/DataClass。")]
        public string InternalCSharpOutputSuffix = "Data/AutoGen/DataClass";

        [Header("外部 C# 输出路径")]
        [Tooltip("项目相对路径，不能位于 Assets 目录内。默认：FinkFramework_Data/AutoGen/DataClass")]
        public string ExternalCSharpOutputPath = "FinkFramework_Data/AutoGen/DataClass";

        #endregion

        #region ===== 存档配置 =====

        [Header("存档数据格式")]
        [Tooltip("只控制存档系统。Json 使用 JSON 数据；Binary 使用 Odin 二进制数据。与数据管线的数据导出模式相互独立。")]
        public EnvironmentState.DataLoadMode SaveDataLoadMode = EnvironmentState.DataLoadMode.Binary;

        [Header("二进制存档文件后缀")]
        [Tooltip("只控制存档系统的二进制文件后缀，例如 .sav、.save、.bytes；与数据管线的二进制文件后缀相互独立。")]
        public string SaveBinaryExtension = DefaultSaveBinaryExtension;

        [Header("启用多槽位存档")]
        [Tooltip("关闭时固定使用 Slot 1，并禁用槽位创建、删除、枚举和选择接口。")]
        public bool MultiSlotMode = false;

        [Header("保留历史存档")]
        [Tooltip("开启后，每次成功覆盖主存档前都会保存上一代有效存档。")]
        public bool EnableSaveHistory = false;

        [Header("每个目标保留的历史存档数量")]
        [Tooltip("仅在启用历史存档时生效。Backup 不计入此数量。")]
        [Min(0)]
        public int SaveHistoryLimit = 5;

        [Header("压缩存档 Payload")]
        [Tooltip("仅 Binary 存档写盘前使用 GZip 压缩。JSON 模式为保持文件可读会忽略此项。")]
        public bool EnableSaveCompression = false;

        /// <summary>
        /// 将用户配置规范为安全的单段文件后缀。仅保留字母、数字、下划线和连字符；
        /// 空值或包含路径/通配符等非法字符时回退为 <see cref="DefaultSaveBinaryExtension"/>。
        /// </summary>
        /// <param name="value">带点或不带点的后缀文本。</param>
        /// <returns>以点开头的安全后缀。</returns>
        public static string NormalizeSaveBinaryExtension(string value)
        {
            string extension = (value ?? string.Empty).Trim();
            if (extension.StartsWith(".", System.StringComparison.Ordinal))
                extension = extension.Substring(1);

            if (string.IsNullOrEmpty(extension))
                return DefaultSaveBinaryExtension;

            for (int i = 0; i < extension.Length; i++)
            {
                char character = extension[i];
                if (!char.IsLetterOrDigit(character) && character != '_' && character != '-')
                    return DefaultSaveBinaryExtension;
            }

            return "." + extension;
        }

        #endregion
        
        #region ===== 加密配置 =====

        [Header("是否全局开启加密")]
        [Tooltip("数据管线与 Binary 存档是否使用 AES。JSON 存档为保持可读会始终保存为明文。")]
        public bool EnableEncryption = true;
        
        [Header("AES 加密使用的密钥")]
        [Tooltip("AES 加密使用的密钥（请务必根据项目需求自行修改）。注意：不建议在正式线上版本中使用简单字符串，建议将密钥外部化或混淆处理。")]
        public string Password = "finkkk";
        
        [Header("框架生成的加密数据文件的后缀名")]
        [Tooltip("数据管线生成的加密数据文件后缀名。存档系统的二进制后缀在存档配置中单独设置。")]
        public string EncryptedExtension = ".fink";
        
        #endregion

        #region ===== 资源配置 =====

        [Header("资源构建型后端")]
        [Tooltip("是否启用构建型资源系统（AB / Addressables 等）")]
        public EnvironmentState.ResourceBackendType ResourceBackend = EnvironmentState.ResourceBackendType.None;

        [Tooltip("AssetBundle 后端配置（仅当 ResourceBackend=AssetBundle 时使用）")]
        public AssetBundleBackendSettingsAsset AssetBundleSettings;

        [Tooltip("Addressables 后端配置（仅当 ResourceBackend=Addressables 时使用）")]
        public AddressablesBackendSettingsAsset AddressablesSettings;

        [Tooltip("自定义资源后端配置（仅当 ResourceBackend=Custom 时使用）")]
        public ScriptableObject CustomBackendSettings;

        #endregion
        
        /// <summary>
        /// 当用户在 Inspector 修改字段时自动同步到运行时
        /// </summary>
#if UNITY_EDITOR
        private void OnValidate()
        {
            // 检查是否存在多个实例
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:GlobalSettingsAsset");

            if (guids.Length > 1)
            {
                LogUtil.Error("FinkFramework","检测到多个 GlobalSettingsAsset，框架只允许存在一个全局设置的 SO 文件！");

                // 高级：可以自动删除重复的，但为了安全不建议立即删除
            }
        }
#endif
    }
}
