using Cysharp.Threading.Tasks;
using FinkFramework.Runtime.Utils;
using UnityEngine;

namespace FinkFramework.Runtime.ResLoad.Base
{
    /// <summary>
    /// 泛型资源信息对象 主要用于存储资源信息 异步加载委托信息 异步加载协程信息
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ResInfo<T> : BaseResInfo where T : Object
    {
        // 已加载的资源
        public T asset;
        
        // 当前资源是否正在异步加载 → 保存 UniTask 任务
        public UniTask<T>? task;
        
        // 获取当前资源实例（用于 ResManager 的统一资源管理）
        public override Object GetAsset() => asset;
        
        // 设置资源实例（用于同步/异步加载结束）
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
