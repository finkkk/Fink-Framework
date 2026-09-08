using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace FinkFramework.Editor.Utils
{
    /// <summary>
    /// 以可恢复的方式替换框架目录。
    /// 更新期间仅修改 Assets/FinkFramework；Library 中的文件只是临时包、备份和恢复记录。
    /// </summary>
    [InitializeOnLoad]
    internal static class FrameworkPackageUpdater
    {
        private const string FrameworkPath = "Assets/FinkFramework";
        private const string BackupPath = "Library/FinkFrameworkUpdateBackup";
        private const string JournalPath = "Library/FinkFrameworkUpdate.json";
        private const string PackagePath = "Library/FinkFrameworkUpdate.unitypackage";
        private const string UpdatingState = "updating";
        private const string ImportingState = "importing";

        [Serializable]
        private sealed class UpdateJournal
        {
            public string state;
            public string version;
            public string packagePath;
        }

        static FrameworkPackageUpdater()
        {
            AssetDatabase.importPackageCompleted += OnImportCompleted;
            AssetDatabase.importPackageFailed += OnImportFailed;
            AssetDatabase.importPackageCancelled += OnImportCancelled;
            EditorApplication.delayCall += RecoverInterruptedUpdate;
        }

        internal static void Start(string packagePath, string version)
        {
            if (string.IsNullOrEmpty(packagePath) || !File.Exists(packagePath))
            {
                EditorUtility.DisplayDialog("Fink Framework 更新", "更新包不存在，更新已取消。", "确定");
                return;
            }

            // 删除任何项目文件之前，必须完整校验下载包。
            if (!ValidatePackage(packagePath, out string validationError))
            {
                EditorUtility.DisplayDialog("Fink Framework 更新", "更新包校验失败：\n" + validationError, "确定");
                DeleteDownloadedPackage(packagePath);
                return;
            }

            try
            {
                WriteJournal(UpdatingState, version, packagePath);
                CreateBackup();
                AssetDatabase.DeleteAsset(FrameworkPath);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                WriteJournal(ImportingState, version, packagePath);
                AssetDatabase.ImportPackage(packagePath, false);
            }
            catch (Exception ex)
            {
                FailAndRollback("准备或导入更新时发生错误：" + ex.Message);
            }
        }

        internal static bool ValidatePackage(string packagePath, out string error)
        {
            error = null;
            try
            {
                bool hasPathName = false;
                bool hasMeta = false;
                using (var file = File.OpenRead(packagePath))
                using (var gzip = new GZipStream(file, CompressionMode.Decompress))
                {
                    byte[] header = new byte[512];
                    while (TryReadTarHeader(gzip, header))
                    {
                        if (IsZeroBlock(header))
                            break;

                        long size = ReadOctal(header, 124, 12);
                        if (size < 0)
                            throw new InvalidDataException("包内 TAR 条目长度无效。");

                        string name = ReadTarText(header, 0, 100);
                        // Unity Package 以 GUID 目录保存 asset、asset.meta 和 pathname。
                        if (name.EndsWith("/asset.meta", StringComparison.Ordinal))
                            hasMeta = true;

                        if (name.EndsWith("/pathname", StringComparison.Ordinal))
                        {
                            if (size <= 0 || size > 4096)
                                throw new InvalidDataException("包内 pathname 条目无效。");
                            byte[] pathBytes = ReadBytes(gzip, (int)size);
                            string assetPath = Encoding.UTF8.GetString(pathBytes).TrimEnd('\0', '\r', '\n').Replace('\\', '/');
                            if (!IsAllowedAssetPath(assetPath))
                                throw new InvalidDataException("包包含框架目录外的资源：" + assetPath);
                            hasPathName = true;
                        }
                        else
                        {
                            Skip(gzip, size);
                        }

                        long padding = (512 - (size % 512)) % 512;
                        Skip(gzip, padding);
                    }
                }

                if (!hasPathName)
                    throw new InvalidDataException("包内没有 Unity 资源路径记录。");
                if (!hasMeta)
                    throw new InvalidDataException("包内没有 .meta 文件，无法保证资源 GUID。\n");
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool IsAllowedAssetPath(string path)
        {
            return !string.IsNullOrEmpty(path) &&
                   !Path.IsPathRooted(path) &&
                   path.IndexOf("..", StringComparison.Ordinal) < 0 &&
                   (path.Equals(FrameworkPath, StringComparison.Ordinal) ||
                    path.Equals(FrameworkPath + ".meta", StringComparison.Ordinal) ||
                    path.StartsWith(FrameworkPath + "/", StringComparison.Ordinal));
        }

        private static void CreateBackup()
        {
            if (Directory.Exists(BackupPath))
                Directory.Delete(BackupPath, true);
            Directory.CreateDirectory(BackupPath);
            if (AssetDatabase.IsValidFolder(FrameworkPath))
                FileUtil.CopyFileOrDirectory(FrameworkPath, BackupPath + "/FinkFramework");
            string metaPath = FrameworkPath + ".meta";
            if (File.Exists(metaPath))
                FileUtil.CopyFileOrDirectory(metaPath, BackupPath + "/FinkFramework.meta");
        }

        private static void OnImportCompleted(string packageName)
        {
            if (!IsCurrentPackage(packageName)) return;
            Complete();
        }

        private static void OnImportFailed(string packageName, string error)
        {
            if (IsCurrentPackage(packageName)) FailAndRollback("导入失败：" + error);
        }

        private static void OnImportCancelled(string packageName)
        {
            if (IsCurrentPackage(packageName)) FailAndRollback("导入被取消。");
        }

        private static bool IsCurrentPackage(string packageName)
        {
            UpdateJournal journal = ReadJournal();
            return journal != null && journal.state == ImportingState &&
                   string.Equals(Path.GetFullPath(packageName), Path.GetFullPath(journal.packagePath), StringComparison.OrdinalIgnoreCase);
        }

        private static void Complete()
        {
            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                if (!AssetDatabase.IsValidFolder(FrameworkPath))
                    throw new InvalidOperationException("导入后未找到 Assets/FinkFramework。");
                ClearRecoveryFiles();
                Debug.Log("[Fink Framework] 更新成功。请等待 Unity 完成脚本编译。");
            }
            catch (Exception ex)
            {
                FailAndRollback("更新完成校验失败：" + ex.Message);
            }
        }

        private static void RecoverInterruptedUpdate()
        {
            UpdateJournal journal = ReadJournal();
            if (journal == null) return;

            // Unity 在导入完成、清理临时文件前退出时，只需确认新目录存在后收尾。
            if (journal.state == ImportingState && AssetDatabase.IsValidFolder(FrameworkPath))
            {
                Complete();
                return;
            }

            if (!EditorUtility.DisplayDialog("Fink Framework 更新恢复",
                    "检测到上次框架更新未完成。是否恢复更新前的框架文件？", "恢复", "稍后"))
                return;

            try { Rollback(); }
            finally { ClearRecoveryFiles(); }
        }

        private static void FailAndRollback(string message)
        {
            Debug.LogError("[Fink Framework] " + message);
            try
            {
                Rollback();
            }
            catch (Exception rollbackEx)
            {
                Debug.LogError("[Fink Framework] 回滚失败：" + rollbackEx.Message);
            }
            finally
            {
                // 更新已得到明确结果后，不保留任何 Library 临时数据。
                ClearRecoveryFiles();
            }
        }

        private static void Rollback()
        {
            if (!Directory.Exists(BackupPath))
                throw new DirectoryNotFoundException("找不到更新备份：" + BackupPath);
            AssetDatabase.DeleteAsset(FrameworkPath);
            FileUtil.CopyFileOrDirectory(BackupPath + "/FinkFramework", FrameworkPath);
            string metaBackup = BackupPath + "/FinkFramework.meta";
            if (File.Exists(metaBackup)) FileUtil.CopyFileOrDirectory(metaBackup, FrameworkPath + ".meta");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("[Fink Framework] 已恢复更新前的框架文件。");
        }

        private static void WriteJournal(string state, string version, string packagePath)
        {
            File.WriteAllText(JournalPath, JsonUtility.ToJson(new UpdateJournal { state = state, version = version, packagePath = packagePath }));
        }

        private static UpdateJournal ReadJournal()
        {
            try { return File.Exists(JournalPath) ? JsonUtility.FromJson<UpdateJournal>(File.ReadAllText(JournalPath)) : null; }
            catch { return null; }
        }

        private static void ClearRecoveryFiles()
        {
            DeleteDirectory(BackupPath);
            DeleteFile(PackagePath);
            DeleteFile(JournalPath);
        }

        /// <summary>下载包校验失败时只移除本次下载，不影响可能存在的异常退出恢复记录。</summary>
        internal static void DeleteDownloadedPackage(string packagePath) => DeleteFile(packagePath);

        private static void DeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path)) Directory.Delete(path, true);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Fink Framework] 无法清理临时目录 " + path + "：" + ex.Message);
            }
        }

        private static void DeleteFile(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Fink Framework] 无法清理临时文件 " + path + "：" + ex.Message);
            }
        }

        private static bool ReadExactly(Stream stream, byte[] buffer, int count)
        {
            int offset = 0;
            while (offset < count)
            {
                int read = stream.Read(buffer, offset, count - offset);
                if (read == 0) return offset == 0;
                offset += read;
            }
            return true;
        }

        /// <summary>读取一个完整 TAR 头；只允许在头边界正常结束，拒绝被截断的包。</summary>
        private static bool TryReadTarHeader(Stream stream, byte[] header)
        {
            int offset = 0;
            while (offset < header.Length)
            {
                int read = stream.Read(header, offset, header.Length - offset);
                if (read == 0)
                {
                    if (offset == 0) return false;
                    throw new EndOfStreamException("更新包的 TAR 头被截断。");
                }
                offset += read;
            }
            return true;
        }

        private static byte[] ReadBytes(Stream stream, int count)
        {
            byte[] result = new byte[count];
            if (!ReadExactly(stream, result, count)) throw new EndOfStreamException("更新包意外结束。");
            return result;
        }

        private static void Skip(Stream stream, long count)
        {
            byte[] buffer = new byte[8192];
            while (count > 0)
            {
                int read = stream.Read(buffer, 0, (int)Math.Min(buffer.Length, count));
                if (read == 0) throw new EndOfStreamException("更新包意外结束。");
                count -= read;
            }
        }

        private static bool IsZeroBlock(byte[] buffer)
        {
            for (int i = 0; i < buffer.Length; i++) if (buffer[i] != 0) return false;
            return true;
        }

        private static string ReadTarText(byte[] buffer, int offset, int length)
        {
            int end = offset;
            while (end < offset + length && buffer[end] != 0) end++;
            return Encoding.UTF8.GetString(buffer, offset, end - offset);
        }

        private static long ReadOctal(byte[] buffer, int offset, int length)
        {
            string value = ReadTarText(buffer, offset, length).Trim();
            return string.IsNullOrEmpty(value) ? 0 : Convert.ToInt64(value, 8);
        }
    }
}
