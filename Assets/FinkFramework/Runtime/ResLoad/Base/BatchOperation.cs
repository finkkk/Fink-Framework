using System.Collections.Generic;
using UnityEngine.Events;

// ReSharper disable CollectionNeverQueried.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
namespace FinkFramework.Runtime.ResLoad.Base
{
    /// <summary>
    /// 批量异步资源加载操作句柄（同时加载多个资源）
    /// --------------------------------------------------------------------
    /// 主要用于场景切换界面的整体进度条
    /// </summary>
    public class BatchOperation
    {
        /// <summary>
        /// 是否已完成批量资源加载。true 表示加载过程全部结束（成功或失败）。
        /// </summary>
        public bool IsDone { get; internal set; }
        
        /// <summary>
        /// 异步加载进度（0~1）。若 Provider 不支持真实进度，则 ResManager 会进行模拟增长。
        /// </summary>
        public float Progress { get; internal set; }
        
        /// <summary>
        /// 批量加载得到的全部资源对象列表（允许混合资源类型）。加载失败的资源会得到 null, 调用者需自行判断是否成功加载
        /// </summary>
        public List<object> Results { get; } = new();

        /// <summary>
        /// 批量加载完成事件。加载完成时自动触发（包括成功或失败的资源）。可用于关闭 Loading UI、进入下一个流程等。
        /// </summary>
        public event UnityAction<BatchOperation> Completed;

        /// <summary>
        /// 设置整体进度（供 ResManager 内部调用）。
        /// </summary>
        internal void SetProgress(float p)
        {
            Progress = p;
        }

        /// <summary>
        /// 完成批量加载（内部调用）。标记完成并将进度强制设为 1.0，然后触发 Completed 回调。
        /// </summary>
        internal void Finish()
        {
            IsDone = true;
            Progress = 1f;
            Completed?.Invoke(this);
        }

        internal void AddResult(object obj)
        {
            Results.Add(obj);
        }
    }
}