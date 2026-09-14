using System;
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
        private readonly IInputDeviceActivitySource activitySource;
        private readonly Func<DeviceDetectionSettings> settingsProvider;

        /// <summary>最近一次产生有效操作的设备类别。</summary>
        public InputDeviceType CurrentDevice { get; private set; } = InputDeviceType.Unknown;

        /// <summary>当前是否启用了全局设备输入检测。</summary>
        public bool IsDetectionEnabled => settingsProvider().IsEnabled;

        /// <summary>最近主要输入设备改变时触发，参数依次为旧设备与新设备。</summary>
        public event Action<InputDeviceType, InputDeviceType> DeviceChanged;

        private DeviceDetectionManager()
        {
            activitySource = CreateActivitySource();
            settingsProvider = ReadSettings;
            DeviceDetectionDriver.Create(this);
        }

        internal DeviceDetectionManager(
            IInputDeviceActivitySource activitySource,
            Func<DeviceDetectionSettings> settingsProvider)
        {
            this.activitySource = activitySource ?? throw new ArgumentNullException(nameof(activitySource));
            this.settingsProvider = settingsProvider ?? throw new ArgumentNullException(nameof(settingsProvider));
        }

        internal void Tick()
        {
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
            DeviceChanged?.Invoke(previous, device);
        }

        private static IInputDeviceActivitySource CreateActivitySource()
        {
#if ENABLE_INPUT_SYSTEM
            if (EnvironmentState.FinalUseNewInputSystem)
                return new NewInputDeviceActivitySource();
#endif
            return new LegacyInputDeviceActivitySource();
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
