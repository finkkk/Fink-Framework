// ReSharper disable ConvertToConstant.Global
#pragma warning disable CS0162 // 检测到不可到达的代码
namespace FinkFramework.Runtime.Environments
{
        /// <summary>
        /// 全局框架环境状态 FrameworkEnvironment
        /// ------------------------------------------------------------
        /// 该类用于存储框架在运行时的环境状态（Environment State）。
        /// 包括：宏定义检测环境信息、全局设置提供可配置字段和枚举、自动计算最终环境状态等。
        /// 该类为 Runtime-only，不会进入构建之外的 Asset 系统。
        /// 用户不会直接修改此类，而是通过 ScriptableObject（GlobalSettingsAsset）
        /// 由编辑器在初始化时注入运行时值。
        /// ------------------------------------------------------------
        /// </summary>
    public static class EnvironmentState
    {
        #region 框架信息

        /// <summary>
        /// 框架版本号
        /// </summary>
        public const string FrameworkVersion = "0.1.0";

        #endregion
        
        
        #region 全局设置-可配置字段定义
        
        /// <summary>
        /// 是否强制关闭 XR。（由设置面板注入）
        /// 若为 true，则即使已安装 XRI 也会禁用 XR 相关系统若为 false，则按自动检测结果处理
        /// </summary>
        public static bool ForceDisableXR { get; internal set; }

        /// <summary>
        /// 是否强制关闭新输入系统。（由设置面板注入）
        /// 若为 true，则即使安装了 InputSystem 也用旧输入系统若为 false，则按自动检测结果处理
        /// </summary>
        public static bool ForceDisableNewInputSystem { get; internal set; }

        /// <summary>
        /// 是否强制关闭 URP。（由设置面板注入）
        /// 如果为 true，即使项目使用 URP 也按非 URP 处理。
        /// </summary>
        public static bool ForceDisableURP { get; internal set; }

        /// <summary>
        /// 当前 UI 渲染模式。（由设置面板注入）
        /// 默认使用 ScreenSpace-Camera。如果项目为 VR，请务必使用 WorldSpace 模式。
        /// </summary>
        public static UIMode CurrentUIMode { get; internal set; } = UIMode.Auto;

        /// <summary>
        /// 框架生成的加密数据文件的后缀名。（由设置面板注入）
        /// 用于存档、配置文件、数据表等加密存储。
        /// </summary>
        public static string ENCRYPTED_FILE_EXTENSION { get; internal set; } = ".fink";

        /// <summary>
        /// AES 加密使用的密钥。（由设置面板注入）
        /// 注意：请务必根据项目需求自行修改。不建议在正式线上版本中使用简单字符串，建议将密钥外部化或混淆处理。
        /// </summary>
        public static string PASSWORD { get; internal set; } = "finkkk";

        #endregion

        #region 环境信息-基于宏编译自动检测
        
        /// <summary>
        /// 自动检测：是否启用框架调试模式。对象池会启用调试信息、日志输出更详细、某些运行时检查更严格
        /// </summary>
        #if UNITY_EDITOR || DEVELOPMENT_BUILD
        public const bool DebugMode = true;
        #else
        public const bool DebugMode = false;
        #endif
            
        /// <summary>
        /// 自动检测：是否导入了 XRI（com.unity.xr.interaction.toolkit）
        /// </summary>
        #if ENABLE_XRI
        public const bool AutoXR = true;
        #else
        public const bool AutoXR = false;
        #endif
        
        /// <summary>
        /// 自动检测：是否导入了 Unity 新输入系统
        /// </summary>
        #if ENABLE_INPUT_SYSTEM
        public const bool AutoNewInputSystem = true;
        #else
        public const bool AutoNewInputSystem = false;
        #endif
        
        /// <summary>
        /// 自动检测：当前项目是否启用了 URP。
        /// 根据 UNITY_RENDER_PIPELINE_URP 宏判断。
        /// </summary>
        #if UNITY_RENDER_PIPELINE_URP
        public const bool AutoURP = true;
        #else
        public const bool AutoURP = false;
        #endif

        #endregion 

        #region 配置枚举-为全局设置所需的枚举定义

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

        #endregion 
        
        #region 最终状态-自动计算最终环境状态
        
        /// <summary>
        /// 最终 VR 模式 = 自动检测 XR 是否存在 且 未被强制关闭
        /// </summary>
        public static bool FinalIsVR => AutoXR && !ForceDisableXR;
        
        /// <summary>
        /// 最终输入系统 = 自动检测结果 且 未被强制关闭
        /// </summary>
        public static bool FinalUseNewInputSystem => AutoNewInputSystem && !ForceDisableNewInputSystem;
        
        /// <summary>
        /// 最终 URP 判定（优先级：ForceDisableURP > AutoURP）
        /// </summary>
        public static bool FinalUseURP => AutoURP && !ForceDisableURP;

        #endregion
    }
}