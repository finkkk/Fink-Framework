using FinkFramework.Utils;
using UnityEngine;
using UnityEngine.Events;

namespace FinkFramework.ResLoad.Base
{
    /// <summary>
    /// 泛型资源信息对象 主要用于存储资源信息 异步加载委托信息 异步加载协程信息
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ResInfo<T> : BaseResInfo where T : Object
    {
        // 资源信息
        public T asset;
        // 主要用于异步加载结束后 传递资源到外部的委托
        public UnityAction<T> callback;
        // 用于存储异步加载时开启的协同程序
        public Coroutine coroutine;
        // 获取当前资源实例（用于 ResManager 的统一资源管理）
        public override Object GetAsset() => asset;
        // 设置资源实例（在异步/同步加载完成时由 ResManager 统一调用）
        public override void SetAsset(Object obj) => asset = obj as T;
        // 引用计数 +1（表示有一个新地使用方需要该资源）
        public void AddRefCount() => ++refCount;
        /// <summary>
        /// 引用计数 -1（不再使用该资源）
        /// 若引用计数降为负数，说明使用方的加载/卸载不配对，是严重的逻辑错误。
        /// </summary>
        public void SubRefCount()
        {
            --refCount;
            if (refCount < 0)
            {
                LogUtil.Error("引用计数变为负数，请检查使用与卸载是否配对执行");
            }
        }
    }
}
