using System;
using FinkFramework.Runtime.Singleton;
using UnityEngine;
using UnityEngine.Events;

namespace FinkFramework.Runtime.Mono
{
    /// <summary>
    /// 公共生命周期桥接器，为纯 C# 业务提供协程、逐帧更新和 Gizmos 回调。
    /// </summary>
    public class MonoManager : SingletonAutoMono<MonoManager>
    {
        private UnityAction updateEvent;
        private UnityAction fixedUpdateEvent;
        private UnityAction lateUpdateEvent;
        private UnityAction gizmosEvent;
        private UnityAction gizmosSelectedEvent;

        #region 帧更新注册
        /// <summary>
        /// 添加 Update 监听。传入 null 会被忽略。
        /// </summary>
        public void AddUpdateListener(UnityAction unityAction)
        {
            AddListener(ref updateEvent, unityAction);
        }
        /// <summary>
        /// 移除 Update 监听。
        /// </summary>
        public void RemoveUpdateListener(UnityAction unityAction)
        {
            RemoveListener(ref updateEvent, unityAction);
        }
        /// <summary>
        /// 添加 FixedUpdate 监听。
        /// </summary>
        public void AddFixedUpdateListener(UnityAction unityAction)
        {
            AddListener(ref fixedUpdateEvent, unityAction);
        }
        /// <summary>
        /// 移除 FixedUpdate 监听。
        /// </summary>
        public void RemoveFixedUpdateListener(UnityAction unityAction)
        {
            RemoveListener(ref fixedUpdateEvent, unityAction);
        }
        /// <summary>
        /// 添加 LateUpdate 监听。
        /// </summary>
        public void AddLateUpdateListener(UnityAction unityAction)
        {
            AddListener(ref lateUpdateEvent, unityAction);
        }
        /// <summary>
        /// 移除 LateUpdate 监听。
        /// </summary>
        public void RemoveLateUpdateListener(UnityAction unityAction)
        {
            RemoveListener(ref lateUpdateEvent, unityAction);
        }
        #endregion
        
        #region Gizmos 注册
        /// <summary>
        /// 添加 OnDrawGizmos 监听。
        /// </summary>
        /// <param name="unityAction"></param>
        public void AddGizmosListener(UnityAction unityAction) => AddListener(ref gizmosEvent, unityAction);
        /// <summary>
        /// 移除 OnDrawGizmos 监听。
        /// </summary>
        /// <param name="unityAction"></param>
        public void RemoveGizmosListener(UnityAction unityAction) => RemoveListener(ref gizmosEvent, unityAction);
        /// <summary>
        /// 添加 OnDrawGizmosSelected 监听。
        /// </summary>
        /// <param name="unityAction"></param>
        public void AddGizmosSelectedListener(UnityAction unityAction) =>
            AddListener(ref gizmosSelectedEvent, unityAction);
        /// <summary>
        /// 移除 OnDrawGizmosSelected 监听。
        /// </summary>
        /// <param name="unityAction"></param>
        public void RemoveGizmosSelectedListener(UnityAction unityAction) =>
            RemoveListener(ref gizmosSelectedEvent, unityAction);
        #endregion
        
        private void Update()
        {
            InvokeListeners(updateEvent);
        }
        private void FixedUpdate()
        {
            InvokeListeners(fixedUpdateEvent);
        }
        private void LateUpdate()
        {
            InvokeListeners(lateUpdateEvent);
        }
        private void OnDrawGizmos()
        {
            InvokeListeners(gizmosEvent);
        }
        private void OnDrawGizmosSelected()
        {
            InvokeListeners(gizmosSelectedEvent);
        }

        private static void AddListener(ref UnityAction listeners, UnityAction listener)
        {
            if (listener != null)
                listeners += listener;
        }

        private static void RemoveListener(ref UnityAction listeners, UnityAction listener)
        {
            if (listener != null)
                listeners -= listener;
        }

        /// <summary>
        /// 独立执行每个监听器，避免一个业务异常中断同一帧的其他系统更新。
        /// </summary>
        private static void InvokeListeners(UnityAction listeners)
        {
            if (listeners == null)
                return;

            foreach (Delegate callback in listeners.GetInvocationList())
            {
                try
                {
                    ((UnityAction)callback).Invoke();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }
    }
}
