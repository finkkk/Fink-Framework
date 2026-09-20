using System;
using System.Collections.Generic;
using FinkFramework.Runtime.Environments;
using FinkFramework.Runtime.Input;
using FinkFramework.Runtime.Singleton;
using FinkFramework.Runtime.Settings.Loaders;
using FinkFramework.Runtime.Utils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using RebindingOperation = UnityEngine.InputSystem.InputActionRebindingExtensions.RebindingOperation;
using UnityObject = UnityEngine.Object;

namespace FinkFramework.Runtime.InputSystem
{
    /// <summary>
    /// Unity Input System 的统一运行时入口。
    /// 管理 Action Asset、Action Map、绑定覆盖、交互式改键、冲突检查和持久化。
    /// </summary>
    public sealed class NewInputManager : Singleton<NewInputManager>
    {
        private const string PlayerPrefsPrefix = "FinkFramework.InputSystem.Bindings.v1.";

        private static NewInputManager liveInstance;

        private readonly List<int> rebindBindingIndices = new();
        private readonly List<Guid> actionMapsToRestore = new();

        private InputActionAsset runtimeAsset;
        private bool ownsRuntimeAsset;
        private PlayerInput managedPlayerInput;
        private InputActionAsset playerInputOriginalActions;

        private RebindingOperation rebindOperation;
        private InputAction rebindAction;
        private NewInputRebindOptions rebindOptions;
        private Action<NewInputRebindResult> rebindCallback;
        private Guid requestedBindingId;
        private int rebindStep;
        private bool targetActionWasEnabled;
        private bool cancelRequested;
        private double rebindDeadline;
        private string rebindOverridesBackup;
        private NewInputBindingResult currentApplyResult;
        private string currentAppliedPath;
        private NewInputConflictInfo? currentConflict;
        private NewInputConflictInfo? sessionConflict;
        private static bool playerPrefsWarningIssued;

        static NewInputManager()
        {
            SingletonRuntimeReset.Register(ResetRuntimeState);
        }

        private NewInputManager()
        {
            liveInstance = this;
        }

        /// <summary>当前最终环境是否选择了新版 Input System 后端。</summary>
        public bool IsActive => EnvironmentState.FinalUseNewInputSystem;

        /// <summary>是否已经绑定了可供查询和改键的运行时 Action Asset。</summary>
        public bool IsInitialized => runtimeAsset != null;

        /// <summary>是否存在正在监听玩家输入的交互式改键会话。</summary>
        public bool IsRebinding => rebindOperation != null;

        /// <summary>
        /// 管理器当前实际操作的资产。默认是传入资产的运行时副本；未初始化时为 null。
        /// 外部只应读取，不应绕过管理器直接修改其绑定覆盖。
        /// </summary>
        public InputActionAsset RuntimeAsset => runtimeAsset;

        /// <summary>任意受管 Action 进入 Started 阶段时触发。</summary>
        public event Action<InputAction.CallbackContext> ActionStarted;

        /// <summary>任意受管 Action 进入 Performed 阶段时触发。</summary>
        public event Action<InputAction.CallbackContext> ActionPerformed;

        /// <summary>任意受管 Action 进入 Canceled 阶段时触发。</summary>
        public event Action<InputAction.CallbackContext> ActionCanceled;

        /// <summary>一条具体绑定成功改变或恢复后触发。</summary>
        public event Action<NewInputBindingInfo> BindingChanged;

        /// <summary>一批绑定覆盖成功改变后触发一次，适合统一刷新设置界面。</summary>
        public event Action BindingsChanged;

        /// <summary>交互式改键成功开始监听时触发。</summary>
        public event Action<NewInputBindingInfo> RebindStarted;

        /// <summary>交互式改键成功、取消、超时、冲突或失败后触发。</summary>
        public event Action<NewInputRebindResult> RebindFinished;

        /// <summary>
        /// 使用 <see cref="InputActionAsset"/> 初始化管理器。
        /// 默认实例化运行时副本，所有绑定覆盖都写入副本，不会污染项目中的源资产。
        /// </summary>
        /// <param name="sourceAsset">包含 Action Map、Action 和默认绑定的源资产。</param>
        /// <param name="enableAfterInitialization">初始化成功后是否启用资产内全部 Action Map。</param>
        /// <param name="cloneAsset">是否创建并由管理器持有运行时副本；一般应保持 true。</param>
        /// <returns>初始化结果。再次调用会释放上一份运行时资产；不能将当前受管副本直接转为外部资产。</returns>
        public NewInputBindingResult Initialize(
            InputActionAsset sourceAsset,
            bool enableAfterInitialization = true,
            bool cloneAsset = true)
        {
            if (!IsActive)
                return NewInputBindingResult.Inactive;
            if (sourceAsset == null)
                return NewInputBindingResult.InvalidAsset;
            if (sourceAsset == runtimeAsset && ownsRuntimeAsset && !cloneAsset)
                return NewInputBindingResult.InvalidAsset;

            InputActionAsset nextAsset;
            try
            {
                // 先克隆再释放，避免重新初始化时 sourceAsset 恰好就是当前受管副本。
                nextAsset = cloneAsset ? UnityObject.Instantiate(sourceAsset) : sourceAsset;
            }
            catch (Exception exception)
            {
                // 创建新副本失败时保留旧资产及其改键状态。
                Debug.LogException(exception);
                return NewInputBindingResult.Failed;
            }

            try
            {
                string sourceName = sourceAsset.name;
                ReleaseRuntimeAsset();
                runtimeAsset = nextAsset;
                ownsRuntimeAsset = cloneAsset;
                if (ownsRuntimeAsset)
                    runtimeAsset.name = sourceName + " (Fink Runtime)";

                SubscribeActionCallbacks();
                if (enableAfterInitialization)
                    runtimeAsset.Enable();
                return NewInputBindingResult.Success;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                ReleaseRuntimeAsset();
                return NewInputBindingResult.Failed;
            }
        }

        /// <summary>
        /// 使用 <see cref="PlayerInput"/> 当前实际使用的 Actions 初始化。
        /// 默认直接管理 PlayerInput 已经隔离好的运行时资产；只有明确需要第二份副本时才设置 cloneActions。
        /// </summary>
        public NewInputBindingResult Initialize(PlayerInput playerInput, bool cloneActions = false)
        {
            if (playerInput == null)
                return NewInputBindingResult.InvalidAsset;

            InputActionAsset sourceAsset = playerInput == managedPlayerInput
                && playerInput.actions == runtimeAsset
                ? playerInputOriginalActions
                : playerInput.actions;
            if (sourceAsset == null)
                return NewInputBindingResult.InvalidAsset;

            NewInputBindingResult result = Initialize(
                sourceAsset,
                enableAfterInitialization: false,
                cloneAsset: cloneActions);

            if (result == NewInputBindingResult.Success && cloneActions)
            {
                managedPlayerInput = playerInput;
                playerInputOriginalActions = sourceAsset;
                playerInput.actions = runtimeAsset;
            }

            return result;
        }

        /// <summary>
        /// 停止改键、解除 Action 回调，并释放由本管理器创建的运行时资产副本。
        /// 对外部传入且未克隆的资产只解除管理，不会销毁资产。
        /// </summary>
        public void Shutdown()
        {
            ReleaseRuntimeAsset();
        }

        /// <summary>启用运行时资产中的全部 Action Map；改键期间拒绝执行。</summary>
        public NewInputBindingResult EnableAll()
        {
            if (!TryValidateMutable(out NewInputBindingResult result))
                return result;

            runtimeAsset.Enable();
            return NewInputBindingResult.Success;
        }

        /// <summary>禁用运行时资产中的全部 Action Map；不会清除任何绑定覆盖。</summary>
        public NewInputBindingResult DisableAll()
        {
            if (!TryValidateMutable(out NewInputBindingResult result))
                return result;

            runtimeAsset.Disable();
            return NewInputBindingResult.Success;
        }

        /// <summary>按名称或 GUID 字符串启用、禁用一个 Action Map。</summary>
        public NewInputBindingResult SetActionMapEnabled(string mapNameOrId, bool enabled)
        {
            if (!TryValidateMutable(out NewInputBindingResult result))
                return result;
            if (string.IsNullOrWhiteSpace(mapNameOrId))
                return NewInputBindingResult.InvalidAction;

            InputActionMap map = runtimeAsset.FindActionMap(mapNameOrId, false);
            if (map == null)
                return NewInputBindingResult.InvalidAction;

            if (enabled)
                map.Enable();
            else
                map.Disable();
            return NewInputBindingResult.Success;
        }

        /// <summary>按名称、GUID 字符串或“Map/Action”路径查询运行时 Action。</summary>
        public bool TryGetAction(string actionNameOrId, out InputAction action)
        {
            action = string.IsNullOrWhiteSpace(actionNameOrId)
                ? null
                : runtimeAsset?.FindAction(actionNameOrId, false);
            return action != null;
        }

        /// <summary>按稳定 GUID 查询运行时 Action；推荐持久化和 UI 逻辑使用此重载。</summary>
        public bool TryGetAction(Guid actionId, out InputAction action)
        {
            action = actionId == Guid.Empty ? null : runtimeAsset?.FindAction(actionId);
            return action != null;
        }

        /// <summary>
        /// 按 Action 名称、GUID 字符串或“Map/Action”路径返回全部绑定快照。
        /// 找不到 Action 时返回空集合，不抛出异常。
        /// </summary>
        public IReadOnlyList<NewInputBindingInfo> GetBindings(string actionNameOrId)
        {
            return TryGetAction(actionNameOrId, out InputAction action)
                ? CreateBindingInfoList(action)
                : Array.Empty<NewInputBindingInfo>();
        }

        /// <summary>返回 Action 的所有绑定，包含组合绑定根节点和组合部件。</summary>
        public IReadOnlyList<NewInputBindingInfo> GetBindings(Guid actionId)
        {
            if (!TryGetAction(actionId, out InputAction action))
                return Array.Empty<NewInputBindingInfo>();

            return CreateBindingInfoList(action);
        }

        /// <summary>使用稳定的 Action GUID 与 Binding GUID 查询绑定快照。</summary>
        public bool TryGetBindingInfo(
            Guid actionId,
            Guid bindingId,
            out NewInputBindingInfo bindingInfo)
        {
            bindingInfo = default;
            if (!TryResolveBinding(actionId, bindingId, out InputAction action, out int bindingIndex))
                return false;

            bindingInfo = CreateBindingInfo(action, bindingIndex);
            return true;
        }

        /// <summary>返回适合直接展示给玩家的本地化控制名称；绑定不存在时返回空字符串。</summary>
        public string GetBindingDisplayString(
            Guid actionId,
            Guid bindingId,
            InputBinding.DisplayStringOptions options = default)
        {
            return TryResolveBinding(actionId, bindingId, out InputAction action, out int bindingIndex)
                ? action.GetBindingDisplayString(bindingIndex, options)
                : string.Empty;
        }

        /// <summary>
        /// 直接应用一条绑定路径覆盖。目标使用 Action/Binding GUID 定位，传入标准 Input System 控制路径，
        /// 例如 &lt;Keyboard&gt;/space。组合根节点不能直接覆盖，应改其部件或使用交互式改键。
        /// </summary>
        /// <param name="actionId">运行时资产内目标 Action 的 GUID。</param>
        /// <param name="bindingId">目标普通绑定或组合部件的 GUID。</param>
        /// <param name="controlPath">要应用的 Input System 控制路径。</param>
        /// <param name="allowConflict">为 null 时使用输入系统设置；为 false 时拒绝冲突，为 true 时允许冲突。</param>
        /// <param name="conflictScope">冲突检测限定在 Action、Action Map 或整个资产。</param>
        /// <param name="respectBindingGroups">是否把没有共同 Binding Group 的绑定视为互不冲突。</param>
        public NewInputBindingResult ApplyBindingOverride(
            Guid actionId,
            Guid bindingId,
            string controlPath,
            bool? allowConflict = null,
            NewInputConflictScope conflictScope = NewInputConflictScope.ActionMap,
            bool respectBindingGroups = true)
        {
            if (!TryValidateMutable(out NewInputBindingResult result))
                return result;
            if (string.IsNullOrWhiteSpace(controlPath))
                return NewInputBindingResult.InvalidBinding;
            controlPath = controlPath.Trim();
            if (!TryResolveBinding(actionId, bindingId, out InputAction action, out int bindingIndex))
                return NewInputBindingResult.BindingNotFound;
            if (action.bindings[bindingIndex].isComposite)
                return NewInputBindingResult.InvalidBinding;

            bool rejectConflict = !allowConflict ?? GetDefaultConflictMode() == InputConflictMode.Strict;
            bool hasConflict = TryFindConflict(
                action,
                bindingIndex,
                controlPath,
                conflictScope,
                respectBindingGroups,
                out _);
            if (rejectConflict && hasConflict)
                return NewInputBindingResult.Conflict;

            try
            {
                action.ApplyBindingOverride(bindingIndex, controlPath);
                NotifyBindingChanged(action, bindingIndex);
                return hasConflict
                    ? NewInputBindingResult.SuccessWithConflict
                    : NewInputBindingResult.Success;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return NewInputBindingResult.InvalidBinding;
            }
        }

        /// <summary>通过应用空路径覆盖来禁用一条普通绑定，同时保留资产中的默认路径以便恢复。</summary>
        public NewInputBindingResult DisableBinding(Guid actionId, Guid bindingId)
        {
            if (!TryValidateMutable(out NewInputBindingResult result))
                return result;
            if (!TryResolveBinding(actionId, bindingId, out InputAction action, out int bindingIndex))
                return NewInputBindingResult.BindingNotFound;
            if (action.bindings[bindingIndex].isComposite)
                return NewInputBindingResult.InvalidBinding;

            try
            {
                action.ApplyBindingOverride(bindingIndex, string.Empty);
                NotifyBindingChanged(action, bindingIndex);
                return NewInputBindingResult.Success;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return NewInputBindingResult.InvalidBinding;
            }
        }

        /// <summary>
        /// 移除一条绑定的运行时覆盖。指定组合根节点时，根节点及其连续组合部件会一起恢复。
        /// </summary>
        public NewInputBindingResult ResetBinding(Guid actionId, Guid bindingId)
        {
            if (!TryValidateMutable(out NewInputBindingResult result))
                return result;
            if (!TryResolveBinding(actionId, bindingId, out InputAction action, out int bindingIndex))
                return NewInputBindingResult.BindingNotFound;

            int endIndex = bindingIndex + 1;
            if (action.bindings[bindingIndex].isComposite)
            {
                while (endIndex < action.bindings.Count && action.bindings[endIndex].isPartOfComposite)
                    endIndex++;
            }

            // 先完成整组恢复，再通知监听者，避免 UI 读取到只恢复了一部分的组合绑定。
            for (int index = bindingIndex; index < endIndex; index++)
                action.RemoveBindingOverride(index);
            for (int index = bindingIndex; index < endIndex; index++)
                InvokeSafely(BindingChanged, CreateBindingInfo(action, index));

            InvokeSafely(BindingsChanged);
            return NewInputBindingResult.Success;
        }

        /// <summary>移除运行时资产中的全部绑定覆盖，不修改 PlayerPrefs 中已保存的配置档。</summary>
        public NewInputBindingResult ResetAllBindingOverrides()
        {
            if (!TryValidateMutable(out NewInputBindingResult result))
                return result;

            runtimeAsset.RemoveAllBindingOverrides();
            InvokeSafely(BindingsChanged);
            return NewInputBindingResult.Success;
        }

        /// <summary>
        /// 预检查候选控制路径是否会与现有绑定冲突，不会修改任何绑定。
        /// 找不到目标、路径无效或没有冲突时返回 false。
        /// </summary>
        public bool TryFindBindingConflict(
            Guid actionId,
            Guid bindingId,
            string candidateControlPath,
            out NewInputConflictInfo conflict,
            NewInputConflictScope scope = NewInputConflictScope.ActionMap,
            bool respectBindingGroups = true)
        {
            conflict = default;
            return TryValidateReady(out _)
                   && !string.IsNullOrWhiteSpace(candidateControlPath)
                   && TryResolveBinding(actionId, bindingId, out InputAction action, out int bindingIndex)
                   && !action.bindings[bindingIndex].isComposite
                   && TryFindConflict(
                       action,
                       bindingIndex,
                       candidateControlPath.Trim(),
                       scope,
                       respectBindingGroups,
                       out conflict);
        }

        /// <summary>使用 Action 名称和首个顶层绑定开始改键；若它是组合根节点，则依次改绑其部件。</summary>
        public NewInputBindingResult StartRebind(
            string actionNameOrId,
            Action<NewInputRebindResult> callback = null,
            NewInputRebindOptions options = null)
        {
            if (!TryValidateReady(out NewInputBindingResult result))
                return result;
            if (!TryGetAction(actionNameOrId, out InputAction action))
                return NewInputBindingResult.InvalidAction;

            int bindingIndex = FindDefaultRebindBindingIndex(action);
            if (bindingIndex < 0)
                return NewInputBindingResult.InvalidBinding;

            return StartRebind(
                action.id,
                action.bindings[bindingIndex].id,
                options,
                callback);
        }

        /// <summary>使用 Action 名称和绑定索引开始改键，适用于一个 Action 有多个绑定的情况。</summary>
        public NewInputBindingResult StartRebind(
            string actionNameOrId,
            int bindingIndex,
            Action<NewInputRebindResult> callback = null,
            NewInputRebindOptions options = null)
        {
            if (!TryValidateReady(out NewInputBindingResult result))
                return result;
            if (!TryGetAction(actionNameOrId, out InputAction action))
                return NewInputBindingResult.InvalidAction;
            if (bindingIndex < 0 || bindingIndex >= action.bindings.Count)
                return NewInputBindingResult.InvalidBinding;

            return StartRebind(
                action.id,
                action.bindings[bindingIndex].id,
                options,
                callback);
        }

        /// <summary>
        /// 开始交互式改键。传入组合绑定根节点时，会依次改绑其所有部件。
        /// 整个会话具有事务性：严格模式下的冲突、取消、超时或失败会恢复全部部件；警告模式允许冲突并继续改绑。
        /// </summary>
        /// <param name="actionId">运行时资产内目标 Action 的 GUID。</param>
        /// <param name="bindingId">普通绑定或组合根节点的 GUID。</param>
        /// <param name="options">改键选项；传 null 使用安全默认值，并在开始时复制快照。</param>
        /// <param name="callback">仅接收本次会话最终结果；允许为 null。</param>
        public NewInputBindingResult StartRebind(
            Guid actionId,
            Guid bindingId,
            NewInputRebindOptions options = null,
            Action<NewInputRebindResult> callback = null)
        {
            if (!TryValidateReady(out NewInputBindingResult readyResult))
                return readyResult;
            if (IsRebinding)
                return NewInputBindingResult.RebindInProgress;
            if (!TryResolveBinding(actionId, bindingId, out InputAction action, out int bindingIndex))
                return NewInputBindingResult.BindingNotFound;

            rebindBindingIndices.Clear();
            InputBinding targetBinding = action.bindings[bindingIndex];
            if (targetBinding.isComposite)
            {
                for (int index = bindingIndex + 1;
                     index < action.bindings.Count && action.bindings[index].isPartOfComposite;
                     index++)
                {
                    rebindBindingIndices.Add(index);
                }

                if (rebindBindingIndices.Count == 0)
                    return NewInputBindingResult.InvalidBinding;
            }
            else
            {
                rebindBindingIndices.Add(bindingIndex);
            }

            rebindAction = action;
            requestedBindingId = bindingId;
            rebindOptions = (options ?? CreateDefaultRebindOptions()).Snapshot();
            rebindCallback = callback;
            rebindStep = 0;
            cancelRequested = false;
            sessionConflict = null;
            try
            {
                rebindOverridesBackup = runtimeAsset.SaveBindingOverridesAsJson();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                ClearRebindState();
                return NewInputBindingResult.Failed;
            }

            NewInputBindingInfo targetInfo;
            try
            {
                SuspendActionsForRebind();
                targetInfo = CreateBindingInfo(action, bindingIndex);
                if (!BeginCurrentRebind())
                {
                    FinishRebind(NewInputBindingResult.Failed, string.Empty, null);
                    return NewInputBindingResult.Failed;
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                FinishRebind(NewInputBindingResult.Failed, string.Empty, null);
                return NewInputBindingResult.Failed;
            }

            InvokeSafely(RebindStarted, targetInfo);
            return NewInputBindingResult.Success;
        }

        /// <summary>主动取消当前改键会话并回滚本次会话已经应用的全部组合部件。</summary>
        public NewInputBindingResult CancelRebind()
        {
            if (!IsRebinding)
                return NewInputBindingResult.NoRebindInProgress;

            cancelRequested = true;
            try
            {
                rebindOperation.Cancel();
                return NewInputBindingResult.Success;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                FinishRebind(NewInputBindingResult.Failed, string.Empty, null);
                return NewInputBindingResult.Failed;
            }
        }

        /// <summary>
        /// 导出 Unity Input System 原生覆盖 JSON。
        /// 改键进行中返回会话开始前的稳定快照，避免导出只完成一部分的组合绑定。
        /// </summary>
        public string ExportBindingOverridesJson()
        {
            if (runtimeAsset == null)
                return string.Empty;

            try
            {
                return rebindOverridesBackup ?? runtimeAsset.SaveBindingOverridesAsJson();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return string.Empty;
            }
        }

        /// <summary>
        /// 导入 Input System 原生绑定覆盖 JSON。冲突策略未显式指定时使用输入系统设置；
        /// Warning 模式会应用并返回 SuccessWithConflict，Strict 模式才会拒绝并恢复导入前快照。
        /// </summary>
        /// <param name="json">由 Input System SaveBindingOverridesAsJson 兼容格式生成的数据。</param>
        /// <param name="rejectConflicts">显式指定是否拒绝冲突；传 null 使用输入系统设置。</param>
        public NewInputBindingResult ImportBindingOverridesJson(
            string json,
            bool? rejectConflicts = null)
        {
            if (!TryValidateMutable(out NewInputBindingResult result))
                return result;
            if (string.IsNullOrWhiteSpace(json))
                return NewInputBindingResult.InvalidData;

            string backup = null;
            try
            {
                backup = runtimeAsset.SaveBindingOverridesAsJson();
                runtimeAsset.LoadBindingOverridesFromJson(json, true);
                bool hasConflict = TryFindAnyConflict();
                bool shouldRejectConflict = rejectConflicts
                    ?? GetDefaultConflictMode() == InputConflictMode.Strict;
                if (hasConflict && shouldRejectConflict)
                {
                    runtimeAsset.LoadBindingOverridesFromJson(backup, true);
                    return NewInputBindingResult.Conflict;
                }

                InvokeSafely(BindingsChanged);
                return hasConflict
                    ? NewInputBindingResult.SuccessWithConflict
                    : NewInputBindingResult.Success;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (backup != null)
                {
                    try
                    {
                        runtimeAsset.LoadBindingOverridesFromJson(backup, true);
                    }
                    catch (Exception restoreException)
                    {
                        Debug.LogException(restoreException);
                    }
                }

                return backup == null
                    ? NewInputBindingResult.Failed
                    : NewInputBindingResult.InvalidData;
            }
        }

        /// <summary>
        /// 把当前覆盖 JSON 保存到指定 PlayerPrefs 配置档。
        /// 这是框架提供的保底机制；正式项目建议将 ExportBindingOverridesJson() 的结果写入 Global 存档。
        /// </summary>
        public NewInputBindingResult SaveBindingOverrides(string profile = "Default")
        {
            if (!TryValidateMutable(out NewInputBindingResult result))
                return result;

            WarnPlayerPrefsPersistenceFallback();

            try
            {
                PlayerPrefs.SetString(GetPlayerPrefsKey(profile), runtimeAsset.SaveBindingOverridesAsJson());
                PlayerPrefs.Save();
                return NewInputBindingResult.Success;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return NewInputBindingResult.Failed;
            }
        }

        /// <summary>
        /// 从指定 PlayerPrefs 配置档读取覆盖；数据无效或冲突时保留读取前状态。
        /// 这是框架提供的保底机制；正式项目建议从 Global 存档读取后调用 ImportBindingOverridesJson()。
        /// </summary>
        public NewInputBindingResult LoadBindingOverrides(
            string profile = "Default",
            bool? rejectConflicts = null)
        {
            if (!TryValidateMutable(out NewInputBindingResult result))
                return result;

            WarnPlayerPrefsPersistenceFallback();

            string key = GetPlayerPrefsKey(profile);
            if (!PlayerPrefs.HasKey(key))
                return NewInputBindingResult.BindingNotFound;

            return ImportBindingOverridesJson(PlayerPrefs.GetString(key), rejectConflicts);
        }

        /// <summary>
        /// 删除指定 PlayerPrefs 配置档；可选同时清除当前运行时覆盖。
        /// 只删除存档时不要求管理器已经初始化。
        /// </summary>
        public NewInputBindingResult ClearSavedBindingOverrides(
            string profile = "Default",
            bool resetRuntimeOverrides = true)
        {
            if (!IsActive)
                return NewInputBindingResult.Inactive;
            if (IsRebinding)
                return NewInputBindingResult.RebindInProgress;
            if (resetRuntimeOverrides && runtimeAsset == null)
                return NewInputBindingResult.NotInitialized;

            WarnPlayerPrefsPersistenceFallback();

            PlayerPrefs.DeleteKey(GetPlayerPrefsKey(profile));
            PlayerPrefs.Save();

            if (resetRuntimeOverrides)
            {
                runtimeAsset.RemoveAllBindingOverrides();
                InvokeSafely(BindingsChanged);
            }

            return NewInputBindingResult.Success;
        }

        private bool BeginCurrentRebind()
        {
            if (rebindAction == null
                || rebindStep < 0
                || rebindStep >= rebindBindingIndices.Count)
                return false;

            int bindingIndex = rebindBindingIndices[rebindStep];
            rebindDeadline = InputState.currentTime + rebindOptions.TimeoutSeconds;
            float remainingSeconds = rebindOptions.TimeoutSeconds;

            currentApplyResult = NewInputBindingResult.Failed;
            currentAppliedPath = string.Empty;
            currentConflict = null;

            try
            {
                RebindingOperation operation = rebindAction.PerformInteractiveRebinding(bindingIndex)
                    .WithTimeout(remainingSeconds)
                    .OnApplyBinding(HandleApplyBinding)
                    .OnCancel(HandleRebindCanceled)
                    .OnComplete(HandleRebindCompleted);

                if (!string.IsNullOrWhiteSpace(rebindOptions.CancelControlPath))
                    operation.WithCancelingThrough(rebindOptions.CancelControlPath);

                foreach (string excludedPath in rebindOptions.ExcludedControlPaths)
                {
                    if (!string.IsNullOrWhiteSpace(excludedPath))
                        operation.WithControlsExcluding(excludedPath);
                }

                rebindOperation = operation;
                operation.Start();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                DisposeRebindOperation();
                return false;
            }
        }

        private void HandleApplyBinding(RebindingOperation operation, string path)
        {
            int bindingIndex = rebindBindingIndices[rebindStep];
            currentAppliedPath = path ?? string.Empty;

            if (string.IsNullOrWhiteSpace(currentAppliedPath))
            {
                currentApplyResult = NewInputBindingResult.InvalidBinding;
                return;
            }

            if (TryFindConflict(
                    rebindAction,
                    bindingIndex,
                    currentAppliedPath,
                    rebindOptions.ConflictScope,
                    rebindOptions.RespectBindingGroups,
                    out NewInputConflictInfo conflict))
            {
                currentConflict = conflict;
                sessionConflict ??= conflict;
                if (rebindOptions.ConflictMode == InputConflictMode.Strict)
                {
                    currentApplyResult = NewInputBindingResult.Conflict;
                    return;
                }

                currentApplyResult = NewInputBindingResult.SuccessWithConflict;
            }

            try
            {
                rebindAction.ApplyBindingOverride(bindingIndex, currentAppliedPath);
                if (currentApplyResult != NewInputBindingResult.SuccessWithConflict)
                    currentApplyResult = NewInputBindingResult.Success;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                currentApplyResult = NewInputBindingResult.InvalidBinding;
            }
        }

        private void HandleRebindCompleted(RebindingOperation operation)
        {
            NewInputBindingResult result = currentApplyResult;
            string appliedPath = currentAppliedPath;
            NewInputConflictInfo? conflict = currentConflict;
            DisposeRebindOperation(operation);

            if (result != NewInputBindingResult.Success
                && result != NewInputBindingResult.SuccessWithConflict)
            {
                FinishRebind(result, appliedPath, conflict);
                return;
            }

            rebindStep++;
            if (rebindStep < rebindBindingIndices.Count)
            {
                if (!BeginCurrentRebind())
                    FinishRebind(NewInputBindingResult.Failed, appliedPath, null);
                return;
            }

            FinishRebind(
                sessionConflict.HasValue
                    ? NewInputBindingResult.SuccessWithConflict
                    : NewInputBindingResult.Success,
                appliedPath,
                sessionConflict);
        }

        private void HandleRebindCanceled(RebindingOperation operation)
        {
            bool timedOut = !cancelRequested && InputState.currentTime >= rebindDeadline - 0.01d;
            DisposeRebindOperation(operation);
            FinishRebind(
                timedOut ? NewInputBindingResult.TimedOut : NewInputBindingResult.Cancelled,
                string.Empty,
                null);
        }

        private void FinishRebind(
            NewInputBindingResult result,
            string appliedPath,
            NewInputConflictInfo? conflict)
        {
            Guid actionId = rebindAction?.id ?? Guid.Empty;
            Guid bindingId = requestedBindingId;
            Action<NewInputRebindResult> callback = rebindCallback;
            List<NewInputBindingInfo> changedBindings = null;

            if ((result == NewInputBindingResult.Success
                 || result == NewInputBindingResult.SuccessWithConflict)
                && rebindAction != null)
            {
                changedBindings = new List<NewInputBindingInfo>(rebindBindingIndices.Count);
                foreach (int bindingIndex in rebindBindingIndices)
                    changedBindings.Add(CreateBindingInfo(rebindAction, bindingIndex));
            }
            else
                RestoreRebindBackup();

            DisposeRebindOperation();
            RestoreActionsAfterRebind();
            ClearRebindState();

            var rebindResult = new NewInputRebindResult(
                result,
                actionId,
                bindingId,
                appliedPath,
                conflict);

            if (changedBindings != null)
            {
                foreach (NewInputBindingInfo bindingInfo in changedBindings)
                    InvokeSafely(BindingChanged, bindingInfo);
                InvokeSafely(BindingsChanged);
            }

            InvokeSafely(RebindFinished, rebindResult);
            InvokeSafely(callback, rebindResult);
        }

        private void SuspendActionsForRebind()
        {
            actionMapsToRestore.Clear();
            targetActionWasEnabled = false;

            if (rebindOptions.DisableAllActionMaps)
            {
                foreach (InputActionMap map in runtimeAsset.actionMaps)
                {
                    if (map.enabled)
                        actionMapsToRestore.Add(map.id);
                }

                runtimeAsset.Disable();
                return;
            }

            targetActionWasEnabled = rebindAction.enabled;
            rebindAction.Disable();
        }

        private void RestoreActionsAfterRebind()
        {
            if (runtimeAsset == null || rebindOptions == null)
                return;

            if (rebindOptions.DisableAllActionMaps)
            {
                foreach (Guid mapId in actionMapsToRestore)
                    runtimeAsset.FindActionMap(mapId)?.Enable();
            }
            else if (targetActionWasEnabled && rebindAction != null)
            {
                rebindAction.Enable();
            }

            actionMapsToRestore.Clear();
            targetActionWasEnabled = false;
        }

        private bool TryFindConflict(
            InputAction targetAction,
            int targetBindingIndex,
            string candidatePath,
            NewInputConflictScope scope,
            bool respectBindingGroups,
            out NewInputConflictInfo conflict)
        {
            conflict = default;
            if (targetAction == null
                || targetBindingIndex < 0
                || targetBindingIndex >= targetAction.bindings.Count
                || string.IsNullOrWhiteSpace(candidatePath))
                return false;

            if (!Enum.IsDefined(typeof(NewInputConflictScope), scope))
                scope = NewInputConflictScope.ActionMap;

            InputBinding targetBinding = targetAction.bindings[targetBindingIndex];
            if (scope == NewInputConflictScope.Action)
                return TryFindConflictInAction(
                    targetAction,
                    targetAction,
                    targetBindingIndex,
                    targetBinding,
                    candidatePath,
                    respectBindingGroups,
                    out conflict);

            if (scope == NewInputConflictScope.ActionMap)
            {
                InputActionMap map = targetAction.actionMap;
                if (map == null)
                    return false;

                foreach (InputAction action in map.actions)
                {
                    if (TryFindConflictInAction(
                            action,
                            targetAction,
                            targetBindingIndex,
                            targetBinding,
                            candidatePath,
                            respectBindingGroups,
                            out conflict))
                        return true;
                }

                return false;
            }

            foreach (InputActionMap map in runtimeAsset.actionMaps)
            {
                foreach (InputAction action in map.actions)
                {
                    if (TryFindConflictInAction(
                            action,
                            targetAction,
                            targetBindingIndex,
                            targetBinding,
                            candidatePath,
                            respectBindingGroups,
                            out conflict))
                        return true;
                }
            }

            return false;
        }

        private static bool TryFindConflictInAction(
            InputAction actionToInspect,
            InputAction targetAction,
            int targetBindingIndex,
            InputBinding targetBinding,
            string candidatePath,
            bool respectBindingGroups,
            out NewInputConflictInfo conflict)
        {
            conflict = default;
            for (int index = 0; index < actionToInspect.bindings.Count; index++)
            {
                if (actionToInspect == targetAction && index == targetBindingIndex)
                    continue;

                InputBinding existingBinding = actionToInspect.bindings[index];
                if (existingBinding.isComposite || string.IsNullOrEmpty(existingBinding.effectivePath))
                    continue;
                if (!string.Equals(
                        existingBinding.effectivePath,
                        candidatePath,
                        StringComparison.OrdinalIgnoreCase))
                    continue;
                if (respectBindingGroups
                    && !BindingGroupsOverlap(targetBinding.groups, existingBinding.groups))
                    continue;

                conflict = new NewInputConflictInfo(actionToInspect, existingBinding);
                return true;
            }

            return false;
        }

        private bool TryFindAnyConflict()
        {
            foreach (InputActionMap map in runtimeAsset.actionMaps)
            {
                foreach (InputAction action in map.actions)
                {
                    for (int index = 0; index < action.bindings.Count; index++)
                    {
                        InputBinding binding = action.bindings[index];
                        if (binding.overridePath == null
                            || binding.isComposite
                            || string.IsNullOrEmpty(binding.effectivePath))
                            continue;

                        if (TryFindConflict(
                                action,
                                index,
                                binding.effectivePath,
                                NewInputConflictScope.Asset,
                                true,
                                out _))
                            return true;
                    }
                }
            }

            return false;
        }

        private static bool BindingGroupsOverlap(string first, string second)
        {
            if (string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(second))
                return true;

            string[] firstGroups = first.Split(InputBinding.Separator);
            string[] secondGroups = second.Split(InputBinding.Separator);
            foreach (string firstGroup in firstGroups)
            {
                foreach (string secondGroup in secondGroups)
                {
                    if (string.Equals(
                            firstGroup.Trim(),
                            secondGroup.Trim(),
                            StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            return false;
        }

        private bool TryResolveBinding(
            Guid actionId,
            Guid bindingId,
            out InputAction action,
            out int bindingIndex)
        {
            bindingIndex = -1;
            if (!TryGetAction(actionId, out action) || bindingId == Guid.Empty)
                return false;

            for (int index = 0; index < action.bindings.Count; index++)
            {
                if (action.bindings[index].id != bindingId)
                    continue;

                bindingIndex = index;
                return true;
            }

            return false;
        }

        private NewInputBindingInfo CreateBindingInfo(InputAction action, int bindingIndex)
        {
            InputBinding binding = action.bindings[bindingIndex];
            string displayString = action.GetBindingDisplayString(
                bindingIndex,
                out string deviceLayoutName,
                out string controlPath);

            NewInputConflictInfo? conflict = null;
            if (!binding.isComposite
                && !string.IsNullOrWhiteSpace(binding.effectivePath)
                && TryFindConflict(
                    action,
                    bindingIndex,
                    binding.effectivePath,
                    NewInputConflictScope.ActionMap,
                    true,
                    out NewInputConflictInfo foundConflict))
            {
                conflict = foundConflict;
            }

            return new NewInputBindingInfo(
                action,
                binding,
                bindingIndex,
                displayString,
                deviceLayoutName,
                controlPath,
                conflict);
        }

        private IReadOnlyList<NewInputBindingInfo> CreateBindingInfoList(InputAction action)
        {
            var result = new List<NewInputBindingInfo>(action.bindings.Count);
            for (int index = 0; index < action.bindings.Count; index++)
                result.Add(CreateBindingInfo(action, index));
            return result;
        }

        private void NotifyBindingChanged(InputAction action, int bindingIndex)
        {
            InvokeSafely(BindingChanged, CreateBindingInfo(action, bindingIndex));
            InvokeSafely(BindingsChanged);
        }

        private static NewInputRebindOptions CreateDefaultRebindOptions()
        {
            var options = new NewInputRebindOptions();
            if (GlobalSettingsRuntimeLoader.TryGet(out var settings))
                options.ConflictMode = settings.InputConflictMode;
            return options;
        }

        private static InputConflictMode GetDefaultConflictMode()
        {
            return GlobalSettingsRuntimeLoader.TryGet(out var settings)
                ? settings.InputConflictMode
                : InputConflictMode.Warning;
        }

        private static int FindDefaultRebindBindingIndex(InputAction action)
        {
            if (action == null)
                return -1;

            for (int index = 0; index < action.bindings.Count; index++)
            {
                if (!action.bindings[index].isPartOfComposite)
                    return index;
            }

            return -1;
        }

        private bool TryValidateReady(out NewInputBindingResult result)
        {
            if (!IsActive)
            {
                result = NewInputBindingResult.Inactive;
                return false;
            }

            if (runtimeAsset == null)
            {
                result = NewInputBindingResult.NotInitialized;
                return false;
            }

            result = NewInputBindingResult.Success;
            return true;
        }

        private bool TryValidateMutable(out NewInputBindingResult result)
        {
            if (!TryValidateReady(out result))
                return false;

            if (IsRebinding)
            {
                result = NewInputBindingResult.RebindInProgress;
                return false;
            }

            return true;
        }

        private void SubscribeActionCallbacks()
        {
            if (runtimeAsset == null)
                return;

            foreach (InputActionMap map in runtimeAsset.actionMaps)
            {
                foreach (InputAction action in map.actions)
                {
                    action.started += ForwardActionStarted;
                    action.performed += ForwardActionPerformed;
                    action.canceled += ForwardActionCanceled;
                }
            }
        }

        private void UnsubscribeActionCallbacks()
        {
            if (runtimeAsset == null)
                return;

            foreach (InputActionMap map in runtimeAsset.actionMaps)
            {
                foreach (InputAction action in map.actions)
                {
                    action.started -= ForwardActionStarted;
                    action.performed -= ForwardActionPerformed;
                    action.canceled -= ForwardActionCanceled;
                }
            }
        }

        private void ForwardActionStarted(InputAction.CallbackContext context) => InvokeSafely(ActionStarted, context);
        private void ForwardActionPerformed(InputAction.CallbackContext context) => InvokeSafely(ActionPerformed, context);
        private void ForwardActionCanceled(InputAction.CallbackContext context) => InvokeSafely(ActionCanceled, context);

        private void ReleaseRuntimeAsset()
        {
            AbortRebindSilently();
            UnsubscribeActionCallbacks();

            // PlayerInput 不会替管理器释放外部赋予的 Actions，先解除其对受管副本的引用。
            if (managedPlayerInput != null && managedPlayerInput.actions == runtimeAsset)
                managedPlayerInput.actions = playerInputOriginalActions;
            managedPlayerInput = null;
            playerInputOriginalActions = null;

            if (runtimeAsset != null && ownsRuntimeAsset)
            {
                runtimeAsset.Disable();
                if (Application.isPlaying)
                    UnityObject.Destroy(runtimeAsset);
                else
                    UnityObject.DestroyImmediate(runtimeAsset);
            }

            runtimeAsset = null;
            ownsRuntimeAsset = false;
        }

        private void AbortRebindSilently()
        {
            DisposeRebindOperation();
            RestoreRebindBackup();
            RestoreActionsAfterRebind();
            ClearRebindState();
        }

        private void RestoreRebindBackup()
        {
            if (runtimeAsset == null || rebindOverridesBackup == null)
                return;

            try
            {
                // 整个改键会话是一个事务。组合绑定改到一半时取消、超时、冲突、
                // 重新初始化或关闭管理器，都恢复会话开始前的完整覆盖快照。
                runtimeAsset.LoadBindingOverridesFromJson(rebindOverridesBackup, true);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private void ClearRebindState()
        {
            rebindAction = null;
            rebindOptions = null;
            rebindCallback = null;
            requestedBindingId = Guid.Empty;
            rebindBindingIndices.Clear();
            actionMapsToRestore.Clear();
            rebindStep = 0;
            targetActionWasEnabled = false;
            cancelRequested = false;
            rebindDeadline = 0d;
            rebindOverridesBackup = null;
            currentApplyResult = NewInputBindingResult.Failed;
            currentConflict = null;
            sessionConflict = null;
            currentAppliedPath = string.Empty;
        }

        private static void InvokeSafely(Action callback)
        {
            if (callback == null)
                return;

            foreach (Delegate subscriber in callback.GetInvocationList())
            {
                try
                {
                    if (subscriber is Action action)
                        action();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        private static void InvokeSafely<T>(Action<T> callback, T value)
        {
            if (callback == null)
                return;

            foreach (Delegate subscriber in callback.GetInvocationList())
            {
                try
                {
                    if (subscriber is Action<T> action)
                        action(value);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        private void DisposeRebindOperation(RebindingOperation operation = null)
        {
            RebindingOperation operationToDispose = operation ?? rebindOperation;
            if (operationToDispose == null)
                return;

            if (ReferenceEquals(rebindOperation, operationToDispose))
                rebindOperation = null;
            operationToDispose.Dispose();
        }

        private static string GetPlayerPrefsKey(string profile)
        {
            string normalizedProfile = string.IsNullOrWhiteSpace(profile)
                ? "Default"
                : profile.Trim();
            return PlayerPrefsPrefix + normalizedProfile;
        }

        private static void WarnPlayerPrefsPersistenceFallback()
        {
            if (playerPrefsWarningIssued)
                return;

            playerPrefsWarningIssued = true;
            LogUtil.Warn(
                "NewInputManager",
                "当前输入绑定使用 PlayerPrefs 作为保底持久化方式；正式项目建议将 ExportBindingOverridesJson() 的结果写入 SaveManager 的 Global 存档，并在启动时通过 ImportBindingOverridesJson() 恢复。");
        }

        private static void ResetRuntimeState()
        {
            liveInstance?.Shutdown();
            liveInstance = null;
        }
    }
}
