using FinkFramework.Runtime.Utils;
using UnityEngine;

namespace FinkFramework.Runtime.UI.Surface
{
    /// <summary>
    /// 把场景中的 Canvas 注册为独立 UI Surface。
    /// 屏幕 UI、世界空间 UI 和手部菜单都使用同一种模型，不再切换全局 UI 模式。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas))]
    public sealed class UISurfaceRoot : MonoBehaviour
    {
        [InspectorName("挂载表面标识")]
        [Tooltip("同一时刻必须唯一，例如 WorldMap、WristMenu。")]
        [SerializeField] private string surfaceId;

        [InspectorName("面板挂载根节点")]
        [Tooltip("留空时使用当前 Canvas；也可以指定包含 Bottom、Middle、Top、System 子节点的容器。")]
        [SerializeField] private Transform panelRoot;

        [InspectorName("生命周期")]
        [Tooltip("场景级会在所属场景卸载时自动注销；持久级会把当前对象的整个根节点设为跨场景对象。")]
        [SerializeField] private UISurfaceLifetime lifetime = UISurfaceLifetime.Scene;

        [InspectorName("事件相机")]
        [Tooltip("可选。世界空间 UI 通常填写玩家或 XR 相机；留空时尝试使用主相机。")]
        [SerializeField] private Camera eventCamera;

        private Canvas ownedCanvas;
        private bool registered;
        private UISurfaceId registeredSurfaceId;

        public UISurfaceId SurfaceId => new(surfaceId);

        private void Reset()
        {
            surfaceId = gameObject.name;
            panelRoot = transform;
        }

        private void OnEnable()
        {
            if (registered)
                return;

            if (string.IsNullOrWhiteSpace(surfaceId))
            {
                LogUtil.Error("UI", $"{name} 的 Surface Id 为空，无法注册。");
                return;
            }

            ownedCanvas = GetComponent<Canvas>();
            if (eventCamera)
                ownedCanvas.worldCamera = eventCamera;
            if (lifetime == UISurfaceLifetime.Persistent)
                DontDestroyOnLoad(transform.root.gameObject);

            UISurfaceId id = new(surfaceId);
            registered = UIManager.Instance.RegisterSurface(
                id,
                ownedCanvas,
                panelRoot ? panelRoot : transform,
                lifetime: lifetime);
            if (registered)
                registeredSurfaceId = id;
        }

        private void OnDestroy()
        {
            if (!registered)
                return;

            UIManager manager = UIManager.TryGetInstance();
            manager?.UnregisterSurface(registeredSurfaceId, ownedCanvas);
            registered = false;
        }
    }
}
