using FinkFramework.Runtime.Environments;
using FinkFramework.Runtime.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FinkFramework.Runtime.InputSystem
{
    /// <summary>Input System 对框架核心输入模块的运行时适配器。</summary>
    internal static class InputSystemRuntimeAdapter
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            InputSystemHooks.CreateNewActivitySource = CreateActivitySource;
            InputSystemHooks.GetPointerPosition = GetPointerPosition;
            InputSystemHooks.IsPointerPressed = IsPointerPressed;
            InputSystemHooks.IsNavigationPressed = IsNavigationPressed;
        }

        private static IInputDeviceActivitySource CreateActivitySource()
        {
            return EnvironmentState.FinalUseNewInputSystem
                ? new NewInputDeviceActivitySource()
                : null;
        }

        private static Vector3 GetPointerPosition()
        {
            if (Touchscreen.current != null
                && Touchscreen.current.primaryTouch.press.isPressed)
            {
                return Touchscreen.current.primaryTouch.position.ReadValue();
            }

            return Mouse.current != null
                ? Mouse.current.position.ReadValue()
                : Vector3.zero;
        }

        private static bool IsPointerPressed()
        {
            bool mousePressed = Mouse.current != null
                                && (Mouse.current.leftButton.wasPressedThisFrame
                                    || Mouse.current.rightButton.wasPressedThisFrame
                                    || Mouse.current.middleButton.wasPressedThisFrame);
            bool touchPressed = Touchscreen.current != null
                                && Touchscreen.current.primaryTouch.press.wasPressedThisFrame;
            return mousePressed || touchPressed;
        }

        private static bool IsNavigationPressed()
        {
            bool gamepadPressed = Gamepad.current != null
                                 && (Gamepad.current.dpad.ReadValue().sqrMagnitude > 0.01f
                                     || Gamepad.current.leftStick.ReadValue().sqrMagnitude > 0.25f
                                     || Gamepad.current.buttonSouth.wasPressedThisFrame);

            Keyboard keyboard = Keyboard.current;
            bool keyboardPressed = keyboard != null
                                   && (keyboard.upArrowKey.wasPressedThisFrame
                                       || keyboard.downArrowKey.wasPressedThisFrame
                                       || keyboard.leftArrowKey.wasPressedThisFrame
                                       || keyboard.rightArrowKey.wasPressedThisFrame
                                       || keyboard.tabKey.wasPressedThisFrame
                                       || keyboard.enterKey.wasPressedThisFrame
                                       || keyboard.spaceKey.wasPressedThisFrame);
            return gamepadPressed || keyboardPressed;
        }
    }
}
