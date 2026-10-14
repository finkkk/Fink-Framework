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
            GlobalSettingsRuntimeLoader.TryGet(out var settings);
            bool custom = settings && settings.UseCustomMainCanvas;
            if (custom)
                ValidateCustomCanvas(settings.CustomMainCanvasPrefab);
            RenderMode renderMode = ResolveMainCanvasRenderMode(settings
                ? settings.CurrentUIMode
                : EnvironmentState.UIMode.Auto);
            Camera = renderMode == RenderMode.ScreenSpaceCamera ? CreateCamera() : null;
            MainCanvas = CreateMainCanvas(Camera, renderMode);
            EnsureEventSystem();

            var roots = EnsureLayerRoots(MainCanvas);

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
            if (!Camera)
                return;

            if (EnvironmentState.FinalUseURP
                && UIRuntimePipelineHooks.TryConfigureCameraStack(Camera, MainCamera))
            {
                return;
            }
            ConfigureStandaloneClearMode(Camera, MainCamera);
        }

        private static RenderMode ResolveMainCanvasRenderMode(EnvironmentState.UIMode mode)
        {
            return mode switch
            {
                EnvironmentState.UIMode.WorldSpace => RenderMode.WorldSpace,
                EnvironmentState.UIMode.Auto => EnvironmentState.FinalIsVR
                    ? RenderMode.WorldSpace
                    : RenderMode.ScreenSpaceCamera,
                _ => RenderMode.ScreenSpaceCamera
            };
        }

        private static Camera CreateCamera()
        {
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

        private static Canvas CreateMainCanvas(Camera camera, RenderMode renderMode)
        {
            GlobalSettingsRuntimeLoader.TryGet(out var settings);
            if (settings && settings.UseCustomMainCanvas)
            {
                GameObject instance = Object.Instantiate(settings.CustomMainCanvasPrefab);
                Canvas customCanvas = instance.GetComponent<Canvas>();
                ConfigureCanvasRendering(customCanvas, camera, renderMode);
                Object.DontDestroyOnLoad(instance);
                return customCanvas;
            }

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

                ConfigureCanvasRendering(canvas, camera, renderMode);
                if (settings)
                {
                    canvas.sortingLayerID = settings.UICanvasSortingLayerId;
                    canvas.sortingOrder = settings.UICanvasSortingOrder;
                    if (renderMode != RenderMode.WorldSpace)
                    {
                        canvas.pixelPerfect = settings.UICanvasPixelPerfect;
                        canvas.planeDistance = Mathf.Max(0.01f, settings.UICanvasPlaneDistance);
                        UnityEngine.UI.CanvasScaler scaler =
                            canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
                        if (!scaler)
                            scaler = canvas.gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();

                        scaler.uiScaleMode = settings.UICanvasScaleMode;
                        scaler.referenceResolution = new Vector2(
                            Mathf.Max(1f, settings.UIReferenceResolution.x),
                            Mathf.Max(1f, settings.UIReferenceResolution.y));
                        scaler.screenMatchMode = settings.UIScreenMatchMode;
                        scaler.matchWidthOrHeight = Mathf.Clamp01(settings.UIMatchWidthOrHeight);
                        scaler.scaleFactor = Mathf.Max(0.01f, settings.UIScaleFactor);
                        scaler.referencePixelsPerUnit = Mathf.Max(0.01f, settings.UIReferencePixelsPerUnit);
                        scaler.physicalUnit = settings.UIPhysicalUnit;
                        scaler.fallbackScreenDPI = Mathf.Max(1f, settings.UIFallbackScreenDPI);
                        scaler.defaultSpriteDPI = Mathf.Max(1f, settings.UIDefaultSpriteDPI);
                    }
                }
                return canvas;
            }
            finally
            {
                ResManager.Instance.UnloadAsset<GameObject>(assetPath, true);
            }
        }

        private static void ConfigureCanvasRendering(Canvas canvas, Camera camera, RenderMode renderMode)
        {
            // 两种预制体使用同一条接线规则；以解析出的目标模式决定绑定，
            // 不依赖尚未绑定相机时 Canvas 组件报告的模式。
            if (renderMode == RenderMode.ScreenSpaceCamera)
            {
                if (!camera)
                    throw new InvalidOperationException("Main Canvas 使用屏幕相机模式，但框架 UI Camera 未创建成功。");
                canvas.renderMode = renderMode;
                canvas.worldCamera = camera;
                if (canvas.worldCamera != camera)
                    throw new InvalidOperationException("Main Canvas 绑定框架 UI Camera 失败。");
                return;
            }

            canvas.renderMode = renderMode;
            if (renderMode == RenderMode.WorldSpace && !canvas.worldCamera)
                canvas.worldCamera = UnityEngine.Camera.main;
        }

        private static void ValidateCustomCanvas(GameObject prefab)
        {
            if (!prefab || !prefab.GetComponent<Canvas>())
                throw new InvalidOperationException("已开启自定义 Main Canvas，请指定根对象带有 Canvas 的预制体。");

            foreach (UILayer layer in Enum.GetValues(typeof(UILayer)))
            {
                int count = 0;
                foreach (Transform child in prefab.transform)
                {
                    if (child.name != layer.ToString())
                        continue;
                    count++;
                    if (!(child is RectTransform))
                        throw new InvalidOperationException($"自定义 Main Canvas 的 {layer} 层必须使用 RectTransform。");
                }
                if (count > 1)
                    throw new InvalidOperationException($"自定义 Main Canvas 有重复的直接子节点 {layer}，请修正预制体。");
            }
        }

        private static Dictionary<UILayer, Transform> EnsureLayerRoots(Canvas canvas)
        {
            var roots = new Dictionary<UILayer, Transform>();
            foreach (UILayer layer in Enum.GetValues(typeof(UILayer)))
            {
                Transform child = canvas.transform.Find(layer.ToString());
                if (!child)
                {
                    var rect = (RectTransform)new GameObject(layer.ToString(), typeof(RectTransform)).transform;
                    rect.gameObject.layer = canvas.gameObject.layer;
                    rect.SetParent(canvas.transform, false);
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.one;
                    rect.offsetMin = Vector2.zero;
                    rect.offsetMax = Vector2.zero;
                    child = rect;
                }
                // 保留层的布局与内容，仅保证框架层之间的渲染顺序。
                child.SetAsLastSibling();
                roots[layer] = child;
            }
            return roots;
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
