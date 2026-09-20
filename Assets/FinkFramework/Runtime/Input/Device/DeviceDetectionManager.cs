using System;
using FinkFramework.Runtime.Environments;
using FinkFramework.Runtime.Settings.Loaders;
using FinkFramework.Runtime.Settings.ScriptableObjects;
using FinkFramework.Runtime.Singleton;
using UnityEngine;
namespace FinkFramework.Runtime.Input
{
    /// <summary>
    /// 统一识别最近一次有效输入来自键鼠、手柄或触摸。
    /// 新旧输入系统只负责上报输入活动；具体模块可订阅变化事件并决定自己的交互策略。
    /// </summary>
    public sealed class DeviceDetectionManager : Singleton<DeviceDetectionManager>
    {
        private IInputDeviceActivitySource activitySource;
        private readonly Func<DeviceDetectionSettings> settingsProvider;
        private readonly bool usesInjectedSource;
        private bool sourceBackendInitialized;
        private bool sourceUsesNewInputSystem;

        /// <summary>
        /// 最近一次产生有效操作的设备类别；它描述玩家当前主要操作来源，不代表设备连接状态。
        /// </summary>
        public InputDeviceType CurrentDevice { get; private set; } = InputDeviceType.Unknown;

        /// <summary>
        /// 获取全局设置中是否启用了设备检测。
        /// 该值每次访问都会读取当前运行时设置，因此运行期间修改设置后无需重建管理器。
        /// </summary>
        public bool IsDetectionEnabled => settingsProvider().IsEnabled;

        /// <summary>
        /// 最近主要输入设备改变时触发，参数依次为旧设备与新设备。
        /// 每个订阅者独立执行，一个订阅者抛异常不会阻止其他订阅者。
        /// </summary>
        public event Action<InputDeviceType, InputDeviceType> DeviceChanged;

        private DeviceDetectionManager()
        {
            settingsProvider = ReadSettings;
            EnsureActivitySource();
            DeviceDetectionDriver.Create(this);
        }

        internal DeviceDetectionManager(
            IInputDeviceActivitySource activitySource,
            Func<DeviceDetectionSettings> settingsProvider)
        {
            this.activitySource = activitySource ?? throw new ArgumentNullException(nameof(activitySource));
            this.settingsProvider = settingsProvider ?? throw new ArgumentNullException(nameof(settingsProvider));
            usesInjectedSource = true;
        }

        internal void Tick()
        {
            EnsureActivitySource();

            DeviceDetectionSettings settings = settingsProvider();
            if (!settings.IsEnabled)
            {
                SetCurrentDevice(InputDeviceType.Unknown);
                return;
            }

            if (activitySource.TryGetActiveDevice(settings, out InputDeviceType device))
                SetCurrentDevice(device);
        }

        private void SetCurrentDevice(InputDeviceType device)
        {
            if (CurrentDevice == device)
                return;

            InputDeviceType previous = CurrentDevice;
            CurrentDevice = device;
            InvokeDeviceChangedSafely(previous, device);
        }

        /// <summary>
        /// 保证活动源与当前最终输入后端一致。
        /// GlobalSettings 可能晚于本管理器加载，适配器也可能在不同程序集稍后注册，
        /// 因此不能只在构造函数中永久决定一次后端。
        /// </summary>
        private void EnsureActivitySource()
        {
            if (usesInjectedSource)
                return;

            bool useNewInputSystem = EnvironmentState.FinalUseNewInputSystem;
            if (sourceBackendInitialized
                && sourceUsesNewInputSystem == useNewInputSystem
                && activitySource != NullInputDeviceActivitySource.Instance)
                return;

            bool backendChanged = sourceBackendInitialized
                                  && sourceUsesNewInputSystem != useNewInputSystem;
            sourceBackendInitialized = true;
            sourceUsesNewInputSystem = useNewInputSystem;

            IInputDeviceActivitySource source = useNewInputSystem
                ? InputSystemHooks.CreateNewActivitySource?.Invoke()
                : InputSystemHooks.CreateLegacyActivitySource?.Invoke();

            activitySource = source ?? NullInputDeviceActivitySource.Instance;
            if (backendChanged)
                SetCurrentDevice(InputDeviceType.Unknown);
        }

        private void InvokeDeviceChangedSafely(InputDeviceType previous, InputDeviceType current)
        {
            if (DeviceChanged == null)
                return;

            foreach (var @delegate in DeviceChanged.GetInvocationList())
            {
                var subscriber = (Action<InputDeviceType, InputDeviceType>)@delegate;
                try
                {
                    subscriber(previous, current);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        private static DeviceDetectionSettings ReadSettings()
        {
            if (!GlobalSettingsRuntimeLoader.TryGet(out var settings))
                return DeviceDetectionSettings.Default;

            return new DeviceDetectionSettings(
                settings.EnableDeviceDetection,
                settings.TreatMouseMovementAsKeyboardMouseInput,
                Mathf.Max(0f, settings.MouseMovementDetectionThreshold));
        }
    }

    /// <summary>输入系统适配层的最小契约，便于替换与编辑器测试。</summary>
    internal interface IInputDeviceActivitySource
    {
        bool TryGetActiveDevice(DeviceDetectionSettings settings, out InputDeviceType device);
    }

    /// <summary>
    /// Input System 程序集向核心输入模块注册的可选适配器入口。
    /// 核心程序集不直接引用 UnityEngine.InputSystem。
    /// </summary>
    internal static class InputSystemHooks
    {
        internal static Func<IInputDeviceActivitySource> CreateLegacyActivitySource { get; set; }
        internal static Func<IInputDeviceActivitySource> CreateNewActivitySource { get; set; }
        internal static Func<Vector3> GetPointerPosition { get; set; }
        internal static Func<bool> IsPointerPressed { get; set; }
        internal static Func<bool> IsNavigationPressed { get; set; }
    }

    /// <summary>当对应输入后端未注册时，保持设备检测模块可用但不产生设备活动。</summary>
    internal sealed class NullInputDeviceActivitySource : IInputDeviceActivitySource
    {
        internal static readonly NullInputDeviceActivitySource Instance = new();

        public bool TryGetActiveDevice(DeviceDetectionSettings settings, out InputDeviceType device)
        {
            device = InputDeviceType.Unknown;
            return false;
        }
    }

    /// <summary>检测器每帧读取的配置快照，避免适配器依赖具体配置资产。</summary>
    internal readonly struct DeviceDetectionSettings
    {
        public static readonly DeviceDetectionSettings Default = new(
            true,
            true,
            GlobalSettingsAsset.DefaultMouseMovementDetectionThreshold);

        public readonly bool IsEnabled;
        public readonly bool TreatMouseMovementAsKeyboardMouseInput;
        public readonly float MouseMovementThreshold;

        public DeviceDetectionSettings(
            bool isEnabled,
            bool treatMouseMovementAsKeyboardMouseInput,
            float mouseMovementThreshold)
        {
            IsEnabled = isEnabled;
            TreatMouseMovementAsKeyboardMouseInput = treatMouseMovementAsKeyboardMouseInput;
            MouseMovementThreshold = mouseMovementThreshold;
        }
    }

    /// <summary>为全局设备检测器提供早于 UGUI 的逐帧更新。</summary>
    [DefaultExecutionOrder(-1100)]
    internal sealed class DeviceDetectionDriver : MonoBehaviour
    {
        private DeviceDetectionManager manager;

        internal static DeviceDetectionDriver Create(DeviceDetectionManager manager)
        {
            var gameObject = new GameObject("[FinkFramework] Device Detection");
            DontDestroyOnLoad(gameObject);

            DeviceDetectionDriver driver = gameObject.AddComponent<DeviceDetectionDriver>();
            driver.manager = manager;
            return driver;
        }

        private void Update() => manager?.Tick();
    }
}
