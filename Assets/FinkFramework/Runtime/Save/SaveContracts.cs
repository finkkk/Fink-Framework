using System;
using System.Collections.Generic;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace FinkFramework.Runtime.Save
{
    /// <summary>
    /// 存档操作的最终状态。调用方应优先判断 <see cref="SaveResult.Succeeded"/>
    /// 或 <see cref="LoadResult{T}.Succeeded"/>，需要展示或诊断时再读取具体状态。
    /// </summary>
    public enum SaveOperationStatus
    {
        /// <summary>操作成功完成。</summary>
        Success,
        /// <summary>操作在提交前被取消。</summary>
        Cancelled,
        /// <summary>槽位、数据或路径参数无效。</summary>
        InvalidArgument,
        /// <summary>当前单槽位/多槽位模式不支持该操作。</summary>
        ModeNotSupported,
        /// <summary>存档类型不满足可序列化 Schema 约束。</summary>
        SchemaInvalid,
        /// <summary>数据编码、压缩或加密失败。</summary>
        SerializationFailed,
        /// <summary>临时文件写入失败。</summary>
        TempWriteFailed,
        /// <summary>文件结构、长度或内容校验失败。</summary>
        ValidationFailed,
        /// <summary>使用临时文件替换主存档失败。</summary>
        ReplaceFailed,
        /// <summary>创建或轮换备份失败。</summary>
        BackupFailed,
        /// <summary>目标存档不存在；加载接口会同时返回当前版本默认数据。</summary>
        FileNotFound,
        /// <summary>二进制存档 Payload 的 SHA-256 校验失败。</summary>
        ChecksumFailed,
        /// <summary>AES 解密失败，常见原因是密钥变化或文件损坏。</summary>
        DecryptionFailed,
        /// <summary>JSON/Odin 无法还原目标类型。</summary>
        DeserializationFailed,
        /// <summary>旧档结构与当前类型不兼容。</summary>
        SchemaIncompatible,
        /// <summary>主档、即时备份及历史备份均无法恢复。</summary>
        RecoveryFailed,
        /// <summary>删除槽位文件失败。</summary>
        DeleteFailed,
        /// <summary>未能归类的文件系统或运行时错误。</summary>
        UnknownError
    }

    /// <summary>
    /// 本次加载实际采用的数据来源。
    /// </summary>
    public enum SaveDataSource
    {
        /// <summary>没有获得可用数据。</summary>
        None,
        /// <summary>槽位或全局主存档。</summary>
        Main,
        /// <summary>最近一次覆盖前保留的即时备份（<c>_bak</c>）。</summary>
        Backup,
        /// <summary>编号历史备份（<c>_bak1</c>、<c>_bak2</c>……）。</summary>
        History,
        /// <summary>文件不存在时，由当前数据类型构造的默认实例。</summary>
        Default
    }

    /// <summary>
    /// 保存、删除、创建槽位及历史回档操作的结果。
    /// 所有预期失败均通过该对象返回，通常不会作为异常抛给业务层。
    /// </summary>
    public sealed class SaveResult
    {
        /// <summary>操作状态。</summary>
        public SaveOperationStatus Status { get; }

        /// <summary>本次操作对应的主文件或失败文件路径；无目标时为空字符串。</summary>
        public string Path { get; }

        /// <summary>适合日志或调试显示的补充信息；成功时通常为空。</summary>
        public string Message { get; }

        /// <summary>导致失败的原始异常；成功或参数校验失败时可能为空。</summary>
        public Exception Exception { get; }

        /// <summary>仅当 <see cref="Status"/> 为 <see cref="SaveOperationStatus.Success"/> 时为 true。</summary>
        public bool Succeeded => Status == SaveOperationStatus.Success;

        internal SaveResult(
            SaveOperationStatus status,
            string path,
            string message = null,
            Exception exception = null)
        {
            Status = status;
            Path = path;
            Message = message ?? string.Empty;
            Exception = exception;
        }

        internal static SaveResult Success(string path) =>
            new(SaveOperationStatus.Success, path);

        internal static SaveResult Failure(
            SaveOperationStatus status,
            string path,
            string message,
            Exception exception = null) =>
            new(status, path, message, exception);
    }

    /// <summary>
    /// 强类型加载结果，包含数据、来源、恢复状态及诊断信息。
    /// </summary>
    /// <typeparam name="T">存档根数据类型。</typeparam>
    public sealed class LoadResult<T>
    {
        /// <summary>加载状态。</summary>
        public SaveOperationStatus Status { get; }

        /// <summary>最终采用的数据来源。</summary>
        public SaveDataSource Source { get; }

        /// <summary>
        /// 还原后的数据。文件不存在时为当前类型的默认实例；其他失败通常为 default。
        /// </summary>
        public T Data { get; }

        /// <summary>成功读取的候选文件路径，或原始主存档路径。</summary>
        public string Path { get; }

        /// <summary>恢复、缺档或失败原因说明。</summary>
        public string Message { get; }

        /// <summary>最后一次读取失败的原始异常；正常加载和缺档时为空。</summary>
        public Exception Exception { get; }

        /// <summary>
        /// 是否得到了可直接使用的数据。文件不存在时系统会创建默认实例，
        /// 因此 <see cref="SaveOperationStatus.FileNotFound"/> 也视为成功结果。
        /// </summary>
        public bool Succeeded =>
            Status == SaveOperationStatus.Success ||
            Status == SaveOperationStatus.FileNotFound;

        /// <summary>是否因文件不存在而返回了当前版本默认数据。</summary>
        public bool UsedDefault => Source == SaveDataSource.Default;

        /// <summary>是否绕过主档并从即时备份或历史备份读取成功。</summary>
        public bool Recovered =>
            Source == SaveDataSource.Backup || Source == SaveDataSource.History;

        internal LoadResult(
            SaveOperationStatus status,
            SaveDataSource source,
            T data,
            string path,
            string message = null,
            Exception exception = null)
        {
            Status = status;
            Source = source;
            Data = data;
            Path = path;
            Message = message ?? string.Empty;
            Exception = exception;
        }
    }

    /// <summary>
    /// 可用于槽位选择界面的只读摘要。属性仅由 <see cref="SaveManager"/> 填充。
    /// </summary>
    public sealed class SaveSlotInfo
    {
        /// <summary>大于 0 的槽位编号。</summary>
        public int SlotId { get; internal set; }

        /// <summary>主存档文件是否存在。</summary>
        public bool HasData { get; internal set; }

        /// <summary>二进制容器中的提交代数；裸 JSON 不携带该元数据，值为 0。</summary>
        public long Generation { get; internal set; }

        /// <summary>最近保存的 UTC 时间；裸 JSON 使用文件最后写入时间。</summary>
        public DateTime LastSavedUtc { get; internal set; }

        /// <summary>主存档的绝对路径。</summary>
        public string Path { get; internal set; }
    }

    /// <summary>
    /// 一份可回档的编号历史备份摘要，不包含即时 <c>_bak</c> 备份。
    /// </summary>
    public sealed class SaveHistoryInfo
    {
        /// <summary>二进制容器中的提交代数；裸 JSON 历史档为 0。</summary>
        public long Generation { get; internal set; }

        /// <summary>保存 UTC 时间；裸 JSON 使用文件最后写入时间。</summary>
        public DateTime SavedUtc { get; internal set; }

        /// <summary>历史文件的绝对路径。</summary>
        public string Path { get; internal set; }
    }

    /// <summary>
    /// 存档根类型或其嵌套成员违反 Schema 规则时抛出的异常。
    /// </summary>
    public sealed class SaveSchemaValidationException : Exception
    {
        /// <summary>全部校验错误；一次校验会尽量收集完整问题列表。</summary>
        public IReadOnlyList<string> Errors { get; }

        /// <summary>
        /// 创建 Schema 校验异常。
        /// </summary>
        /// <param name="type">正在校验的存档根类型。</param>
        /// <param name="errors">已发现的错误列表。</param>
        public SaveSchemaValidationException(Type type, IReadOnlyList<string> errors)
            : base($"存档类型 {type?.FullName ?? "<null>"} 的 Schema 无效：{string.Join("；", errors)}")
        {
            Errors = errors;
        }
    }
}
