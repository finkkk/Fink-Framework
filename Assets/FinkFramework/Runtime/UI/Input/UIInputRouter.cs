using System;
using System.Collections.Generic;
using FinkFramework.Runtime.Environments;
using FinkFramework.Runtime.Input;
using FinkFramework.Runtime.UI.Base;
using FinkFramework.Runtime.UI.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FinkFramework.Runtime.UI.Input
{
    /// <summary>
    /// 统一处理鼠标、键盘和手柄焦点。它只关心当前交互面板，不负责页面导航策略。
    /// </summary>
    internal sealed class UIInputRouter
    {
        private const float PointerMoveThreshold = 1f;

        private readonly Dictionary<UIPanelKey, GameObject> rememberedSelections = new();
        private readonly Action<UIInputMode> modeChanged;
        private readonly Func<EventSystem> eventSystemProvider;

        private UIPanelRecord activeRecord;
        private EventSystem navigationEventSystem;
        private Vector3 lastPointerPosition;
        private int focusDelayFrames;
        private bool pointerInitialized;
        private static bool legacyAxesAvailable = true;

        public UIInputMode Mode { get; private set; } = UIInputMode.Pointer;
        public UIPanelKey ActiveKey => activeRecord?.Key ?? default;
        /// <summary>
        /// 是否允许键盘/手柄导航、提交和取消事件进入 UGUI。
        /// 关闭时不影响鼠标、触摸等指针点击。
        /// </summary>
        public bool NavigationInteractionEnabled { get; private set; } = true;

        public UIInputRouter(
            Action<UIInputMode> modeChanged,
            Func<EventSystem> eventSystemProvider = null)
        {
            this.modeChanged = modeChanged;
            this.eventSystemProvider = eventSystemProvider ?? (() => EventSystem.current);
        }

        public void Tick()
        {
            SynchronizeEventSystemNavigation();
            if (!NavigationInteractionEnabled)
                return;

            DetectInputMode();

            if (activeRecord?.Panel == null || !activeRecord.Panel.gameObject.activeInHierarchy)
                return;

            RememberCurrentSelection(activeRecord);
            if (Mode != UIInputMode.Navigation)
                return;

            if (focusDelayFrames > 0)
            {
                focusDelayFrames--;
                return;
            }

            if (!IsCurrentSelectionValid(activeRecord.Panel))
                Focus(activeRecord, false);
        }

        public void Activate(UIPanelRecord record)
        {
            if (record?.Panel == null)
                return;

            if (activeRecord != record)
                RememberCurrentSelection(activeRecord);

            activeRecord = record;
            focusDelayFrames = 1;

            if (Mode == UIInputMode.Navigation)
                Focus(record, false);
        }

        public void Deactivate(UIPanelRecord record)
        {
            if (record == null)
                return;

            RememberCurrentSelection(record);
            if (activeRecord != record)
                return;

            ClearSelectionIfOwned(record.Panel);
            activeRecord = null;
        }

        public bool Focus(UIPanelRecord record, bool force)
        {
            if (!NavigationInteractionEnabled
                || record?.Panel == null
                || !record.Panel.gameObject.activeInHierarchy)
                return false;

            EventSystem eventSystem = GetEventSystem();
            if (!eventSystem)
                return false;

            if (!force && Mode != UIInputMode.Navigation)
                return false;

            GameObject target = GetRememberedSelection(record);
            if (!IsSelectableValid(target, record.Panel))
            {
                Selectable selectable = record.Panel.ResolveDefaultSelectableInternal();
                target = selectable ? selectable.gameObject : null;
            }

            if (!target)
                return false;

            activeRecord = record;
            eventSystem.SetSelectedGameObject(target);
            rememberedSelections[record.Key] = target;
            return true;
        }

        public void Remove(UIPanelRecord record)
        {
            if (record == null)
                return;

            Deactivate(record);
            rememberedSelections.Remove(record.Key);
        }

        public void Clear()
        {
            rememberedSelections.Clear();
            activeRecord = null;
            ClearCurrentSelection();
        }

        /// <summary>
        /// 全局启用或关闭 UGUI 导航交互。
        /// 关闭时会清空当前焦点并禁用 EventSystem 的导航事件，因此 Space、Enter、方向键和手柄提交
        /// 不会再触发 Selectable；鼠标与触摸点击保持可用。
        /// </summary>
        public void SetNavigationInteractionEnabled(bool enabled)
        {
            if (NavigationInteractionEnabled == enabled)
            {
                SynchronizeEventSystemNavigation();
                return;
            }

            NavigationInteractionEnabled = enabled;
            if (!enabled)
            {
                focusDelayFrames = 0;
                rememberedSelections.Clear();
                SetMode(UIInputMode.Pointer);
                ClearCurrentSelection();
            }
            else
            {
                focusDelayFrames = 1;
            }

            SynchronizeEventSystemNavigation();
        }

        public void SetMode(UIInputMode mode)
        {
            if (!NavigationInteractionEnabled && mode == UIInputMode.Navigation)
                return;

            if (Mode == mode)
                return;

            Mode = mode;
            modeChanged?.Invoke(mode);
            if (mode == UIInputMode.Navigation && activeRecord != null)
                Focus(activeRecord, true);
        }

        private void DetectInputMode()
        {
            Vector3 pointerPosition = GetPointerPosition();
            if (!pointerInitialized)
            {
                pointerInitialized = true;
                lastPointerPosition = pointerPosition;
            }

            bool pointerMoved = (pointerPosition - lastPointerPosition).sqrMagnitude
                                >= PointerMoveThreshold * PointerMoveThreshold;
            lastPointerPosition = pointerPosition;

            if (pointerMoved || IsPointerPressed())
            {
                SetMode(UIInputMode.Pointer);
                return;
            }

            if (IsNavigationPressed())
                SetMode(UIInputMode.Navigation);
        }

        private static Vector3 GetPointerPosition()
        {
            if (EnvironmentState.FinalUseNewInputSystem
                && InputSystemHooks.GetPointerPosition != null)
                return InputSystemHooks.GetPointerPosition();

            return UnityEngine.Input.mousePosition;
        }

        private static bool IsPointerPressed()
        {
            if (EnvironmentState.FinalUseNewInputSystem
                && InputSystemHooks.IsPointerPressed != null)
                return InputSystemHooks.IsPointerPressed();

            return UnityEngine.Input.GetMouseButtonDown(0)
                   || UnityEngine.Input.GetMouseButtonDown(1)
                   || UnityEngine.Input.GetMouseButtonDown(2);
        }

        private static bool IsNavigationPressed()
        {
            if (EnvironmentState.FinalUseNewInputSystem
                && InputSystemHooks.IsNavigationPressed != null)
                return InputSystemHooks.IsNavigationPressed();

            return UnityEngine.Input.GetKeyDown(KeyCode.UpArrow)
                   || UnityEngine.Input.GetKeyDown(KeyCode.DownArrow)
                   || UnityEngine.Input.GetKeyDown(KeyCode.LeftArrow)
                   || UnityEngine.Input.GetKeyDown(KeyCode.RightArrow)
                   || UnityEngine.Input.GetKeyDown(KeyCode.Tab)
                   || UnityEngine.Input.GetKeyDown(KeyCode.Return)
                   || UnityEngine.Input.GetKeyDown(KeyCode.Space)
                   || UnityEngine.Input.GetKeyDown(KeyCode.JoystickButton0)
                   || IsLegacyAxisNavigationPressed();
        }

        private static bool IsLegacyAxisNavigationPressed()
        {
            if (!legacyAxesAvailable)
                return false;

            try
            {
                return Mathf.Abs(UnityEngine.Input.GetAxisRaw("Horizontal")) > 0.25f
                       || Mathf.Abs(UnityEngine.Input.GetAxisRaw("Vertical")) > 0.25f;
            }
            catch (ArgumentException)
            {
                // 项目删除了默认轴时停止轮询，避免每帧重复抛错。
                legacyAxesAvailable = false;
                return false;
            }
        }

        private void RememberCurrentSelection(UIPanelRecord record)
        {
            EventSystem eventSystem = GetEventSystem();
            if (record?.Panel == null || !eventSystem)
                return;

            GameObject selected = eventSystem.currentSelectedGameObject;
            if (IsSelectableValid(selected, record.Panel))
                rememberedSelections[record.Key] = selected;
        }

        private GameObject GetRememberedSelection(UIPanelRecord record)
        {
            return rememberedSelections.GetValueOrDefault(record.Key);
        }

        private bool IsCurrentSelectionValid(BasePanel panel)
        {
            EventSystem eventSystem = GetEventSystem();
            return eventSystem && IsSelectableValid(eventSystem.currentSelectedGameObject, panel);
        }

        private static bool IsSelectableValid(GameObject target, BasePanel panel)
        {
            if (!target || !panel || !target.transform.IsChildOf(panel.transform))
                return false;

            Selectable selectable = target.GetComponent<Selectable>();
            return selectable
                   && target.activeInHierarchy
                   && selectable.IsInteractable()
                   && selectable.navigation.mode != UnityEngine.UI.Navigation.Mode.None;
        }

        private void ClearSelectionIfOwned(BasePanel panel)
        {
            EventSystem eventSystem = GetEventSystem();
            if (!eventSystem || !panel)
                return;

            GameObject selected = eventSystem.currentSelectedGameObject;
            if (selected && selected.transform.IsChildOf(panel.transform))
                eventSystem.SetSelectedGameObject(null);
        }

        private void ClearCurrentSelection()
        {
            EventSystem eventSystem = GetEventSystem();
            if (eventSystem)
                eventSystem.SetSelectedGameObject(null);
        }

        private EventSystem GetEventSystem()
        {
            EventSystem current = eventSystemProvider();
            if (current)
                return current;

            // 新场景的 EventSystem 销毁后，EventSystem.current 可能暂时为空；
            // 回退到仍存活的常驻 EventSystem，避免 UI 切场景后失去焦点和导航。
            return UnityEngine.Object.FindFirstObjectByType<EventSystem>();
        }

        /// <summary>
        /// EventSystem 的 sendNavigationEvents 会同时控制移动与 Submit/Cancel；
        /// 因而应通过它关闭整条导航输入通道，而不是只停止框架自己的焦点分配。
        /// </summary>
        private void SynchronizeEventSystemNavigation()
        {
            EventSystem current = GetEventSystem();
            if (!current)
            {
                navigationEventSystem = null;
                return;
            }

            if (navigationEventSystem != current
                || current.sendNavigationEvents != NavigationInteractionEnabled)
            {
                current.sendNavigationEvents = NavigationInteractionEnabled;
                navigationEventSystem = current;
            }
        }
    }
}
