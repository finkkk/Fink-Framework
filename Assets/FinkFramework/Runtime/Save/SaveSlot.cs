using System;
using System.Threading;
using Cysharp.Threading.Tasks;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace FinkFramework.Runtime.Save
{
    /// <summary>
    /// 一个显式槽位的轻量使用封装。
    /// 它不会引入全局当前槽位，项目只需在创建时指定一次槽位编号。
    /// </summary>
    /// <typeparam name="T">槽位根数据类型。</typeparam>
    public sealed class SaveSlot<T>
    {
        private readonly SaveManager storage;

        internal SaveSlot(SaveManager storage, int slotId)
        {
            this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
            SlotId = slotId;
        }

        /// <summary>此封装绑定的槽位编号。</summary>
        public int SlotId { get; }

        /// <summary>最近一次成功加载或保存的数据。</summary>
        public T Data { get; private set; }

        /// <summary>当前是否已经通过加载或保存获得可用数据。</summary>
        public bool HasData { get; private set; }

        /// <summary>最近一次加载结果。</summary>
        public LoadResult<T> LastLoad { get; private set; }

        /// <summary>最近一次保存结果。</summary>
        public SaveResult LastSave { get; private set; }

        /// <summary>当前槽位是否存在主存档或旧版存档。</summary>
        public bool Exists => storage.SlotExists(SlotId);

        /// <summary>加载槽位。主档不可用时会按底层策略尝试备份和历史档。</summary>
        public async UniTask<LoadResult<T>> LoadAsync(CancellationToken cancellationToken = default)
        {
            LoadResult<T> result = await storage.LoadAsync<T>(SlotId, cancellationToken);
            LastLoad = result;
            if (result.HasUsableData)
            {
                Data = result.Data;
                HasData = true;
            }

            return result;
        }

        /// <summary>
        /// 加载或创建默认数据。
        /// 只有“文件不存在”会创建默认数据；损坏、解密失败和版本不兼容会抛出
        /// <see cref="SaveLoadException{T}"/>，避免调用方误把损坏存档覆盖掉。
        /// </summary>
        public async UniTask<T> LoadOrCreateAsync(CancellationToken cancellationToken = default)
        {
            LoadResult<T> result = await LoadAsync(cancellationToken);
            if (result.HasUsableData)
                return Data;

            if (result.Status == SaveOperationStatus.Cancelled)
                throw new OperationCanceledException(cancellationToken);

            throw new SaveLoadException<T>(result);
        }

        /// <summary>保存指定数据，只有提交成功后才更新本封装内的数据引用。</summary>
        public async UniTask<SaveResult> SaveAsync(
            T data,
            CancellationToken cancellationToken = default)
        {
            SaveResult result = await storage.SaveAsync(data, SlotId, cancellationToken);
            LastSave = result;
            if (result.Succeeded)
            {
                Data = data;
                HasData = true;
            }

            return result;
        }

        /// <summary>保存最近一次成功加载或保存的数据。</summary>
        public UniTask<SaveResult> SaveAsync(CancellationToken cancellationToken = default)
        {
            if (!HasData)
                return UniTask.FromResult(SaveResult.Failure(
                    SaveOperationStatus.InvalidArgument,
                    string.Empty,
                    "当前槽位尚未加载或设置数据，无法执行无参数保存。"));

            return SaveAsync(Data, cancellationToken);
        }

        /// <summary>
        /// 将已从备份读取的数据重新提交为主存档。
        /// 项目完成自己的版本迁移后再调用此方法。
        /// </summary>
        public UniTask<SaveResult> RepairAsync(CancellationToken cancellationToken = default)
        {
            if (LastLoad == null || !LastLoad.HasUsableData || !LastLoad.NeedsRepair)
                return UniTask.FromResult(SaveResult.Failure(
                    SaveOperationStatus.InvalidArgument,
                    string.Empty,
                    "当前槽位最近一次加载不是从备份或历史存档恢复，无法执行修复提交。"));

            return SaveAsync(LastLoad.Data, cancellationToken);
        }
    }
}
