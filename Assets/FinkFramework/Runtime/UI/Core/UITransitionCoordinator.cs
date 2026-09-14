using System;
using Cysharp.Threading.Tasks;
using FinkFramework.Runtime.UI.Base;
using FinkFramework.Runtime.Utils;

namespace FinkFramework.Runtime.UI.Core
{
    /// <summary>
    /// 统一管理面板过渡任务的共享、系统取消和最终状态提交。
    /// UIManager 只决定“要进入还是退出”，不持有具体动画执行细节。
    /// </summary>
    internal sealed class UITransitionCoordinator
    {
        private readonly UITransitionRunner runner = new();

        public UIPanelTransitionOperation StartEnter(
            UIPanelRecord record,
            BasePanel panel,
            int operationVersion,
            Action<UIPanelRecord, BasePanel> onCompleted)
        {
            var operation = new UIPanelTransitionOperation();
            record.TransitionOperation = operation;
            operation.Task = RunEnterAsync(
                record,
                panel,
                operationVersion,
                operation,
                onCompleted).Preserve();
            return operation;
        }

        public UIPanelTransitionOperation StartExit(
            UIPanelRecord record,
            BasePanel panel,
            int operationVersion,
            bool destroyOnComplete,
            Action<UIPanelRecord, BasePanel, bool> onCompleted)
        {
            var operation = new UIPanelTransitionOperation
            {
                DestroyOnComplete = destroyOnComplete
            };
            record.TransitionOperation = operation;
            operation.Task = RunExitAsync(
                record,
                panel,
                operationVersion,
                operation,
                onCompleted).Preserve();
            return operation;
        }

        public void Cancel(UIPanelRecord record)
        {
            UIPanelTransitionOperation operation = record?.TransitionOperation;
            if (operation == null)
                return;

            record.TransitionOperation = null;
            record.OperationVersion++;
            try
            {
                operation.Cancel();
            }
            catch (Exception exception)
            {
                LogUtil.Error("UI", $"取消面板 {record.Key} 的过渡时发生异常：{exception}");
            }
        }

        public void CompleteEnter(UIPanelRecord record, BasePanel panel)
        {
            try
            {
                runner.CompleteEnter(panel);
            }
            catch (Exception exception)
            {
                LogUtil.Error("UI", $"面板 {record.Key} 的进入过渡收尾发生异常：{exception}");
            }
        }

        public void CompleteExit(UIPanelRecord record, BasePanel panel)
        {
            try
            {
                runner.CompleteExit(panel);
            }
            catch (Exception exception)
            {
                LogUtil.Error("UI", $"面板 {record.Key} 的退出过渡收尾发生异常：{exception}");
            }
        }

        private async UniTask RunEnterAsync(
            UIPanelRecord record,
            BasePanel panel,
            int operationVersion,
            UIPanelTransitionOperation operation,
            Action<UIPanelRecord, BasePanel> onCompleted)
        {
            try
            {
                await runner.PlayEnterAsync(panel, operation.Token);
            }
            catch (OperationCanceledException) when (operation.IsCancellationRequested)
            {
                // 被更新的打开或关闭操作取代，最终状态由新操作负责。
            }
            catch (Exception exception)
            {
                LogUtil.Error("UI", $"面板 {record.Key} 的进入过渡发生异常：{exception}");
            }
            finally
            {
                try
                {
                    if (ReferenceEquals(record.TransitionOperation, operation))
                    {
                        record.TransitionOperation = null;
                        if (!record.Removed
                            && record.OperationVersion == operationVersion
                            && record.State == UIPanelState.Opening)
                        {
                            CompleteEnter(record, panel);
                            onCompleted?.Invoke(record, panel);
                        }
                    }
                }
                catch (Exception exception)
                {
                    LogUtil.Error("UI", $"提交面板 {record.Key} 的进入状态时发生异常：{exception}");
                }
                finally
                {
                    operation.Dispose();
                }
            }
        }

        private async UniTask RunExitAsync(
            UIPanelRecord record,
            BasePanel panel,
            int operationVersion,
            UIPanelTransitionOperation operation,
            Action<UIPanelRecord, BasePanel, bool> onCompleted)
        {
            try
            {
                await runner.PlayExitAsync(panel, operation.Token);
            }
            catch (OperationCanceledException) when (operation.IsCancellationRequested)
            {
                // 被重新打开或同步关闭取代，最终状态由新操作负责。
            }
            catch (Exception exception)
            {
                LogUtil.Error("UI", $"面板 {record.Key} 的退出过渡发生异常：{exception}");
            }
            finally
            {
                try
                {
                    if (ReferenceEquals(record.TransitionOperation, operation))
                    {
                        record.TransitionOperation = null;
                        if (!record.Removed
                            && record.OperationVersion == operationVersion
                            && record.State == UIPanelState.Closing)
                        {
                            CompleteExit(record, panel);
                            onCompleted?.Invoke(record, panel, operation.DestroyOnComplete);
                        }
                    }
                }
                catch (Exception exception)
                {
                    LogUtil.Error("UI", $"提交面板 {record.Key} 的退出状态时发生异常：{exception}");
                }
                finally
                {
                    operation.Dispose();
                }
            }
        }
    }
}
