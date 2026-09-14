using System;
using System.Threading;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace FinkFramework.Runtime.UI
{
    /// <summary>由 UIManager 创建并在面板初始化时注入的运行时上下文。</summary>
    public sealed class UIPanelContext
    {
        private CancellationTokenSource visibilityCancellation;
        private bool disposed;

        public UIPanelKey Key { get; }
        public UILayer Layer { get; internal set; }
        public string AssetPath { get; }

        /// <summary>
        /// 当前显示周期的取消令牌。
        /// 面板关闭或被销毁时会取消；页面被上层页面暂停时不会取消。
        /// 在 <c>OnEnter</c>/<c>OnResume</c> 中启动的异步流程应传入此令牌，避免关闭后继续刷新 UI。
        /// </summary>
        public CancellationToken VisibilityToken => visibilityCancellation?.Token
                                                    ?? (disposed
                                                        ? new CancellationToken(true)
                                                        : CancellationToken.None);

        internal UIPanelContext(UIPanelKey key, UILayer layer, string assetPath)
        {
            Key = key;
            Layer = layer;
            AssetPath = assetPath;
        }

        internal void BeginVisibilitySession()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(UIPanelContext));

            if (visibilityCancellation is { IsCancellationRequested: false })
                return;

            visibilityCancellation?.Dispose();
            visibilityCancellation = new CancellationTokenSource();
        }

        internal void EndVisibilitySession()
        {
            if (visibilityCancellation == null || visibilityCancellation.IsCancellationRequested)
                return;

            visibilityCancellation.Cancel();
        }

        internal void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            if (visibilityCancellation == null)
                return;

            EndVisibilitySession();
            visibilityCancellation.Dispose();
            visibilityCancellation = null;
        }
    }
}
