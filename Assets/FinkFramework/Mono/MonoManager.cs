using FinkFramework.Singleton;
using UnityEngine.Events;

namespace FinkFramework.Mono
{
    /// <summary>
    /// 公共MONO模块 给未继承Mono的脚本提供生命周期函数调用和协程的调用  也可以统一管理所有帧更新逻辑（无论是否继承Mono）
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
        /// 添加帧更新监听函数
        /// </summary>
        /// <param name="unityAction"></param>
        public void AddUpdateListener(UnityAction unityAction)
        {
            updateEvent += unityAction;
        }
        /// <summary>
        /// 移除帧更新监听函数
        /// </summary>
        /// <param name="unityAction"></param>
        public void RemoveUpdateListener(UnityAction unityAction)
        {
            updateEvent -= unityAction;
        }
        /// <summary>
        /// 添加fixed帧更新监听函数
        /// </summary>
        /// <param name="unityAction"></param>
        public void AddFixedUpdateListener(UnityAction unityAction)
        {
            fixedUpdateEvent += unityAction;
        }
        /// <summary>
        /// 移除fixed帧更新监听函数
        /// </summary>
        /// <param name="unityAction"></param>
        public void RemoveFixedUpdateListener(UnityAction unityAction)
        {
            fixedUpdateEvent -= unityAction;
        } 
        /// <summary>
        /// 添加Late帧更新监听函数
        /// </summary>
        /// <param name="unityAction"></param>
        public void AddLateUpdateListener(UnityAction unityAction)
        {
            lateUpdateEvent += unityAction;
        }
        /// <summary>
        /// 移除Late帧更新监听函数
        /// </summary>
        /// <param name="unityAction"></param>
        public void RemoveLateUpdateListener(UnityAction unityAction)
        {
            lateUpdateEvent -= unityAction;
        }
        #endregion
        
        #region Gizmos 注册
        /// <summary>
        /// 添加Gizmos监听函数
        /// </summary>
        /// <param name="unityAction"></param>
        public void AddGizmosListener(UnityAction unityAction) => gizmosEvent += unityAction;
        /// <summary>
        /// 移除Gizmos监听函数
        /// </summary>
        /// <param name="unityAction"></param>
        public void RemoveGizmosListener(UnityAction unityAction) => gizmosEvent -= unityAction;
        /// <summary>
        /// 添加GizmosSelected监听函数
        /// </summary>
        /// <param name="unityAction"></param>
        public void AddGizmosSelectedListener(UnityAction unityAction) => gizmosSelectedEvent += unityAction;
        /// <summary>
        /// 移除GizmosSelected监听函数
        /// </summary>
        /// <param name="unityAction"></param>
        public void RemoveGizmosSelectedListener(UnityAction unityAction) => gizmosSelectedEvent -= unityAction;
        #endregion
        
        private void Update()
        {
            updateEvent?.Invoke();
        }
        private void FixedUpdate()
        {
            fixedUpdateEvent?.Invoke();
        }
        private void LateUpdate()
        {
            lateUpdateEvent?.Invoke();
        }
        private void OnDrawGizmos()
        {
            gizmosEvent?.Invoke();
        }
        private void OnDrawGizmosSelected()
        {
            gizmosSelectedEvent?.Invoke();
        }
    }
}
