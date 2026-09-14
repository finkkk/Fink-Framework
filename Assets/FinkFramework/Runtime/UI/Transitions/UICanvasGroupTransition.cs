using System.Threading;
using Cysharp.Threading.Tasks;
using FinkFramework.Runtime.UI.Base;
using UnityEngine;

namespace FinkFramework.Runtime.UI.Transitions
{
    /// <summary>
    /// 不依赖第三方动画库的通用 UI 过渡。
    /// 挂到面板根节点后，UIManager 的异步打开和关闭会自动播放它。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class UICanvasGroupTransition : MonoBehaviour, IUITransition
    {
        [Header("过渡时间")]
        [Min(0f)]
        [SerializeField] private float enterDuration = 0.18f;
        [Min(0f)]
        [SerializeField] private float exitDuration = 0.12f;
        [SerializeField] private bool useUnscaledTime = true;

        [Header("视觉效果")]
        [SerializeField] private bool fade = true;
        [SerializeField] private bool scale = true;
        [Range(0.01f, 1f)]
        [SerializeField] private float hiddenScale = 0.96f;
        [SerializeField] private AnimationCurve easing =
            AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        private CanvasGroup canvasGroup;
        private Vector3 shownScale;
        private bool visualStateInitialized;

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            shownScale = transform.localScale;
        }

        public void CompleteEnter()
        {
            EnsureCached();
            visualStateInitialized = true;
            Apply(1f, shownScale);
        }

        public void CompleteExit()
        {
            EnsureCached();
            visualStateInitialized = true;
            Apply(0f, GetHiddenScale());
        }

        public UniTask PlayEnterAsync(CancellationToken cancellationToken)
        {
            EnsureCached();
            if (!visualStateInitialized)
            {
                visualStateInitialized = true;
                Apply(0f, GetHiddenScale());
            }

            float fromAlpha = canvasGroup.alpha;
            Vector3 fromScale = transform.localScale;
            return AnimateAsync(
                fromAlpha,
                1f,
                fromScale,
                shownScale,
                enterDuration,
                cancellationToken);
        }

        public UniTask PlayExitAsync(CancellationToken cancellationToken)
        {
            EnsureCached();
            visualStateInitialized = true;
            return AnimateAsync(
                canvasGroup.alpha,
                0f,
                transform.localScale,
                GetHiddenScale(),
                exitDuration,
                cancellationToken);
        }

        private async UniTask AnimateAsync(
            float fromAlpha,
            float toAlpha,
            Vector3 fromScale,
            Vector3 toScale,
            float duration,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (duration <= 0f)
            {
                Apply(toAlpha, toScale);
                return;
            }

            float elapsed = 0f;
            Apply(fromAlpha, fromScale);
            while (elapsed < duration)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float eased = easing?.Evaluate(progress) ?? progress;
                Apply(
                    Mathf.LerpUnclamped(fromAlpha, toAlpha, eased),
                    Vector3.LerpUnclamped(fromScale, toScale, eased));
            }

            cancellationToken.ThrowIfCancellationRequested();
            Apply(toAlpha, toScale);
        }

        private void Apply(float alpha, Vector3 localScale)
        {
            if (fade)
                canvasGroup.alpha = alpha;
            if (scale)
                transform.localScale = localScale;
        }

        private Vector3 GetHiddenScale() => shownScale * hiddenScale;

        private void EnsureCached()
        {
            if (!canvasGroup)
                canvasGroup = GetComponent<CanvasGroup>();
            if (shownScale == default)
                shownScale = transform.localScale;
        }
    }
}
