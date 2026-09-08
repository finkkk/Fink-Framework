using System;
using System.IO;
using System.Text;

namespace FinkFramework.Editor.Modules.Localization
{
    /// <summary>
    /// 本地化编辑器文件写入辅助，避免保存过程中留下半截 JSON 文件。
    /// </summary>
    internal static class LocalizationEditorFileUtil
    {
        /// <summary>
        /// 以 UTF-8 无 BOM 原子替换文本文件，尽量避免编辑器中断时留下半截文件。
        /// </summary>
        public static void WriteUtf8TextAtomic(string filePath, string content)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("文件路径不能为空。", nameof(filePath));

            string temporaryPath = filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporaryPath, content ?? string.Empty, new UTF8Encoding(false));
                if (!File.Exists(filePath))
                {
                    File.Move(temporaryPath, filePath);
                    return;
                }

                try
                {
                    File.Replace(temporaryPath, filePath, null);
                }
                catch (PlatformNotSupportedException)
                {
                    ReplaceWithoutFileReplace(temporaryPath, filePath);
                }
                catch (NotSupportedException)
                {
                    ReplaceWithoutFileReplace(temporaryPath, filePath);
                }
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }

        private static void ReplaceWithoutFileReplace(string temporaryPath, string filePath)
        {
            string backupPath = filePath + "." + Guid.NewGuid().ToString("N") + ".bak";
            File.Move(filePath, backupPath);
            try
            {
                File.Move(temporaryPath, filePath);
            }
            catch
            {
                if (!File.Exists(filePath) && File.Exists(backupPath))
                    File.Move(backupPath, filePath);
                throw;
            }
            finally
            {
                if (File.Exists(backupPath))
                    File.Delete(backupPath);
            }
        }
    }
}
