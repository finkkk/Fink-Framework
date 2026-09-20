using FinkFramework.Runtime.Environments;
using FinkFramework.Runtime.Input;
using UnityEngine;

namespace FinkFramework.Runtime.LegacyInput
{
    /// <summary>把旧版输入设备活动实现注册到 Device 模块。</summary>
    internal static class LegacyInputRuntimeAdapter
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            InputSystemHooks.CreateLegacyActivitySource = CreateActivitySource;
        }

        private static IInputDeviceActivitySource CreateActivitySource()
        {
            return EnvironmentState.FinalUseNewInputSystem
                ? null
                : new LegacyInputDeviceActivitySource();
        }
    }
}
