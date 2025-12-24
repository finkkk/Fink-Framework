// ReSharper disable ConvertToConstant.Global
using FinkFramework.Runtime.Settings.Loaders;
using FinkFramework.Runtime.Settings.ScriptableObjects;

#pragma warning disable CS0162 // 检测到不可到达的代码
namespace FinkFramework.Runtime.Environments
{
        /// <summary>
        /// 全局框架环境状态 FrameworkEnvironment
        /// ------------------------------------------------------------
        /// 该类用于存储框架在运行时的环境状态（Environment State）。
        /// 包括：宏定义检测环境信息、自动计算最终环境状态等。
        /// 该类为 Runtime-only，不会进入构建之外的 Asset 系统。
        /// 用户不会直接修改此类，而是通过 ScriptableObject（GlobalSettingsAsset）
        /// 由编辑器在初始化时注入运行时值。
        /// ------------------------------------------------------------
        /// </summary>
    public static class EnvironmentState
    {
        #region 框架信息-版本号等基础信息

        /// <summary>
        /// 框架版本号
        /// </summary>
        public const string FrameworkVersion = "0.3.2";

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
        #if ENABLE_URP
        public const bool AutoURP = true;
        #else
        public const bool AutoURP = false;
        #endif

        #endregion 

        #region 配置枚举-为全局设置所需的枚举定义
        
        /// <summary>
        /// 资源构建型后端 类型（使用ab包还是addressables）
        /// </summary>
        public enum ResourceBackendType
        {
            None,             // 不启用任何资源构建型后端
            AssetBundle,      // 传统 AB
            Addressables,     // 官方推荐
            Custom            // 用户自定义(如YooAsset / 自研)
        }

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
        /// 数据管线 运行时读取的数据源模式
        /// </summary>
        public enum DataLoadMode
        {
            Binary,     // 默认模式：运行时加载二进制加密数据（安全、轻量、生产环境使用）
            Json        // 调试模式：运行时直接读取 JSON（不需要生成或使用 Binary）
        }

        #endregion 
        
        #region 最终状态-自动计算最终环境状态
        
        /// <summary>
        /// 全局配置 SO 缓存
        /// </summary>
        private static GlobalSettingsAsset GS => GlobalSettingsRuntimeLoader.Current;
        
        /// <summary>
        /// 最终 VR 模式 = 自动检测 XR 是否存在 且 未被强制关闭
        /// </summary>
        public static bool FinalIsVR => AutoXR && !GS.ForceDisableXR;
        
        /// <summary>
        /// 最终输入系统 = 自动检测结果 且 未被强制关闭
        /// </summary>
        public static bool FinalUseNewInputSystem => AutoNewInputSystem && !GS.ForceDisableNewInputSystem;
        
        /// <summary>
        /// 最终 URP 判定（优先级：ForceDisableURP > AutoURP）
        /// </summary>
        public static bool FinalUseURP => AutoURP && !GS.ForceDisableURP;

        #endregion
    }
}