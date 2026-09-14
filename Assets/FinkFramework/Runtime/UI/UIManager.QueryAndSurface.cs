using System;
using System.Collections.Generic;
using FinkFramework.Runtime.Input;
using FinkFramework.Runtime.UI.Base;
using FinkFramework.Runtime.UI.Core;
using FinkFramework.Runtime.UI.Surface;
using FinkFramework.Runtime.Utils;
using UnityEngine;

namespace FinkFramework.Runtime.UI
{
    /// <summary>
    /// UIManager 的查询与 Surface 管理入口。
    /// 与面板生命周期调度分离，避免 UIManager 核心流程继续膨胀。
    /// </summary>
    public sealed partial class UIManager
    {
        #region Query

        public T Get<T>(UIInstanceId instanceId = default, UISurfaceId surfaceId = default)
            where T : BasePanel
        {
            UIPanelKey key = new(UIPanelId.From<T>(), instanceId, surfaceId);
            return repository.TryGet(key, out UIPanelRecord record) && record.Panel
                ? record.Panel as T
                : null;
        }

        public bool TryGet<T>(
            out T panel,
            UIInstanceId instanceId = default,
            UISurfaceId surfaceId = default)
            where T : BasePanel
        {
            panel = Get<T>(instanceId, surfaceId);
            return panel;
        }

        public bool IsOpen<T>(
            UIInstanceId instanceId = default,
            UISurfaceId surfaceId = default)
            where T : BasePanel
        {
            UIPanelKey key = new(UIPanelId.From<T>(), instanceId, surfaceId);
            return repository.TryGet(key, out UIPanelRecord record)
                   && record.Panel
                   && record.State is UIPanelState.Opening or UIPanelState.Active or UIPanelState.Paused;
        }

        public bool TryGetState<T>(
            out UIPanelState state,
            UIInstanceId instanceId = default,
            UISurfaceId surfaceId = default)
            where T : BasePanel
        {
            UIPanelKey key = new(UIPanelId.From<T>(), instanceId, surfaceId);
            if (repository.TryGet(key, out UIPanelRecord record))
            {
                state = record.State;
                return true;
            }

            state = UIPanelState.Disposed;
            return false;
        }

        /// <summary>主动把键盘或手柄焦点移到可见面板。</summary>
        public bool Focus<T>(
            UIInstanceId instanceId = default,
            UISurfaceId surfaceId = default)
            where T : BasePanel
        {
            UIPanelKey key = new(UIPanelId.From<T>(), instanceId, surfaceId);
            return Focus(key);
        }

        /// <summary>按完整实例标识主动转移键盘或手柄焦点。</summary>
        public bool Focus(UIPanelKey key) =>
            repository.TryGet(key, out UIPanelRecord record)
            && inputRouter.Focus(record, true);

        /// <summary>获取当前所有面板的只读快照；适合调试器和自动化测试。</summary>
        public IReadOnlyList<UIPanelSnapshot> GetPanelSnapshots()
        {
            List<UIPanelRecord> records = repository.Snapshot();
            var result = new List<UIPanelSnapshot>(records.Count);
            foreach (UIPanelRecord record in records)
            {
                BasePanel panel = record.Panel;
                result.Add(new UIPanelSnapshot(
                    record.Key,
                    record.State,
                    record.Options,
                    record.AssetPath,
                    panel ? panel.gameObject.name : string.Empty,
                    panel && panel.gameObject.activeInHierarchy,
                    record.Options.Lifetime,
                    GetSceneName(record.OwnerSceneHandle)));
            }

            return result;
        }

        /// <summary>获取当前已注册 Surface 的只读快照。</summary>
        public IReadOnlyList<UISurfaceSnapshot> GetSurfaceSnapshots()
        {
            List<UISurface> registeredSurfaces = surfaces.Snapshot();
            var result = new List<UISurfaceSnapshot>(registeredSurfaces.Count);
            foreach (UISurface surface in registeredSurfaces)
            {
                result.Add(new UISurfaceSnapshot(
                    surface.Id,
                    surface.Lifetime,
                    surface.OwnerSceneName,
                    surface.IsWorldSpace,
                    surface.IsActive));
            }

            return result;
        }

        /// <summary>手动切换 UI 输入模式，适合无障碍设置或自定义输入后端。</summary>
        public void SetInputMode(UIInputMode mode) => inputRouter.SetMode(mode);

        /// <summary>当前是否允许 UGUI 接收键盘与手柄导航/提交事件。</summary>
        public bool NavigationInteractionEnabled => inputRouter.NavigationInteractionEnabled;

        /// <summary>最近一次有效操作使用的主要输入设备。</summary>
        public InputDeviceType CurrentInputDevice => inputDeviceDetectionManager.CurrentDevice;

        /// <summary>主要输入设备改变时触发，参数依次为旧设备与新设备。</summary>
        public event Action<InputDeviceType, InputDeviceType> InputDeviceChanged
        {
            add => inputDeviceDetectionManager.DeviceChanged += value;
            remove => inputDeviceDetectionManager.DeviceChanged -= value;
        }

        /// <summary>
        /// 全局启用或关闭键盘、手柄的导航交互。
        /// 关闭时 Space、Enter、方向键和手柄 Submit 不会触发当前 UI 控件；鼠标与触摸点击不受影响。
        /// 当 UI 设置启用自动按设备切换时，下一帧会由最近的输入设备重新应用该策略。
        /// </summary>
        public void SetNavigationInteractionEnabled(bool enabled) =>
            inputRouter.SetNavigationInteractionEnabled(enabled);

        /// <summary>关闭键盘与手柄导航交互，保留鼠标和触摸点击。</summary>
        public void DisableNavigationInteraction() =>
            SetNavigationInteractionEnabled(false);

        /// <summary>重新启用键盘与手柄导航交互。</summary>
        public void EnableNavigationInteraction() =>
            SetNavigationInteractionEnabled(true);

        #endregion

        #region Surface

        /// <summary>注册独立 UI Surface；屏幕与世界空间 Canvas 使用同一模型。</summary>
        public bool RegisterSurface(
            UISurfaceId surfaceId,
            Canvas canvas,
            Transform panelRoot = null,
            bool replace = false,
            UISurfaceLifetime lifetime = UISurfaceLifetime.Scene)
        {
            if (!canvas)
            {
                LogUtil.Error("UI", $"注册 Surface {surfaceId} 失败：Canvas 为空。");
                return false;
            }

            if (surfaceId.Equals(UISurfaceId.Main))
            {
                LogUtil.Error("UI", "Main 是框架保留的 Surface Id，不能被外部覆盖。");
                return false;
            }

            if (!canvas.worldCamera)
            {
                if (canvas.renderMode == RenderMode.ScreenSpaceCamera && UICamera)
                    canvas.worldCamera = UICamera;
                else if (canvas.renderMode == RenderMode.WorldSpace && Camera.main)
                    canvas.worldCamera = Camera.main;
            }

            var surface = new UISurface(
                surfaceId,
                canvas,
                panelRoot ? panelRoot : canvas.transform,
                lifetime: lifetime);

            if (replace)
                CloseSurface(surfaceId, true);

            if (surfaces.Register(surface, replace))
                return true;

            LogUtil.Warn("UI", $"Surface {surfaceId} 已存在；如需替换，请显式传入 replace: true。");
            return false;
        }

        public bool UnregisterSurface(UISurfaceId surfaceId)
        {
            if (surfaceId.Equals(UISurfaceId.Main))
                return false;

            CloseSurface(surfaceId, true);
            return surfaces.Remove(surfaceId);
        }

        internal bool UnregisterSurface(UISurfaceId surfaceId, Canvas owner)
        {
            if (surfaceId.Equals(UISurfaceId.Main)
                || !surfaces.IsOwnedBy(surfaceId, owner))
                return false;

            CloseSurface(surfaceId, true);
            return surfaces.Remove(surfaceId, owner);
        }

        #endregion
    }
}
