using FinkFramework.Runtime.Input;
using FinkFramework.Runtime.Settings.Loaders;
using FinkFramework.Runtime.UI.Core;
using UnityEngine;

namespace FinkFramework.Runtime.UI.Input
{
    /// <summary>把纯 C# 输入路由接入 Unity Update。</summary>
    [DefaultExecutionOrder(-1000)]
    internal sealed class UIInputDriver : MonoBehaviour
    {
        private UIInputRouter router;
        private DeviceDetectionManager deviceDetectionManager;
        private UIRuntimeRoot runtimeRoot;
        private bool hasAppliedAutomaticNavigationPolicy;
        private bool lastAutomaticNavigationEnabled;

        public static UIInputDriver Create(
            UIInputRouter router,
            DeviceDetectionManager deviceDetectionManager,
            UIRuntimeRoot runtimeRoot)
        {
            var gameObject = new GameObject("[FinkFramework] UI Input Router");
            DontDestroyOnLoad(gameObject);
            UIInputDriver driver = gameObject.AddComponent<UIInputDriver>();
            driver.router = router;
            driver.deviceDetectionManager = deviceDetectionManager;
            driver.runtimeRoot = runtimeRoot;
            return driver;
        }

        private void Update()
        {
            runtimeRoot?.Tick();
            ApplyAutomaticNavigationPolicy();
            router?.Tick();
        }

        private void ApplyAutomaticNavigationPolicy()
        {
            if (router == null || deviceDetectionManager == null)
                return;

            bool automatic = !GlobalSettingsRuntimeLoader.TryGet(out var settings)
                             || settings.EnableAutoNavigationInteractionByInputDevice;

            // 关闭自动策略后，导航开关完全交还给 UIManager 的公开 API，
            // 不能在下一帧又把业务手动 Disable 的结果强行改回 true。
            if (!automatic)
            {
                hasAppliedAutomaticNavigationPolicy = false;
                return;
            }

            bool enable = !deviceDetectionManager.IsDetectionEnabled
                           || deviceDetectionManager.CurrentDevice is InputDeviceType.Gamepad or InputDeviceType.Unknown;
            if (hasAppliedAutomaticNavigationPolicy
                && lastAutomaticNavigationEnabled == enable)
                return;

            router.SetNavigationInteractionEnabled(enable);
            lastAutomaticNavigationEnabled = enable;
            hasAppliedAutomaticNavigationPolicy = true;
        }
    }
}
