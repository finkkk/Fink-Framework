using System;
using System.Collections.Generic;
using FinkFramework.Runtime.Environments;
using FinkFramework.Runtime.ResLoad;
using FinkFramework.Runtime.Settings.Loaders;
using FinkFramework.Runtime.UI.Surface;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;
// ReSharper disable UnusedParameter.Local
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace FinkFramework.Runtime.UI.Core
{
    /// <summary>只负责创建框架默认的 Camera、Canvas 与 EventSystem。</summary>
    internal sealed class UIRuntimeRoot
    {
        private const string BasePath = "FinkFramework/UI/Base/";
        private const string DefaultCameraPrefabName = "UICamera";
        private const string UrpCameraPrefabName = "UICamera_URP";

        public Camera Camera { get; }
        public Camera MainCamera { get; private set; }
        public Canvas MainCanvas { get; }
        public UISurface MainSurface { get; }

        public UIRuntimeRoot()
        {
            Camera = CreateCamera();
            MainCanvas = CreateMainCanvas(Camera);
            EnsureEventSystem();

            var roots = new Dictionary<UILayer, Transform>();
            foreach (UILayer layer in Enum.GetValues(typeof(UILayer)))
            {
                Transform child = MainCanvas.transform.Find(layer.ToString());
                roots[layer] = child ? child : MainCanvas.transform;
            }

            MainSurface = new UISurface(
                UISurfaceId.Main,
                MainCanvas,
                MainCanvas.transform,
                roots,
                UISurfaceLifetime.Persistent);

            SetMainCamera(Camera.main);
        }

        /// <summary>
        /// 设置与框架 UI Camera 组合的游戏主相机，并立即刷新渲染管线配置。
        /// 玩家相机创建、替换或销毁时，由业务显式传入新相机或 null。
        /// </summary>
        internal void SetMainCamera(Camera mainCamera)
        {
            MainCamera = mainCamera && mainCamera != Camera ? mainCamera : null;
            RefreshCameraComposition();
        }

        private void RefreshCameraComposition()
        {
            if (EnvironmentState.FinalIsVR || !Camera)
                return;

            if (EnvironmentState.FinalUseURP
                && UIRuntimePipelineHooks.TryConfigureCameraStack(Camera, MainCamera))
            {
                return;
            }
            ConfigureStandaloneClearMode(Camera, MainCamera);
        }

        private static Camera CreateCamera()
        {
            if (EnvironmentState.FinalIsVR)
                return null;

            string prefabName = EnvironmentState.FinalUseURP
                ? UrpCameraPrefabName
                : DefaultCameraPrefabName;
            string assetPath = $"res://{BasePath}{prefabName}";
            GameObject prefab = ResManager.Instance.Load<GameObject>(assetPath);
            if (!prefab)
                throw new InvalidOperationException($"缺少框架 UI Camera 预制体：{prefabName}。");

            try
            {
                GameObject instance = Object.Instantiate(prefab);
                Camera camera = instance.GetComponent<Camera>();
                if (!camera)
                {
                    Object.Destroy(instance);
                    throw new InvalidOperationException("UI Camera 预制体根对象没有 Camera 组件。");
                }

                Object.DontDestroyOnLoad(camera.gameObject);
                return camera;
            }
            finally
            {
                // 实例已经脱离预制体引用，及时归还 ResManager 的缓存引用。
                ResManager.Instance.UnloadAsset<GameObject>(assetPath, true);
            }
        }

        private static Canvas CreateMainCanvas(Camera camera)
        {
            string assetPath = $"res://{BasePath}MainCanvas";
            GameObject prefab = ResManager.Instance.Load<GameObject>(assetPath);
            if (!prefab)
                throw new InvalidOperationException("缺少框架 MainCanvas 预制体。");

            try
            {
                GameObject instance = Object.Instantiate(prefab);
                Canvas canvas = instance.GetComponent<Canvas>();
                if (!canvas)
                {
                    Object.Destroy(instance);
                    throw new InvalidOperationException("MainCanvas 预制体根对象没有 Canvas 组件。");
                }

                Object.DontDestroyOnLoad(canvas.gameObject);

                EnvironmentState.UIMode mode = GlobalSettingsRuntimeLoader.TryGet(out var settings)
                    ? settings.CurrentUIMode
                    : EnvironmentState.UIMode.Auto;

                canvas.renderMode = mode switch
                {
                    EnvironmentState.UIMode.ScreenSpace => RenderMode.ScreenSpaceCamera,
                    EnvironmentState.UIMode.WorldSpace => RenderMode.WorldSpace,
                    EnvironmentState.UIMode.Auto => EnvironmentState.FinalIsVR
                        ? RenderMode.WorldSpace
                        : RenderMode.ScreenSpaceCamera,
                    _ => RenderMode.ScreenSpaceCamera
                };

                canvas.worldCamera = EnvironmentState.FinalIsVR ? null : camera;
                return canvas;
            }
            finally
            {
                ResManager.Instance.UnloadAsset<GameObject>(assetPath, true);
            }
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current)
            {
                // MainCanvas 是跨场景对象；复用场景 EventSystem 时也必须提升它，
                // 否则切场景后 EventSystem 被卸载，所有 UI 将失去点击和导航。
                Object.DontDestroyOnLoad(EventSystem.current.gameObject);
                return;
            }

            string prefabName = EnvironmentState.FinalIsVR
                ? "EventSystem_XR"
                : EnvironmentState.FinalUseNewInputSystem
                    ? "EventSystem_New"
                    : "EventSystem_Old";

            string assetPath = $"res://{BasePath}{prefabName}";
            GameObject prefab = ResManager.Instance.Load<GameObject>(assetPath);
            if (!prefab)
                throw new InvalidOperationException($"缺少框架 EventSystem 预制体：{prefabName}。");

            try
            {
                GameObject eventSystem = Object.Instantiate(prefab);
                Object.DontDestroyOnLoad(eventSystem);
            }
            finally
            {
                ResManager.Instance.UnloadAsset<GameObject>(assetPath, true);
            }
        }

        /// <summary>
        /// 内置管线下，有主相机时 UI Camera 只叠加深度；没有主相机时必须清除颜色。
        /// 否则 UI 被隐藏后，Game View 会保留上一帧的颜色缓冲，形成“不可交互但仍可见”的残影。
        /// </summary>
        private static void ConfigureStandaloneClearMode(Camera uiCamera, Camera mainCamera)
        {
            uiCamera.clearFlags = mainCamera && mainCamera != uiCamera
                ? CameraClearFlags.Depth
                : CameraClearFlags.SolidColor;
        }
    }

    /// <summary>
    /// 可选渲染管线适配器的核心挂钩。
    /// 具体管线程序集在加载时注册实现，核心程序集不直接引用任何管线包。
    /// </summary>
    internal static class UIRuntimePipelineHooks
    {
        internal static Func<Camera, Camera, bool> ConfigureCameraStack { get; set; }

        internal static bool TryConfigureCameraStack(Camera uiCamera, Camera mainCamera)
        {
            return ConfigureCameraStack?.Invoke(uiCamera, mainCamera) == true;
        }
    }
}
