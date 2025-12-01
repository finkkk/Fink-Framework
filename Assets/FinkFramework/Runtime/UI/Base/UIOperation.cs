using Cysharp.Threading.Tasks;
using UnityEngine.Events;

namespace FinkFramework.Runtime.UI.Base
{
    /// <summary>
    /// UI 面板异步加载操作句柄（与 ResOperation / SceneOperation / AudioOperation 风格一致）
    /// --------------------------------------------------------------------
    /// 用于非 await 场景，如需要：
    /// 1) 轮询加载进度 (Progress)
    /// 2) 检测是否加载完成 (IsDone)
    /// 3) 提前加载 UI Panel 后延迟显示 (预加载场景)
    /// 4) 用于 Loading UI 显示加载进度
    /// 5) 或者需要可取消的面板加载
    /// </summary>
    public class UIOperation<T> where T : BasePanel
    {
        /// <summary>
        /// 是否已经完成加载（成功 or 失败）
        /// </summary>
        public bool IsDone { get; internal set; }

        /// <summary>
        /// 是否加载失败
        /// </summary>
        public bool IsFailed { get; internal set; }

        /// <summary>
        /// 模拟加载进度（UI 面板的 prefab 加载通常瞬间完成，但保留接口以统一框架）
        /// </summary>
        public float Progress { get; internal set; }

        /// <summary>
        /// 实际得到的 Panel 实例
        /// </summary>
        public T Panel { get; internal set; }

        /// <summary>
        /// 加载完成事件（成功 or 失败都会触发）
        /// </summary>
        public event UnityAction<UIOperation<T>> Completed;

        /// <summary>
        /// 设置进度（内部调用）
        /// </summary>
        internal void SetProgress(float p)
        {
            Progress = p;
        }

        /// <summary>
        /// 设置加载结果
        /// </summary>
        internal void SetResult(T panel)
        {
            Panel = panel;
            Progress = 1f;
            IsDone = true;
            Completed?.Invoke(this);
        }

        /// <summary>
        /// 标记加载失败
        /// </summary>
        internal void SetFailed()
        {
            IsFailed = true;
            Progress = 1f;
            IsDone = true;
            Completed?.Invoke(this);
        }

        /// <summary>
        /// 等待加载完成（用于 async/await）
        /// </summary>
        public async UniTask WaitUntilDone()
        {
            while (!IsDone)
                await UniTask.Yield();
        }
    }
}