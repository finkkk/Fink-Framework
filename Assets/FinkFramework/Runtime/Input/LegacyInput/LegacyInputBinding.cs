using System;
using UnityEngine;
using UnityInput = UnityEngine.Input;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace FinkFramework.Runtime.LegacyInput
{
    /// <summary>旧版输入系统支持的物理控制类型。</summary>
    public enum LegacyInputControlType
    {
        Key,
        MouseButton
    }

    /// <summary>旧版输入绑定的触发时机。</summary>
    public enum LegacyInputTrigger
    {
        Down,
        Up,
        Held
    }

    /// <summary>旧版输入绑定 API 的统一执行结果。</summary>
    public enum LegacyInputBindingResult
    {
        Success,
        Inactive,
        InvalidAction,
        InvalidBinding,
        Conflict,
        BindingNotFound,
        DefaultAlreadyRegistered,
        RebindInProgress,
        NoRebindInProgress,
        Cancelled,
        TimedOut
    }

    /// <summary>一次旧版键盘或鼠标绑定的不可变描述。</summary>
    public readonly struct LegacyInputBinding : IEquatable<LegacyInputBinding>
    {
        /// <summary>绑定使用键盘按键还是鼠标按钮。</summary>
        public LegacyInputControlType ControlType { get; }

        /// <summary>按下、松开或持续按住时触发。</summary>
        public LegacyInputTrigger Trigger { get; }

        /// <summary>键盘绑定使用的键；鼠标绑定固定为 <see cref="KeyCode.None"/>。</summary>
        public KeyCode Key { get; }

        /// <summary>鼠标按钮编号；键盘绑定固定为 -1。</summary>
        public int MouseButton { get; }

        private LegacyInputBinding(
            LegacyInputControlType controlType,
            LegacyInputTrigger trigger,
            KeyCode key,
            int mouseButton)
        {
            ControlType = controlType;
            Trigger = trigger;
            Key = key;
            MouseButton = mouseButton;
        }

        /// <summary>创建一条键盘绑定。鼠标和手柄 KeyCode 会在有效性检查中被拒绝。</summary>
        public static LegacyInputBinding Keyboard(KeyCode key, LegacyInputTrigger trigger)
        {
            return new LegacyInputBinding(LegacyInputControlType.Key, trigger, key, -1);
        }

        /// <summary>创建一条鼠标绑定。当前稳定支持左、中、右三个按钮（编号 0～2）。</summary>
        public static LegacyInputBinding Mouse(int mouseButton, LegacyInputTrigger trigger)
        {
            return new LegacyInputBinding(LegacyInputControlType.MouseButton, trigger, KeyCode.None, mouseButton);
        }

        /// <summary>绑定的控制类型、按键和触发方式是否组成可执行的有效绑定。</summary>
        public bool IsValid => ControlType switch
        {
            LegacyInputControlType.Key => Key != KeyCode.None
                                          && !IsMouseKeyCode(Key)
                                          && !IsJoystickKeyCode(Key)
                                          && IsValidTrigger(Trigger),
            LegacyInputControlType.MouseButton => MouseButton is >= 0 and <= 2
                                                   && IsValidTrigger(Trigger),
            _ => false
        };

        /// <summary>
        /// 判断是否与另一条绑定占用同一个物理控制。
        /// 触发时机不参与冲突判断，避免同一个按键同时绑定到多个行为。
        /// </summary>
        public bool UsesSameControl(LegacyInputBinding other)
        {
            if (!IsValid || !other.IsValid || ControlType != other.ControlType)
                return false;

            return ControlType == LegacyInputControlType.Key
                ? Key == other.Key
                : MouseButton == other.MouseButton;
        }

        /// <summary>按照当前帧的 <see cref="UnityEngine.Input"/> 状态判断该绑定是否触发。</summary>
        public bool MatchesCurrentInput()
        {
            if (!IsValid)
                return false;

            if (ControlType == LegacyInputControlType.Key)
            {
                return Trigger switch
                {
                    LegacyInputTrigger.Down => UnityInput.GetKeyDown(Key),
                    LegacyInputTrigger.Up => UnityInput.GetKeyUp(Key),
                    LegacyInputTrigger.Held => UnityInput.GetKey(Key),
                    _ => false
                };
            }

            return Trigger switch
            {
                LegacyInputTrigger.Down => UnityInput.GetMouseButtonDown(MouseButton),
                LegacyInputTrigger.Up => UnityInput.GetMouseButtonUp(MouseButton),
                LegacyInputTrigger.Held => UnityInput.GetMouseButton(MouseButton),
                _ => false
            };
        }

        public bool Equals(LegacyInputBinding other)
        {
            return ControlType == other.ControlType
                   && Trigger == other.Trigger
                   && Key == other.Key
                   && MouseButton == other.MouseButton;
        }

        public override bool Equals(object obj)
        {
            return obj is LegacyInputBinding other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (int)ControlType;
                hash = hash * 31 + (int)Trigger;
                hash = hash * 31 + (int)Key;
                hash = hash * 31 + MouseButton;
                return hash;
            }
        }

        public override string ToString()
        {
            return ControlType == LegacyInputControlType.Key
                ? $"{Key} ({Trigger})"
                : $"Mouse {MouseButton} ({Trigger})";
        }

        public static bool operator ==(LegacyInputBinding left, LegacyInputBinding right) => left.Equals(right);
        public static bool operator !=(LegacyInputBinding left, LegacyInputBinding right) => !left.Equals(right);

        internal static bool IsMouseKeyCode(KeyCode key)
        {
            return key is >= KeyCode.Mouse0 and <= KeyCode.Mouse6;
        }

        internal static bool IsJoystickKeyCode(KeyCode key)
        {
            return key >= KeyCode.JoystickButton0;
        }

        private static bool IsValidTrigger(LegacyInputTrigger trigger)
        {
            return trigger is LegacyInputTrigger.Down
                or LegacyInputTrigger.Up
                or LegacyInputTrigger.Held;
        }
    }

    /// <summary>
    /// 一次交互式改键产生的结果。
    /// 冲突属于可继续输入的中间结果；成功、取消或超时属于最终结果。
    /// </summary>
    public readonly struct LegacyInputRebindResult
    {
        /// <summary>发起改键的逻辑行为枚举值。</summary>
        public Enum Action { get; }

        /// <summary>本次捕获或会话结束的状态。</summary>
        public LegacyInputBindingResult Result { get; }

        /// <summary>捕获到的候选绑定；取消和超时时为默认值。</summary>
        public LegacyInputBinding Binding { get; }

        public LegacyInputRebindResult(
            Enum action,
            LegacyInputBindingResult result,
            LegacyInputBinding binding)
        {
            Action = action;
            Result = result;
            Binding = binding;
        }
    }
}
