using UnityEngine.Events;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace FinkFramework.Runtime.ResLoad.Base
{
    /// <summary>
    /// 异步资源加载操作句柄（类似 Unity 的 AsyncOperation）
    /// --------------------------------------------------------------------
    /// 主要用于：
    /// 1. 异步加载进度显示（Progress 0~1）
    /// 2. 检测是否加载完成（IsDone）
    /// 3. 获取最终加载的资源（Result）
    /// 4. 注册加载完成事件（Completed）
    ///
    /// 用于无 await 场景，比如：不想使用 async/await 的业务代码或者需要进度条的加载界面（Loading UI）
    /// </summary>
    /// <typeparam name="T">资源类型</typeparam>
    public class ResOperation<T>
    {
        /// <summary>
        /// 是否已完成资源加载。true 表示加载过程全部结束（成功或失败）。
        /// </summary>
        public bool IsDone { get; internal set; }
        
        /// <summary>
        /// 异步加载进度（0~1）。若 Provider 不支持真实进度，则 ResManager 会进行模拟增长。
        /// </summary>
        public float Progress { get; internal set; }
        
        /// <summary>
        /// 加载完成后的资源对象。仅当 IsDone == true 时有效。若加载失败 Result 可能为 null。
        /// </summary>
        public T Result { get; internal set; }
        
        /// <summary>
        /// 加载完成事件（包括成功或失败）。无须轮询 IsDone，可通过注册该事件来监听加载完成。
        /// </summary>
        public event UnityAction<ResOperation<T>> Completed;
        
        /// <summary>
        /// 设置进度（供 ResManager 内部调用）。
        /// </summary>
        internal void SetProgress(float p)
        {
            Progress = p;
        }

        /// <summary>
        /// 设置最终结果并触发完成回调（供 ResManager 内部调用）。
        /// 调用后：
        /// - Result 将保存加载到的资源
        /// - Progress 会被设为 1
        /// - IsDone = true
        /// - 自动触发 Completed 事件
        /// </summary>
        /// <param name="r">加载完成的资源</param>
        internal void SetResult(T r)
        {
            Result = r;
            Progress = 1f;
            IsDone = true;
            Completed?.Invoke(this);
        }
    }
}