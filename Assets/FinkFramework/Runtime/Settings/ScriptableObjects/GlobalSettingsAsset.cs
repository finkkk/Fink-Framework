using FinkFramework.Runtime.Environments;
using FinkFramework.Runtime.Utils;
using UnityEngine;
using UnityEngine.Serialization;

namespace FinkFramework.Runtime.Settings.ScriptableObjects
{
    /// <summary>
    /// 全局配置 SO 文件（唯一存在）
    /// </summary>
    public class GlobalSettingsAsset : ScriptableObject
    {
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
        
        #region ===== UI配置 =====
        
        [Header("当前 UI 渲染模式")]
        [Tooltip("当前 UI 渲染模式。默认使用 ScreenSpace-Camera。如果项目为 VR，请务必使用 WorldSpace 模式。")]
        public EnvironmentState.UIMode CurrentUIMode = EnvironmentState.UIMode.Auto;
        
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
        
        #region ===== 加密配置 =====

        [Header("是否全局开启加密")]
        [Tooltip("true：所有数据文件都加密 false：所有数据文件都不加密")]
        public bool EnableEncryption = true;
        
        [Header("AES 加密使用的密钥")]
        [Tooltip("AES 加密使用的密钥（请务必根据项目需求自行修改）。注意：不建议在正式线上版本中使用简单字符串，建议将密钥外部化或混淆处理。")]
        public string Password = "finkkk";
        
        [Header("框架生成的加密数据文件的后缀名")]
        [Tooltip("框架生成的加密数据文件的后缀名。用于存档、配置文件、数据表等加密存储。")]
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
