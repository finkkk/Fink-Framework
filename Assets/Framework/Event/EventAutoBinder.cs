using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Framework.Event
{
    /// <summary>
    /// 事件自动绑定工具类
    /// 一行代码实现：
    /// 1. Bind() —— 立即注册，OnDestroy 自动解绑（默认模式）
    /// 2. BindAuto() —— OnEnable 自动注册，OnDisable 自动解绑（UI/临时对象模式）
    /// </summary>
    public static class EventAutoBinder 
    {
        // 每个使用者脚本都挂一个隐藏的生命周期代理组件，用来监听生命周期
        private class BinderProxy : MonoBehaviour
        {
            public readonly List<Action> bindOnEnable = new();
            public readonly List<Action> unbindOnDisable = new();
            public readonly List<Action> unbindOnDestroy = new();
            // 记录是否已经注册监听 防止重复注册
            public bool hasImmediateBound = false;

            private void OnEnable()
            {
                foreach (var b in bindOnEnable)
                    b.Invoke();
            }

            private void OnDisable()
            {
                foreach (var u in unbindOnDisable)
                    u.Invoke();
                hasImmediateBound = false; 
            }

            private void OnDestroy()
            {
                foreach (var u in unbindOnDestroy)
                    u.Invoke();

                bindOnEnable.Clear();
                unbindOnDisable.Clear();
                unbindOnDestroy.Clear();
                hasImmediateBound = false;
            }
        }

        #region 默认模式(OnDestroy 自动解绑)
        /// <summary>
        /// 默认绑定模式: 无参委托事件 (立即绑定事件 + OnDestroy 自动解绑)
        /// </summary>
        public static void Bind(MonoBehaviour owner, E_EventType type, UnityAction callback, bool sticky = false)
        {
            var proxy = GetProxy(owner);
            // 立即绑定
            EventManager.Instance.AddEventListener(type, callback, sticky);
            // OnDestroy 自动解绑
            proxy.unbindOnDestroy.Add(() =>
            {
                EventManager.Instance.RemoveEventListener(type, callback);
            });
        }
        
        /// <summary>
        /// 默认绑定模式: 1参委托事件 (立即绑定事件 + OnDestroy 自动解绑)
        /// </summary>
        public static void Bind<T>(MonoBehaviour owner, E_EventType type, UnityAction<T> callback, bool sticky = false)
        {
            var proxy = GetProxy(owner);
            // 立即绑定             
            EventManager.Instance.AddEventListener(type, callback, sticky);
            // OnDestroy 自动解绑
            proxy.unbindOnDestroy.Add(() =>
            {
                EventManager.Instance.RemoveEventListener(type, callback);
            });
        }
        
        /// <summary>
        /// 默认绑定模式: 2参委托事件 (立即绑定事件 + OnDestroy 自动解绑)
        /// </summary>
        public static void Bind<T1, T2>(MonoBehaviour owner, E_EventType type, UnityAction<T1, T2> callback, bool sticky = false)
        {
            var proxy = GetProxy(owner);

            // 立即绑定
            EventManager.Instance.AddEventListener(type, callback, sticky);

            // OnDestroy 自动解绑
            proxy.unbindOnDestroy.Add(() =>
            {
                EventManager.Instance.RemoveEventListener(type, callback);
            });
        }
        #endregion

        #region 自动模式(OnEnable 自动注册，OnDisable 自动解绑)
        /// <summary>
        /// 自动模式： 无参委托事件 (OnEnable 注册、OnDisable 解绑)
        /// </summary>
        public static void BindAuto(MonoBehaviour owner, E_EventType type, UnityAction callback, bool sticky = false)
        {
            var proxy = GetProxy(owner);

            // 在 Enable 时注册
            proxy.bindOnEnable.Add(() =>
            {
                EventManager.Instance.AddEventListener(type, callback, sticky);
            });

            // 在 Disable / Destroy 时解绑
            proxy.unbindOnDisable.Add(() =>
            {
                EventManager.Instance.RemoveEventListener(type, callback);
            });

            // 当前已经启用 → 立即绑定一次
            if (owner.enabled && owner.gameObject.activeInHierarchy)
            {
                if (!proxy.hasImmediateBound)
                {
                    EventManager.Instance.AddEventListener(type, callback, sticky);
                    proxy.hasImmediateBound = true; // 保证只绑定一次
                }
            }
        }

        /// <summary>
        /// 自动模式： 1参委托事件 (OnEnable 注册、OnDisable 解绑)
        /// </summary>
        public static void BindAuto<T>(MonoBehaviour owner, E_EventType type, UnityAction<T> callback, bool sticky = false)
        {
            var proxy = GetProxy(owner);

            proxy.bindOnEnable.Add(() =>
            {
                EventManager.Instance.AddEventListener(type, callback, sticky);
            });

            proxy.unbindOnDisable.Add(() =>
            {
                EventManager.Instance.RemoveEventListener(type, callback);
            });

            // 当前已经启用 → 立即绑定一次
            if (owner.enabled && owner.gameObject.activeInHierarchy)
            {
                if (!proxy.hasImmediateBound)
                {
                    EventManager.Instance.AddEventListener(type, callback, sticky);
                    proxy.hasImmediateBound = true; // 保证只绑定一次
                }
            }
        }
        
        /// <summary>
        /// 自动模式： 2参委托事件 (OnEnable 注册、OnDisable 解绑)
        /// </summary>
        public static void BindAuto<T1, T2>(MonoBehaviour owner, E_EventType type, UnityAction<T1, T2> callback, bool sticky = false)
        {
            var proxy = GetProxy(owner);

            // Enable 注册
            proxy.bindOnEnable.Add(() =>
            {
                EventManager.Instance.AddEventListener(type, callback, sticky);
            });

            // Disable 解绑
            proxy.unbindOnDisable.Add(() =>
            {
                EventManager.Instance.RemoveEventListener(type, callback);
            });

            // 立即注册一次（仅第一次）
            if (owner.enabled && owner.gameObject.activeInHierarchy)
            {
                if (!proxy.hasImmediateBound)
                {
                    EventManager.Instance.AddEventListener(type, callback, sticky);
                    proxy.hasImmediateBound = true;
                }
            }
        }
        #endregion
 
        /// <summary>
        /// 工具方法：获取或挂载 Proxy 组件
        /// </summary>
        private static BinderProxy GetProxy(MonoBehaviour owner)
        {
            var p = owner.GetComponent<BinderProxy>();
            if (!p)
                p = owner.gameObject.AddComponent<BinderProxy>();
            return p;
        }
    }
}