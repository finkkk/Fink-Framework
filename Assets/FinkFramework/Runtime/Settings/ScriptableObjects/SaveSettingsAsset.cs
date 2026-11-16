using FinkFramework.Runtime.Environments;
using UnityEngine;

namespace FinkFramework.Runtime.Settings.ScriptableObjects
{
    /// <summary>存档格式、槽位、历史与压缩配置。AES 参数由本模块独立保存。</summary>
    [CreateAssetMenu(fileName = "SaveSettingsAsset", menuName = "Fink Framework/Settings/Save Settings")]
    public sealed class SaveSettingsAsset : ScriptableObject
    {

        [Header("AES 加密")]
        [Tooltip("仅控制本模块的 Binary 数据。JSON 数据不使用 AES。")]
        public bool EnableEncryption = true;

        [Tooltip("仅用于本模块的二进制数据加密与解密。")]
        public string Password = "finkkk";
        /// <summary>二进制存档的默认文件后缀。</summary>
        public const string DefaultSaveBinaryExtension = ".sav";

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
    }
}
