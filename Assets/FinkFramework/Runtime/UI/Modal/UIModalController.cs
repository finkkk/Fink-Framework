using System.Collections.Generic;

namespace FinkFramework.Runtime.UI.Modal
{
    /// <summary>维护每个 Surface 的 Modal 顺序以及关闭后应恢复的焦点对象。</summary>
    internal sealed class UIModalController
    {
        private sealed class Entry
        {
            public UIPanelKey Key;
            public UIPanelKey UnderlyingKey;
        }

        private readonly Dictionary<UISurfaceId, List<Entry>> stacks = new();

        public bool HasModal(UISurfaceId surfaceId) =>
            stacks.TryGetValue(surfaceId, out List<Entry> stack) && stack.Count > 0;

        public bool TryPeek(UISurfaceId surfaceId, out UIPanelKey key)
        {
            if (stacks.TryGetValue(surfaceId, out List<Entry> stack) && stack.Count > 0)
            {
                key = stack[^1].Key;
                return true;
            }

            key = default;
            return false;
        }

        public void Push(UIPanelKey key, UIPanelKey underlyingKey)
        {
            List<Entry> stack = GetOrCreate(key.SurfaceId);
            int existingIndex = stack.FindIndex(entry => entry.Key.Equals(key));
            if (existingIndex >= 0)
                stack.RemoveAt(existingIndex);

            stack.Add(new Entry { Key = key, UnderlyingKey = underlyingKey });
        }

        public bool Remove(UIPanelKey key, out UIPanelKey restoreKey)
        {
            restoreKey = default;
            if (!stacks.TryGetValue(key.SurfaceId, out List<Entry> stack))
                return false;

            int index = stack.FindIndex(entry => entry.Key.Equals(key));
            if (index < 0)
                return false;

            Entry removed = stack[index];
            bool wasTop = index == stack.Count - 1;
            stack.RemoveAt(index);

            foreach (Entry entry in stack)
            {
                if (entry.UnderlyingKey.Equals(key))
                    entry.UnderlyingKey = removed.UnderlyingKey;
            }

            if (wasTop)
                restoreKey = removed.UnderlyingKey;

            if (stack.Count == 0)
                stacks.Remove(key.SurfaceId);
            return true;
        }

        public void Clear(UISurfaceId surfaceId) => stacks.Remove(surfaceId);
        public void ClearAll() => stacks.Clear();

        private List<Entry> GetOrCreate(UISurfaceId surfaceId)
        {
            if (stacks.TryGetValue(surfaceId, out List<Entry> stack))
                return stack;

            stack = new List<Entry>();
            stacks.Add(surfaceId, stack);
            return stack;
        }
    }
}
