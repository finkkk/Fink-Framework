using System.Collections.Generic;

namespace FinkFramework.Runtime.UI.Core
{
    /// <summary>
    /// 面板记录仓库。这里只维护身份与实例，不负责加载、生命周期或显示策略。
    /// </summary>
    internal sealed class UIPanelRepository
    {
        private readonly Dictionary<UIPanelKey, UIPanelRecord> records = new();

        public bool TryGet(UIPanelKey key, out UIPanelRecord record) =>
            records.TryGetValue(key, out record);

        public bool Add(UIPanelRecord record) => records.TryAdd(record.Key, record);

        public bool Remove(UIPanelKey key, out UIPanelRecord record)
        {
            if (!records.Remove(key, out record))
                return false;

            return true;
        }

        public List<UIPanelRecord> Snapshot() => new(records.Values);
    }
}
