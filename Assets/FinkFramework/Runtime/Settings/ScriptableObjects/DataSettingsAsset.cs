using FinkFramework.Runtime.Environments;
using UnityEngine;

namespace FinkFramework.Runtime.Settings.ScriptableObjects
{
    /// <summary>数据管线的数据格式与代码生成目录配置。</summary>
    [CreateAssetMenu(fileName = "DataSettingsAsset", menuName = "Fink Framework/Settings/Data Settings")]
    public sealed class DataSettingsAsset : ScriptableObject
    {

        [Header("AES 加密")]
        [Tooltip("仅控制本模块的 Binary 数据。JSON 数据不使用 AES。")]
        public bool EnableEncryption = true;

        [Tooltip("仅用于本模块的二进制数据加密与解密。")]
        public string Password = "finkkk";
        #region ===== 数据配置 =====

        [Header("运行时数据加载模式")]
        [Tooltip("Binary：使用加密二进制作为运行时数据源(Json依然导出)  Json：只使用 JSON 作为运行时数据源(二进制不导出)")]
        public EnvironmentState.DataLoadMode CurrentDataLoadMode = EnvironmentState.DataLoadMode.Binary;

        [Header("C# 数据类路径模式")]
        [Tooltip("选择生成的 C# 数据类输出到 Assets 内部还是项目外部目录。")]
        public EnvironmentState.CSharpOutputPathMode CSharpPathMode =
            EnvironmentState.CSharpOutputPathMode.Internal;

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

        [Header("二进制数据文件后缀")]
        [Tooltip("仅控制数据管线导出的 Binary 文件，不影响存档后缀。即使关闭加密也使用此后缀。反序列化格式由数据加载模式决定。 ")]
        public string EncryptedExtension = ".fink";
    }
}
