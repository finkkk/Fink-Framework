using System;
using UnityEngine;
using UnityInput = UnityEngine.Input;

namespace FinkFramework.Runtime.Input
{
    /// <summary>
    /// Legacy Input Manager 的输入活动适配器。
    /// 旧系统的 Horizontal/Vertical 轴可能同时绑定键盘与手柄，因此明确的键鼠按键
    /// 会优先归类为键鼠；只有没有键盘按键事件时，轴首次越过死区才归类为手柄。
    /// </summary>
    internal sealed class LegacyInputDeviceActivitySource : IInputDeviceActivitySource
    {
        private const float AxisActivationThreshold = 0.25f;
        private const float DeviceScanInterval = 1f;

        private bool axesWereActive;
        private bool axesAvailable = true;
        private bool hasConnectedGamepad;
        private bool mousePositionInitialized;
        private Vector3 lastMousePosition;
        private float nextDeviceScanTime;

        public bool TryGetActiveDevice(DeviceDetectionSettings settings, out InputDeviceType device)
        {
            RefreshConnectedGamepadCache();
            bool axisActivated = UpdateGamepadAxisActivity();

            if (HasJoystickButtonDown())
            {
                device = InputDeviceType.Gamepad;
                return true;
            }

            if (UnityInput.touchCount > 0)
            {
                device = InputDeviceType.Touch;
                return true;
            }

            if (HasMouseActivity(settings))
            {
                device = InputDeviceType.KeyboardMouse;
                return true;
            }

            // 先处理键盘按键，避免 WASD/方向键触发的混合轴被误判为手柄摇杆。
            if (UnityInput.anyKeyDown)
            {
                device = InputDeviceType.KeyboardMouse;
                return true;
            }

            if (axisActivated)
            {
                device = InputDeviceType.Gamepad;
                return true;
            }

            device = InputDeviceType.Unknown;
            return false;
        }

        private void RefreshConnectedGamepadCache()
        {
            if (Time.unscaledTime < nextDeviceScanTime)
                return;

            nextDeviceScanTime = Time.unscaledTime + DeviceScanInterval;
            hasConnectedGamepad = false;
            foreach (string name in UnityInput.GetJoystickNames())
            {
                if (!string.IsNullOrWhiteSpace(name))
                {
                    hasConnectedGamepad = true;
                    break;
                }
            }
        }

        private bool UpdateGamepadAxisActivity()
        {
            if (!hasConnectedGamepad || !axesAvailable)
            {
                axesWereActive = false;
                return false;
            }

            try
            {
                bool axesAreActive = Mathf.Abs(UnityInput.GetAxisRaw("Horizontal")) >= AxisActivationThreshold
                                      || Mathf.Abs(UnityInput.GetAxisRaw("Vertical")) >= AxisActivationThreshold;
                bool activated = axesAreActive && !axesWereActive;
                axesWereActive = axesAreActive;
                return activated;
            }
            catch (ArgumentException)
            {
                // 项目移除了默认轴时只停用轴检测；手柄按钮检测仍可继续工作。
                axesAvailable = false;
                axesWereActive = false;
                return false;
            }
        }

        private bool HasMouseActivity(DeviceDetectionSettings settings)
        {
            Vector3 mousePosition = UnityInput.mousePosition;
            float movementThreshold = Mathf.Max(0.01f, settings.MouseMovementThreshold);
            bool moved = settings.TreatMouseMovementAsKeyboardMouseInput
                         && mousePositionInitialized
                         && (mousePosition - lastMousePosition).sqrMagnitude
                         >= movementThreshold * movementThreshold;
            lastMousePosition = mousePosition;
            mousePositionInitialized = true;

            return UnityInput.GetMouseButtonDown(0)
                   || UnityInput.GetMouseButtonDown(1)
                   || UnityInput.GetMouseButtonDown(2)
                   || moved;
        }

        private static bool HasJoystickButtonDown()
        {
            for (int button = 0; button <= 19; button++)
            {
                if (UnityInput.GetKeyDown((KeyCode)((int)KeyCode.JoystickButton0 + button)))
                    return true;
            }

            return false;
        }
    }
}
