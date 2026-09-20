using System;
using System.Collections.Generic;
using System.Linq;
using FinkFramework.Runtime.Environments;
using FinkFramework.Runtime.Event;
using FinkFramework.Runtime.Mono;
using FinkFramework.Runtime.Singleton;
using FinkFramework.Runtime.Utils;
using UnityEngine;
using UnityInput = UnityEngine.Input;

namespace FinkFramework.Runtime.LegacyInput
{
    /// <summary>
    /// 旧版输入系统管理器。
    /// 只负责 UnityEngine.Input 的键盘、鼠标绑定，不与新版 Input System 混用。
    /// </summary>
    public sealed class LegacyInputManager : Singleton<LegacyInputManager>
    {
        private static readonly KeyCode[] RebindableKeys = CreateRebindableKeys();
        private static LegacyInputManager liveInstance;
        private static bool playerPrefsWarningIssued;

        private readonly Dictionary<Enum, LegacyInputBinding> bindings = new();
        private readonly Dictionary<Enum, LegacyInputBinding> defaultBindings = new();

        private bool inputCheckEnabled;
        private bool updateRegistered;

        private Enum rebindingAction;
        private Action<LegacyInputRebindResult> rebindingCallback;
        private float rebindingDeadline;
        private int rebindingStartFrame;
        private bool allowRebindingConflict;
        private KeyCode rebindingCancelKey;

        static LegacyInputManager()
        {
            SingletonRuntimeReset.Register(ResetRuntimeState);
        }

        private LegacyInputManager()
        {
            liveInstance = this;
            MonoManager.Instance.AddUpdateListener(InputUpdate);
            updateRegistered = true;
        }

        /// <summary>
        /// 当前最终环境是否选择了旧版输入后端。
        /// 此值不做构造时缓存，可正确响应 GlobalSettings 在管理器之后完成加载的情况。
        /// </summary>
        public bool IsActive => !EnvironmentState.FinalUseNewInputSystem;

        /// <summary>是否正在触发已注册的输入事件。</summary>
        public bool IsInputCheckEnabled => inputCheckEnabled;

        /// <summary>是否存在正在进行的交互式改键。</summary>
        public bool IsRebinding => rebindingAction != null;

        /// <summary>当前正在改键的行为。</summary>
        public Enum RebindingAction => rebindingAction;

        /// <summary>
        /// 开启或关闭正常的绑定轮询。交互式改键不受此开关影响，避免关闭游戏输入后无法设置按键。
        /// </summary>
        public void ToggleInputCheck(bool enabled)
        {
            inputCheckEnabled = enabled;
        }

        /// <summary>
        /// 注册行为的默认键盘绑定，并在该行为尚无当前绑定时立即启用它。
        /// 同一行为只能注册一次默认值；默认值用于恢复和判断是否需要持久化覆盖。
        /// </summary>
        public LegacyInputBindingResult RegisterDefaultKeyboard(
            Enum action,
            KeyCode key,
            LegacyInputTrigger trigger,
            bool allowConflict = false)
        {
            return RegisterDefault(action, LegacyInputBinding.Keyboard(key, trigger), allowConflict);
        }

        /// <summary>注册行为的默认鼠标绑定；规则与 <see cref="RegisterDefaultKeyboard"/> 相同。</summary>
        public LegacyInputBindingResult RegisterDefaultMouse(
            Enum action,
            int mouseButton,
            LegacyInputTrigger trigger,
            bool allowConflict = false)
        {
            return RegisterDefault(action, LegacyInputBinding.Mouse(mouseButton, trigger), allowConflict);
        }

        /// <summary>修改当前键盘绑定，不改变默认值；改键会话进行中会拒绝外部修改。</summary>
        public LegacyInputBindingResult SetKeyboardBinding(
            Enum action,
            KeyCode key,
            LegacyInputTrigger trigger,
            bool allowConflict = false)
        {
            return SetBinding(action, LegacyInputBinding.Keyboard(key, trigger), allowConflict);
        }

        /// <summary>修改当前鼠标绑定，不改变默认值；改键会话进行中会拒绝外部修改。</summary>
        public LegacyInputBindingResult SetMouseBinding(
            Enum action,
            int mouseButton,
            LegacyInputTrigger trigger,
            bool allowConflict = false)
        {
            return SetBinding(action, LegacyInputBinding.Mouse(mouseButton, trigger), allowConflict);
        }

        /// <summary>
        /// 修改当前绑定而不改变默认值。行为必须已经注册；默认按当前生效绑定检查物理控制冲突。
        /// </summary>
        public LegacyInputBindingResult SetBinding(
            Enum action,
            LegacyInputBinding binding,
            bool allowConflict = false)
        {
            if (!CanModify(action, binding, out LegacyInputBindingResult failure))
                return failure;
            if (IsRebinding)
                return LegacyInputBindingResult.RebindInProgress;

            return SetBindingCore(action, binding, allowConflict);
        }

        /// <summary>移除当前绑定，但保留默认绑定，便于稍后恢复。</summary>
        public LegacyInputBindingResult RemoveBinding(Enum action)
        {
            if (!IsActive)
                return LegacyInputBindingResult.Inactive;
            if (IsRebinding)
                return LegacyInputBindingResult.RebindInProgress;
            if (action == null)
                return LegacyInputBindingResult.InvalidAction;
            if (!bindings.Remove(action))
                return LegacyInputBindingResult.BindingNotFound;

            return LegacyInputBindingResult.Success;
        }

        /// <summary>将一个行为恢复为已注册的默认绑定，并按当前其他绑定执行冲突检查。</summary>
        public LegacyInputBindingResult ResetBinding(Enum action, bool allowConflict = false)
        {
            if (!IsActive)
                return LegacyInputBindingResult.Inactive;
            if (IsRebinding)
                return LegacyInputBindingResult.RebindInProgress;
            if (action == null)
                return LegacyInputBindingResult.InvalidAction;
            if (!defaultBindings.TryGetValue(action, out LegacyInputBinding binding))
                return LegacyInputBindingResult.BindingNotFound;
            if (!allowConflict && HasConflict(action, binding))
                return LegacyInputBindingResult.Conflict;

            bindings[action] = binding;
            return LegacyInputBindingResult.Success;
        }

        /// <summary>
        /// 以全有或全无的方式恢复所有默认绑定。默认配置自身存在冲突且未允许冲突时不会修改任何绑定。
        /// </summary>
        public LegacyInputBindingResult ResetAllBindings(bool allowConflict = false)
        {
            if (!IsActive)
                return LegacyInputBindingResult.Inactive;
            if (IsRebinding)
                return LegacyInputBindingResult.RebindInProgress;

            foreach ((Enum action, LegacyInputBinding binding) in defaultBindings)
            {
                if (!allowConflict && HasConflict(action, binding, defaultBindings))
                    return LegacyInputBindingResult.Conflict;
            }

            foreach ((Enum action, LegacyInputBinding binding) in defaultBindings)
                bindings[action] = binding;

            return LegacyInputBindingResult.Success;
        }

        /// <summary>查询当前生效绑定；行为不存在或当前已解除绑定时返回 false。</summary>
        public bool TryGetBinding(Enum action, out LegacyInputBinding binding)
        {
            binding = default;
            return action != null && bindings.TryGetValue(action, out binding);
        }

        /// <summary>查询注册时保存的默认绑定；默认值不会被普通改键覆盖。</summary>
        public bool TryGetDefaultBinding(Enum action, out LegacyInputBinding binding)
        {
            binding = default;
            return action != null && defaultBindings.TryGetValue(action, out binding);
        }

        /// <summary>判断当前绑定是否仍然等于默认绑定。</summary>
        public bool IsUsingDefaultBinding(Enum action)
        {
            return TryGetBinding(action, out LegacyInputBinding binding)
                   && TryGetDefaultBinding(action, out LegacyInputBinding defaultBinding)
                   && binding == defaultBinding;
        }

        /// <summary>
        /// 判断候选绑定是否与另一个当前绑定占用同一物理控制。
        /// 触发时机不参与冲突判断。
        /// </summary>
        public bool HasConflict(Enum action, LegacyInputBinding binding)
        {
            return HasConflict(action, binding, bindings);
        }

        /// <summary>
        /// 开始一次交互式改键。新按键会沿用该行为当前绑定的触发时机。
        /// 冲突结果会通过回调通知但不会结束会话，用户可以继续输入其他按键；
        /// 成功、取消或超时后会话才结束。
        /// </summary>
        /// <param name="action">已注册默认绑定或当前绑定的行为枚举值。</param>
        /// <param name="callback">接收冲突中间结果以及最终结果；允许为 null。</param>
        /// <param name="timeoutSeconds">最长等待秒数，使用不受 Time.timeScale 影响的时间。</param>
        /// <param name="allowConflict">是否允许与其他行为使用同一个物理控制。</param>
        /// <param name="cancelKey">取消键；传入 <see cref="KeyCode.None"/> 可允许绑定所有键。</param>
        public LegacyInputBindingResult StartRebind(
            Enum action,
            Action<LegacyInputRebindResult> callback,
            float timeoutSeconds = 10f,
            bool allowConflict = false,
            KeyCode cancelKey = KeyCode.Escape)
        {
            if (!IsActive)
                return LegacyInputBindingResult.Inactive;
            if (action == null || (!bindings.ContainsKey(action) && !defaultBindings.ContainsKey(action)))
                return LegacyInputBindingResult.InvalidAction;
            if (IsRebinding)
                return LegacyInputBindingResult.RebindInProgress;

            rebindingAction = action;
            rebindingCallback = callback;
            if (float.IsNaN(timeoutSeconds) || float.IsInfinity(timeoutSeconds) || timeoutSeconds <= 0f)
                timeoutSeconds = 10f;

            rebindingDeadline = Time.unscaledTime + Mathf.Max(0.1f, timeoutSeconds);
            rebindingStartFrame = Time.frameCount;
            allowRebindingConflict = allowConflict;
            rebindingCancelKey = cancelKey;
            return LegacyInputBindingResult.Success;
        }

        /// <summary>取消当前交互式改键。</summary>
        public LegacyInputBindingResult CancelRebind()
        {
            if (!IsRebinding)
                return LegacyInputBindingResult.NoRebindInProgress;

            FinishRebind(LegacyInputBindingResult.Cancelled, default);
            return LegacyInputBindingResult.Success;
        }

        /// <summary>
        /// 将当前绑定覆盖保存到 PlayerPrefs。默认绑定不会写入，恢复默认后对应覆盖会被删除。
        /// 这是没有接入项目 Global 存档时的保底机制；正式项目建议自行将输入配置写入 Global 数据。
        /// </summary>
        public bool SaveBindings()
        {
            if (!IsActive)
                return false;

            WarnPlayerPrefsPersistenceFallback();

            foreach (Enum action in defaultBindings.Keys)
            {
                string key = GetPersistenceKey(action);
                if (!bindings.TryGetValue(action, out LegacyInputBinding binding)
                    || !binding.IsValid
                    || IsUsingDefaultBinding(action))
                {
                    DeletePersistenceKeys(key);
                    continue;
                }

                PlayerPrefs.SetInt(key + ".control", (int)binding.ControlType);
                PlayerPrefs.SetInt(key + ".trigger", (int)binding.Trigger);
                PlayerPrefs.SetInt(key + ".key", (int)binding.Key);
                PlayerPrefs.SetInt(key + ".mouse", binding.MouseButton);
            }

            PlayerPrefs.Save();
            return true;
        }

        /// <summary>
        /// 读取已经注册过默认值的行为的绑定覆盖。应在注册完默认绑定后调用。
        /// 这是没有接入项目 Global 存档时的保底机制；正式项目建议从 Global 数据恢复输入配置。
        /// </summary>
        public int LoadBindings(bool allowConflict = false)
        {
            if (!IsActive || IsRebinding)
                return 0;

            WarnPlayerPrefsPersistenceFallback();

            int loadedCount = 0;
            foreach (Enum action in defaultBindings.Keys.ToArray())
            {
                string key = GetPersistenceKey(action);
                if (!PlayerPrefs.HasKey(key + ".control"))
                    continue;

                LegacyInputControlType controlType =
                    (LegacyInputControlType)PlayerPrefs.GetInt(key + ".control", -1);
                LegacyInputTrigger trigger =
                    (LegacyInputTrigger)PlayerPrefs.GetInt(key + ".trigger", -1);
                if (controlType is not LegacyInputControlType.Key
                    and not LegacyInputControlType.MouseButton)
                    continue;

                LegacyInputBinding binding = controlType == LegacyInputControlType.Key
                    ? LegacyInputBinding.Keyboard(
                        (KeyCode)PlayerPrefs.GetInt(key + ".key", (int)KeyCode.None),
                        trigger)
                    : LegacyInputBinding.Mouse(
                        PlayerPrefs.GetInt(key + ".mouse", -1),
                        trigger);

                if (SetBinding(action, binding, allowConflict) == LegacyInputBindingResult.Success)
                    loadedCount++;
            }

            return loadedCount;
        }

        /// <summary>清除所有已经注册行为的持久化绑定覆盖。</summary>
        public bool ClearSavedBindings()
        {
            if (!IsActive)
                return false;

            WarnPlayerPrefsPersistenceFallback();

            foreach (Enum action in defaultBindings.Keys)
                DeletePersistenceKeys(GetPersistenceKey(action));

            PlayerPrefs.Save();
            return true;
        }

        private LegacyInputBindingResult RegisterDefault(
            Enum action,
            LegacyInputBinding binding,
            bool allowConflict)
        {
            if (!CanModify(action, binding, out LegacyInputBindingResult failure))
                return failure;
            if (IsRebinding)
                return LegacyInputBindingResult.RebindInProgress;
            if (defaultBindings.ContainsKey(action))
                return LegacyInputBindingResult.DefaultAlreadyRegistered;
            if (!allowConflict && HasConflict(action, binding))
                return LegacyInputBindingResult.Conflict;

            defaultBindings.Add(action, binding);
            bindings.TryAdd(action, binding);
            return LegacyInputBindingResult.Success;
        }

        private bool CanModify(
            Enum action,
            LegacyInputBinding binding,
            out LegacyInputBindingResult failure)
        {
            if (!IsActive)
            {
                failure = LegacyInputBindingResult.Inactive;
                return false;
            }

            if (action == null)
            {
                failure = LegacyInputBindingResult.InvalidAction;
                return false;
            }

            if (!binding.IsValid)
            {
                failure = LegacyInputBindingResult.InvalidBinding;
                return false;
            }

            failure = LegacyInputBindingResult.Success;
            return true;
        }

        private bool HasConflict(
            Enum action,
            LegacyInputBinding binding,
            IReadOnlyDictionary<Enum, LegacyInputBinding> source)
        {
            return source.Any(pair => !Equals(pair.Key, action) && pair.Value.UsesSameControl(binding));
        }

        private LegacyInputBindingResult SetBindingCore(
            Enum action,
            LegacyInputBinding binding,
            bool allowConflict)
        {
            if (!bindings.ContainsKey(action) && !defaultBindings.ContainsKey(action))
                return LegacyInputBindingResult.InvalidAction;

            // 冲突必须针对当前生效绑定检查，而不是默认绑定表。
            if (!allowConflict && HasConflict(action, binding, bindings))
                return LegacyInputBindingResult.Conflict;

            bindings[action] = binding;
            return LegacyInputBindingResult.Success;
        }

        private void InputUpdate()
        {
            if (!IsActive)
            {
                if (IsRebinding)
                    FinishRebind(LegacyInputBindingResult.Inactive, default);
                return;
            }

            if (IsRebinding)
            {
                ProcessRebind();
                return;
            }

            if (!inputCheckEnabled)
                return;

            foreach ((Enum action, LegacyInputBinding binding) in bindings.ToArray())
            {
                if (binding.MatchesCurrentInput())
                    EventManager.Instance.EventTrigger(action);
            }
        }

        private void ProcessRebind()
        {
            if (Time.unscaledTime >= rebindingDeadline)
            {
                FinishRebind(LegacyInputBindingResult.TimedOut, default);
                return;
            }

            // StartRebind 通常由当前帧的 UI 事件调用，跳过当前帧避免把启动改键的按键立即绑定进去。
            if (Time.frameCount <= rebindingStartFrame)
                return;

            if (rebindingCancelKey != KeyCode.None && UnityInput.GetKeyDown(rebindingCancelKey))
            {
                FinishRebind(LegacyInputBindingResult.Cancelled, default);
                return;
            }

            LegacyInputTrigger trigger = bindings.TryGetValue(rebindingAction, out LegacyInputBinding current)
                ? current.Trigger
                : LegacyInputTrigger.Down;
            if (!TryCaptureBinding(trigger, out LegacyInputBinding capturedBinding))
                return;

            LegacyInputBindingResult result = SetBindingCore(
                rebindingAction,
                capturedBinding,
                allowRebindingConflict);

            if (result == LegacyInputBindingResult.Conflict)
            {
                InvokeSafely(rebindingCallback, new LegacyInputRebindResult(
                    rebindingAction,
                    result,
                    capturedBinding));
                return;
            }

            FinishRebind(result, capturedBinding);
        }

        private static bool TryCaptureBinding(
            LegacyInputTrigger trigger,
            out LegacyInputBinding binding)
        {
            foreach (KeyCode key in RebindableKeys)
            {
                if (UnityInput.GetKeyDown(key))
                {
                    binding = LegacyInputBinding.Keyboard(key, trigger);
                    return true;
                }
            }

            for (int mouseButton = 0; mouseButton <= 2; mouseButton++)
            {
                if (UnityInput.GetMouseButtonDown(mouseButton))
                {
                    binding = LegacyInputBinding.Mouse(mouseButton, trigger);
                    return true;
                }
            }

            binding = default;
            return false;
        }

        private void FinishRebind(LegacyInputBindingResult result, LegacyInputBinding binding)
        {
            Enum action = rebindingAction;
            Action<LegacyInputRebindResult> callback = rebindingCallback;

            rebindingAction = null;
            rebindingCallback = null;
            rebindingDeadline = 0f;
            rebindingStartFrame = 0;
            allowRebindingConflict = false;
            rebindingCancelKey = KeyCode.None;

            InvokeSafely(callback, new LegacyInputRebindResult(action, result, binding));
        }

        private static KeyCode[] CreateRebindableKeys()
        {
            return Enum.GetValues(typeof(KeyCode))
                .Cast<KeyCode>()
                .Where(key => key != KeyCode.None
                              && !LegacyInputBinding.IsMouseKeyCode(key)
                              && !LegacyInputBinding.IsJoystickKeyCode(key))
                .ToArray();
        }

        private static void InvokeSafely(
            Action<LegacyInputRebindResult> callback,
            LegacyInputRebindResult result)
        {
            if (callback == null)
                return;

            foreach (var @delegate in callback.GetInvocationList())
            {
                var subscriber = (Action<LegacyInputRebindResult>)@delegate;
                try
                {
                    subscriber(result);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        private void UnregisterUpdate()
        {
            if (!updateRegistered)
                return;

            MonoManager manager = MonoManager.TryGetInstance();
            manager?.RemoveUpdateListener(InputUpdate);
            updateRegistered = false;
        }

        private static void ResetRuntimeState()
        {
            liveInstance?.UnregisterUpdate();
            liveInstance = null;
        }

        private static string GetPersistenceKey(Enum action)
        {
            return "FinkFramework.LegacyInput.Binding.v1."
                   + action.GetType().FullName
                   + "."
                   + action;
        }

        private static void DeletePersistenceKeys(string key)
        {
            PlayerPrefs.DeleteKey(key + ".control");
            PlayerPrefs.DeleteKey(key + ".trigger");
            PlayerPrefs.DeleteKey(key + ".key");
            PlayerPrefs.DeleteKey(key + ".mouse");
        }

        private static void WarnPlayerPrefsPersistenceFallback()
        {
            if (playerPrefsWarningIssued)
                return;

            playerPrefsWarningIssued = true;
            LogUtil.Warn(
                "LegacyInputManager",
                "当前输入绑定使用 PlayerPrefs 作为保底持久化方式；正式项目建议将输入配置写入 SaveManager 的 Global 存档。");
        }
    }
}
