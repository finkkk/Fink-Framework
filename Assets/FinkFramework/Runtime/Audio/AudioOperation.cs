using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

namespace FinkFramework.Runtime.Audio
{
    /// <summary>
    /// 一次音频加载及开始播放的结果，必须在 Unity 主线程访问。
    /// 完成表示已开始播放或请求失败，不表示音频自然播放结束，也不额外持有资源引用。
    /// </summary>
    public class AudioOperation
    {
        /// <summary>加载和播放请求是否结束，成功与失败均为 true。</summary>
        public bool IsDone { get; internal set; }

        /// <summary>加载失败、播放器失效或播放请求被后续操作取消时为 true。</summary>
        public bool IsFailed { get; internal set; }

        /// <summary>0～1 的加载进度；请求结束后固定为 1，不作为成功判断。</summary>
        public float Progress { get; internal set; }

        /// <summary>
        /// 框架持有的播放器；加载期间可用于 StopSound，失败时为 null。
        /// 音效回收后可能被对象池复用，调用方不要长期保留此引用控制后续播放。
        /// </summary>
        public AudioSource Source { get; internal set; }

        /// <summary>成功加载的音频，失败时为 null；资源生命周期由 AudioManager 管理，调用方不应卸载。</summary>
        public AudioClip Clip { get; internal set; }

        private UnityAction<AudioOperation> completed;

        /// <summary>
        /// 请求结束事件，成功和失败均触发。结束后再订阅会立即收到当前结果；
        /// 各监听器的异常独立处理，不阻断其他监听器。
        /// </summary>
        public event UnityAction<AudioOperation> Completed
        {
            add
            {
                if (value == null) return;
                if (IsDone) InvokeCallbacks(value);
                else completed += value;
            }
            remove => completed -= value;
        }

        internal void SetProgress(float progress)
        {
            if (IsDone) return;
            Progress = float.IsNaN(progress) ? 0f : Mathf.Clamp01(progress);
        }

        internal void SetResult(AudioClip clip)
        {
            if (IsDone) return;
            if (!clip)
            {
                SetFailed();
                return;
            }
            Clip = clip;
            Complete(false);
        }

        internal void SetFailed()
        {
            if (IsDone) return;
            Source = null;
            Clip = null;
            Complete(true);
        }

        private void Complete(bool failed)
        {
            IsFailed = failed;
            IsDone = true;
            Progress = 1f;
            // 先脱离监听列表，回调中重新订阅会走已完成分支，不会重复完成本次请求。
            var callbacks = completed;
            completed = null;
            InvokeCallbacks(callbacks);
        }

        private void InvokeCallbacks(UnityAction<AudioOperation> callbacks)
        {
            if (callbacks == null) return;
            foreach (Delegate callback in callbacks.GetInvocationList())
            {
                try
                {
                    ((UnityAction<AudioOperation>)callback)(this);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        /// <summary>等待请求结束；调用方应继续检查 IsFailed，失败不会在这里转换为异常。</summary>
        /// <returns>请求完成时结束的等待任务。</returns>
        public async UniTask WaitUntilDone()
        {
            while (!IsDone)
                await UniTask.Yield();
        }
    }
}
