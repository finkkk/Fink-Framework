using Cysharp.Threading.Tasks;
using FinkFramework.Runtime.Utils;
using UnityEngine;

namespace FinkFramework.Runtime.ResLoad.Base
{
    /// <summary>
    /// 泛型资源缓存记录，保存资源对象和正在进行中的加载任务。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ResInfo<T> : BaseResInfo where T : Object
    {
        /// <summary>已加载的资源。</summary>
        public T asset;

        /// <summary>
        /// 当前异步加载任务。任务使用 Preserve 后保存，因此允许多个调用方等待同一任务。
        /// </summary>
        public UniTask<T>? task;

        public override bool IsLoading => task.HasValue;

        public override Object GetAsset() => asset;

        public override void SetAsset(Object obj) => asset = obj as T;

        public override async UniTask WaitForLoadAsync()
        {
            if (!task.HasValue)
                return;

            try
            {
                await task.Value;
            }
            catch
            {
                // 清理流程只需要等待任务结束，具体错误已由 ResManager 记录。
            }
        }

        /// <summary>增加一个资源使用方。</summary>
        public void AddRefCount() => ++refCount;

        /// <summary>
        /// 移除一个资源使用方。
        /// 重复卸载只记录错误，不让计数继续变成负数，避免错误调用破坏后续释放判断。
        /// </summary>
        public bool TrySubRefCount()
        {
            if (refCount <= 0)
            {
                LogUtil.Error("ResManager", "资源引用计数已经为 0，可能存在重复卸载：" + fullPath);
                return false;
            }

            --refCount;
            return true;
        }
    }
}
