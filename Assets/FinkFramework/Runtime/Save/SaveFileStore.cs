using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using FinkFramework.Runtime.Data;
using FinkFramework.Runtime.Environments;
using FinkFramework.Runtime.Utils;
using Newtonsoft.Json.Linq;

namespace FinkFramework.Runtime.Save
{
    internal readonly struct SaveFileHeader
    {
        public readonly EnvironmentState.DataLoadMode Format;
        public readonly bool Encrypted;
        public readonly bool Compressed;
        public readonly long Generation;
        public readonly DateTime SavedUtc;
        public readonly string DataType;

        public SaveFileHeader(
            EnvironmentState.DataLoadMode format,
            bool encrypted,
            bool compressed,
            long generation,
            DateTime savedUtc,
            string dataType)
        {
            Format = format;
            Encrypted = encrypted;
            Compressed = compressed;
            Generation = generation;
            SavedUtc = savedUtc;
            DataType = dataType;
        }
    }

    internal sealed class SaveChecksumException : IOException
    {
        public SaveChecksumException(string message) : base(message) { }
    }

    internal sealed class SaveCommitException : IOException
    {
        public SaveOperationStatus Status { get; }

        public SaveCommitException(
            SaveOperationStatus status,
            string message,
            Exception innerException)
            : base(message, innerException)
        {
            Status = status;
        }
    }

    internal static class SaveFileStore
    {
        private const uint Magic = 0x31565346; // FSV1
        private const byte ContainerVersion = 1;
        private const byte EncryptedFlag = 1 << 0;
        private const byte CompressedFlag = 1 << 1;
        private const int ChecksumLength = 32;
        private const int MaxPayloadLength = 512 * 1024 * 1024;
        private const int MaxContainerOverhead = 64 * 1024;

        public static byte[] Pack<T>(
            T data,
            EnvironmentState.DataLoadMode format,
            bool encrypted,
            long generation,
            DateTime savedUtc)
        {
            return Pack(data, format, encrypted, false, generation, savedUtc);
        }

        public static byte[] Pack<T>(
            T data,
            EnvironmentState.DataLoadMode format,
            bool encrypted,
            bool compressed,
            long generation,
            DateTime savedUtc)
        {
            byte[] payload = DataUtil.SerializeSaveValue(data, format, encrypted, compressed);
            return PackPayload(
                payload,
                format,
                encrypted,
                compressed,
                generation,
                savedUtc,
                typeof(T).FullName ?? typeof(T).Name);
        }

        public static byte[] PackPayload(
            byte[] payload,
            EnvironmentState.DataLoadMode format,
            bool encrypted,
            long generation,
            DateTime savedUtc,
            string dataType)
        {
            return PackPayload(payload, format, encrypted, false, generation, savedUtc, dataType);
        }

        public static byte[] PackPayload(
            byte[] payload,
            EnvironmentState.DataLoadMode format,
            bool encrypted,
            bool compressed,
            long generation,
            DateTime savedUtc,
            string dataType)
        {
            if (payload == null || payload.Length == 0)
                throw new InvalidDataException("存档 Payload 为空。");

            // JSON 存档保持为 DataUtil 生成的裸 JSON，便于开发期直接查看和编辑。
            // 加密或压缩会破坏可读性，因此 SaveManager 在 JSON 模式下会关闭这两项处理。
            if (format == EnvironmentState.DataLoadMode.Json && !encrypted && !compressed)
                return payload;

            byte[] checksum = ComputeChecksum(payload);

            using var stream = new MemoryStream(payload.Length + 256);
            using var writer = new BinaryWriter(stream, new UTF8Encoding(false), true);
            writer.Write(Magic);
            writer.Write(ContainerVersion);
            writer.Write((byte)format);
            byte flags = 0;
            if (encrypted)
                flags |= EncryptedFlag;
            if (compressed)
                flags |= CompressedFlag;
            writer.Write(flags);
            writer.Write((byte)0);
            writer.Write(generation);
            writer.Write(savedUtc.Ticks);
            writer.Write(dataType ?? string.Empty);
            writer.Write(payload.Length);
            writer.Write(checksum);
            writer.Write(payload);
            writer.Flush();
            return stream.ToArray();
        }

        public static T Unpack<T>(byte[] container, out SaveFileHeader header)
        {
            byte[] payload = ReadPayload(container, out header);
            string expectedType = typeof(T).FullName ?? typeof(T).Name;
            if (!string.IsNullOrEmpty(header.DataType) &&
                !string.Equals(header.DataType, expectedType, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"存档数据类型不匹配。文件为 {header.DataType}，请求为 {expectedType}。");
            }

            return DataUtil.DeserializeSaveValue<T>(
                payload,
                header.Format,
                header.Encrypted,
                header.Compressed);
        }

        public static SaveFileHeader ReadHeader(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            ReadPayload(bytes, out SaveFileHeader header);

            // 裸 JSON 没有外层容器头；读取文件元数据时用文件时间补足展示信息。
            if (header is { Format: EnvironmentState.DataLoadMode.Json, Generation: 0 } &&
                string.IsNullOrEmpty(header.DataType))
            {
                header = new SaveFileHeader(
                    header.Format,
                    header.Encrypted,
                    header.Compressed,
                    header.Generation,
                    File.GetLastWriteTimeUtc(path),
                    header.DataType);
            }

            return header;
        }

        public static void ValidateContainer(byte[] container)
        {
            ReadPayload(container, out _);
        }

        public static void Commit(
            string mainPath,
            string backupPath,
            byte[] container,
            bool keepHistory,
            int historyLimit)
        {
            if (container == null || container.Length == 0)
                throw new InvalidDataException("待提交的存档内容为空。");

            string directory = Path.GetDirectoryName(mainPath);
            if (string.IsNullOrEmpty(directory))
                throw new InvalidOperationException("存档路径缺少父目录。");

            string tempPath = Path.Combine(
                directory,
                Path.GetFileName(mainPath) + "." + Guid.NewGuid().ToString("N") + ".tmp");

            try
            {
                try
                {
                    Directory.CreateDirectory(directory);
                    using var stream = new FileStream(
                        tempPath,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None,
                        64 * 1024,
                        FileOptions.WriteThrough);
                    stream.Write(container, 0, container.Length);
                    stream.Flush(true);
                }
                catch (Exception exception)
                {
                    throw new SaveCommitException(
                        SaveOperationStatus.TempWriteFailed,
                        "存档临时文件写入失败。",
                        exception);
                }

                try
                {
                    byte[] persisted = File.ReadAllBytes(tempPath);
                    ValidateContainer(persisted);
                }
                catch (Exception exception)
                {
                    throw new SaveCommitException(
                        SaveOperationStatus.ValidationFailed,
                        "存档临时文件回读校验失败。",
                        exception);
                }

                if (File.Exists(mainPath))
                {
                    try
                    {
                        RotateBackups(backupPath, keepHistory ? Math.Max(0, historyLimit) : 0);
                    }
                    catch (Exception exception)
                    {
                        throw new SaveCommitException(
                            SaveOperationStatus.BackupFailed,
                            "存档历史备份轮换失败。",
                            exception);
                    }

                    try
                    {
                        ReplaceExisting(tempPath, mainPath, backupPath);
                    }
                    catch (Exception exception)
                    {
                        throw new SaveCommitException(
                            SaveOperationStatus.ReplaceFailed,
                            "存档主文件替换失败。",
                            exception);
                    }
                }
                else
                {
                    try
                    {
                        File.Move(tempPath, mainPath);
                    }
                    catch (Exception exception)
                    {
                        throw new SaveCommitException(
                            SaveOperationStatus.ReplaceFailed,
                            "存档主文件提交失败。",
                            exception);
                    }
                }
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    try
                    {
                        File.Delete(tempPath);
                    }
                    catch (Exception exception)
                    {
                        // 清理失败不能覆盖真正的提交异常；残留 .tmp 下次不会参与加载。
                        LogUtil.Warn("SaveSystem", $"存档临时文件清理失败：{tempPath} → {exception.Message}");
                    }
                }
            }
        }

        public static IReadOnlyList<string> GetBackupPaths(string mainPath, string backupPath)
        {
            var paths = new List<(int Index, string Path)>();
            string directory = Path.GetDirectoryName(backupPath) ?? string.Empty;
            string backupStem = Path.GetFileNameWithoutExtension(backupPath);
            string extension = Path.GetExtension(backupPath);
            if (Directory.Exists(directory))
            {
                foreach (string path in Directory.GetFiles(directory, "*", SearchOption.TopDirectoryOnly))
                {
                    string fileName = Path.GetFileNameWithoutExtension(path);
                    if (!TryGetBackupIndex(fileName, backupStem, out int index) ||
                        !string.Equals(Path.GetExtension(path), extension, StringComparison.OrdinalIgnoreCase))
                        continue;

                    paths.Add((index, path));
                }
            }

            var result = paths
                .OrderBy(item => item.Index)
                .Select(item => item.Path)
                .ToList();

            // 兼容旧版的 Slot_x/History 目录。
            string legacyHistoryDirectory = Path.Combine(
                Path.GetDirectoryName(mainPath) ?? string.Empty,
                "History");
            if (Directory.Exists(legacyHistoryDirectory))
            {
                result.AddRange(Directory.GetFiles(legacyHistoryDirectory, "*", SearchOption.TopDirectoryOnly)
                    .Where(path => !path.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(path => path, StringComparer.Ordinal));
            }

            return result.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        public static IReadOnlyList<SaveHistoryInfo> GetHistory(string mainPath, string backupPath)
        {
            var historyPaths = GetDirectBackupPaths(backupPath, includeImmediate: false).ToList();
            string legacyHistoryDirectory = Path.Combine(
                Path.GetDirectoryName(mainPath) ?? string.Empty,
                "History");
            if (Directory.Exists(legacyHistoryDirectory))
            {
                historyPaths.AddRange(Directory.GetFiles(legacyHistoryDirectory, "*", SearchOption.TopDirectoryOnly)
                    .Where(path => !path.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(path => path, StringComparer.Ordinal));
            }

            var results = new List<SaveHistoryInfo>();

            // bak 是恢复用的即时备份，GetHistory 对外只返回 bak1、bak2... 以及旧版 History。
            foreach (string file in historyPaths.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    SaveFileHeader header = ReadHeader(file);
                    results.Add(new SaveHistoryInfo
                    {
                        Generation = header.Generation,
                        SavedUtc = header.SavedUtc,
                        Path = file
                    });
                }
                catch (Exception exception)
                {
                    LogUtil.Warn("SaveSystem", $"忽略无效历史存档：{file} → {exception.Message}");
                }
            }

            return results
                .OrderByDescending(info => info.Generation)
                .ThenByDescending(info => info.SavedUtc)
                .ToArray();
        }

        public static bool IsHistoryPath(string mainPath, string backupPath, string historyPath)
        {
            string fullHistoryPath = Path.GetFullPath(historyPath);
            string directDirectory = Path.GetFullPath(Path.GetDirectoryName(backupPath) ?? string.Empty);
            string historyDirectory = Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(mainPath) ?? string.Empty,
                "History"));

            if (fullHistoryPath.StartsWith(historyDirectory.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
                return true;

            if (!string.Equals(Path.GetDirectoryName(fullHistoryPath), directDirectory, StringComparison.OrdinalIgnoreCase))
                return false;

            string fileName = Path.GetFileNameWithoutExtension(fullHistoryPath);
            string backupStem = Path.GetFileNameWithoutExtension(backupPath);
            return TryGetBackupIndex(fileName, backupStem, out int index) && index > 0;
        }

        private static byte[] ReadPayload(byte[] container, out SaveFileHeader header)
        {
            if (container == null || container.Length == 0)
                throw new InvalidDataException("存档文件过短或内容为空。");
            if (container.Length > MaxPayloadLength + MaxContainerOverhead)
                throw new InvalidDataException("存档文件超过允许的最大尺寸。");

            // 当前 JSON 模式直接保存 DataUtil 生成的 UTF-8 JSON，不再套 FSV1 容器。
            // 仍保留下面的容器解析分支，因此旧版带容器的 .json 文件可以继续读取。
            bool isContainer = container.Length >= sizeof(uint) &&
                               BitConverter.ToUInt32(container, 0) == Magic;
            if (!isContainer)
            {
                try
                {
                    var json = new UTF8Encoding(false, true).GetString(container)
                        .TrimStart('\uFEFF');
                    JToken.Parse(json);
                }
                catch (Exception exception) when (
                    exception is DecoderFallbackException ||
                    exception is Newtonsoft.Json.JsonException ||
                    exception is ArgumentException)
                {
                    throw new InvalidDataException("不是有效的 JSON 存档文件。", exception);
                }

                header = new SaveFileHeader(
                    EnvironmentState.DataLoadMode.Json,
                    false,
                    false,
                    0,
                    DateTime.MinValue,
                    string.Empty);
                return container;
            }

            if (container.Length < 64)
                throw new InvalidDataException("存档文件过短或内容为空。");

            using var stream = new MemoryStream(container, false);
            using var reader = new BinaryReader(stream, Encoding.UTF8, true);

            if (reader.ReadUInt32() != Magic)
                throw new InvalidDataException("不是有效的 Fink Framework 存档文件。");

            byte version = reader.ReadByte();
            if (version != ContainerVersion)
                throw new InvalidDataException($"不支持的存档容器版本：{version}。");

            byte rawFormat = reader.ReadByte();
            if (!Enum.IsDefined(typeof(EnvironmentState.DataLoadMode), (int)rawFormat))
                throw new InvalidDataException($"未知的数据格式：{rawFormat}。");

            byte flags = reader.ReadByte();
            const byte knownFlags = EncryptedFlag | CompressedFlag;
            if ((flags & ~knownFlags) != 0)
                throw new InvalidDataException($"存档包含未知标记：0x{flags:X2}。");

            byte reserved = reader.ReadByte();
            if (reserved != 0)
                throw new InvalidDataException("存档保留字段无效。");
            long generation = reader.ReadInt64();
            long ticks = reader.ReadInt64();
            string dataType = reader.ReadString();
            int payloadLength = reader.ReadInt32();
            if (payloadLength <= 0 || payloadLength > MaxPayloadLength)
                throw new InvalidDataException($"非法的 Payload 长度：{payloadLength}。");

            byte[] expectedChecksum = reader.ReadBytes(ChecksumLength);
            if (expectedChecksum.Length != ChecksumLength)
                throw new InvalidDataException("存档校验信息不完整。");

            if (stream.Length - stream.Position != payloadLength)
                throw new InvalidDataException("存档 Payload 长度与文件头不一致。");

            byte[] payload = reader.ReadBytes(payloadLength);
            byte[] actualChecksum = ComputeChecksum(payload);
            if (!FixedTimeEquals(expectedChecksum, actualChecksum))
                throw new SaveChecksumException("存档 Payload 校验失败。");

            DateTime savedUtc;
            try
            {
                savedUtc = new DateTime(ticks, DateTimeKind.Utc);
            }
            catch (ArgumentOutOfRangeException exception)
            {
                throw new InvalidDataException("存档时间信息无效。", exception);
            }

            header = new SaveFileHeader(
                (EnvironmentState.DataLoadMode)rawFormat,
                (flags & EncryptedFlag) != 0,
                (flags & CompressedFlag) != 0,
                generation,
                savedUtc,
                dataType);
            return payload;
        }

        private static void ReplaceExisting(string tempPath, string mainPath, string backupPath)
        {
            try
            {
                File.Replace(tempPath, mainPath, backupPath, true);
            }
            catch (PlatformNotSupportedException)
            {
                FallbackReplace(tempPath, mainPath, backupPath);
            }
        }

        private static void FallbackReplace(string tempPath, string mainPath, string backupPath)
        {
            File.Copy(mainPath, backupPath, true);
            File.Delete(mainPath);
            File.Move(tempPath, mainPath);
        }

        private static void RotateBackups(string backupPath, int historyLimit)
        {
            string directory = Path.GetDirectoryName(backupPath) ?? string.Empty;
            if (!Directory.Exists(directory))
                return;

            if (historyLimit <= 0)
            {
                foreach (string path in GetDirectBackupPaths(backupPath, includeImmediate: false))
                    File.Delete(path);
                return;
            }

            for (int index = historyLimit; index >= 1; index--)
            {
                string source = GetNumberedBackupPath(backupPath, index - 1);
                string target = GetNumberedBackupPath(backupPath, index);
                if (File.Exists(source))
                    File.Copy(source, target, true);
                else if (File.Exists(target))
                    File.Delete(target);
            }

            foreach (string path in GetDirectBackupPaths(backupPath, includeImmediate: false))
            {
                string fileName = Path.GetFileNameWithoutExtension(path);
                if (TryGetBackupIndex(fileName, Path.GetFileNameWithoutExtension(backupPath), out int index) &&
                    index > historyLimit)
                    File.Delete(path);
            }
        }

        private static IReadOnlyList<string> GetDirectBackupPaths(string backupPath, bool includeImmediate)
        {
            string directory = Path.GetDirectoryName(backupPath) ?? string.Empty;
            string backupStem = Path.GetFileNameWithoutExtension(backupPath);
            string extension = Path.GetExtension(backupPath);
            if (!Directory.Exists(directory))
                return Array.Empty<string>();

            var paths = new List<(int Index, string Path)>();
            foreach (string path in Directory.GetFiles(directory, "*", SearchOption.TopDirectoryOnly))
            {
                if (!string.Equals(Path.GetExtension(path), extension, StringComparison.OrdinalIgnoreCase))
                    continue;

                string fileName = Path.GetFileNameWithoutExtension(path);
                if (!TryGetBackupIndex(fileName, backupStem, out int index) ||
                    (!includeImmediate && index == 0))
                    continue;

                paths.Add((index, path));
            }

            return paths.OrderBy(item => item.Index).Select(item => item.Path).ToArray();
        }

        private static string GetNumberedBackupPath(string backupPath, int index)
        {
            if (index <= 0)
                return backupPath;

            string directory = Path.GetDirectoryName(backupPath) ?? string.Empty;
            string stem = Path.GetFileNameWithoutExtension(backupPath);
            string extension = Path.GetExtension(backupPath);
            return Path.Combine(directory, stem + index + extension);
        }

        private static bool TryGetBackupIndex(string fileName, string backupStem, out int index)
        {
            index = -1;
            if (string.Equals(fileName, backupStem, StringComparison.OrdinalIgnoreCase))
            {
                index = 0;
                return true;
            }

            if (fileName == null || backupStem == null ||
                !fileName.StartsWith(backupStem, StringComparison.OrdinalIgnoreCase))
                return false;

            return int.TryParse(fileName.Substring(backupStem.Length), out index) && index > 0;
        }

        private static byte[] ComputeChecksum(byte[] payload)
        {
            using SHA256 sha256 = SHA256.Create();
            return sha256.ComputeHash(payload);
        }

        private static bool FixedTimeEquals(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length)
                return false;

            int difference = 0;
            for (int i = 0; i < left.Length; i++)
                difference |= left[i] ^ right[i];
            return difference == 0;
        }
    }
}
