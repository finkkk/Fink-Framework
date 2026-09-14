using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using FinkFramework.Runtime.UI.Base;
using UnityEngine;

namespace FinkFramework.Runtime.UI.Core
{
    /// <summary>UIManager 内部持有的面板实例记录。</summary>
    internal sealed class UIPanelRecord
    {
        public UIPanelKey Key { get; }
        public string AssetPath { get; }
        public UIOpenOptions Options { get; set; }
        public BasePanel Panel { get; set; }
        public UIPanelState State { get; set; }
        public UniTask<BasePanel> LoadTask { get; set; }
        public bool Removed { get; set; }
        public int OperationVersion { get; set; }
        public GameObject ModalBackdrop { get; set; }
        public UIPanelTransitionOperation TransitionOperation { get; set; }
        public int OwnerSceneHandle { get; set; }

        public UIPanelRecord(
            UIPanelKey key,
            string assetPath,
            UIOpenOptions options,
            int ownerSceneHandle)
        {
            Key = key;
            AssetPath = assetPath;
            Options = options;
            OwnerSceneHandle = ownerSceneHandle;
            State = UIPanelState.Loading;
        }
    }

    /// <summary>
    /// 一个面板当前正在执行的进入或退出过渡。
    /// 系统取消令牌与调用方取消令牌分离，避免某个调用方停止等待时破坏全局 UI 状态。
    /// </summary>
    internal sealed class UIPanelTransitionOperation : IDisposable
    {
        private readonly CancellationTokenSource cancellation = new();
        private bool disposed;

        public CancellationToken Token => cancellation.Token;
        public UniTask Task { get; set; } = UniTask.CompletedTask;
        public bool DestroyOnComplete { get; set; }
        public bool IsCancellationRequested { get; private set; }

        public void Cancel()
        {
            if (disposed || IsCancellationRequested)
                return;

            IsCancellationRequested = true;
            cancellation.Cancel();
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            cancellation.Dispose();
        }
    }
}
