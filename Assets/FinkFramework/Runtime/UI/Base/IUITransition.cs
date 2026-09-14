using System.Threading;
using Cysharp.Threading.Tasks;

namespace FinkFramework.Runtime.UI.Base
{
    /// <summary>
    /// 可选的面板过渡能力。同步 API 使用 Complete 方法直接落到最终状态，
    /// 异步 API 则等待 Play 方法完成。
    /// </summary>
    public interface IUITransition
    {
        void CompleteEnter();
        void CompleteExit();
        UniTask PlayEnterAsync(CancellationToken cancellationToken);
        UniTask PlayExitAsync(CancellationToken cancellationToken);
    }
}
