using System.IO;
using FinkFramework.Runtime.Data;
using FinkFramework.Runtime.Settings.Loaders;
using FinkFramework.Runtime.Utils;

namespace FinkFramework.Editor.Modules.Data
{
    /// <summary>
    /// 独立的二进制导出工具（支持加密 / 明文）。
    /// </summary>
    public static class BinaryExportTool
    {
        public static void ExportBinary(object container, string binaryPath)
        {
            try
            {
                // 自动创建目录
                Directory.CreateDirectory(Path.GetDirectoryName(binaryPath) ?? string.Empty);

                // 执行保存（内部会根据 EnableEncryption 决定加密与否）
                DataUtil.Save(binaryPath, container);

                string mode = GlobalSettingsRuntimeLoader.Current.EnableEncryption ? "加密二进制" : "二进制（明文）";
                LogUtil.Success("DataBinaryTool", $"已生成 {mode} 数据：{binaryPath}");
            }
            catch (System.Exception ex)
            {
                LogUtil.Error("DataBinaryTool", $"Binary 导出失败：{binaryPath} → {ex.Message}");
            }
        }
    }
}