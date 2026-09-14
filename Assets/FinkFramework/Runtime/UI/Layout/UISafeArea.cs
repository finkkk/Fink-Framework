using System;
using UnityEngine;

namespace FinkFramework.Runtime.UI.Layout
{
    /// <summary>指定需要避让刘海、圆角和系统手势区域的屏幕边缘。</summary>
    [Flags]
    public enum UISafeAreaEdges
    {
        None = 0,
        Left = 1 << 0,
        Right = 1 << 1,
        Bottom = 1 << 2,
        Top = 1 << 3,
        All = Left | Right | Bottom | Top
    }

    /// <summary>
    /// 把当前 RectTransform 限制在 Screen.safeArea 内。
    /// 建议挂在全屏内容容器上，让背景仍可铺满屏幕而交互内容避开危险区域。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("Fink Framework/UI/屏幕安全区域")]
    public sealed class UISafeArea : MonoBehaviour
    {
        [InspectorName("需要避让的边缘")]
        [SerializeField] private UISafeAreaEdges edges = UISafeAreaEdges.All;

        [InspectorName("禁用时恢复原布局")]
        [SerializeField] private bool restoreOnDisable = true;

        private RectTransform rectTransform;
        private Rect lastSafeArea;
        private Vector2Int lastScreenSize;
        private Vector2 originalAnchorMin;
        private Vector2 originalAnchorMax;
        private Vector2 originalOffsetMin;
        private Vector2 originalOffsetMax;
        private bool originalLayoutCaptured;
        private Canvas parentCanvas;
        private RenderMode lastRenderMode;

        private void Awake()
        {
            CacheRectTransform();
            CaptureOriginalLayout();
        }

        private void OnEnable()
        {
            CacheRectTransform();
            CaptureOriginalLayout();
            InvalidateCache();
            ApplyNow();
        }

        private void LateUpdate()
        {
            Rect safeArea = Screen.safeArea;
            var screenSize = new Vector2Int(Screen.width, Screen.height);
            RenderMode renderMode = ResolveCanvasRenderMode();
            if (safeArea != lastSafeArea
                || screenSize != lastScreenSize
                || renderMode != lastRenderMode)
                ApplyNow();
        }

        private void OnTransformParentChanged()
        {
            parentCanvas = null;
            InvalidateCache();
        }

        private void OnDisable()
        {
            if (!restoreOnDisable || !originalLayoutCaptured || !rectTransform)
                return;

            RestoreOriginalLayout();
            InvalidateCache();
        }

        /// <summary>立即重新读取 Screen.safeArea 并刷新布局。</summary>
        public void ApplyNow()
        {
            CacheRectTransform();
            if (!rectTransform || Screen.width <= 0 || Screen.height <= 0)
                return;

            Rect safeArea = Screen.safeArea;
            RenderMode renderMode = ResolveCanvasRenderMode();
            if (renderMode == RenderMode.WorldSpace)
            {
                RestoreOriginalLayout();
                lastSafeArea = safeArea;
                lastScreenSize = new Vector2Int(Screen.width, Screen.height);
                lastRenderMode = renderMode;
                return;
            }

            Vector2 anchorMin = safeArea.position;
            Vector2 anchorMax = safeArea.position + safeArea.size;
            anchorMin.x /= Screen.width;
            anchorMin.y /= Screen.height;
            anchorMax.x /= Screen.width;
            anchorMax.y /= Screen.height;

            if ((edges & UISafeAreaEdges.Left) == 0)
                anchorMin.x = 0f;
            if ((edges & UISafeAreaEdges.Bottom) == 0)
                anchorMin.y = 0f;
            if ((edges & UISafeAreaEdges.Right) == 0)
                anchorMax.x = 1f;
            if ((edges & UISafeAreaEdges.Top) == 0)
                anchorMax.y = 1f;

            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            lastSafeArea = safeArea;
            lastScreenSize = new Vector2Int(Screen.width, Screen.height);
            lastRenderMode = renderMode;
        }

        private void CacheRectTransform()
        {
            if (!rectTransform)
                rectTransform = GetComponent<RectTransform>();
        }

        private void CaptureOriginalLayout()
        {
            if (originalLayoutCaptured || !rectTransform)
                return;

            originalAnchorMin = rectTransform.anchorMin;
            originalAnchorMax = rectTransform.anchorMax;
            originalOffsetMin = rectTransform.offsetMin;
            originalOffsetMax = rectTransform.offsetMax;
            originalLayoutCaptured = true;
        }

        private void InvalidateCache()
        {
            lastSafeArea = new Rect(float.NaN, float.NaN, float.NaN, float.NaN);
            lastScreenSize = new Vector2Int(-1, -1);
        }

        private RenderMode ResolveCanvasRenderMode()
        {
            if (!parentCanvas)
                parentCanvas = GetComponentInParent<Canvas>();

            return parentCanvas ? parentCanvas.renderMode : RenderMode.ScreenSpaceOverlay;
        }

        private void RestoreOriginalLayout()
        {
            if (!originalLayoutCaptured || !rectTransform)
                return;

            rectTransform.anchorMin = originalAnchorMin;
            rectTransform.anchorMax = originalAnchorMax;
            rectTransform.offsetMin = originalOffsetMin;
            rectTransform.offsetMax = originalOffsetMax;
        }
    }
}
