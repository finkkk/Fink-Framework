using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using FinkFramework.Runtime.Input;
using FinkFramework.Runtime.Singleton;
using FinkFramework.Runtime.UI.Base;
using FinkFramework.Runtime.UI.Core;
using FinkFramework.Runtime.UI.Input;
using FinkFramework.Runtime.UI.Modal;
using FinkFramework.Runtime.UI.Navigation;
using FinkFramework.Runtime.UI.Surface;
using FinkFramework.Runtime.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace FinkFramework.Runtime.UI
{
    /// <summary>
    /// UI 系统的唯一公共入口。
    /// 对外提供简洁的同步/异步 API；加载、实例仓库和 Surface 注册由内部服务分别负责。
    /// </summary>
    public sealed partial class UIManager : Singleton<UIManager>
    {
        private readonly UIPanelRepository repository;
        private readonly UIPanelLoader loader;
        private readonly UISurfaceRegistry surfaces;
        private readonly UINavigationController navigation;
        private readonly UITransitionCoordinator transitions;
        private readonly UIInputRouter inputRouter;
        private readonly DeviceDetectionManager inputDeviceDetectionManager;
        private readonly UIModalController modals;
        private readonly UIInteractionController interactions;
        private readonly UIModalBackdropController modalBackdrops;
        private readonly UIRuntimeRoot runtimeRoot;

        private UIManager()
        {
            repository = new UIPanelRepository();
            loader = new UIPanelLoader();
            surfaces = new UISurfaceRegistry();
            navigation = new UINavigationController();
            transitions = new UITransitionCoordinator();
            modals = new UIModalController();
            interactions = new UIInteractionController();
            modalBackdrops = new UIModalBackdropController();
            runtimeRoot = new UIRuntimeRoot();
            surfaces.Register(runtimeRoot.MainSurface, true);
            inputRouter = new UIInputRouter(HandleInputModeChanged);
            inputDeviceDetectionManager = DeviceDetectionManager.Instance;
            UIInputDriver.Create(inputRouter, inputDeviceDetectionManager);
            SceneManager.sceneUnloaded += HandleSceneUnloaded;

            LogUtil.Success("UI", "UI 系统初始化完成。");
        }

        public Camera UICamera => runtimeRoot.Camera;
        public Camera MainCamera => runtimeRoot.MainCamera;
        public Canvas MainCanvas => runtimeRoot.MainCanvas;
        public UIInputMode InputMode => inputRouter.Mode;

        /// <summary>
        /// 设置与框架 UI Camera 组合的游戏主相机。
        /// 玩家相机创建或替换后传入新实例；销毁前传入 null 以解除旧 URP 相机栈关系。
        /// </summary>
        public void SetMainCamera(Camera mainCamera) => runtimeRoot.SetMainCamera(mainCamera);

        /// <summary>面板状态发生变化时触发，适合调试工具和自动化测试订阅。</summary>
        public event Action<UIPanelKey, UIPanelState> PanelStateChanged;

        /// <summary>鼠标与键盘/手柄导航模式切换时触发。</summary>
        public event Action<UIInputMode> InputModeChanged;

        #region Preload

        /// <summary>同步预加载默认 Surface 上的面板，不触发打开生命周期。</summary>
        public T Preload<T>() where T : BasePanel => Preload<T>(UIOpenOptions.Default);

        /// <summary>
        /// 同步预加载面板。资源必须支持同步加载；面板会保持隐藏，后续 Open 可直接使用。
        /// </summary>
        public T Preload<T>(UIOpenOptions options) where T : BasePanel
        {
            options = options.Normalize();
            if (!TryResolveSurface(options.SurfaceId, out UISurface surface))
                return null;
            if (!CanUseOptions(surface, options))
                return null;

            UIPanelKey key = BuildKey<T>(options);
            if (repository.TryGet(key, out UIPanelRecord existing))
            {
                if (existing.State == UIPanelState.Loading)
                {
                    LogUtil.Error(
                        "UI",
                        $"{key} 正在异步加载，不能用同步 Preload 等待它。请继续使用 PreloadAsync。");
                    return null;
                }

                if (existing.Panel)
                    return PreparePreloadedPanel<T>(existing, surface, options);

                RemoveBrokenRecord(existing);
            }

            string assetPath = loader.GetAssetPath<T>();
            var record = new UIPanelRecord(
                key,
                assetPath,
                options,
                ResolveOwnerSceneHandle(surface, options.Lifetime));
            if (!repository.Add(record))
                return null;

            try
            {
                T panel = loader.Load<T>(assetPath, surface.GetRoot(options.Layer));
                AttachLoadedPanel(record, panel);
                return PreparePreloadedPanel<T>(record, surface, options);
            }
            catch (Exception exception)
            {
                FailRecord(record, exception);
                return null;
            }
        }

        /// <summary>异步预加载默认 Surface 上的面板，不触发打开生命周期。</summary>
        public UniTask<T> PreloadAsync<T>(CancellationToken cancellationToken = default)
            where T : BasePanel =>
            PreloadAsync<T>(UIOpenOptions.Default, cancellationToken);

        /// <summary>
        /// 异步预加载面板。调用方取消等待不会取消其他调用方共享的底层加载。
        /// </summary>
        public async UniTask<T> PreloadAsync<T>(
            UIOpenOptions options,
            CancellationToken cancellationToken = default)
            where T : BasePanel
        {
            cancellationToken.ThrowIfCancellationRequested();
            options = options.Normalize();
            if (!TryResolveSurface(options.SurfaceId, out UISurface surface))
                return null;
            if (!CanUseOptions(surface, options))
                return null;

            UIPanelKey key = BuildKey<T>(options);
            if (!repository.TryGet(key, out var record))
            {
                string assetPath = loader.GetAssetPath<T>();
                record = new UIPanelRecord(
                    key,
                    assetPath,
                    options,
                    ResolveOwnerSceneHandle(surface, options.Lifetime));
                if (!repository.Add(record))
                    return null;

                record.LoadTask = LoadRecordAsync<T>(
                    record,
                    surface.GetRoot(options.Layer)).Preserve();
            }
            else if (record.State != UIPanelState.Loading && !record.Panel)
            {
                RemoveBrokenRecord(record);
                return await PreloadAsync<T>(options, cancellationToken);
            }

            if (record.State == UIPanelState.Loading
                && !await record.LoadTask.AttachExternalCancellation(cancellationToken))
                return null;

            cancellationToken.ThrowIfCancellationRequested();
            if (record.Removed || !record.Panel)
                return null;

            return PreparePreloadedPanel<T>(record, surface, options);
        }

        private T PreparePreloadedPanel<T>(
            UIPanelRecord record,
            UISurface surface,
            UIOpenOptions options)
            where T : BasePanel
        {
            if (!(record.Panel is T panel) || !panel)
            {
                LogUtil.Error("UI", $"面板记录 {record.Key} 的实例类型与 {typeof(T).Name} 不一致。");
                return null;
            }

            // 已显示的实例只保证“已经加载”，不能让预加载调用偷偷改变它的显示配置。
            if (record.State != UIPanelState.Hidden)
                return panel;

            Transform targetRoot = surface.GetRoot(options.Layer);
            if (targetRoot && panel.transform.parent != targetRoot)
                panel.transform.SetParent(targetRoot, false);

            record.Options = options;
            record.OwnerSceneHandle = ResolveOwnerSceneHandle(surface, options.Lifetime);
            panel.Context.Layer = options.Layer;
            panel.gameObject.SetActive(false);
            return panel;
        }

        #endregion

        #region Open

        /// <summary>同步打开默认 Surface 上的面板。</summary>
        public T Open<T>() where T : BasePanel => Open<T>(UIOpenOptions.Default);

        /// <summary>同步打开面板。资源必须能被底层 Provider 同步加载。</summary>
        public T Open<T>(UIOpenOptions options) where T : BasePanel =>
            OpenCore<T>(options.Normalize(), null);

        /// <summary>同步打开并传入强类型参数。</summary>
        public T Open<T, TArgs>(TArgs args) where T : BasePanel, IUIArgsReceiver<TArgs> =>
            Open<T, TArgs>(args, UIOpenOptions.Default);

        /// <summary>同步打开并传入强类型参数。</summary>
        public T Open<T, TArgs>(TArgs args, UIOpenOptions options)
            where T : BasePanel, IUIArgsReceiver<TArgs> =>
            OpenCore<T>(options.Normalize(), panel => panel.ApplyArgs(args));

        /// <summary>异步打开默认 Surface 上的面板。</summary>
        public UniTask<T> OpenAsync<T>(CancellationToken cancellationToken = default)
            where T : BasePanel =>
            OpenAsync<T>(UIOpenOptions.Default, cancellationToken);

        /// <summary>
        /// 异步打开面板。首次打开会真正等待异步资源加载；已缓存的面板会立即完成。
        /// </summary>
        public UniTask<T> OpenAsync<T>(
            UIOpenOptions options,
            CancellationToken cancellationToken = default)
            where T : BasePanel =>
            OpenAsyncCore<T>(options.Normalize(), null, cancellationToken);

        /// <summary>异步打开并传入强类型参数。</summary>
        public UniTask<T> OpenAsync<T, TArgs>(
            TArgs args,
            CancellationToken cancellationToken = default)
            where T : BasePanel, IUIArgsReceiver<TArgs> =>
            OpenAsync<T, TArgs>(args, UIOpenOptions.Default, cancellationToken);

        /// <summary>异步打开并传入强类型参数。</summary>
        public UniTask<T> OpenAsync<T, TArgs>(
            TArgs args,
            UIOpenOptions options,
            CancellationToken cancellationToken = default)
            where T : BasePanel, IUIArgsReceiver<TArgs> =>
            OpenAsyncCore<T>(
                options.Normalize(),
                panel => panel.ApplyArgs(args),
                cancellationToken);

        private T OpenCore<T>(UIOpenOptions options, Action<T> applyArgs) where T : BasePanel
        {
            if (!TryResolveSurface(options.SurfaceId, out UISurface surface))
                return null;
            if (!CanUseOptions(surface, options))
                return null;
            if (!CanOpen(options))
                return null;

            UIPanelKey key = BuildKey<T>(options);
            if (repository.TryGet(key, out UIPanelRecord existing))
            {
                if (existing.State == UIPanelState.Loading)
                {
                    LogUtil.Error(
                        "UI",
                        $"{key} 正在异步加载，不能用同步 Open 等待它。请继续使用 OpenAsync。");
                    return null;
                }

                if (existing.Panel)
                    return Present(existing, surface, options, applyArgs);

                RemoveBrokenRecord(existing);
            }

            string assetPath = loader.GetAssetPath<T>();
            var record = new UIPanelRecord(
                key,
                assetPath,
                options,
                ResolveOwnerSceneHandle(surface, options.Lifetime));
            if (!repository.Add(record))
                return null;

            try
            {
                T panel = loader.Load<T>(assetPath, surface.GetRoot(options.Layer));
                AttachLoadedPanel(record, panel);
                return Present(record, surface, options, applyArgs);
            }
            catch (Exception exception)
            {
                FailRecord(record, exception);
                return null;
            }
        }

        private async UniTask<T> OpenAsyncCore<T>(
            UIOpenOptions options,
            Action<T> applyArgs,
            CancellationToken cancellationToken)
            where T : BasePanel
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryResolveSurface(options.SurfaceId, out UISurface surface))
                return null;
            if (!CanUseOptions(surface, options))
                return null;
            if (!CanOpen(options))
                return null;

            UIPanelKey key = BuildKey<T>(options);

            if (!repository.TryGet(key, out var record))
            {
                string assetPath = loader.GetAssetPath<T>();
                record = new UIPanelRecord(
                    key,
                    assetPath,
                    options,
                    ResolveOwnerSceneHandle(surface, options.Lifetime));
                if (!repository.Add(record))
                    return null;

                record.LoadTask = LoadRecordAsync<T>(
                    record,
                    surface.GetRoot(options.Layer)).Preserve();
            }
            else if (record.State != UIPanelState.Loading && !record.Panel)
            {
                RemoveBrokenRecord(record);
                return await OpenAsyncCore(options, applyArgs, cancellationToken);
            }

            if (record.State == UIPanelState.Loading)
            {
                var loadedPanel = await record.LoadTask.AttachExternalCancellation(cancellationToken);
                if (!loadedPanel)
                    return null;
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (record.Removed || !record.Panel)
                return null;

            return await PresentAsync(record, surface, options, applyArgs, cancellationToken);
        }

        private async UniTask<BasePanel> LoadRecordAsync<T>(UIPanelRecord record, Transform parent)
            where T : BasePanel
        {
            try
            {
                T panel = await loader.LoadAsync<T>(record.AssetPath, parent);
                if (record.Removed)
                {
                    panel.DisposeInternal();
                    Object.Destroy(panel.gameObject);
                    loader.Release(record.AssetPath);
                    return null;
                }

                AttachLoadedPanel(record, panel);
                return panel;
            }
            catch (Exception exception)
            {
                if (!record.Removed)
                    FailRecord(record, exception);
                return null;
            }
        }

        private T Present<T>(
            UIPanelRecord record,
            UISurface surface,
            UIOpenOptions options,
            Action<T> applyArgs)
            where T : BasePanel
        {
            return BeginPresent(record, surface, options, applyArgs, true, out _);
        }

        private async UniTask<T> PresentAsync<T>(
            UIPanelRecord record,
            UISurface surface,
            UIOpenOptions options,
            Action<T> applyArgs,
            CancellationToken cancellationToken)
            where T : BasePanel
        {
            T panel = BeginPresent(
                record,
                surface,
                options,
                applyArgs,
                false,
                out int operationVersion);

            if (!panel)
                return panel;

            UIPanelTransitionOperation operation = record.TransitionOperation;
            if (operation == null
                && operationVersion != 0
                && record.State == UIPanelState.Opening)
            {
                operation = transitions.StartEnter(
                    record,
                    panel,
                    operationVersion,
                    HandleEnterTransitionCompleted);
            }

            if (operation != null)
                await operation.Task.AttachExternalCancellation(cancellationToken);

            return panel;
        }

        private T BeginPresent<T>(
            UIPanelRecord record,
            UISurface surface,
            UIOpenOptions options,
            Action<T> applyArgs,
            bool completeImmediately,
            out int operationVersion)
            where T : BasePanel
        {
            operationVersion = 0;
            if (!(record.Panel is T panel) || !panel)
                return null;

            try
            {
                Transform targetRoot = surface.GetRoot(options.Layer);
                if (targetRoot && panel.transform.parent != targetRoot)
                    panel.transform.SetParent(targetRoot, false);

                bool alreadyPresented = record.State is
                    UIPanelState.Active or UIPanelState.Opening or
                    UIPanelState.Paused or UIPanelState.Closing;
                if (alreadyPresented && record.Options.Presentation != options.Presentation)
                {
                    LogUtil.Error(
                        "UI",
                        $"面板 {record.Key} 正在显示，不能直接从 {record.Options.Presentation} "
                        + $"切换为 {options.Presentation}。请先关闭面板。");
                    return null;
                }

                if (alreadyPresented
                    && options.Presentation == UIPresentationMode.Modal
                    && modals.TryPeek(options.SurfaceId, out UIPanelKey topModal)
                    && !topModal.Equals(record.Key))
                {
                    LogUtil.Warn(
                        "UI",
                        $"面板 {record.Key} 上方仍有 Modal，不能直接把它提到最上层。请先关闭上层 Modal。");
                    return null;
                }

                record.Options = options;
                record.OwnerSceneHandle = ResolveOwnerSceneHandle(surface, options.Lifetime);
                panel.Context.Layer = options.Layer;
                applyArgs?.Invoke(panel);
                panel.transform.SetAsLastSibling();

                if (record.State == UIPanelState.Closing)
                    transitions.Cancel(record);

                if (options.Presentation == UIPresentationMode.Page)
                {
                    PreparePageNavigation(record.Key, options.Navigation);
                    if (navigation.Push(record.Key, out UIPanelKey previousTop))
                        PauseNavigationTarget(previousTop);
                }
                else if (options.Presentation == UIPresentationMode.Modal)
                {
                    if (!alreadyPresented)
                    {
                        UIPanelKey underlyingKey = ResolveTopPresentationKey(options.SurfaceId);
                        modals.Push(record.Key, underlyingKey);
                        BlockInteraction(underlyingKey);
                    }
                    modalBackdrops.Show(record);
                }

                if (record.State == UIPanelState.Active)
                    return panel;

                if (record.State == UIPanelState.Opening)
                {
                    if (completeImmediately)
                    {
                        transitions.Cancel(record);
                        transitions.CompleteEnter(record, panel);
                        ChangeState(record, UIPanelState.Active);
                        interactions.Unblock(record);
                    }

                    return panel;
                }

                UIPanelState previousState = record.State;
                bool resumesPausedPanel = previousState == UIPanelState.Paused;
                if (!resumesPausedPanel)
                    panel.Context.BeginVisibilitySession();

                panel.gameObject.SetActive(true);
                if (!completeImmediately)
                    interactions.Block(record);
                ChangeState(record, UIPanelState.Opening);
                operationVersion = ++record.OperationVersion;

                if (resumesPausedPanel)
                    panel.ResumeInternal();
                else
                    panel.EnterInternal();

                if (completeImmediately)
                {
                    transitions.CompleteEnter(record, panel);
                    ChangeState(record, UIPanelState.Active);
                }
                return panel;
            }
            catch (Exception exception)
            {
                LogUtil.Error("UI", $"打开面板 {record.Key} 时发生异常：{exception}");
                transitions.Cancel(record);
                if (panel)
                    panel.gameObject.SetActive(false);
                ChangeState(record, UIPanelState.Hidden);
                RemoveFromPresentation(record, true);
                return null;
            }
        }

        #endregion

        #region Close

        public bool Close<T>(bool destroy = false) where T : BasePanel =>
            Close<T>(UIInstanceId.Default, UISurfaceId.Main, destroy);

        public bool Close<T>(
            UIInstanceId instanceId,
            UISurfaceId surfaceId,
            bool destroy = false)
            where T : BasePanel
        {
            UIPanelKey key = new(UIPanelId.From<T>(), instanceId, surfaceId);
            return Close(key, destroy);
        }

        /// <summary>按完整实例标识同步关闭面板，供调试工具和非泛型业务入口使用。</summary>
        public bool Close(UIPanelKey key, bool destroy = false) =>
            repository.TryGet(key, out UIPanelRecord record) && CloseRecord(record, destroy);

        /// <summary>异步关闭面板，并等待可选的退出过渡完成。</summary>
        public UniTask<bool> CloseAsync<T>(
            bool destroy = false,
            CancellationToken cancellationToken = default)
            where T : BasePanel =>
            CloseAsync<T>(UIInstanceId.Default, UISurfaceId.Main, destroy, cancellationToken);

        public async UniTask<bool> CloseAsync<T>(
            UIInstanceId instanceId,
            UISurfaceId surfaceId,
            bool destroy = false,
            CancellationToken cancellationToken = default)
            where T : BasePanel
        {
            UIPanelKey key = new(UIPanelId.From<T>(), instanceId, surfaceId);
            return await CloseAsync(key, destroy, cancellationToken);
        }

        /// <summary>按完整实例标识异步关闭面板。</summary>
        public async UniTask<bool> CloseAsync(
            UIPanelKey key,
            bool destroy = false,
            CancellationToken cancellationToken = default)
        {
            return repository.TryGet(key, out UIPanelRecord record)
                   && await CloseRecordAsync(record, destroy, cancellationToken);
        }

        /// <summary>关闭指定 Surface 的栈顶页面，并恢复上一页。</summary>
        public bool Back(UISurfaceId surfaceId = default, bool destroy = false)
        {
            return TryGetBackTarget(surfaceId, out UIPanelRecord record)
                   && RequestBack(record, destroy);
        }

        public async UniTask<bool> BackAsync(
            UISurfaceId surfaceId = default,
            bool destroy = false,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryGetBackTarget(surfaceId, out UIPanelRecord record))
                return false;

            return await RequestBackAsync(record, destroy, cancellationToken);
        }

        public int GetNavigationDepth(UISurfaceId surfaceId = default) =>
            navigation.GetDepth(surfaceId);

        public bool HasModal(UISurfaceId surfaceId = default) => modals.HasModal(surfaceId);

        public int CloseSurface(UISurfaceId surfaceId, bool destroy = false)
        {
            int closed = 0;
            foreach (UIPanelRecord record in repository.Snapshot())
            {
                if (record.Key.SurfaceId.Equals(surfaceId)
                    && CloseRecord(record, destroy, false))
                    closed++;
            }

            navigation.Clear(surfaceId);
            modals.Clear(surfaceId);
            return closed;
        }

        public int CloseAll(bool destroy = false)
        {
            int closed = 0;
            foreach (UIPanelRecord record in repository.Snapshot())
            {
                if (CloseRecord(record, destroy, false))
                    closed++;
            }

            navigation.ClearAll();
            modals.ClearAll();
            interactions.Clear();
            return closed;
        }

        /// <summary>
        /// 释放归属于指定场景的场景级面板。持久面板以及其他场景的面板不受影响。
        /// </summary>
        public int CloseScenePanels(UnityEngine.SceneManagement.Scene scene)
        {
            if (!scene.IsValid())
                return 0;

            return CloseScenePanels(scene.handle);
        }

        /// <summary>销毁全部面板实例和对应的面板资源引用。</summary>
        public void ClearAll()
        {
            // CloseAll(true) 已同步清空导航、Modal 与交互状态；无需重复执行。
            CloseAll(true);
        }

        private bool CloseRecord(
            UIPanelRecord record,
            bool destroy,
            bool restorePresentation = true)
        {
            if (record == null || record.Removed || record.State == UIPanelState.Disposed)
                return false;

            if (record.State == UIPanelState.Loading)
            {
                record.Removed = true;
                repository.Remove(record.Key, out _);
                ChangeState(record, UIPanelState.Disposed);
                return true;
            }

            BasePanel panel = record.Panel;
            if (!panel)
            {
                RemoveBrokenRecord(record);
                return false;
            }

            bool shouldDestroy = destroy
                                 || record.Options.CachePolicy == UICachePolicy.DestroyOnClose
                                 || record.TransitionOperation?.DestroyOnComplete == true;
            if (record.State == UIPanelState.Hidden && !shouldDestroy)
                return false;

            bool wasClosing = record.State == UIPanelState.Closing;
            bool needsExit = record.State is
                UIPanelState.Active or UIPanelState.Opening or UIPanelState.Paused;
            transitions.Cancel(record);
            try
            {
                if (needsExit)
                {
                    panel.Context.EndVisibilitySession();
                    ChangeState(record, UIPanelState.Closing);
                    panel.ExitInternal();
                }
            }
            catch (Exception exception)
            {
                LogUtil.Error("UI", $"关闭面板 {record.Key} 时发生异常：{exception}");
            }

            if (needsExit || wasClosing)
            {
                transitions.CompleteExit(record, panel);
            }

            if (!shouldDestroy)
            {
                panel.gameObject.SetActive(false);
                ChangeState(record, UIPanelState.Hidden);
                RemoveFromPresentation(record, restorePresentation);
                return true;
            }

            DestroyRecord(record);
            RemoveFromPresentation(record, restorePresentation);
            return true;
        }

        private async UniTask<bool> CloseRecordAsync(
            UIPanelRecord record,
            bool destroy,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (record == null || record.Removed || record.State == UIPanelState.Disposed)
                return false;

            if (record.State == UIPanelState.Loading || !record.Panel)
                return CloseRecord(record, destroy);

            if (record.State == UIPanelState.Closing)
            {
                UIPanelTransitionOperation current = record.TransitionOperation;
                if (current == null)
                    return CloseRecord(record, destroy);

                if (destroy)
                    current.DestroyOnComplete = true;

                await current.Task.AttachExternalCancellation(cancellationToken);
                return true;
            }

            bool shouldDestroy = destroy || record.Options.CachePolicy == UICachePolicy.DestroyOnClose;
            if (record.State == UIPanelState.Hidden)
                return shouldDestroy && CloseRecord(record, true);

            BasePanel panel = record.Panel;
            transitions.Cancel(record);
            interactions.Block(record);
            panel.Context.EndVisibilitySession();
            ChangeState(record, UIPanelState.Closing);
            int operationVersion = ++record.OperationVersion;

            try
            {
                panel.ExitInternal();
            }
            catch (Exception exception)
            {
                LogUtil.Error("UI", $"面板 {record.Key} 的退出生命周期发生异常：{exception}");
            }

            UIPanelTransitionOperation operation = transitions.StartExit(
                record,
                panel,
                operationVersion,
                shouldDestroy,
                HandleExitTransitionCompleted);
            await operation.Task.AttachExternalCancellation(cancellationToken);

            return true;
        }

        private bool RequestBack(UIPanelRecord record, bool destroy)
        {
            if (TryHandleBack(record))
                return true;

            return CloseRecord(record, destroy);
        }

        private async UniTask<bool> RequestBackAsync(
            UIPanelRecord record,
            bool destroy,
            CancellationToken cancellationToken)
        {
            if (TryHandleBack(record))
                return true;

            return await CloseRecordAsync(record, destroy, cancellationToken);
        }

        /// <summary>
        /// 统一解析返回目标，并顺便清理外部销毁后残留的导航记录。
        /// 同步和异步返回入口必须共用这条规则，避免两个 API 的栈行为分叉。
        /// </summary>
        private bool TryGetBackTarget(UISurfaceId surfaceId, out UIPanelRecord record)
        {
            record = null;
            if (modals.TryPeek(surfaceId, out UIPanelKey modalKey))
            {
                if (repository.TryGet(modalKey, out record))
                    return true;

                modals.Remove(modalKey, out _);
            }

            if (!navigation.TryPeek(surfaceId, out UIPanelKey pageKey))
                return false;

            if (repository.TryGet(pageKey, out record))
                return true;

            if (navigation.Remove(pageKey, out UIPanelKey nextTop))
                FocusOrResumeNavigationTarget(nextTop);
            return false;
        }

        private static bool TryHandleBack(UIPanelRecord record)
        {
            // ReSharper 只能看到当前程序集，无法识别业务或外部程序集未来对该扩展接口的实现。
            // ReSharper disable once SuspiciousTypeConversion.Global
            if (record?.Panel is not IUIBackHandler handler)
                return false;

            try
            {
                return handler.TryHandleBack();
            }
            catch (Exception exception)
            {
                LogUtil.Error("UI", $"面板 {record.Key} 处理返回请求时发生异常：{exception}");
                return false;
            }
        }

        #endregion

        private static UIPanelKey BuildKey<T>(UIOpenOptions options) where T : BasePanel =>
            new(UIPanelId.From<T>(), options.InstanceId, options.SurfaceId);

        private static int ResolveOwnerSceneHandle(
            UISurface surface,
            UIPanelLifetime lifetime)
        {
            if (lifetime == UIPanelLifetime.Persistent)
                return -1;

            return surface.Lifetime == UISurfaceLifetime.Scene
                ? surface.OwnerSceneHandle
                : SceneManager.GetActiveScene().handle;
        }

        private bool TryResolveSurface(UISurfaceId surfaceId, out UISurface surface)
        {
            if (surfaces.TryGet(surfaceId, out surface))
                return true;

            LogUtil.Error(
                "UI",
                $"未注册 UI Surface：{surfaceId}。请在对应 Canvas 上添加 UISurfaceRoot，"
                + "或先调用 UIManager.RegisterSurface。");
            return false;
        }

        private void AttachLoadedPanel(UIPanelRecord record, BasePanel panel)
        {
            record.Panel = panel;
            panel.Destroyed += HandlePanelDestroyed;
            panel.InitializeInternal(new UIPanelContext(record.Key, record.Options.Layer, record.AssetPath));
            ChangeState(record, UIPanelState.Hidden);
        }

        private void FailRecord(UIPanelRecord record, Exception exception)
        {
            transitions.Cancel(record);
            ChangeState(record, UIPanelState.Failed);
            record.Removed = true;
            repository.Remove(record.Key, out _);

            if (record.Panel)
            {
                record.Panel.DisposeInternal();
                Object.Destroy(record.Panel.gameObject);
                loader.Release(record.AssetPath);
            }

            LogUtil.Error("UI", $"加载面板 {record.Key} 失败：{exception}");
        }

        private void RemoveBrokenRecord(UIPanelRecord record)
        {
            transitions.Cancel(record);
            record.Removed = true;
            repository.Remove(record.Key, out _);
            ChangeState(record, UIPanelState.Disposed);
            loader.Release(record.AssetPath);
        }

        private void DestroyRecord(UIPanelRecord record)
        {
            transitions.Cancel(record);
            record.Removed = true;
            repository.Remove(record.Key, out _);
            ChangeState(record, UIPanelState.Disposed);
            interactions.Remove(record);
            modalBackdrops.Remove(record);

            if (record.Panel)
            {
                record.Panel.DisposeInternal();
                Object.Destroy(record.Panel.gameObject);
            }

            loader.Release(record.AssetPath);
        }

        private void HandlePanelDestroyed(BasePanel panel)
        {
            if (panel?.Context == null)
                return;

            UIPanelKey key = panel.Context.Key;
            if (!repository.TryGet(key, out UIPanelRecord record) || record.Panel != panel)
                return;

            transitions.Cancel(record);
            record.Removed = true;
            repository.Remove(key, out _);
            ChangeState(record, UIPanelState.Disposed);
            interactions.Remove(record);
            modalBackdrops.Remove(record);
            loader.Release(record.AssetPath);
            RemoveFromPresentation(record, true);
        }

        private void ChangeState(UIPanelRecord record, UIPanelState state)
        {
            record.State = state;

            switch (state)
            {
                case UIPanelState.Active:
                    if (record.Options.TakeFocus && CanReceiveFocus(record))
                        inputRouter.Activate(record);
                    break;
                case UIPanelState.Closing:
                case UIPanelState.Paused:
                case UIPanelState.Hidden:
                    inputRouter.Deactivate(record);
                    break;
                case UIPanelState.Disposed:
                case UIPanelState.Failed:
                    inputRouter.Remove(record);
                    break;
            }

            try
            {
                PanelStateChanged?.Invoke(record.Key, state);
            }
            catch (Exception exception)
            {
                LogUtil.Error("UI", $"面板状态监听器发生异常：{exception}");
            }
        }

        private void PreparePageNavigation(UIPanelKey targetKey, UINavigationMode mode)
        {
            foreach (UIPanelKey key in navigation.GetRemovalPlan(targetKey, mode))
                RemoveNavigationEntrySilently(key);
        }

        private void RemoveNavigationEntrySilently(UIPanelKey key)
        {
            if (repository.TryGet(key, out UIPanelRecord record)
                && CloseRecord(record, false, false))
                return;

            navigation.Remove(key, out _);
        }

        private void PauseNavigationTarget(UIPanelKey key)
        {
            if (string.IsNullOrEmpty(key.PanelId.Value)
                || !repository.TryGet(key, out UIPanelRecord record))
                return;

            if (!record.Panel
                || record.State is not (UIPanelState.Active or UIPanelState.Opening))
                return;

            if (record.State == UIPanelState.Opening)
            {
                transitions.Cancel(record);
                transitions.CompleteEnter(record, record.Panel);
            }

            try
            {
                record.Panel.PauseInternal();
            }
            catch (Exception exception)
            {
                LogUtil.Error("UI", $"暂停面板 {record.Key} 时发生异常：{exception}");
            }

            ChangeState(record, UIPanelState.Paused);
            record.Panel.gameObject.SetActive(false);
        }

        private void RemoveFromPresentation(UIPanelRecord record, bool restoreFocus = true)
        {
            if (record == null)
                return;

            interactions.Remove(record);
            UIPanelKey key = record.Key;
            if (record.Options.Presentation == UIPresentationMode.Modal
                && modals.Remove(key, out UIPanelKey restoreKey))
            {
                if (record.Removed)
                    modalBackdrops.Remove(record);
                else
                    modalBackdrops.Hide(record);
                interactions.Unblock(restoreKey);
                if (restoreFocus)
                    FocusOrResumeNavigationTarget(restoreKey);
                return;
            }

            if (record.Options.Presentation == UIPresentationMode.Page
                && navigation.Remove(key, out UIPanelKey nextTop))
            {
                if (restoreFocus)
                    FocusOrResumeNavigationTarget(nextTop);
                return;
            }

            if (restoreFocus)
                FocusTopPresentation(key.SurfaceId);
        }

        private void FocusOrResumeNavigationTarget(UIPanelKey key)
        {
            if (string.IsNullOrEmpty(key.PanelId.Value)
                || !repository.TryGet(key, out UIPanelRecord record))
                return;

            if (!record.Panel)
                return;

            if (record.State == UIPanelState.Active)
            {
                inputRouter.Activate(record);
                return;
            }

            if (record.State != UIPanelState.Paused)
                return;

            try
            {
                record.Panel.gameObject.SetActive(true);
                record.Panel.ResumeInternal();
            }
            catch (Exception exception)
            {
                LogUtil.Error("UI", $"恢复面板 {record.Key} 时发生异常：{exception}");
            }

            ChangeState(record, UIPanelState.Active);
        }

        private bool CanOpen(UIOpenOptions options)
        {
            if (options.Presentation != UIPresentationMode.Page
                || !modals.HasModal(options.SurfaceId))
                return true;

            LogUtil.Warn(
                "UI",
                $"Surface {options.SurfaceId} 上仍有 Modal，不能在其下方打开新页面。"
                + "请先关闭 Modal，或将新面板明确设置为 Modal/Overlay。");
            return false;
        }

        private static bool CanUseOptions(UISurface surface, UIOpenOptions options)
        {
            if (options.Lifetime != UIPanelLifetime.Persistent
                || surface.Lifetime == UISurfaceLifetime.Persistent)
                return true;

            LogUtil.Error(
                "UI",
                $"不能把跨场景面板挂载到场景级 Surface：{surface.Id}。"
                + "请将 UISurfaceRoot 的生命周期设为‘持久级’，"
                + "或把面板生命周期改为‘场景级’。");
            return false;
        }

        private void BlockInteraction(UIPanelKey key)
        {
            if (!string.IsNullOrEmpty(key.PanelId.Value)
                && repository.TryGet(key, out UIPanelRecord record))
                interactions.Block(record);
        }

        private void FocusTopPresentation(UISurfaceId surfaceId)
        {
            if (modals.TryPeek(surfaceId, out UIPanelKey modalKey))
            {
                FocusOrResumeNavigationTarget(modalKey);
                return;
            }

            if (navigation.TryPeek(surfaceId, out UIPanelKey pageKey))
                FocusOrResumeNavigationTarget(pageKey);
        }

        private void HandleInputModeChanged(UIInputMode mode)
        {
            try
            {
                InputModeChanged?.Invoke(mode);
            }
            catch (Exception exception)
            {
                LogUtil.Error("UI", $"输入模式监听器发生异常：{exception}");
            }
        }

        private int CloseScenePanels(int sceneHandle)
        {
            int closed = 0;
            var affectedSurfaces = new HashSet<UISurfaceId>();
            foreach (UIPanelRecord record in repository.Snapshot())
            {
                if (record.Options.Lifetime == UIPanelLifetime.Scene
                    && record.OwnerSceneHandle == sceneHandle
                    && CloseRecord(record, true, false))
                {
                    closed++;
                    affectedSurfaces.Add(record.Key.SurfaceId);
                }
            }

            foreach (UISurfaceId surfaceId in affectedSurfaces)
                FocusTopPresentation(surfaceId);

            return closed;
        }

        private void HandleSceneUnloaded(UnityEngine.SceneManagement.Scene scene)
        {
            CloseScenePanels(scene.handle);

            foreach (UISurface surface in surfaces.Snapshot())
            {
                if (surface.Lifetime != UISurfaceLifetime.Scene
                    || surface.OwnerSceneHandle != scene.handle)
                    continue;

                CloseSurface(surface.Id, true);
                surfaces.Remove(surface.Id);
            }
        }

        private static string GetSceneName(int sceneHandle)
        {
            if (sceneHandle < 0)
                return "跨场景";

            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                UnityEngine.SceneManagement.Scene scene = SceneManager.GetSceneAt(index);
                if (scene.handle == sceneHandle)
                    return scene.name;
            }

            return $"已卸载场景 ({sceneHandle})";
        }

        private void HandleEnterTransitionCompleted(UIPanelRecord record, BasePanel panel)
        {
            if (panel)
            {
                ChangeState(record, UIPanelState.Active);
                interactions.Unblock(record);
            }
        }

        private void HandleExitTransitionCompleted(
            UIPanelRecord record,
            BasePanel panel,
            bool destroyOnComplete)
        {
            if (destroyOnComplete)
                DestroyRecord(record);
            else if (panel)
            {
                panel.gameObject.SetActive(false);
                ChangeState(record, UIPanelState.Hidden);
            }

            RemoveFromPresentation(record, true);
        }

        private UIPanelKey ResolveTopPresentationKey(UISurfaceId surfaceId)
        {
            if (modals.TryPeek(surfaceId, out UIPanelKey modalKey))
                return modalKey;

            UIPanelKey activeKey = inputRouter.ActiveKey;
            if (!string.IsNullOrEmpty(activeKey.PanelId.Value)
                && activeKey.SurfaceId.Equals(surfaceId))
                return activeKey;

            return navigation.TryPeek(surfaceId, out UIPanelKey pageKey) ? pageKey : default;
        }

        private bool CanReceiveFocus(UIPanelRecord record)
        {
            return !modals.TryPeek(record.Key.SurfaceId, out UIPanelKey modalKey)
                   || modalKey.Equals(record.Key);
        }
    }
}
