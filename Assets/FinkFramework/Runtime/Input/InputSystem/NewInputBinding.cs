using System;
using FinkFramework.Runtime.Input;
using UnityEngine.InputSystem;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace FinkFramework.Runtime.InputSystem
{
    /// <summary>新版 Input System 管理与绑定 API 的统一执行结果。</summary>
    public enum NewInputBindingResult
    {
        Success,
        /// <summary>绑定已应用，但与现有绑定重复，仅作为警告返回。</summary>
        SuccessWithConflict,
        Inactive,
        NotInitialized,
        InvalidAsset,
        InvalidAction,
        InvalidBinding,
        BindingNotFound,
        RebindInProgress,
        NoRebindInProgress,
        Conflict,
        Cancelled,
        TimedOut,
        InvalidData,
        Failed
    }

    /// <summary>搜索重复物理控制路径时采用的范围。</summary>
    public enum NewInputConflictScope
    {
        /// <summary>仅检查目标 Action 自己的其他绑定。</summary>
        Action,

        /// <summary>检查目标 Action Map 中的所有 Action。</summary>
        ActionMap,

        /// <summary>检查运行时 InputActionAsset 中的全部 Action Map。</summary>
        Asset
    }

    /// <summary>
    /// 交互式改键选项。
    /// <see cref="M:FinkFramework.Runtime.InputSystem.NewInputManager.StartRebind(System.String,System.Action{FinkFramework.Runtime.InputSystem.NewInputRebindResult},FinkFramework.Runtime.InputSystem.NewInputRebindOptions)"/>
    /// 开始时会复制配置快照，之后修改本对象不会影响进行中的会话。
    /// </summary>
    public sealed class NewInputRebindOptions
    {
        /// <summary>每个普通绑定或组合部件等待输入的秒数；无效值会回退到 10 秒。</summary>
        public float TimeoutSeconds { get; set; } = 10f;

        /// <summary>冲突时是允许绑定并警告，还是拒绝绑定。</summary>
        public InputConflictMode ConflictMode { get; set; } = InputConflictMode.Warning;

        /// <summary>
        /// 兼容旧 API 的冲突开关。false 等价于 <see cref="InputConflictMode.Strict"/>；
        /// 新代码应使用 <see cref="ConflictMode"/>。
        /// </summary>
        public bool AllowConflicts { get; set; } = true;

        /// <summary>冲突检测时是否只比较具有重叠 Binding Group 的绑定。</summary>
        public bool RespectBindingGroups { get; set; } = true;

        /// <summary>
        /// 改键期间是否暂停资产中所有已启用的 Action Map。
        /// 默认开启，可避免改键按键同时触发游戏行为；结束后只恢复原先启用的 Map。
        /// </summary>
        public bool DisableAllActionMaps { get; set; } = true;

        /// <summary>冲突检测范围，默认限制在同一 Action Map；警告模式也会使用此范围。</summary>
        public NewInputConflictScope ConflictScope { get; set; } = NewInputConflictScope.ActionMap;

        /// <summary>
        /// 用于取消改键的 Input System 控制路径。默认为 Escape；设为空字符串可允许绑定 Escape。
        /// </summary>
        public string CancelControlPath { get; set; } = "<Keyboard>/escape";

        /// <summary>不会被当作候选绑定的控制路径集合，主要用于排除指针位移等噪声控制。</summary>
        public string[] ExcludedControlPaths { get; set; } =
        {
            "<Pointer>/position",
            "<Pointer>/delta",
            "<Touchscreen>/touch*/position",
            "<Touchscreen>/touch*/delta",
            "<Mouse>/clickCount"
        };

        internal NewInputRebindOptions Snapshot()
        {
            float timeout = TimeoutSeconds;
            if (float.IsNaN(timeout) || float.IsInfinity(timeout) || timeout <= 0f)
                timeout = 10f;

            NewInputConflictScope scope = Enum.IsDefined(typeof(NewInputConflictScope), ConflictScope)
                ? ConflictScope
                : NewInputConflictScope.ActionMap;

            InputConflictMode conflictMode = Enum.IsDefined(typeof(InputConflictMode), ConflictMode)
                ? ConflictMode
                : InputConflictMode.Warning;
            if (!AllowConflicts && conflictMode == InputConflictMode.Warning)
                conflictMode = InputConflictMode.Strict;

            return new NewInputRebindOptions
            {
                TimeoutSeconds = Math.Max(0.1f, timeout),
                ConflictMode = conflictMode,
                AllowConflicts = AllowConflicts,
                RespectBindingGroups = RespectBindingGroups,
                DisableAllActionMaps = DisableAllActionMaps,
                ConflictScope = scope,
                CancelControlPath = CancelControlPath,
                ExcludedControlPaths = ExcludedControlPaths == null
                    ? Array.Empty<string>()
                    : (string[])ExcludedControlPaths.Clone()
            };
        }
    }

    /// <summary>
    /// 一条只读的 Input System 绑定快照，供设置界面显示并用 Action/Binding GUID 稳定定位。
    /// </summary>
    public readonly struct NewInputBindingInfo
    {
        /// <summary>所属 Action 的稳定 GUID。</summary>
        public Guid ActionId { get; }

        /// <summary>该绑定在 InputActionAsset 中的稳定 GUID。</summary>
        public Guid BindingId { get; }

        /// <summary>所属 Action Map 名称。</summary>
        public string ActionMapName { get; }

        /// <summary>所属 Action 名称。</summary>
        public string ActionName { get; }

        /// <summary>组合绑定部件名称，例如 Up、Down、Left、Right；普通绑定通常为空。</summary>
        public string BindingName { get; }

        /// <summary>资产中声明的默认控制路径。</summary>
        public string OriginalPath { get; }

        /// <summary>运行时覆盖路径；无路径覆盖与显式禁用绑定均显示为空字符串，使用 HasPathOverride 区分。</summary>
        public string OverridePath { get; }

        /// <summary>是否显式覆盖了控制路径；即使覆盖值为空（禁用绑定）也为 true。</summary>
        public bool HasPathOverride { get; }

        /// <summary>当前真正生效的控制路径，已合并默认值与覆盖值。</summary>
        public string EffectivePath { get; }

        /// <summary>绑定所属的 Control Scheme/Binding Group 列表。</summary>
        public string Groups { get; }

        /// <summary>适合直接显示给玩家的控制名称。</summary>
        public string DisplayString { get; }

        /// <summary>显示字符串对应的设备布局名称。</summary>
        public string DeviceLayoutName { get; }

        /// <summary>显示字符串对应的设备内控制路径。</summary>
        public string ControlPath { get; }

        /// <summary>本快照生成时绑定在所属 Action 中的索引；持久化时应使用 BindingId 而不是索引。</summary>
        public int BindingIndex { get; }

        /// <summary>是否为组合绑定根节点。</summary>
        public bool IsComposite { get; }

        /// <summary>是否为组合绑定的一个部件。</summary>
        public bool IsPartOfComposite { get; }

        /// <summary>该绑定当前是否存在路径、处理器或交互覆盖。</summary>
        public bool HasOverride { get; }

        /// <summary>该绑定当前是否与同一 Action Map 中绑定组相容的其他绑定发生冲突。</summary>
        public bool HasConflict { get; }

        /// <summary>当前找到的第一条冲突绑定；没有冲突时为空。</summary>
        public NewInputConflictInfo? Conflict { get; }

        internal NewInputBindingInfo(
            InputAction action,
            InputBinding binding,
            int bindingIndex,
            string displayString,
            string deviceLayoutName,
            string controlPath,
            NewInputConflictInfo? conflict)
        {
            ActionId = action.id;
            BindingId = binding.id;
            ActionMapName = action.actionMap?.name ?? string.Empty;
            ActionName = action.name;
            BindingName = binding.name ?? string.Empty;
            OriginalPath = binding.path ?? string.Empty;
            OverridePath = binding.overridePath ?? string.Empty;
            HasPathOverride = binding.overridePath != null;
            EffectivePath = binding.effectivePath ?? string.Empty;
            Groups = binding.groups ?? string.Empty;
            DisplayString = displayString ?? string.Empty;
            DeviceLayoutName = deviceLayoutName ?? string.Empty;
            ControlPath = controlPath ?? string.Empty;
            BindingIndex = bindingIndex;
            IsComposite = binding.isComposite;
            IsPartOfComposite = binding.isPartOfComposite;
            HasOverride = binding.hasOverrides;
            Conflict = conflict;
            HasConflict = conflict.HasValue;
        }
    }

    /// <summary>描述与候选控制路径发生冲突的现有绑定。</summary>
    public readonly struct NewInputConflictInfo
    {
        /// <summary>冲突绑定所属 Action 的稳定 GUID。</summary>
        public Guid ActionId { get; }

        /// <summary>冲突绑定的稳定 GUID。</summary>
        public Guid BindingId { get; }

        /// <summary>冲突绑定所属 Action Map 名称。</summary>
        public string ActionMapName { get; }

        /// <summary>冲突绑定所属 Action 名称。</summary>
        public string ActionName { get; }

        /// <summary>造成冲突的当前有效控制路径。</summary>
        public string EffectivePath { get; }

        internal NewInputConflictInfo(InputAction action, InputBinding binding)
        {
            ActionId = action.id;
            BindingId = binding.id;
            ActionMapName = action.actionMap?.name ?? string.Empty;
            ActionName = action.name;
            EffectivePath = binding.effectivePath ?? string.Empty;
        }
    }

    /// <summary>一次交互式改键会话的最终结果。</summary>
    public readonly struct NewInputRebindResult
    {
        /// <summary>改键会话最终状态。</summary>
        public NewInputBindingResult Result { get; }

        /// <summary>发起改键的 Action GUID。</summary>
        public Guid ActionId { get; }

        /// <summary>发起改键的 Binding GUID；组合绑定时是根节点 GUID。</summary>
        public Guid BindingId { get; }

        /// <summary>最后一个成功捕获或发生冲突的候选控制路径。</summary>
        public string AppliedPath { get; }

        /// <summary>结果为 Conflict 或 SuccessWithConflict 时提供冲突对象，其余结果通常为 null。</summary>
        public NewInputConflictInfo? Conflict { get; }

        /// <summary>改键是否完整成功；带冲突警告的结果也算成功。</summary>
        public bool Succeeded => Result is NewInputBindingResult.Success
            or NewInputBindingResult.SuccessWithConflict;

        /// <summary>改键是否成功但存在冲突警告。</summary>
        public bool HasConflictWarning => Result == NewInputBindingResult.SuccessWithConflict;

        internal NewInputRebindResult(
            NewInputBindingResult result,
            Guid actionId,
            Guid bindingId,
            string appliedPath,
            NewInputConflictInfo? conflict)
        {
            Result = result;
            ActionId = actionId;
            BindingId = bindingId;
            AppliedPath = appliedPath ?? string.Empty;
            Conflict = conflict;
        }
    }
}
