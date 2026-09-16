using FinkFramework.Runtime.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FinkFramework.Runtime.InputSystem
{
    /// <summary>Input System Package 的输入活动适配器。</summary>
    internal sealed class NewInputDeviceActivitySource : IInputDeviceActivitySource
    {
        private const float TouchMoveThreshold = 0.01f;
        private const float StickActivationThreshold = 0.25f;

        private bool gamepadAxisWasActive;

        public bool TryGetActiveDevice(DeviceDetectionSettings settings, out InputDeviceType device)
        {
            if (HasTouchActivity())
            {
                device = InputDeviceType.Touch;
                return true;
            }

            if (HasPointerActivity(settings))
            {
                device = InputDeviceType.KeyboardMouse;
                return true;
            }

            if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
            {
                device = InputDeviceType.KeyboardMouse;
                return true;
            }

            if (HasGamepadActivity())
            {
                device = InputDeviceType.Gamepad;
                return true;
            }

            device = InputDeviceType.Unknown;
            return false;
        }

        private static bool HasTouchActivity()
        {
            if (Touchscreen.current == null)
                return false;

            var touch = Touchscreen.current.primaryTouch;
            return touch.press.wasPressedThisFrame
                   || (touch.press.isPressed
                       && touch.delta.ReadValue().sqrMagnitude
                       >= TouchMoveThreshold * TouchMoveThreshold);
        }

        private static bool HasPointerActivity(DeviceDetectionSettings settings)
        {
            if (Mouse.current == null)
                return false;

            float movementThreshold = Mathf.Max(0.01f, settings.MouseMovementThreshold);
            return Mouse.current.leftButton.wasPressedThisFrame
                   || Mouse.current.rightButton.wasPressedThisFrame
                   || Mouse.current.middleButton.wasPressedThisFrame
                   || (settings.TreatMouseMovementAsKeyboardMouseInput
                       && Mouse.current.delta.ReadValue().sqrMagnitude
                       >= movementThreshold * movementThreshold);
        }

        private bool HasGamepadActivity()
        {
            Gamepad gamepad = Gamepad.current;
            if (gamepad == null)
            {
                gamepadAxisWasActive = false;
                return false;
            }

            bool buttonPressed = gamepad.buttonSouth.wasPressedThisFrame
                                 || gamepad.buttonNorth.wasPressedThisFrame
                                 || gamepad.buttonWest.wasPressedThisFrame
                                 || gamepad.buttonEast.wasPressedThisFrame
                                 || gamepad.startButton.wasPressedThisFrame
                                 || gamepad.selectButton.wasPressedThisFrame
                                 || gamepad.leftShoulder.wasPressedThisFrame
                                 || gamepad.rightShoulder.wasPressedThisFrame
                                 || gamepad.leftTrigger.wasPressedThisFrame
                                 || gamepad.rightTrigger.wasPressedThisFrame
                                 || gamepad.dpad.up.wasPressedThisFrame
                                 || gamepad.dpad.down.wasPressedThisFrame
                                 || gamepad.dpad.left.wasPressedThisFrame
                                 || gamepad.dpad.right.wasPressedThisFrame;

            bool axisIsActive = gamepad.leftStick.ReadValue().sqrMagnitude
                                >= StickActivationThreshold * StickActivationThreshold
                                || gamepad.rightStick.ReadValue().sqrMagnitude
                                >= StickActivationThreshold * StickActivationThreshold;
            bool axisActivatedThisFrame = axisIsActive && !gamepadAxisWasActive;
            gamepadAxisWasActive = axisIsActive;
            return buttonPressed || axisActivatedThisFrame;
        }
    }
}
