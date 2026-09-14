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

#if ENABLE_URP
using UnityEngine.Rendering.Universal;
#endif

namespace FinkFramework.Runtime.UI.Core
{
    /// <summary>只负责创建框架默认的 Camera、Canvas 与 EventSystem。</summary>
    internal sealed class UIRuntimeRoot
    {
        private const string BasePath = "FinkFramework/UI/Base/";
        private const string DefaultCameraPrefabName = "UICamera";
        private const string UrpCameraPrefabName = "UICamera_URP";
        private const float CameraCompositionCheckInterval = 0.5f;

        private Camera compositionMainCamera;
        private float nextCameraCompositionCheckTime;
        private bool cameraCompositionInitialized;
        private Camera _mainCamera;
#if ENABLE_URP
        private Camera stackedMainCamera;
#endif

        public Camera Camera { get; }
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

            RefreshCameraComposition(true);
        }

        private void Start()
        {
            _mainCamera = Camera.main;
        }

        /// <summary>
        /// 让 UI Camera 跟随主相机的创建、销毁或替换。
        /// 常见于启动场景先初始化框架、稍后才生成玩家相机的项目。
        /// </summary>
        public void Tick()
        {
            if (Time.unscaledTime < nextCameraCompositionCheckTime)
                return;

            nextCameraCompositionCheckTime = Time.unscaledTime + CameraCompositionCheckInterval;
            RefreshCameraComposition();
        }

        private void RefreshCameraComposition(bool force = false)
        {
            if (EnvironmentState.FinalIsVR || !Camera)
                return;

            if (!force && cameraCompositionInitialized && _mainCamera == compositionMainCamera)
                return;

            cameraCompositionInitialized = true;
            compositionMainCamera = _mainCamera;

#if ENABLE_URP
            if (EnvironmentState.FinalUseURP)
            {
                SetupCameraStack(Camera, mainCamera);
                return;
            }
#endif
            ConfigureStandaloneClearMode(Camera, _mainCamera);
        }

        private static Camera CreateCamera()
        {
            if (EnvironmentState.FinalIsVR)
                return null;

            string prefabName = EnvironmentState.FinalUseURP
                ? UrpCameraPrefabName
                : DefaultCameraPrefabName;
            GameObject prefab = ResManager.Instance.Load<GameObject>($"res://{BasePath}{prefabName}");
            if (!prefab)
                throw new InvalidOperationException($"缺少框架 UI Camera 预制体：{prefabName}。");

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

        private static Canvas CreateMainCanvas(Camera camera)
        {
            GameObject prefab = ResManager.Instance.Load<GameObject>($"res://{BasePath}MainCanvas");
            if (!prefab)
                throw new InvalidOperationException("缺少框架 MainCanvas 预制体。");

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

        private static void EnsureEventSystem()
        {
            if (EventSystem.current)
                return;

            string prefabName = EnvironmentState.FinalIsVR
                ? "EventSystem_XR"
                : EnvironmentState.FinalUseNewInputSystem
                    ? "EventSystem_New"
                    : "EventSystem_Old";

            GameObject prefab = ResManager.Instance.Load<GameObject>($"res://{BasePath}{prefabName}");
            if (!prefab)
                throw new InvalidOperationException($"缺少框架 EventSystem 预制体：{prefabName}。");

            GameObject eventSystem = Object.Instantiate(prefab);
            Object.DontDestroyOnLoad(eventSystem);
        }

        private void SetupCameraStack(Camera uiCamera, Camera mainCamera)
        {
#if ENABLE_URP
            if (EnvironmentState.FinalIsVR || !EnvironmentState.FinalUseURP || !uiCamera)
                return;

            if (stackedMainCamera && stackedMainCamera != mainCamera
                && stackedMainCamera.TryGetComponent(out UniversalAdditionalCameraData previousMainData))
            {
                previousMainData.cameraStack.Remove(uiCamera);
            }

            stackedMainCamera = null;
            if (!uiCamera.TryGetComponent(out UniversalAdditionalCameraData uiData))
            {
                Debug.LogWarning(
                    "[FinkFramework] URP 相机栈配置已跳过：UI 相机缺少 UniversalAdditionalCameraData。",
                    uiCamera);
                return;
            }

            // 没有可叠加的主相机时，Overlay Camera 不会独立输出画面。
            // 退化为 Base Camera，同时清色以避免上一帧 UI 残影。
            if (!mainCamera || mainCamera == uiCamera)
            {
                uiData.renderType = CameraRenderType.Base;
                uiCamera.clearFlags = CameraClearFlags.SolidColor;
                return;
            }

            if (!mainCamera.TryGetComponent(out UniversalAdditionalCameraData mainData))
            {
                Debug.LogWarning(
                    "[FinkFramework] URP 相机栈配置已跳过：主相机缺少 UniversalAdditionalCameraData。",
                    mainCamera);
                uiData.renderType = CameraRenderType.Base;
                uiCamera.clearFlags = CameraClearFlags.SolidColor;
                return;
            }

            uiData.renderType = CameraRenderType.Overlay;

            if (!mainData.cameraStack.Contains(uiCamera))
                mainData.cameraStack.Add(uiCamera);

            stackedMainCamera = mainCamera;
#endif
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
}
