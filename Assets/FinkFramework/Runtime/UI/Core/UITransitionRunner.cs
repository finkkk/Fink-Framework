using System.Threading;
using Cysharp.Threading.Tasks;
using FinkFramework.Runtime.UI.Base;

namespace FinkFramework.Runtime.UI.Core
{
    /// <summary>隔离可选过渡组件，UIManager 不依赖具体动画库。</summary>
    internal sealed class UITransitionRunner
    {
        public void CompleteEnter(BasePanel panel) => GetTransition(panel)?.CompleteEnter();
        public void CompleteExit(BasePanel panel) => GetTransition(panel)?.CompleteExit();

        public UniTask PlayEnterAsync(BasePanel panel, CancellationToken cancellationToken)
        {
            IUITransition transition = GetTransition(panel);
            return transition?.PlayEnterAsync(cancellationToken) ?? UniTask.CompletedTask;
        }

        public UniTask PlayExitAsync(BasePanel panel, CancellationToken cancellationToken)
        {
            IUITransition transition = GetTransition(panel);
            return transition?.PlayExitAsync(cancellationToken) ?? UniTask.CompletedTask;
        }

        private static IUITransition GetTransition(BasePanel panel)
        {
            if (!panel)
                return null;

            return panel.GetComponent(typeof(IUITransition)) as IUITransition;
        }
    }
}
