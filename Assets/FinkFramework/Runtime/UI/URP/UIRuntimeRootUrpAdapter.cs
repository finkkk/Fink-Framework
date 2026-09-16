using FinkFramework.Runtime.UI.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace FinkFramework.Runtime.UI.URP
{
    /// <summary>URP 下 UI Camera 的相机栈适配器。</summary>
    internal static class UIRuntimeRootUrpAdapter
    {
        private static Camera stackedMainCamera;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            stackedMainCamera = null;
            UIRuntimePipelineHooks.ConfigureCameraStack = ConfigureCameraStack;
        }

        private static bool ConfigureCameraStack(Camera uiCamera, Camera mainCamera)
        {
            if (!uiCamera)
                return true;

            if (stackedMainCamera
                && stackedMainCamera != mainCamera
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
                return true;
            }

            // 没有可叠加的主相机时，Overlay Camera 不会独立输出画面。
            // 退化为 Base Camera，同时清色以避免上一帧 UI 残影。
            if (!mainCamera || mainCamera == uiCamera)
            {
                uiData.renderType = CameraRenderType.Base;
                uiCamera.clearFlags = CameraClearFlags.SolidColor;
                return true;
            }

            if (!mainCamera.TryGetComponent(out UniversalAdditionalCameraData mainData))
            {
                Debug.LogWarning(
                    "[FinkFramework] URP 相机栈配置已跳过：主相机缺少 UniversalAdditionalCameraData。",
                    mainCamera);
                uiData.renderType = CameraRenderType.Base;
                uiCamera.clearFlags = CameraClearFlags.SolidColor;
                return true;
            }

            uiData.renderType = CameraRenderType.Overlay;

            if (!mainData.cameraStack.Contains(uiCamera))
                mainData.cameraStack.Add(uiCamera);

            stackedMainCamera = mainCamera;
            return true;
        }
    }
}
