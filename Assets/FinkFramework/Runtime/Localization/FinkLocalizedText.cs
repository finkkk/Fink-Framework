using System.Threading;
using Cysharp.Threading.Tasks;
using FinkFramework.Runtime.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FinkFramework.Runtime.Localization
{
    /// <summary>
    /// 将本地化 Key 绑定到 Unity Text 或 TMP 文本，并在语言切换后自动刷新。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Fink Framework/Localization/Localized Text")]
    public sealed class FinkLocalizedText : MonoBehaviour
    {
        [SerializeField] private string key;

        private Text legacyText;
        private TextAnchor legacyTextOriginalAlignment;
        private bool legacyTextOriginalAlignmentCaptured;
        private TMP_Text tmpText;
        private TextAlignmentOptions tmpTextOriginalAlignment;
        private bool tmpTextOriginalRightToLeft;
        private bool tmpTextOriginalAlignmentCaptured;
        private CancellationTokenSource refreshCancellation;
        private bool missingKeyWarningLogged;
        private bool subscribedToLocaleChanges;

        /// <summary>
        /// 绑定的本地化 Key。赋值后会立即尝试刷新目标文本。
        /// </summary>
        public string Key
        {
            get => key;
            set
            {
                key = value;
                if (!string.IsNullOrWhiteSpace(key))
                    missingKeyWarningLogged = false;
                if (isActiveAndEnabled && LocalizationManager.IsEnabled)
                    BeginAsyncRefresh();
            }
        }

        /// <summary>
        /// 缓存同一 GameObject 上的文本组件，后续刷新时避免重复查找。
        /// </summary>
        private void Awake()
        {
            CacheTargetComponents();
        }

        /// <summary>
        /// 订阅语言切换事件，并启动一次文本刷新。
        /// </summary>
        private void OnEnable()
        {
            if (!LocalizationManager.IsEnabled)
                return;

            LocalizationManager.OnLocaleChanged += HandleLocaleChanged;
            subscribedToLocaleChanges = true;
            BeginAsyncRefresh();
        }

        /// <summary>
        /// 停止监听语言切换，并取消尚未完成的异步刷新。
        /// </summary>
        private void OnDisable()
        {
            if (subscribedToLocaleChanges)
            {
                LocalizationManager.OnLocaleChanged -= HandleLocaleChanged;
                subscribedToLocaleChanges = false;
            }

            CancelAsyncRefresh();
        }

        /// <summary>
        /// 释放异步刷新使用的取消令牌，防止对象销毁后继续写入 UI。
        /// </summary>
        private void OnDestroy()
        {
            CancelAsyncRefresh();
        }

        /// <summary>
        /// 编辑器修改序列化字段后重新缓存目标组件；运行时不触发编辑器专用刷新。
        /// </summary>
        private void OnValidate()
        {
            if (!Application.isPlaying)
                CacheTargetComponents();
        }

        /// <summary>
        /// 立即使用当前语言刷新绑定文本。
        /// </summary>
        public bool Refresh()
        {
            if (!LocalizationManager.IsEnabled)
                return false;

            if (string.IsNullOrWhiteSpace(key))
            {
                WarnMissingKeyOnce();
                return false;
            }

            missingKeyWarningLogged = false;
            if (!LocalizationManager.EnsureInitialized())
                return false;

            CacheTargetComponents();
            if (!HasTargetComponent())
                return false;

            string value = LocalizationManager.Get(key);
            return ApplyText(value);
        }

        /// <summary>
        /// 异步使用当前语言刷新绑定文本。OnDemandModule 和移动端会通过此入口按需加载分类。
        /// </summary>
        public async UniTask<bool> RefreshAsync(CancellationToken cancellationToken = default)
        {
            if (!LocalizationManager.IsEnabled)
                return false;

            if (string.IsNullOrWhiteSpace(key))
            {
                WarnMissingKeyOnce();
                return false;
            }

            missingKeyWarningLogged = false;
            if (!LocalizationManager.EnsureInitialized())
                return false;

            CacheTargetComponents();
            if (!HasTargetComponent())
                return false;

            string value = await LocalizationManager.GetAsync(key, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            return ApplyText(value);
        }

        /// <summary>
        /// 将查询到的文本和 RTL 排版设置应用到同一对象上的 Text/TMP_Text。
        /// </summary>
        private bool ApplyText(string value)
        {
            bool rightToLeft = LocalizationManager.IsCurrentLocaleRightToLeft;
            bool applied = false;

            if (legacyText != null)
            {
                legacyText.alignment = rightToLeft
                    ? MirrorLegacyAlignment(legacyTextOriginalAlignment)
                    : legacyTextOriginalAlignment;
                legacyText.text = value;
                applied = true;
            }

            if (tmpText != null)
            {
                bool useRightToLeft = rightToLeft || tmpTextOriginalRightToLeft;
                tmpText.isRightToLeftText = useRightToLeft;
                tmpText.alignment = useRightToLeft
                    ? MirrorTmpAlignment(tmpTextOriginalAlignment)
                    : tmpTextOriginalAlignment;
                tmpText.text = value;
                applied = true;
            }

            return applied;
        }

        /// <summary>
        /// 语言切换后启动可取消的异步刷新，避免旧语言请求覆盖新语言结果。
        /// </summary>
        private void HandleLocaleChanged(string previousLocale, string currentLocale)
        {
            if (LocalizationManager.IsEnabled)
                BeginAsyncRefresh();
        }

        /// <summary>
        /// 未配置 Key 时给出一次可定位的 Console 提示，避免语言切换或重复刷新造成日志刷屏。
        /// </summary>
        private void WarnMissingKeyOnce()
        {
            if (missingKeyWarningLogged)
                return;

            missingKeyWarningLogged = true;
            LogUtil.Warn(
                "Localization",
                $"FinkLocalizedText 未绑定本地化 Key：对象={name}。请在 Inspector 中选择有效 Key。");
        }

        /// <summary>
        /// 编辑器内立即刷新；运行时取消上一轮请求并启动新的异步刷新。
        /// </summary>
        private void BeginAsyncRefresh()
        {
            if (!Application.isPlaying)
            {
                Refresh();
                return;
            }

            CancelAsyncRefresh();
            refreshCancellation = new CancellationTokenSource();
            RefreshAsync(refreshCancellation.Token).Forget();
        }

        /// <summary>
        /// 取消并释放当前文本刷新请求。
        /// </summary>
        private void CancelAsyncRefresh()
        {
            if (refreshCancellation == null)
                return;

            refreshCancellation.Cancel();
            refreshCancellation.Dispose();
            refreshCancellation = null;
        }

        /// <summary>
        /// 缓存目标组件，并只记录一次它们的原始对齐设置，供 RTL 切换恢复。
        /// </summary>
        private void CacheTargetComponents()
        {
            if (legacyText == null)
                legacyText = GetComponent<Text>();
            if (legacyText != null && !legacyTextOriginalAlignmentCaptured)
            {
                legacyTextOriginalAlignment = legacyText.alignment;
                legacyTextOriginalAlignmentCaptured = true;
            }

            TMP_Text currentTmpText = GetComponent<TMP_Text>();
            if (tmpText != currentTmpText)
            {
                tmpText = currentTmpText;
                tmpTextOriginalAlignmentCaptured = false;
            }

            if (tmpText != null && !tmpTextOriginalAlignmentCaptured)
            {
                tmpTextOriginalAlignment = tmpText.alignment;
                tmpTextOriginalRightToLeft = tmpText.isRightToLeftText;
                tmpTextOriginalAlignmentCaptured = true;
            }
        }

        /// <summary>
        /// 将 Unity UI Text 的左右对齐方式镜像到 RTL 方向。
        /// </summary>
        private static TextAnchor MirrorLegacyAlignment(TextAnchor alignment)
        {
            switch (alignment)
            {
                case TextAnchor.UpperLeft:
                    return TextAnchor.UpperRight;
                case TextAnchor.UpperRight:
                    return TextAnchor.UpperLeft;
                case TextAnchor.MiddleLeft:
                    return TextAnchor.MiddleRight;
                case TextAnchor.MiddleRight:
                    return TextAnchor.MiddleLeft;
                case TextAnchor.LowerLeft:
                    return TextAnchor.LowerRight;
                case TextAnchor.LowerRight:
                    return TextAnchor.LowerLeft;
                default:
                    return alignment;
            }
        }

        /// <summary>
        /// 将 TextMeshPro 的左右对齐方式镜像到 RTL 方向。
        /// </summary>
        private static TextAlignmentOptions MirrorTmpAlignment(TextAlignmentOptions alignment)
        {
            switch (alignment)
            {
                case TextAlignmentOptions.TopLeft:
                    return TextAlignmentOptions.TopRight;
                case TextAlignmentOptions.TopRight:
                    return TextAlignmentOptions.TopLeft;
                case TextAlignmentOptions.Left:
                    return TextAlignmentOptions.Right;
                case TextAlignmentOptions.Right:
                    return TextAlignmentOptions.Left;
                case TextAlignmentOptions.BottomLeft:
                    return TextAlignmentOptions.BottomRight;
                case TextAlignmentOptions.BottomRight:
                    return TextAlignmentOptions.BottomLeft;
                case TextAlignmentOptions.BaselineLeft:
                    return TextAlignmentOptions.BaselineRight;
                case TextAlignmentOptions.BaselineRight:
                    return TextAlignmentOptions.BaselineLeft;
                case TextAlignmentOptions.MidlineLeft:
                    return TextAlignmentOptions.MidlineRight;
                case TextAlignmentOptions.MidlineRight:
                    return TextAlignmentOptions.MidlineLeft;
                case TextAlignmentOptions.CaplineLeft:
                    return TextAlignmentOptions.CaplineRight;
                case TextAlignmentOptions.CaplineRight:
                    return TextAlignmentOptions.CaplineLeft;
                default:
                    return alignment;
            }
        }

        /// <summary>
        /// 判断当前对象是否至少绑定了一个可应用本地化文本的目标组件。
        /// </summary>
        private bool HasTargetComponent()
        {
            if (legacyText != null)
                return true;
            if (tmpText != null)
                return true;
            return false;
        }

    }
}
