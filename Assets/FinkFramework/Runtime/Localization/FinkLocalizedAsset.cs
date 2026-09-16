using UnityEngine;
using FinkFramework.Runtime.Utils;

namespace FinkFramework.Runtime.Localization
{
    /// <summary>
    /// 所有本地化资源绑定组件的非泛型基类，便于编辑器统一处理。
    /// </summary>
    public abstract class FinkLocalizedAsset : MonoBehaviour
    {
        /// <summary>绑定的资源本地化 Key。</summary>
        public abstract string Key { get; set; }

        /// <summary>立即按当前语言刷新目标资源。</summary>
        public abstract bool Refresh();
    }

    /// <summary>
    /// 将本地化资源 Key 绑定到 Unity 资源目标。
    /// 具体组件只负责把查到的资源应用到目标组件，资源查找统一由 LocalizationManager 完成。
    /// </summary>
    public abstract class FinkLocalizedAsset<T> : FinkLocalizedAsset where T : Object
    {
        [SerializeField]
        [Tooltip("资源本地化表中的完整 Key，例如 ui.main_menu.logo。")]
        private string key;
        private bool missingKeyWarningLogged;
        private bool subscribedToLocaleChanges;

        /// <summary>
        /// 绑定的资源本地化 Key。
        /// </summary>
        public override string Key
        {
            get => key;
            set
            {
                key = value;
                if (!string.IsNullOrWhiteSpace(key))
                    missingKeyWarningLogged = false;
                if (isActiveAndEnabled && LocalizationManager.IsEnabled)
                    Refresh();
            }
        }

        /// <summary>
        /// 订阅语言切换事件，并立即应用当前语言的资源。
        /// </summary>
        protected virtual void OnEnable()
        {
            if (!LocalizationManager.IsEnabled)
                return;

            LocalizationManager.OnLocaleChanged += HandleLocaleChanged;
            subscribedToLocaleChanges = true;
            Refresh();
        }

        /// <summary>
        /// 取消语言切换订阅，避免禁用对象继续接收刷新通知。
        /// </summary>
        protected virtual void OnDisable()
        {
            if (!subscribedToLocaleChanges)
                return;

            LocalizationManager.OnLocaleChanged -= HandleLocaleChanged;
            subscribedToLocaleChanges = false;
        }

        /// <summary>
        /// 立即使用当前语言刷新资源目标。
        /// </summary>
        public override bool Refresh()
        {
            // 模块关闭时保留场景或预制体原有资源，不应清空目标组件。
            if (!LocalizationManager.IsEnabled)
                return false;

            if (string.IsNullOrWhiteSpace(key))
            {
                WarnMissingKeyOnce();
                ApplyAsset(null);
                return false;
            }

            missingKeyWarningLogged = false;
            if (!LocalizationManager.EnsureInitialized())
            {
                ApplyAsset(null);
                return false;
            }

            T asset = LocalizationManager.GetAsset<T>(key);
            ApplyAsset(asset);
            return asset != null;
        }

        /// <summary>
        /// 响应语言切换事件，重新查询并应用当前语言的资源。
        /// </summary>
        private void HandleLocaleChanged(string previousLocale, string currentLocale)
        {
            Refresh();
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
                $"{GetType().Name} 未绑定资源本地化 Key：对象={name}。运行时会清空目标资源，请在 Inspector 中选择有效 Key。");
        }

        /// <summary>
        /// 将已解析的资源应用到具体 Unity 组件。
        /// 当 Key 为空或资源缺失时会传入 null，派生类应同步清空目标引用。
        /// </summary>
        protected abstract void ApplyAsset(T asset);
    }

}
