using System;
using System.Collections.Generic;
using FinkFramework.Runtime.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FinkFramework.Runtime.UI.Base
{
    /// <summary>
    /// 所有 UI 面板的基类。
    /// Unity 的 Awake/OnDestroy 只负责组件本身，业务生命周期由 UIManager 统一驱动。
    /// </summary>
    public abstract class BasePanel : MonoBehaviour
    {
        private static readonly HashSet<string> TemplateControlNames = new(StringComparer.Ordinal)
        {
            "Image", "Text", "Text (TMP)", "RawImage", "Background", "Checkmark", "Label",
            "Text (Legacy)", "Arrow", "Placeholder", "Fill", "Handle", "Viewport",
            "Scrollbar Horizontal", "Scrollbar Vertical"
        };

        // 按“组件类型 + 对象名”索引，避免同一对象上的 Button、Image 因重名互相覆盖。
        private readonly Dictionary<Type, Dictionary<string, UIBehaviour>> controls = new();
        [Header("导航焦点")]
        [Tooltip("使用键盘或手柄进入面板时优先选中的控件。留空会自动选择第一个可交互控件。")]
        [SerializeField] private Selectable defaultSelectable;
        private bool initialized;
        private bool disposed;

        public UIPanelContext Context { get; private set; }

        internal event Action<BasePanel> Destroyed;

        protected virtual void Awake()
        {
            RebuildControlCache();
        }

        /// <summary>面板实例创建后只调用一次。适合建立长期监听和缓存引用。</summary>
        protected virtual void OnInitialize(UIPanelContext context) { }

        /// <summary>面板从关闭状态进入显示状态时调用。</summary>
        protected virtual void OnEnter() { }

        /// <summary>导航中被上层页面临时覆盖时调用。</summary>
        protected virtual void OnPause() { }

        /// <summary>上层页面退出、当前页面恢复交互时调用。</summary>
        protected virtual void OnResume() { }

        /// <summary>面板开始关闭时调用。</summary>
        protected virtual void OnExit() { }

        /// <summary>面板实例销毁前只调用一次。适合取消长期监听并释放引用。</summary>
        protected virtual void OnDispose() { }

        /// <summary>
        /// 手柄或键盘导航进入此面板时优先选中的控件。
        /// 可在 Inspector 指定，也可以在子类中动态覆盖。
        /// </summary>
        protected virtual Selectable GetDefaultSelectable() => defaultSelectable;

        /// <summary>获取指定名称和类型的控件；未找到时记录错误并返回 null。</summary>
        public T GetControl<T>(string controlName) where T : UIBehaviour
        {
            if (TryGetControl(controlName, out T control))
                return control;

            LogUtil.Error(
                "UI",
                $"{name} 中不存在名为 {controlName}、类型为 {typeof(T).Name} 的控件。");
            return null;
        }

        /// <summary>尝试获取指定名称和类型的控件，不输出错误日志。</summary>
        public bool TryGetControl<T>(string controlName, out T control) where T : UIBehaviour
        {
            control = null;
            if (string.IsNullOrWhiteSpace(controlName))
                return false;

            if (!controls.TryGetValue(typeof(T), out Dictionary<string, UIBehaviour> typedControls))
                return false;

            if (!typedControls.TryGetValue(controlName, out UIBehaviour value))
                return false;

            control = value as T;
            return control;
        }

        protected virtual void OnButtonClicked(string controlName) { }
        protected virtual void OnSliderValueChanged(string controlName, float value) { }
        protected virtual void OnToggleValueChanged(string controlName, bool value) { }
        protected virtual void OnInputValueChanged(string controlName, string value) { }

        internal void InitializeInternal(UIPanelContext context)
        {
            if (initialized)
                return;

            Context = context ?? throw new ArgumentNullException(nameof(context));
            initialized = true;
            OnInitialize(context);
        }

        internal void EnterInternal() => OnEnter();
        internal void PauseInternal() => OnPause();
        internal void ResumeInternal() => OnResume();
        internal void ExitInternal() => OnExit();

        internal void DisposeInternal()
        {
            if (disposed)
                return;

            disposed = true;
            Context?.Dispose();
            OnDispose();
        }

        internal Selectable ResolveDefaultSelectableInternal()
        {
            Selectable preferred = GetDefaultSelectable();
            if (IsSelectableUsable(preferred))
                return preferred;

            foreach (Selectable selectable in GetComponentsInChildren<Selectable>(true))
            {
                if (IsSelectableUsable(selectable))
                    return selectable;
            }

            return null;
        }

        private bool IsSelectableUsable(Selectable selectable)
        {
            return selectable
                   && selectable.transform.IsChildOf(transform)
                   && selectable.gameObject.activeInHierarchy
                   && selectable.IsInteractable()
                   && selectable.navigation.mode != UnityEngine.UI.Navigation.Mode.None;
        }

        private void OnDestroy()
        {
            DisposeInternal();
            Destroyed?.Invoke(this);
            Destroyed = null;
        }

        private void RebuildControlCache()
        {
            controls.Clear();

            RegisterControls<Button>();
            RegisterControls<Toggle>();
            RegisterControls<Slider>();
            RegisterControls<Scrollbar>();
            RegisterControls<InputField>();
            RegisterControls<TMP_InputField>();
            RegisterControls<Dropdown>();
            RegisterControls<TMP_Dropdown>();
            RegisterControls<ScrollRect>();
            RegisterControls<ToggleGroup>();
            RegisterControls<Text>();
            RegisterControls<TextMeshProUGUI>();
            RegisterControls<Image>();
            RegisterControls<RawImage>();
            RegisterControls<VerticalLayoutGroup>();
            RegisterControls<HorizontalLayoutGroup>();
            RegisterControls<GridLayoutGroup>();
        }

        private void RegisterControls<T>() where T : UIBehaviour
        {
            var typedControls = new Dictionary<string, UIBehaviour>(StringComparer.Ordinal);
            controls[typeof(T)] = typedControls;

            foreach (T control in GetComponentsInChildren<T>(true))
            {
                string controlName = control.gameObject.name;
                if (TemplateControlNames.Contains(controlName))
                    continue;

                if (!typedControls.TryAdd(controlName, control))
                {
                    LogUtil.Warn(
                        "UI",
                        $"{name} 中存在重复控件：{typeof(T).Name} {controlName}。"
                        + "GetControl 将使用层级中最先找到的对象。");
                    continue;
                }

                BindBuiltInEvent(control, controlName);
            }
        }

        private void BindBuiltInEvent(UIBehaviour control, string controlName)
        {
            switch (control)
            {
                case Button button:
                    button.onClick.AddListener(() => OnButtonClicked(controlName));
                    break;
                case Slider slider:
                    slider.onValueChanged.AddListener(value => OnSliderValueChanged(controlName, value));
                    break;
                case Toggle toggle:
                    toggle.onValueChanged.AddListener(value => OnToggleValueChanged(controlName, value));
                    break;
                case InputField input:
                    input.onValueChanged.AddListener(value => OnInputValueChanged(controlName, value));
                    break;
                case TMP_InputField tmpInput:
                    tmpInput.onValueChanged.AddListener(value => OnInputValueChanged(controlName, value));
                    break;
            }
        }
    }
}
