using System.Collections.Generic;
using FinkFramework.Runtime.UI.Core;
using UnityEngine;

namespace FinkFramework.Runtime.UI.Modal
{
    /// <summary>暂时保存并关闭面板根节点 CanvasGroup 的交互状态，可被 Modal 和过渡同时叠加使用。</summary>
    internal sealed class UIInteractionController
    {
        private sealed class State
        {
            public CanvasGroup Group;
            public bool Interactable;
            public bool BlocksRaycasts;
            public int BlockCount;
        }

        private readonly Dictionary<UIPanelKey, State> states = new();

        public void Block(UIPanelRecord record)
        {
            if (record?.Panel == null)
                return;

            if (states.TryGetValue(record.Key, out State existing))
            {
                existing.BlockCount++;
                return;
            }

            CanvasGroup group = record.Panel.GetComponent<CanvasGroup>();
            if (!group)
                group = record.Panel.gameObject.AddComponent<CanvasGroup>();

            states.Add(record.Key, new State
            {
                Group = group,
                Interactable = group.interactable,
                BlocksRaycasts = group.blocksRaycasts,
                BlockCount = 1
            });

            group.interactable = false;
            group.blocksRaycasts = false;
        }

        public void Unblock(UIPanelRecord record)
        {
            if (record == null)
                return;

            Unblock(record.Key);
        }

        public void Unblock(UIPanelKey key)
        {
            if (!states.TryGetValue(key, out State state))
                return;

            state.BlockCount--;
            if (state.BlockCount > 0)
                return;

            if (state.Group)
            {
                state.Group.interactable = state.Interactable;
                state.Group.blocksRaycasts = state.BlocksRaycasts;
            }

            states.Remove(key);
        }

        public void Remove(UIPanelRecord record)
        {
            if (record == null || !states.TryGetValue(record.Key, out State state))
                return;

            if (state.Group)
            {
                state.Group.interactable = state.Interactable;
                state.Group.blocksRaycasts = state.BlocksRaycasts;
            }

            states.Remove(record.Key);
        }

        public void Clear()
        {
            foreach (State state in states.Values)
            {
                if (!state.Group)
                    continue;

                state.Group.interactable = state.Interactable;
                state.Group.blocksRaycasts = state.BlocksRaycasts;
            }

            states.Clear();
        }
    }
}
