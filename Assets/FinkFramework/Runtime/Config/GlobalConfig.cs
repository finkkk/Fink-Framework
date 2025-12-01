// ReSharper disable ConvertToConstant.Global
#pragma warning disable CS0162 // 检测到不可到达的代码
namespace FinkFramework.Runtime.Config
{
    /// <summary>
    /// 全局框架配置（Framework Global Settings）
    /// ------------------------------------------------------------
    /// 用于控制框架内部的调试行为、模块开关、UI 渲染模式以及数据加密信息等功能。
    /// 建议根据项目需求在启动时统一设置。
    ///
    /// 本类为静态配置，不建议在运行时频繁修改。
    /// ------------------------------------------------------------
    /// </summary>
    public static class GlobalConfig
    {
        #region 调试相关（Debug Settings）
        
        /// <summary>
        /// 是否启用框架调试模式。
        /// 在调试模式下：
        /// - 对象池会启用调试信息（例如自动归类池、可视化检查等）
        /// - 日志输出更详细
        /// - 某些运行时检查更严格
        ///
        /// 默认行为：编辑器与开发构建（Development Build）自动启用，
        /// 正式发布构建自动关闭。
        /// </summary>
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public const bool DebugMode = true;
#else
        public const bool DebugMode = false;
#endif
        
        #endregion
        
        #region 环境配置（Environment Settings）

        /// <summary>
        /// 是否强制关闭 XR（比自动检测优先级更高）
        /// - 若为 true，则即使已安装 XRI 也会禁用 XR 相关系统
        /// - 若为 false，则按自动检测结果处理
        /// </summary>
        public static bool ForceDisableXR = false;

        /// <summary>
        /// 自动检测：是否导入了 XRI（com.unity.xr.interaction.toolkit）
        /// </summary>
#if ENABLE_XRI
        public const bool AutoXR = true;
#else
        public const bool AutoXR = false;
#endif

        /// <summary>
        /// 最终 VR 模式 = 自动检测 XR 是否存在 且 未被强制关闭
        /// </summary>
        public static bool FinalIsVR => AutoXR && !ForceDisableXR;



        /// <summary>
        /// 是否强制关闭新输入系统（比自动检测优先级更高）
        /// - 若为 true，则即使安装了 InputSystem 也用旧输入系统
        /// - 若为 false，则按自动检测结果处理
        /// </summary>
        public static bool ForceDisableNewInputSystem = false;

        /// <summary>
        /// 自动检测：是否导入了 Unity 新输入系统
        /// </summary>
#if ENABLE_INPUT_SYSTEM
        public const bool AutoNewInputSystem = true;
#else
        public const bool AutoNewInputSystem = false;
#endif

        /// <summary>
        /// 最终输入系统 = 自动检测结果 且 未被强制关闭
        /// </summary>
        public static bool FinalUseNewInputSystem => AutoNewInputSystem && !ForceDisableNewInputSystem;

        #endregion
        
        #region 渲染管线配置（Render Pipeline Settings）

        /// <summary>
        /// 是否强制关闭 URP（优先级高于自动检测）。
        /// 如果为 true，即使项目使用 URP 也按非 URP 处理。
        /// </summary>
        public static bool ForceDisableURP = false;

        /// <summary>
        /// 自动检测：当前项目是否启用了 URP。
        /// 根据 UNITY_RENDER_PIPELINE_URP 宏判断。
        /// </summary>
#if UNITY_RENDER_PIPELINE_URP
public const bool AutoURP = true;
#else
        public const bool AutoURP = false;
#endif

        /// <summary>
        /// 最终 URP 判定（优先级：ForceDisableURP > AutoURP）
        /// </summary>
        public static bool FinalUseURP => AutoURP && !ForceDisableURP;

        #endregion
        
        #region UI 系统设置（UI System Settings）
        
        /// <summary>
        /// UI 渲染模式。
        /// 不同渲染管线（内置 / URP）与不同空间模式（ScreenSpace / WorldSpace）可独立选择。
        /// </summary>
        public enum UIMode
        {
            /// <summary>
            /// 普通 屏幕 UI
            /// </summary>
            ScreenSpace, 
            /// <summary>
            /// 空间 3D  UI
            /// </summary>
            WorldSpace,  
            /// <summary>
            /// 自动判断（默认）         VR → WorldSpace / 非VR → ScreenSpace
            /// </summary>
            Auto            
        }

        /// <summary>
        /// 当前 UI 渲染模式。
        /// 默认使用 ScreenSpace-Camera。
        /// 如果项目为 VR，请务必使用 WorldSpace 模式。
        /// </summary>
        public const UIMode CurrentUIMode = UIMode.Auto;
        
        #endregion

        #region 加密与数据存储（Encryption / Storage）
        
        /// <summary>
        /// 框架生成的加密数据文件的后缀名。
        /// 用于存档、配置文件、数据表等加密存储。
        /// </summary>
        public const string ENCRYPTED_FILE_EXTENSION = ".fink";
        
        /// <summary>
        /// AES 加密使用的密钥（请务必根据项目需求自行修改）。
        /// 注意：不要在正式线上版本中使用简单字符串，建议将密钥外部化或混淆处理。
        /// </summary>
        public static readonly string PASSWORD = string.Concat("fink", "kk"); 
        #endregion
        
    }
}