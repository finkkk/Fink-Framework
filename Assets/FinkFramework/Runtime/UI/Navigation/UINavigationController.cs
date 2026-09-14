using System.Collections.Generic;

namespace FinkFramework.Runtime.UI.Navigation
{
    /// <summary>只维护每个 Surface 的页面顺序，不直接操作面板生命周期。</summary>
    internal sealed class UINavigationController
    {
        private readonly Dictionary<UISurfaceId, List<UIPanelKey>> stacks = new();

        /// <summary>把页面移到栈顶，并返回此前位于栈顶的页面。</summary>
        public bool Push(UIPanelKey key, out UIPanelKey previousTop)
        {
            List<UIPanelKey> stack = GetOrCreate(key.SurfaceId);
            if (stack.Count > 0 && stack[^1].Equals(key))
            {
                previousTop = default;
                return false;
            }

            stack.Remove(key);
            previousTop = stack.Count > 0 ? stack[^1] : default;
            stack.Add(key);
            return true;
        }

        /// <summary>移除页面；如果它原本在栈顶，返回需要恢复的新栈顶页面。</summary>
        public bool Remove(UIPanelKey key, out UIPanelKey nextTop)
        {
            nextTop = default;
            if (!stacks.TryGetValue(key.SurfaceId, out List<UIPanelKey> stack))
                return false;

            int index = stack.IndexOf(key);
            if (index < 0)
                return false;

            bool wasTop = index == stack.Count - 1;
            stack.RemoveAt(index);
            if (stack.Count == 0)
            {
                stacks.Remove(key.SurfaceId);
                return true;
            }

            if (wasTop)
                nextTop = stack[^1];
            return true;
        }

        public bool TryPeek(UISurfaceId surfaceId, out UIPanelKey key)
        {
            if (stacks.TryGetValue(surfaceId, out List<UIPanelKey> stack) && stack.Count > 0)
            {
                key = stack[^1];
                return true;
            }

            key = default;
            return false;
        }

        public int GetDepth(UISurfaceId surfaceId) =>
            stacks.TryGetValue(surfaceId, out List<UIPanelKey> stack) ? stack.Count : 0;

        /// <summary>返回当前顺序的副本，调用方可以安全地在遍历时修改真实栈。</summary>
        public List<UIPanelKey> Snapshot(UISurfaceId surfaceId) =>
            stacks.TryGetValue(surfaceId, out List<UIPanelKey> stack)
                ? new List<UIPanelKey>(stack)
                : new List<UIPanelKey>();

        /// <summary>
        /// 计算指定导航操作需要移除的页面，顺序始终从栈顶到底部。
        /// 这里只计算状态，不触发任何面板生命周期。
        /// </summary>
        public List<UIPanelKey> GetRemovalPlan(UIPanelKey targetKey, UINavigationMode mode)
        {
            var result = new List<UIPanelKey>();
            if (mode == UINavigationMode.Push
                || !stacks.TryGetValue(targetKey.SurfaceId, out List<UIPanelKey> stack)
                || stack.Count == 0)
                return result;

            if (mode == UINavigationMode.Replace)
            {
                UIPanelKey currentTop = stack[^1];
                if (!currentTop.Equals(targetKey))
                    result.Add(currentTop);
                return result;
            }

            int targetIndex = stack.IndexOf(targetKey);
            if (mode == UINavigationMode.PopTo)
            {
                if (targetIndex < 0)
                    return result;

                for (int index = stack.Count - 1; index > targetIndex; index--)
                    result.Add(stack[index]);
                return result;
            }

            if (mode == UINavigationMode.Reset)
            {
                for (int index = stack.Count - 1; index >= 0; index--)
                {
                    if (!stack[index].Equals(targetKey))
                        result.Add(stack[index]);
                }
            }

            return result;
        }

        public void Clear(UISurfaceId surfaceId) => stacks.Remove(surfaceId);

        public void ClearAll() => stacks.Clear();

        private List<UIPanelKey> GetOrCreate(UISurfaceId surfaceId)
        {
            if (stacks.TryGetValue(surfaceId, out List<UIPanelKey> stack))
                return stack;

            stack = new List<UIPanelKey>();
            stacks.Add(surfaceId, stack);
            return stack;
        }
    }
}
