using FinkFramework.Runtime.Environments;
using UnityEngine;

namespace FinkFramework.Editor.Settings.Global
{
    [CreateAssetMenu(fileName = "GlobalSettingsAsset", menuName = "FinkFramework/Settings/GlobalSettings", order = 0)]
    public class GlobalSettingsAsset : ScriptableObject
    {
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
        [Header("当前 UI 渲染模式")]
        [Tooltip("当前 UI 渲染模式。默认使用 ScreenSpace-Camera。如果项目为 VR，请务必使用 WorldSpace 模式。")]
        public EnvironmentState.UIMode CurrentUIMode = EnvironmentState.UIMode.Auto;
        [Header("AES 加密使用的密钥")]
        [Tooltip("AES 加密使用的密钥（请务必根据项目需求自行修改）。注意：不建议在正式线上版本中使用简单字符串，建议将密钥外部化或混淆处理。")]
        public string Password = "finkkk";
        [Header("框架生成的加密数据文件的后缀名")]
        [Tooltip("框架生成的加密数据文件的后缀名。用于存档、配置文件、数据表等加密存储。")]
        public string EncryptedExtension = ".fink";
        
        /// <summary>
        /// 将 ScriptableObject 中的数据同步到运行时 EnvironmentState
        /// </summary>
        public void ApplyToRuntime()
        {
            EnvironmentState.ForceDisableXR = ForceDisableXR;
            EnvironmentState.ForceDisableNewInputSystem = ForceDisableNewInputSystem;
            EnvironmentState.ForceDisableURP = ForceDisableURP;

            EnvironmentState.EnableEditorUrlCheck = EnableEditorUrlCheck;
            
            EnvironmentState.CurrentUIMode = CurrentUIMode;

            EnvironmentState.PASSWORD = Password;
            EnvironmentState.ENCRYPTED_FILE_EXTENSION = EncryptedExtension;
        }

        /// <summary>
        /// 当用户在 Inspector 修改字段时自动同步到运行时
        /// </summary>
        private void OnValidate()
        {
            ApplyToRuntime();
        }
    }
}