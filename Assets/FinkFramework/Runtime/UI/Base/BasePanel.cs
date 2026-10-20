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
        // 保留同类型同名的全部控件，顺序与层级扫描一致；默认名称和内部组件同样参与注册。
        private readonly Dictionary<Type, Dictionary<string, List<UIBehaviour>>> controls = new();
        private readonly HashSet<(Type Type, string Name, Transform Parent)> warnedControlLookups = new();
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

        /// <summary>尝试按名称获取控件；歧义时返回第一个存活对象，同一歧义只警告一次。</summary>
        public bool TryGetControl<T>(string controlName, out T control) where T : UIBehaviour
            => TryGetControl(controlName, transform, out control);

        /// <summary>在指定父节点及其全部后代中按名称获取控件；父节点必须位于当前面板内。</summary>
        public T GetControl<T>(string controlName, Transform parent) where T : UIBehaviour
        {
            if (TryGetControl(controlName, parent, out T control))
                return control;

            LogUtil.Error("UI", $"{name} 的指定父节点范围内不存在名为 {controlName}、类型为 {typeof(T).Name} 的控件。");
            return null;
        }

        /// <summary>限定父节点范围的名称查找；范围内仍有多个匹配时只警告一次并返回第一个。</summary>
        public bool TryGetControl<T>(string controlName, Transform parent, out T control) where T : UIBehaviour
        {
            control = null;
            if (string.IsNullOrWhiteSpace(controlName) || !parent || !parent.IsChildOf(transform))
                return false;

            if (!controls.TryGetValue(typeof(T), out Dictionary<string, List<UIBehaviour>> typedControls))
                return false;

            if (!typedControls.TryGetValue(controlName, out List<UIBehaviour> matches))
                return false;

            int matchCount = 0;
            foreach (UIBehaviour candidate in matches)
            {
                if (!candidate || !candidate.transform.IsChildOf(parent))
                    continue;

                if (matchCount++ == 0)
                    control = candidate as T;
            }

            if (matchCount > 1 && warnedControlLookups.Add((typeof(T), controlName, parent)))
            {
                LogUtil.Warn(
                    "UI",
                    $"{name} 中按名称查找 {typeof(T).Name} {controlName} 存在歧义："
                    + $"在 {parent.name} 范围内找到 {matchCount} 个控件，将返回层级扫描中的第一个。"
                    + "请使用 GetControlByPath 或指定更精确的父节点。");
            }

            return control;
        }

        /// <summary>按相对当前面板的层级路径获取控件，例如 Content/ConfirmButton。</summary>
        public T GetControlByPath<T>(string controlPath) where T : UIBehaviour
        {
            if (TryGetControlByPath(controlPath, out T control))
                return control;

            LogUtil.Error("UI", $"{name} 中路径 {controlPath} 不存在类型为 {typeof(T).Name} 的控件。");
            return null;
        }

        /// <summary>按 Transform.Find 的相对路径规则查找（支持未激活节点），不输出错误日志。</summary>
        public bool TryGetControlByPath<T>(string controlPath, out T control) where T : UIBehaviour
        {
            control = null;
            if (string.IsNullOrWhiteSpace(controlPath) || controlPath.StartsWith("/", StringComparison.Ordinal))
                return false;

            Transform target = transform.Find(controlPath);
            if (!target || !target.IsChildOf(transform))
                return false;

            control = target.GetComponent<T>();
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
            warnedControlLookups.Clear();

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
            var typedControls = new Dictionary<string, List<UIBehaviour>>(StringComparer.Ordinal);
            controls[typeof(T)] = typedControls;

            foreach (T control in GetComponentsInChildren<T>(true))
            {
                string controlName = control.gameObject.name;
                if (!typedControls.TryGetValue(controlName, out List<UIBehaviour> matches))
                {
                    matches = new List<UIBehaviour>();
                    typedControls.Add(controlName, matches);
                }

                matches.Add(control);
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
