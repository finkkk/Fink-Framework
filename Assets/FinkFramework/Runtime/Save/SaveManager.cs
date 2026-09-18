using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using Cysharp.Threading.Tasks;
using FinkFramework.Odin.OdinSerializer;
using FinkFramework.Runtime.Data;
using FinkFramework.Runtime.Environments;
using FinkFramework.Runtime.Settings.Loaders;
using FinkFramework.Runtime.Settings.ScriptableObjects;
using FinkFramework.Runtime.Singleton;
using FinkFramework.Runtime.Utils;
using Newtonsoft.Json;
using UnityEngine;

namespace FinkFramework.Runtime.Save
{
    /// <summary>
    /// 强类型存档入口。DataUtil 负责数据编码；SaveManager 负责作用域、队列、
    /// 文件校验、原子替换、备份与恢复。存档格式和二进制后缀独立于数据管线，
    /// AES 开关与密钥仍复用全局数据管线配置。
    /// </summary>
    public sealed class SaveManager : Singleton<SaveManager>
    {
        private const int DefaultSlotId = 1;
        private const string RootFolderName = "FinkFramework_Save";
        private const string SlotsFolderName = "slot";
        private const string LegacySlotsFolderName = "Slots";
        private const string GlobalFileStem = "global_save";
        private const string GlobalBackupFileStem = "global_save_bak";
        private const string LegacyMainFileName = "main.save";
        private const string LegacyBackupFileName = "backup.save";
        private const string LegacyGlobalFileName = "global.save";
        private const string LegacyGlobalBackupFileName = "global.backup.save";

        private readonly ConcurrentDictionary<string, SemaphoreSlim> targetLocks = new();
        private readonly string rootPath;
        private int currentSlotId = DefaultSlotId;

        private SaveManager()
        {
            // 在主线程完成 Resources 配置加载，后台存档任务只读取缓存值。
            _ = GetSettings();
            rootPath = Path.GetFullPath(Path.Combine(Application.persistentDataPath, RootFolderName));
        }

        /// <summary>
        /// 存档根目录的绝对路径。默认位于
        /// <see cref="Application.persistentDataPath"/>/<c>FinkFramework_Save</c>。
        /// </summary>
        public string RootPath => rootPath;

        /// <summary>
        /// 无槽位参数的保存、读取和自动存档所使用的槽位编号。
        /// 单槽位模式固定为 1；多槽位模式可通过 <see cref="SelectSlot"/> 修改。
        /// </summary>
        public int CurrentSlotId => currentSlotId;

        /// <summary>当前是否启用多槽位模式，值来自全局存档配置。</summary>
        public bool MultiSlotMode => GetSettings().MultiSlotMode;

        /// <summary>
        /// 打开一个绑定到显式槽位编号的轻量存档封装。
        /// 封装不会修改全局当前槽位，也不会创建文件；首次保存时才会创建槽位数据。
        /// </summary>
        public SaveSlot<T> OpenSlot<T>(int slotId)
        {
            return new SaveSlot<T>(this, slotId);
        }

        /// <summary>
        /// 创建当前版本的默认存档实例。构造函数和字段初始化器会正常执行，
        /// 因而可作为首次进入游戏或加载失败后的显式回退值。
        /// </summary>
        /// <typeparam name="T">必须可实例化并提供无参构造函数的存档类型。</typeparam>
        /// <returns>按当前代码默认值构造的新实例。</returns>
        /// <exception cref="InvalidOperationException">类型不可实例化或缺少可用无参构造函数。</exception>
        public static T CreateDefault<T>() => SaveSchema.CreateDefault<T>();

        /// <summary>
        /// 从当前版本的默认存档实例中读取一个字段或属性默认值。
        /// 适用于迁移、重置单个设置项，避免在业务层重复硬编码默认值。
        /// </summary>
        /// <typeparam name="T">存档根类型。</typeparam>
        /// <typeparam name="TValue">要读取的成员类型。</typeparam>
        /// <param name="selector">从默认实例中选择成员的函数。</param>
        /// <returns>选中成员的当前版本默认值。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="selector"/> 为空。</exception>
        public static TValue GetDefault<T, TValue>(Func<T, TValue> selector)
        {
            if (selector == null)
                throw new ArgumentNullException(nameof(selector));
            return selector(CreateDefault<T>());
        }

        /// <summary>
        /// 异步保存到 <see cref="CurrentSlotId"/>。数据会在方法首次让出线程前序列化为快照，
        /// 此后业务层继续修改原对象不会影响本次保存。相同目标的操作串行执行，并使用
        /// 临时文件校验、原子替换、即时备份及可选编号历史备份。
        /// </summary>
        /// <typeparam name="T">存档根类型；公共字段和公共可读写属性会参与序列化。</typeparam>
        /// <param name="data">要保存的非空数据。</param>
        /// <param name="cancellationToken">取消等待队列或提交前工作；原子替换开始后不会中断写盘。</param>
        /// <returns>包含状态、主文件路径和异常信息的结果。</returns>
        public UniTask<SaveResult> SaveAsync<T>(
            T data,
            CancellationToken cancellationToken = default)
        {
            return SaveAsync(data, ResolveSlotId(null), cancellationToken);
        }

        /// <summary>
        /// 异步保存到指定槽位。单槽位模式仅接受槽位 1；多槽位模式要求编号大于 0。
        /// 首次成功保存会自动创建槽位目录和主文件。
        /// </summary>
        /// <typeparam name="T">存档根类型。</typeparam>
        /// <param name="data">要保存的非空数据。</param>
        /// <param name="slotId">目标槽位编号，从 1 开始。</param>
        /// <param name="cancellationToken">取消等待队列或提交前工作。</param>
        /// <returns>保存结果；参数问题通过结果返回，不会抛出业务异常。</returns>
        public UniTask<SaveResult> SaveAsync<T>(
            T data,
            int slotId,
            CancellationToken cancellationToken = default)
        {
            if (!TryValidateSlotId(slotId, out string error))
                return UniTask.FromResult(SaveResult.Failure(
                    SaveOperationStatus.InvalidArgument,
                    string.Empty,
                    error));

            return SaveTargetAsync(data, GetSlotMainPath(slotId), GetSlotBackupPath(slotId), cancellationToken);
        }

        /// <summary>
        /// 从 <see cref="CurrentSlotId"/> 异步加载存档。读取顺序为主档、即时备份、
        /// 编号历史备份；某个候选损坏时会继续尝试下一份。完全没有文件时返回
        /// 当前版本默认实例，状态为 <see cref="SaveOperationStatus.FileNotFound"/>。
        /// </summary>
        /// <typeparam name="T">期望还原的存档根类型。</typeparam>
        /// <param name="cancellationToken">取消等待队列或候选文件读取。</param>
        /// <returns>包含数据、实际来源及诊断信息的加载结果。</returns>
        public UniTask<LoadResult<T>> LoadAsync<T>(CancellationToken cancellationToken = default)
        {
            return LoadAsync<T>(ResolveSlotId(null), cancellationToken);
        }

        /// <summary>
        /// 从指定槽位异步加载存档，并自动尝试即时备份和历史备份。
        /// 兼容旧版 <c>Slots/Slot_N/main.save</c> 目录结构。
        /// </summary>
        /// <typeparam name="T">期望还原的存档根类型。</typeparam>
        /// <param name="slotId">目标槽位编号，从 1 开始。</param>
        /// <param name="cancellationToken">取消等待队列或候选文件读取。</param>
        /// <returns>强类型加载结果。</returns>
        public UniTask<LoadResult<T>> LoadAsync<T>(int slotId, CancellationToken cancellationToken = default)
        {
            if (!TryValidateSlotId(slotId, out string error))
            {
                return UniTask.FromResult(new LoadResult<T>(
                    SaveOperationStatus.InvalidArgument,
                    SaveDataSource.None,
                    default,
                    string.Empty,
                    error));
            }

            string mainPath = GetSlotMainPath(slotId);
            string backupPath = GetSlotBackupPath(slotId);
            UseLegacyPathsWhenNeeded(
                ref mainPath,
                ref backupPath,
                GetLegacySlotMainPath(slotId),
                GetLegacySlotBackupPath(slotId));
            return LoadTargetAsync<T>(mainPath, backupPath, cancellationToken);
        }

        /// <summary>
        /// 兼容旧名称的便捷加载方法。只有缺档或成功恢复时返回数据；损坏、取消和其他
        /// 失败会抛出 <see cref="SaveLoadException{T}"/>。新代码请使用
        /// <see cref="LoadOrCreateAsync{T}(int, CancellationToken)"/> 或 OpenSlot。
        /// </summary>
        /// <typeparam name="T">存档根类型。</typeparam>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>加载数据或新建的默认实例。</returns>
        [Obsolete("LoadOrDefaultAsync 不再对失败静默返回默认值，请使用 LoadOrCreateAsync 或 OpenSlot。")]
        public UniTask<T> LoadOrDefaultAsync<T>(CancellationToken cancellationToken = default)
        {
            return LoadOrCreateAsync<T>(ResolveSlotId(null), cancellationToken);
        }

        /// <summary>
        /// 兼容旧名称的指定槽位便捷加载方法。只有缺档或成功恢复时返回数据；其他失败
        /// 会抛出 <see cref="SaveLoadException{T}"/>。
        /// </summary>
        /// <typeparam name="T">存档根类型。</typeparam>
        /// <param name="slotId">目标槽位编号。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>加载数据或新建的默认实例。</returns>
        [Obsolete("LoadOrDefaultAsync 不再对失败静默返回默认值，请使用 LoadOrCreateAsync 或 OpenSlot。")]
        public UniTask<T> LoadOrDefaultAsync<T>(
            int slotId,
            CancellationToken cancellationToken = default)
        {
            return LoadOrCreateAsync<T>(slotId, cancellationToken);
        }

        /// <summary>
        /// 加载指定槽位或在缺档时返回当前版本默认数据。
        /// 损坏、解密失败、版本不兼容和取消不会静默转换为默认数据。
        /// </summary>
        public async UniTask<T> LoadOrCreateAsync<T>(
            int slotId,
            CancellationToken cancellationToken = default)
        {
            LoadResult<T> result = await LoadAsync<T>(slotId, cancellationToken);
            if (result.HasUsableData)
                return result.Data;

            if (result.Status == SaveOperationStatus.Cancelled)
                throw new OperationCanceledException(cancellationToken);

            throw new SaveLoadException<T>(result);
        }

        /// <summary>
        /// 保存不隶属于任何玩家槽位的全局数据，例如图鉴、全局选项或账号级进度。
        /// 文件名为 <c>global_save</c> 加当前格式后缀，并使用独立备份链。
        /// </summary>
        /// <typeparam name="T">全局存档根类型。</typeparam>
        /// <param name="data">要保存的非空数据。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>保存结果。</returns>
        public UniTask<SaveResult> SaveGlobalAsync<T>(
            T data,
            CancellationToken cancellationToken = default)
        {
            return SaveTargetAsync(data, GetGlobalMainPath(), GetGlobalBackupPath(), cancellationToken);
        }

        /// <summary>
        /// 加载全局存档，并按主档、即时备份、历史备份的顺序自动恢复。
        /// 兼容旧版 <c>Global/global.save</c> 路径。
        /// </summary>
        /// <typeparam name="T">全局存档根类型。</typeparam>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>强类型加载结果。</returns>
        public UniTask<LoadResult<T>> LoadGlobalAsync<T>(CancellationToken cancellationToken = default)
        {
            string mainPath = GetGlobalMainPath();
            string backupPath = GetGlobalBackupPath();
            UseLegacyPathsWhenNeeded(
                ref mainPath,
                ref backupPath,
                GetLegacyGlobalMainPath(),
                GetLegacyGlobalBackupPath());
            return LoadTargetAsync<T>(mainPath, backupPath, cancellationToken);
        }

        /// <summary>
        /// 兼容旧名称的全局便捷加载方法。只有缺档或成功恢复时返回数据；损坏、取消
        /// 和其他失败会抛出 <see cref="SaveLoadException{T}"/>。
        /// </summary>
        /// <typeparam name="T">全局存档根类型。</typeparam>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>加载数据或新建的默认实例。</returns>
        [Obsolete("LoadGlobalOrDefaultAsync 不再对失败静默返回默认值，请使用 LoadGlobalOrCreateAsync。")]
        public UniTask<T> LoadGlobalOrDefaultAsync<T>(CancellationToken cancellationToken = default)
        {
            return LoadGlobalOrCreateAsync<T>(cancellationToken);
        }

        /// <summary>
        /// 加载全局存档或在缺档时返回当前版本默认数据。
        /// 损坏、解密失败、版本不兼容和取消不会静默转换为默认数据。
        /// </summary>
        public async UniTask<T> LoadGlobalOrCreateAsync<T>(CancellationToken cancellationToken = default)
        {
            LoadResult<T> result = await LoadGlobalAsync<T>(cancellationToken);
            if (result.HasUsableData)
                return result.Data;

            if (result.Status == SaveOperationStatus.Cancelled)
                throw new OperationCanceledException(cancellationToken);

            throw new SaveLoadException<T>(result);
        }

        /// <summary>
        /// 设置无槽位参数 API 使用的当前槽位。该方法只修改内存选择，不创建文件。
        /// </summary>
        /// <param name="slotId">大于 0 的槽位编号。</param>
        /// <returns>多槽位模式且编号有效时为 true；单槽位模式返回 false。</returns>
        public bool SelectSlot(int slotId)
        {
            if (!MultiSlotMode || slotId <= 0)
                return false;

            currentSlotId = slotId;
            return true;
        }

        /// <summary>
        /// 兼容旧 API 的目录准备方法。它不会创建真实存档，新代码应直接执行第一次保存。
        /// </summary>
        /// <param name="slotId">要准备的槽位编号，从 1 开始。</param>
        /// <returns>目录创建结果。</returns>
        [Obsolete("CreateSlot 只创建目录，不创建存档；请直接保存默认数据。")]
        public SaveResult CreateSlot(int slotId)
        {
            if (!MultiSlotMode)
                return SaveResult.Failure(
                    SaveOperationStatus.ModeNotSupported,
                    string.Empty,
                    "单槽位模式下不能创建槽位。");

            if (slotId <= 0)
                return SaveResult.Failure(
                    SaveOperationStatus.InvalidArgument,
                    string.Empty,
                    "Slot ID 必须大于 0。");

            string path = GetSlotsRoot();
            try
            {
                Directory.CreateDirectory(path);
                return SaveResult.Success(path);
            }
            catch (Exception exception)
            {
                return SaveResult.Failure(
                    SaveOperationStatus.UnknownError,
                    path,
                    exception.Message,
                    exception);
            }
        }

        /// <summary>
        /// 删除指定槽位的主档、即时备份、全部编号历史备份，以及对应的旧版槽位目录。
        /// 该操作不可由存档系统自动撤销。
        /// </summary>
        /// <param name="slotId">要删除的多槽位编号。</param>
        /// <param name="cancellationToken">取消等待槽位队列；实际删除开始后不会中途取消。</param>
        /// <returns>删除结果；不存在的槽位按成功处理。</returns>
        public async UniTask<SaveResult> DeleteSlotAsync(
            int slotId,
            CancellationToken cancellationToken = default)
        {
            if (!MultiSlotMode)
                return SaveResult.Failure(
                    SaveOperationStatus.ModeNotSupported,
                    string.Empty,
                    "单槽位模式下不能删除槽位。");

            if (slotId <= 0)
                return SaveResult.Failure(
                    SaveOperationStatus.InvalidArgument,
                    string.Empty,
                    "Slot ID 必须大于 0。");

            string mainPath = GetSlotMainPath(slotId);
            SemaphoreSlim gate = GetTargetLock(mainPath);
            try
            {
                await gate.WaitAsync(cancellationToken);
                try
                {
                    return await UniTask.RunOnThreadPool(
                        () => DeleteSlotCore(slotId),
                        true,
                        cancellationToken);
                }
                finally
                {
                    gate.Release();
                }
            }
            catch (OperationCanceledException exception)
            {
                return SaveResult.Failure(
                    SaveOperationStatus.Cancelled,
                    mainPath,
                    "删除槽位已取消。",
                    exception);
            }
        }

        /// <summary>
        /// 判断指定槽位是否存在主存档，同时识别当前扁平目录和旧版嵌套目录。
        /// 单槽位模式下仅槽位 1 是有效查询。
        /// </summary>
        /// <param name="slotId">槽位编号。</param>
        /// <returns>存在主存档文件时为 true。</returns>
        public bool SlotExists(int slotId)
        {
            if (!TryValidateSlotId(slotId, out _))
                return false;

            return File.Exists(GetSlotMainPath(slotId)) ||
                   File.Exists(GetLegacySlotMainPath(slotId));
        }

        /// <summary>
        /// 枚举多槽位模式下已有主存档的槽位摘要。当前格式文件优先，
        /// 同时补充尚未迁移的旧版 <c>Slots/Slot_N/main.save</c>。
        /// </summary>
        /// <returns>按槽位编号升序排列的快照；单槽位模式返回空集合。</returns>
        public IReadOnlyList<SaveSlotInfo> GetSlots()
        {
            if (!MultiSlotMode)
                return Array.Empty<SaveSlotInfo>();

            string slotsRoot = GetSlotsRoot();
            var slots = new Dictionary<int, SaveSlotInfo>();
            if (Directory.Exists(slotsRoot))
            {
                string extension = GetSaveExtension();
                foreach (string mainPath in Directory.GetFiles(
                             slotsRoot,
                             "slot_*" + extension,
                             SearchOption.TopDirectoryOnly))
                {
                    string name = Path.GetFileNameWithoutExtension(mainPath);
                    if (name == null || !name.StartsWith("slot_", StringComparison.OrdinalIgnoreCase))
                        continue;

                    string slotText = name.Substring("slot_".Length);
                    if (slotText.IndexOf("_bak", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        !int.TryParse(slotText, out int slotId) || slotId <= 0)
                        continue;

                    slots[slotId] = CreateSlotInfo(slotId, mainPath);
                }
            }

            // 枚举旧版 Slots/Slot_x/main.save，便于项目升级后继续发现尚未迁移的槽位。
            string legacySlotsRoot = Path.Combine(rootPath, LegacySlotsFolderName);
            if (Directory.Exists(legacySlotsRoot))
            {
                foreach (string directory in Directory.GetDirectories(
                             legacySlotsRoot,
                             "Slot_*",
                             SearchOption.TopDirectoryOnly))
                {
                    string directoryName = Path.GetFileName(directory);
                    string slotText = directoryName?.Substring("Slot_".Length);
                    if (!int.TryParse(slotText, out int slotId) || slotId <= 0 || slots.ContainsKey(slotId))
                        continue;

                    string legacyMainPath = Path.Combine(directory, LegacyMainFileName);
                    if (File.Exists(legacyMainPath))
                        slots[slotId] = CreateSlotInfo(slotId, legacyMainPath);
                }
            }

            return slots.Values.OrderBy(info => info.SlotId).ToArray();
        }

        /// <summary>
        /// 获取指定槽位可供手动回档的编号历史备份。即时 <c>_bak</c> 文件由自动恢复流程使用，
        /// 不包含在返回列表中。未传槽位时使用 <see cref="CurrentSlotId"/>。
        /// </summary>
        /// <param name="slotId">可选槽位编号。</param>
        /// <returns>按代数和保存时间倒序排列的历史快照。</returns>
        public IReadOnlyList<SaveHistoryInfo> GetHistory(int? slotId = null)
        {
            int resolvedSlotId = ResolveSlotId(slotId);
            if (!TryValidateSlotId(resolvedSlotId, out _))
                return Array.Empty<SaveHistoryInfo>();

            string mainPath = GetSlotMainPath(resolvedSlotId);
            string backupPath = GetSlotBackupPath(resolvedSlotId);
            UseLegacyPathsWhenNeeded(
                ref mainPath,
                ref backupPath,
                GetLegacySlotMainPath(resolvedSlotId),
                GetLegacySlotBackupPath(resolvedSlotId));
            return SaveFileStore.GetHistory(mainPath, backupPath);
        }

        /// <summary>
        /// 获取全局存档可供手动回档的编号历史备份，不包含即时备份。
        /// </summary>
        /// <returns>按代数和保存时间倒序排列的历史快照。</returns>
        public IReadOnlyList<SaveHistoryInfo> GetGlobalHistory()
        {
            string mainPath = GetGlobalMainPath();
            string backupPath = GetGlobalBackupPath();
            UseLegacyPathsWhenNeeded(
                ref mainPath,
                ref backupPath,
                GetLegacyGlobalMainPath(),
                GetLegacyGlobalBackupPath());
            return SaveFileStore.GetHistory(mainPath, backupPath);
        }

        /// <summary>
        /// 将选中的历史文件读取为当前 SaveData，再作为新一代主存档提交。
        /// 当前主档仍会按配置进入 Backup / History，因此回档本身也可撤销。
        /// </summary>
        /// <typeparam name="T">历史文件中保存的根数据类型。</typeparam>
        /// <param name="history">必须来自当前目标 <see cref="GetHistory"/> 结果的历史记录。</param>
        /// <param name="slotId">可选槽位编号；省略时使用当前槽位。</param>
        /// <param name="cancellationToken">取消读取或重新提交。</param>
        /// <returns>回档重新提交的保存结果。</returns>
        public UniTask<SaveResult> RestoreHistoryAsync<T>(
            SaveHistoryInfo history,
            int? slotId = null,
            CancellationToken cancellationToken = default)
        {
            int resolvedSlotId = ResolveSlotId(slotId);
            if (!TryValidateSlotId(resolvedSlotId, out string error))
            {
                return UniTask.FromResult(SaveResult.Failure(
                    SaveOperationStatus.InvalidArgument,
                    string.Empty,
                    error));
            }

            string mainPath = GetSlotMainPath(resolvedSlotId);
            string backupPath = GetSlotBackupPath(resolvedSlotId);
            UseLegacyPathsWhenNeeded(
                ref mainPath,
                ref backupPath,
                GetLegacySlotMainPath(resolvedSlotId),
                GetLegacySlotBackupPath(resolvedSlotId));
            return RestoreHistoryTargetAsync<T>(history, mainPath, backupPath, cancellationToken);
        }

        /// <summary>
        /// 将一份全局历史备份读取并重新提交为当前全局主存档。
        /// 当前全局主档仍会进入备份链，因此该操作可再次撤销。
        /// </summary>
        /// <typeparam name="T">历史文件中保存的根数据类型。</typeparam>
        /// <param name="history">必须来自 <see cref="GetGlobalHistory"/> 的历史记录。</param>
        /// <param name="cancellationToken">取消读取或重新提交。</param>
        /// <returns>回档重新提交的保存结果。</returns>
        public UniTask<SaveResult> RestoreGlobalHistoryAsync<T>(
            SaveHistoryInfo history,
            CancellationToken cancellationToken = default)
        {
            string mainPath = GetGlobalMainPath();
            string backupPath = GetGlobalBackupPath();
            UseLegacyPathsWhenNeeded(
                ref mainPath,
                ref backupPath,
                GetLegacyGlobalMainPath(),
                GetLegacyGlobalBackupPath());
            return RestoreHistoryTargetAsync<T>(history, mainPath, backupPath, cancellationToken);
        }

        /// <summary>
        /// 定时调用 Capture 并保存当前槽位。Dispose 返回值即可停止自动存档。
        /// Capture 始终在 Unity 主线程执行，文件保存仍走普通 Save Queue。
        /// </summary>
        /// <typeparam name="T">存档根类型。</typeparam>
        /// <param name="capture">在主线程抓取当前游戏状态并返回独立存档数据的函数。</param>
        /// <param name="interval">每次保存完成后到下一次抓取前的等待间隔，必须大于 0。</param>
        /// <param name="onCompleted">可选主线程回调，每次保存结束后执行。</param>
        /// <returns>控制自动存档生命周期的句柄；调用 <see cref="IDisposable.Dispose"/> 停止后续循环。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="capture"/> 为空。</exception>
        /// <exception cref="ArgumentOutOfRangeException">间隔无效，或当前槽位无效。</exception>
        public IDisposable StartAutoSave<T>(
            Func<T> capture,
            TimeSpan interval,
            Action<SaveResult> onCompleted = null)
        {
            return StartAutoSave(capture, interval, ResolveSlotId(null), onCompleted);
        }

        /// <summary>
        /// 为指定槽位启动自动存档。抓取和完成回调在 Unity 主线程执行，
        /// 编码在调用时冻结数据，文件工作在后台并与手动操作共用目标队列。
        /// </summary>
        /// <typeparam name="T">存档根类型。</typeparam>
        /// <param name="capture">在主线程抓取当前游戏状态的函数。</param>
        /// <param name="interval">保存完成后到下一次抓取前的等待间隔。</param>
        /// <param name="slotId">固定目标槽位编号。</param>
        /// <param name="onCompleted">可选主线程完成回调。</param>
        /// <returns>用于停止自动存档的句柄。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="capture"/> 为空。</exception>
        /// <exception cref="ArgumentOutOfRangeException">间隔或槽位编号无效。</exception>
        public IDisposable StartAutoSave<T>(
            Func<T> capture,
            TimeSpan interval,
            int slotId,
            Action<SaveResult> onCompleted = null)
        {
            if (capture == null)
                throw new ArgumentNullException(nameof(capture));
            if (interval <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(interval), "自动存档间隔必须大于 0。");
            if (!TryValidateSlotId(slotId, out string error))
                throw new ArgumentOutOfRangeException(nameof(slotId), error);

            return new AutoSaveHandle<T>(
                capture,
                interval,
                data => SaveAsync(data, slotId),
                onCompleted);
        }

        /// <summary>
        /// 启动全局数据自动存档。行为与槽位自动存档一致，但写入独立的全局文件及备份链。
        /// </summary>
        /// <typeparam name="T">全局存档根类型。</typeparam>
        /// <param name="capture">在主线程抓取当前全局状态的函数。</param>
        /// <param name="interval">保存完成后到下一次抓取前的等待间隔。</param>
        /// <param name="onCompleted">可选主线程完成回调。</param>
        /// <returns>用于停止自动存档的句柄。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="capture"/> 为空。</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="interval"/> 不大于 0。</exception>
        public IDisposable StartGlobalAutoSave<T>(
            Func<T> capture,
            TimeSpan interval,
            Action<SaveResult> onCompleted = null)
        {
            if (capture == null)
                throw new ArgumentNullException(nameof(capture));
            if (interval <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(interval), "自动存档间隔必须大于 0。");

            return new AutoSaveHandle<T>(
                capture,
                interval,
                data => SaveGlobalAsync(data),
                onCompleted);
        }

        private async UniTask<SaveResult> SaveTargetAsync<T>(
            T data,
            string mainPath,
            string backupPath,
            CancellationToken cancellationToken)
        {
            if (data is null)
                return SaveResult.Failure(
                    SaveOperationStatus.InvalidArgument,
                    mainPath,
                    "SaveData 不能为 null。");

            SemaphoreSlim gate = GetTargetLock(mainPath);
            GlobalSettingsAssetSnapshot settings;
            byte[] snapshotPayload;
            try
            {
                settings = CaptureSettings();
                SaveSchema.Validate<T>();
                // 在第一次 await 之前冻结调用时刻的数据，后续游戏逻辑即使继续修改
                // 原 SaveData，也不会影响已经排队的这一笔保存。
                snapshotPayload = DataUtil.SerializeSaveValue(
                    data,
                    settings.Format,
                    settings.Encrypt,
                    settings.Compress);
            }
            catch (SaveSchemaValidationException exception)
            {
                return SaveResult.Failure(
                    SaveOperationStatus.SchemaInvalid,
                    mainPath,
                    exception.Message,
                    exception);
            }
            catch (SerializationAbortException exception)
            {
                return SaveResult.Failure(
                    SaveOperationStatus.SerializationFailed,
                    mainPath,
                    exception.Message,
                    exception);
            }
            catch (CryptographicException exception)
            {
                return SaveResult.Failure(
                    SaveOperationStatus.SerializationFailed,
                    mainPath,
                    "存档加密失败。",
                    exception);
            }
            catch (Exception exception)
            {
                return SaveResult.Failure(
                    SaveOperationStatus.SerializationFailed,
                    mainPath,
                    exception.Message,
                    exception);
            }

            try
            {
                await gate.WaitAsync(cancellationToken);
                try
                {
                    return await UniTask.RunOnThreadPool(
                        () => SaveCore(
                            snapshotPayload,
                            typeof(T).FullName ?? typeof(T).Name,
                            mainPath,
                            backupPath,
                            settings,
                            cancellationToken),
                        true,
                        cancellationToken);
                }
                finally
                {
                    gate.Release();
                }
            }
            catch (OperationCanceledException exception)
            {
                return SaveResult.Failure(
                    SaveOperationStatus.Cancelled,
                    mainPath,
                    "保存已取消。",
                    exception);
            }
        }

        private async UniTask<SaveResult> RestoreHistoryTargetAsync<T>(
            SaveHistoryInfo history,
            string mainPath,
            string backupPath,
            CancellationToken cancellationToken)
        {
            if (history == null || string.IsNullOrWhiteSpace(history.Path))
            {
                return SaveResult.Failure(
                    SaveOperationStatus.InvalidArgument,
                    mainPath,
                    "必须提供有效的历史存档记录。");
            }

            string historyPath;
            try
            {
                historyPath = Path.GetFullPath(history.Path);
            }
            catch (Exception exception)
            {
                return SaveResult.Failure(
                    SaveOperationStatus.InvalidArgument,
                    history.Path,
                    "历史存档路径无效。",
                    exception);
            }

            if (!SaveFileStore.IsHistoryPath(mainPath, backupPath, historyPath) ||
                !File.Exists(historyPath))
            {
                return SaveResult.Failure(
                    SaveOperationStatus.FileNotFound,
                    historyPath,
                    "历史存档不存在，或不属于当前存档目标。");
            }

            try
            {
                byte[] container = await UniTask.RunOnThreadPool(
                    () => File.ReadAllBytes(historyPath),
                    true,
                    cancellationToken);
                T data = SaveFileStore.Unpack<T>(container, out _);
                return await SaveTargetAsync(data, mainPath, backupPath, cancellationToken);
            }
            catch (OperationCanceledException exception)
            {
                return SaveResult.Failure(
                    SaveOperationStatus.Cancelled,
                    historyPath,
                    "历史回档已取消。",
                    exception);
            }
            catch (Exception exception)
            {
                return SaveResult.Failure(
                    ClassifyLoadFailure(exception),
                    historyPath,
                    exception.Message,
                    exception);
            }
        }

        private SaveResult SaveCore(
            byte[] snapshotPayload,
            string dataType,
            string mainPath,
            string backupPath,
            GlobalSettingsAssetSnapshot settings,
            CancellationToken cancellationToken)
        {
            try
            {
                long generation = GetNextGeneration(mainPath, backupPath);
                byte[] container = SaveFileStore.PackPayload(
                    snapshotPayload,
                    settings.Format,
                    settings.Encrypt,
                    settings.Compress,
                    generation,
                    DateTime.UtcNow,
                    dataType);

                // 原子替换开始前最后一次响应取消；进入 Commit 后必须完成提交或清理。
                cancellationToken.ThrowIfCancellationRequested();
                SaveFileStore.Commit(
                    mainPath,
                    backupPath,
                    container,
                    settings.EnableHistory,
                    settings.HistoryLimit);

                LogUtil.Success("SaveSystem", $"存档保存成功：{mainPath}");
                return SaveResult.Success(mainPath);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (SaveCommitException exception)
            {
                return SaveResult.Failure(
                    exception.Status,
                    mainPath,
                    exception.Message,
                    exception.InnerException ?? exception);
            }
            catch (Exception exception)
            {
                return SaveResult.Failure(
                    SaveOperationStatus.UnknownError,
                    mainPath,
                    exception.Message,
                    exception);
            }
        }

        private async UniTask<LoadResult<T>> LoadTargetAsync<T>(
            string mainPath,
            string backupPath,
            CancellationToken cancellationToken)
        {
            SemaphoreSlim gate = GetTargetLock(mainPath);
            try
            {
                await gate.WaitAsync(cancellationToken);
                try
                {
                    return await UniTask.RunOnThreadPool(
                        () => LoadCore<T>(mainPath, backupPath, cancellationToken),
                        true,
                        cancellationToken);
                }
                finally
                {
                    gate.Release();
                }
            }
            catch (OperationCanceledException exception)
            {
                return new LoadResult<T>(
                    SaveOperationStatus.Cancelled,
                    SaveDataSource.None,
                    default,
                    mainPath,
                    "加载已取消。",
                    exception);
            }
        }

        private LoadResult<T> LoadCore<T>(
            string mainPath,
            string backupPath,
            CancellationToken cancellationToken)
        {
            try
            {
                SaveSchema.Validate<T>();
            }
            catch (SaveSchemaValidationException exception)
            {
                return new LoadResult<T>(
                    SaveOperationStatus.SchemaInvalid,
                    SaveDataSource.None,
                    default,
                    mainPath,
                    exception.Message,
                    exception);
            }

            IReadOnlyList<string> backupCandidates = SaveFileStore.GetBackupPaths(mainPath, backupPath);
            if (!File.Exists(mainPath) && backupCandidates.Count == 0)
            {
                T defaults = SaveSchema.CreateDefault<T>();
                return new LoadResult<T>(
                    SaveOperationStatus.FileNotFound,
                    SaveDataSource.Default,
                    defaults,
                    mainPath,
                    "存档不存在，已返回当前版本默认数据。");
            }

            var attempts = new List<(string Path, SaveDataSource Source)>();
            if (File.Exists(mainPath))
                attempts.Add((mainPath, SaveDataSource.Main));
            for (int i = 0; i < backupCandidates.Count; i++)
            {
                string candidatePath = backupCandidates[i];
                attempts.Add((
                    candidatePath,
                    PathsEqual(candidatePath, backupPath)
                        ? SaveDataSource.Backup
                        : SaveDataSource.History));
            }

            Exception lastException = null;
            SaveOperationStatus lastStatus = SaveOperationStatus.RecoveryFailed;
            foreach ((string path, SaveDataSource source) in attempts)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    byte[] container = File.ReadAllBytes(path);
                    T data = SaveFileStore.Unpack<T>(container, out _);
                    if (source != SaveDataSource.Main)
                        LogUtil.Warn("SaveSystem", $"主存档不可用，已从 {source} 恢复：{path}");

                    return new LoadResult<T>(
                        SaveOperationStatus.Success,
                        source,
                        data,
                        path,
                        source == SaveDataSource.Main ? string.Empty : $"已从 {source} 恢复存档。");
                }
                catch (Exception exception)
                {
                    lastException = exception;
                    lastStatus = ClassifyLoadFailure(exception);
                    LogUtil.Warn("SaveSystem", $"存档候选读取失败：{path} → {exception.Message}");
                }
            }

            return new LoadResult<T>(
                lastStatus,
                SaveDataSource.None,
                default,
                mainPath,
                "主存档、备份和历史存档均无法恢复。",
                lastException);
        }

        private static SaveOperationStatus ClassifyLoadFailure(Exception exception)
        {
            return exception switch
            {
                SaveChecksumException => SaveOperationStatus.ChecksumFailed,
                CryptographicException => SaveOperationStatus.DecryptionFailed,
                SerializationAbortException => SaveOperationStatus.SchemaIncompatible,
                JsonException => SaveOperationStatus.DeserializationFailed,
                InvalidDataException => SaveOperationStatus.ValidationFailed,
                _ => SaveOperationStatus.DeserializationFailed
            };
        }

        private SaveResult DeleteSlotCore(int slotId)
        {
            string slotsRoot = GetSlotsRoot();
            string slotStem = $"slot_{slotId:D2}";
            try
            {
                if (Directory.Exists(slotsRoot))
                {
                    // 不能使用 slot_01.*：该模式匹配不到 slot_01_bak、slot_01_bak1 等文件。
                    foreach (string path in Directory.GetFiles(
                                 slotsRoot,
                                 slotStem + "*",
                                 SearchOption.TopDirectoryOnly))
                    {
                        string fileStem = Path.GetFileNameWithoutExtension(path);
                        string suffix = fileStem?.Substring(Math.Min(fileStem.Length, slotStem.Length)) ?? string.Empty;
                        if (string.Equals(fileStem, slotStem, StringComparison.OrdinalIgnoreCase) ||
                            (suffix.StartsWith("_bak", StringComparison.OrdinalIgnoreCase) &&
                             (suffix.Length == 4 ||
                              int.TryParse(suffix.Substring(4), out int backupIndex) && backupIndex > 0)))
                        {
                            File.Delete(path);
                        }
                    }
                }

                string legacyDirectory = Path.Combine(
                    rootPath,
                    LegacySlotsFolderName,
                    $"Slot_{slotId}");
                if (Directory.Exists(legacyDirectory))
                    Directory.Delete(legacyDirectory, true);

                return SaveResult.Success(GetSlotMainPath(slotId));
            }
            catch (Exception exception)
            {
                return SaveResult.Failure(
                    SaveOperationStatus.DeleteFailed,
                    GetSlotMainPath(slotId),
                    exception.Message,
                    exception);
            }
        }

        private static bool PathsEqual(string left, string right)
        {
            if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right))
                return false;

            return string.Equals(
                Path.GetFullPath(left),
                Path.GetFullPath(right),
                StringComparison.OrdinalIgnoreCase);
        }

        private static SaveSlotInfo CreateSlotInfo(int slotId, string mainPath)
        {
            var info = new SaveSlotInfo
            {
                SlotId = slotId,
                HasData = File.Exists(mainPath),
                Path = mainPath
            };

            if (!info.HasData)
                return info;

            try
            {
                SaveFileHeader header = SaveFileStore.ReadHeader(mainPath);
                info.Generation = header.Generation;
                info.LastSavedUtc = header.SavedUtc;
            }
            catch (Exception exception)
            {
                LogUtil.Warn("SaveSystem", $"槽位 {slotId} 的主存档无效：{exception.Message}");
            }

            return info;
        }

        private long GetNextGeneration(string mainPath, string backupPath)
        {
            long generation = 0;
            foreach (string path in new[] { mainPath, backupPath })
            {
                if (!File.Exists(path))
                    continue;
                try
                {
                    generation = Math.Max(generation, SaveFileStore.ReadHeader(path).Generation);
                }
                catch
                {
                    // 损坏的旧候选不参与新一代编号计算。
                }
            }
            return generation + 1;
        }

        private GlobalSettingsAssetSnapshot CaptureSettings()
        {
            var settings = GetSettings();
            // JSON 模式必须保持为可直接打开的裸 JSON，因此不启用 GZip。
            // Binary 模式则按存档设置使用压缩；AES 开关仍复用数据管线配置。
            bool compress = settings.SaveDataLoadMode == EnvironmentState.DataLoadMode.Binary &&
                            settings.EnableSaveCompression;
            return new GlobalSettingsAssetSnapshot(
                settings.SaveDataLoadMode,
                settings.SaveDataLoadMode == EnvironmentState.DataLoadMode.Binary && settings.EnableEncryption,
                compress,
                settings.EnableSaveHistory,
                Math.Max(0, settings.SaveHistoryLimit));
        }

        private static GlobalSettingsAsset GetSettings()
        {
            if (!GlobalSettingsRuntimeLoader.TryGet(out var settings) || settings == null)
                throw new InvalidOperationException("GlobalSettingsAsset 未加载，SaveSystem 无法初始化。");
            return settings;
        }

        private int ResolveSlotId(int? slotId)
        {
            if (!MultiSlotMode)
                return DefaultSlotId;
            return slotId ?? currentSlotId;
        }

        private bool TryValidateSlotId(int slotId, out string error)
        {
            if (slotId <= 0)
            {
                error = "Slot ID 必须大于 0。";
                return false;
            }

            if (!MultiSlotMode && slotId != DefaultSlotId)
            {
                error = "单槽位模式只能使用 Slot 1。";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private SemaphoreSlim GetTargetLock(string mainPath)
        {
            return targetLocks.GetOrAdd(mainPath, _ => new SemaphoreSlim(1, 1));
        }

        private string GetGlobalMainPath() => Path.Combine(rootPath, GlobalFileStem + GetSaveExtension());
        private string GetGlobalBackupPath() => Path.Combine(rootPath, GlobalBackupFileStem + GetSaveExtension());
        private string GetSlotsRoot() => Path.Combine(rootPath, SlotsFolderName);
        private string GetSlotMainPath(int slotId) => Path.Combine(GetSlotsRoot(), $"slot_{slotId:D2}" + GetSaveExtension());
        private string GetSlotBackupPath(int slotId) => Path.Combine(GetSlotsRoot(), $"slot_{slotId:D2}_bak" + GetSaveExtension());
        private string GetLegacySlotMainPath(int slotId) => Path.Combine(rootPath, LegacySlotsFolderName, $"Slot_{slotId}", LegacyMainFileName);
        private string GetLegacySlotBackupPath(int slotId) => Path.Combine(rootPath, LegacySlotsFolderName, $"Slot_{slotId}", LegacyBackupFileName);
        private string GetLegacyGlobalMainPath() => Path.Combine(rootPath, "Global", LegacyGlobalFileName);
        private string GetLegacyGlobalBackupPath() => Path.Combine(rootPath, "Global", LegacyGlobalBackupFileName);

        private static string GetSaveExtension()
        {
            GlobalSettingsAsset settings = GetSettings();
            if (settings.SaveDataLoadMode == EnvironmentState.DataLoadMode.Json)
                return ".json";

            return GlobalSettingsAsset.NormalizeSaveBinaryExtension(settings.SaveBinaryExtension);
        }

        private static void UseLegacyPathsWhenNeeded(
            ref string mainPath,
            ref string backupPath,
            string legacyMainPath,
            string legacyBackupPath)
        {
            if (File.Exists(mainPath) || File.Exists(backupPath))
                return;

            if (File.Exists(legacyMainPath) || File.Exists(legacyBackupPath))
            {
                mainPath = legacyMainPath;
                backupPath = legacyBackupPath;
            }
        }

        private readonly struct GlobalSettingsAssetSnapshot
        {
            public readonly EnvironmentState.DataLoadMode Format;
            public readonly bool Encrypt;
            public readonly bool Compress;
            public readonly bool EnableHistory;
            public readonly int HistoryLimit;

            public GlobalSettingsAssetSnapshot(
                EnvironmentState.DataLoadMode format,
                bool encrypt,
                bool compress,
                bool enableHistory,
                int historyLimit)
            {
                Format = format;
                Encrypt = encrypt;
                Compress = compress;
                EnableHistory = enableHistory;
                HistoryLimit = historyLimit;
            }
        }

        private sealed class AutoSaveHandle<T> : IDisposable
        {
            private readonly Func<T> capture;
            private readonly TimeSpan interval;
            private readonly Func<T, UniTask<SaveResult>> save;
            private readonly Action<SaveResult> onCompleted;
            private readonly CancellationTokenSource cancellation = new();
            private bool disposed;

            public AutoSaveHandle(
                Func<T> capture,
                TimeSpan interval,
                Func<T, UniTask<SaveResult>> save,
                Action<SaveResult> onCompleted)
            {
                this.capture = capture;
                this.interval = interval;
                this.save = save;
                this.onCompleted = onCompleted;
                RunAsync().Forget();
            }

            public void Dispose()
            {
                if (disposed)
                    return;

                disposed = true;
                cancellation.Cancel();
                cancellation.Dispose();
            }

            private async UniTaskVoid RunAsync()
            {
                CancellationToken token = cancellation.Token;
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        await UniTask.Delay(interval, cancellationToken: token);
                        await UniTask.SwitchToMainThread(token);
                        T data = capture();
                        SaveResult result = await save(data);
                        await UniTask.SwitchToMainThread(token);
                        Notify(result);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception exception)
                    {
                        LogUtil.Error("SaveSystem", $"自动存档失败：{exception.Message}");
                        Notify(SaveResult.Failure(
                            SaveOperationStatus.UnknownError,
                            string.Empty,
                            exception.Message,
                            exception));
                    }
                }
            }

            private void Notify(SaveResult result)
            {
                try
                {
                    onCompleted?.Invoke(result);
                }
                catch (Exception exception)
                {
                    LogUtil.Error("SaveSystem", $"自动存档回调异常：{exception.Message}");
                }
            }
        }
    }
}
