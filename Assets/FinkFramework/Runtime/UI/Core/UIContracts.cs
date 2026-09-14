using System;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace FinkFramework.Runtime.UI
{
    /// <summary>面板在一个 UI Surface 内的显示层级。</summary>
    public enum UILayer
    {
        Bottom,
        Middle,
        Top,
        System
    }

    /// <summary>面板实例关闭后的保留策略。</summary>
    public enum UICachePolicy
    {
        KeepAlive,
        DestroyOnClose
    }

    /// <summary>面板是否跟随创建它的场景一起释放。</summary>
    public enum UIPanelLifetime
    {
        /// <summary>所属场景卸载时自动关闭并释放。</summary>
        Scene,
        /// <summary>跨场景保留，直到显式关闭、释放或清空全部 UI。</summary>
        Persistent
    }

    /// <summary>UI Surface 与场景之间的生命周期关系。</summary>
    public enum UISurfaceLifetime
    {
        /// <summary>Canvas 所属场景卸载时注销 Surface 并释放其面板。</summary>
        Scene,
        /// <summary>不随场景卸载自动注销；承载对象本身也应放入 DontDestroyOnLoad。</summary>
        Persistent
    }

    /// <summary>面板与导航、焦点之间的关系。</summary>
    public enum UIPresentationMode
    {
        /// <summary>参与 Surface 的页面栈，打开时暂停上一页。</summary>
        Page,
        /// <summary>显示在当前页面之上，并阻止下层接收交互。</summary>
        Modal,
        /// <summary>不进入页面栈，也不阻止下层交互。</summary>
        Overlay
    }

    /// <summary>页面打开时对当前 Surface 导航栈采用的操作。</summary>
    public enum UINavigationMode
    {
        /// <summary>压入栈顶；返回时恢复此前页面。</summary>
        Push,
        /// <summary>替换当前栈顶；此前页面不再参与返回。</summary>
        Replace,
        /// <summary>若目标已在栈中，关闭其上方页面并返回目标；否则按 Push 处理。</summary>
        PopTo,
        /// <summary>清空当前 Surface 的其他页面，只保留并打开目标页面。</summary>
        Reset
    }

    /// <summary>面板实例的运行时状态。</summary>
    public enum UIPanelState
    {
        Loading,
        Hidden,
        Opening,
        Active,
        Paused,
        Closing,
        Disposed,
        Failed
    }

    /// <summary>当前 UI 的主要操作设备类型。</summary>
    public enum UIInputMode
    {
        Pointer,
        Navigation
    }

    /// <summary>面板类型的稳定标识，不再使用临时拼接字符串作为字典 Key。</summary>
    public readonly struct UIPanelId : IEquatable<UIPanelId>
    {
        public string Value { get; }

        public UIPanelId(string value)
        {
            Value = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        public static UIPanelId From<T>() => new(typeof(T).FullName ?? typeof(T).Name);

        public bool Equals(UIPanelId other) =>
            string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is UIPanelId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
    }

    /// <summary>UI 挂载表面的标识。Main 是框架创建的默认屏幕 UI。</summary>
    public readonly struct UISurfaceId : IEquatable<UISurfaceId>
    {
        public const string MainValue = "Main";

        public string Value => string.IsNullOrWhiteSpace(value) ? MainValue : value;
        private readonly string value;

        public static UISurfaceId Main => new(MainValue);

        public UISurfaceId(string value)
        {
            this.value = string.IsNullOrWhiteSpace(value) ? MainValue : value.Trim();
        }

        public bool Equals(UISurfaceId other) =>
            string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is UISurfaceId other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value;

        public static implicit operator UISurfaceId(string value) => new(value);
    }

    /// <summary>同类型面板的实例标识。默认值表示该类型在 Surface 上的主实例。</summary>
    public readonly struct UIInstanceId : IEquatable<UIInstanceId>
    {
        public const string DefaultValue = "Default";

        public string Value => string.IsNullOrWhiteSpace(value) ? DefaultValue : value;
        private readonly string value;

        public static UIInstanceId Default => new(DefaultValue);

        public UIInstanceId(string value)
        {
            this.value = string.IsNullOrWhiteSpace(value) ? DefaultValue : value.Trim();
        }

        public bool Equals(UIInstanceId other) =>
            string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is UIInstanceId other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value;

        public static implicit operator UIInstanceId(string value) => new(value);
    }

    /// <summary>一个面板实例的完整身份：面板、实例和 Surface 三部分彼此独立。</summary>
    public readonly struct UIPanelKey : IEquatable<UIPanelKey>
    {
        public UIPanelId PanelId { get; }
        public UIInstanceId InstanceId { get; }
        public UISurfaceId SurfaceId { get; }

        public UIPanelKey(UIPanelId panelId, UIInstanceId instanceId, UISurfaceId surfaceId)
        {
            PanelId = panelId;
            InstanceId = instanceId;
            SurfaceId = surfaceId;
        }

        public bool Equals(UIPanelKey other) =>
            PanelId.Equals(other.PanelId)
            && InstanceId.Equals(other.InstanceId)
            && SurfaceId.Equals(other.SurfaceId);

        public override bool Equals(object obj) => obj is UIPanelKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = PanelId.GetHashCode();
                hash = (hash * 397) ^ InstanceId.GetHashCode();
                hash = (hash * 397) ^ SurfaceId.GetHashCode();
                return hash;
            }
        }

        public override string ToString() => $"{SurfaceId}/{PanelId}#{InstanceId}";
    }

    /// <summary>供运行时调试器和自动化测试读取的面板只读快照。</summary>
    public readonly struct UIPanelSnapshot
    {
        public UIPanelKey Key { get; }
        public UIPanelState State { get; }
        public UIOpenOptions Options { get; }
        public string AssetPath { get; }
        public string GameObjectName { get; }
        public bool ActiveInHierarchy { get; }
        public UIPanelLifetime Lifetime { get; }
        public string OwnerSceneName { get; }

        internal UIPanelSnapshot(
            UIPanelKey key,
            UIPanelState state,
            UIOpenOptions options,
            string assetPath,
            string gameObjectName,
            bool activeInHierarchy,
            UIPanelLifetime lifetime,
            string ownerSceneName)
        {
            Key = key;
            State = state;
            Options = options;
            AssetPath = assetPath ?? string.Empty;
            GameObjectName = gameObjectName ?? string.Empty;
            ActiveInHierarchy = activeInHierarchy;
            Lifetime = lifetime;
            OwnerSceneName = ownerSceneName ?? string.Empty;
        }
    }

    /// <summary>供调试器读取的 UI Surface 只读快照。</summary>
    public readonly struct UISurfaceSnapshot
    {
        public UISurfaceId Id { get; }
        public UISurfaceLifetime Lifetime { get; }
        public string OwnerSceneName { get; }
        public bool IsWorldSpace { get; }
        public bool IsActive { get; }

        internal UISurfaceSnapshot(
            UISurfaceId id,
            UISurfaceLifetime lifetime,
            string ownerSceneName,
            bool isWorldSpace,
            bool isActive)
        {
            Id = id;
            Lifetime = lifetime;
            OwnerSceneName = ownerSceneName ?? string.Empty;
            IsWorldSpace = isWorldSpace;
            IsActive = isActive;
        }
    }

    /// <summary>打开面板时使用的结构化选项。</summary>
    public readonly struct UIOpenOptions
    {
        private readonly bool initialized;

        public UILayer Layer { get; }
        public UISurfaceId SurfaceId { get; }
        public UIInstanceId InstanceId { get; }
        public UICachePolicy CachePolicy { get; }
        public UIPresentationMode Presentation { get; }
        public bool TakeFocus { get; }
        public UIPanelLifetime Lifetime { get; }
        public UINavigationMode Navigation { get; }

        public static UIOpenOptions Default => new(
            UILayer.Middle,
            UISurfaceId.Main,
            UIInstanceId.Default,
            UICachePolicy.KeepAlive,
            UIPresentationMode.Page,
            true,
            UIPanelLifetime.Scene,
            UINavigationMode.Push);

        /// <summary>替换当前页面的常用选项。</summary>
        public static UIOpenOptions ReplacePage =>
            Default.WithNavigation(UINavigationMode.Replace);

        /// <summary>返回到指定类型页面的常用选项。</summary>
        public static UIOpenOptions PopToPage =>
            Default.WithNavigation(UINavigationMode.PopTo);

        /// <summary>以指定类型页面重置导航栈的常用选项。</summary>
        public static UIOpenOptions ResetPage =>
            Default.WithNavigation(UINavigationMode.Reset);

        public static UIOpenOptions Modal => new(
            UILayer.Top,
            UISurfaceId.Main,
            UIInstanceId.Default,
            UICachePolicy.KeepAlive,
            UIPresentationMode.Modal,
            true,
            UIPanelLifetime.Scene,
            UINavigationMode.Push);

        public static UIOpenOptions Overlay => new(
            UILayer.Top,
            UISurfaceId.Main,
            UIInstanceId.Default,
            UICachePolicy.KeepAlive,
            UIPresentationMode.Overlay,
            false,
            UIPanelLifetime.Scene,
            UINavigationMode.Push);

        public UIOpenOptions(
            UILayer layer = UILayer.Middle,
            UISurfaceId surfaceId = default,
            UIInstanceId instanceId = default,
            UICachePolicy cachePolicy = UICachePolicy.KeepAlive,
            UIPresentationMode presentation = UIPresentationMode.Page,
            bool takeFocus = true,
            UIPanelLifetime lifetime = UIPanelLifetime.Scene,
            UINavigationMode navigation = UINavigationMode.Push)
        {
            initialized = true;
            Layer = layer;
            SurfaceId = surfaceId;
            InstanceId = instanceId;
            CachePolicy = cachePolicy;
            Presentation = presentation;
            TakeFocus = takeFocus;
            Lifetime = lifetime;
            Navigation = navigation;
        }

        internal UIOpenOptions Normalize() => initialized ? this : Default;

        public UIOpenOptions WithLayer(UILayer layer)
        {
            UIOpenOptions value = Normalize();
            return new UIOpenOptions(
                layer,
                value.SurfaceId,
                value.InstanceId,
                value.CachePolicy,
                value.Presentation,
                value.TakeFocus,
                value.Lifetime,
                value.Navigation);
        }

        public UIOpenOptions WithSurface(UISurfaceId surfaceId)
        {
            UIOpenOptions value = Normalize();
            return new UIOpenOptions(
                value.Layer,
                surfaceId,
                value.InstanceId,
                value.CachePolicy,
                value.Presentation,
                value.TakeFocus,
                value.Lifetime,
                value.Navigation);
        }

        public UIOpenOptions WithInstance(UIInstanceId instanceId)
        {
            UIOpenOptions value = Normalize();
            return new UIOpenOptions(
                value.Layer,
                value.SurfaceId,
                instanceId,
                value.CachePolicy,
                value.Presentation,
                value.TakeFocus,
                value.Lifetime,
                value.Navigation);
        }

        public UIOpenOptions WithCachePolicy(UICachePolicy cachePolicy)
        {
            UIOpenOptions value = Normalize();
            return new UIOpenOptions(
                value.Layer,
                value.SurfaceId,
                value.InstanceId,
                cachePolicy,
                value.Presentation,
                value.TakeFocus,
                value.Lifetime,
                value.Navigation);
        }

        public UIOpenOptions WithPresentation(UIPresentationMode presentation)
        {
            UIOpenOptions value = Normalize();
            return new UIOpenOptions(
                value.Layer,
                value.SurfaceId,
                value.InstanceId,
                value.CachePolicy,
                presentation,
                value.TakeFocus,
                value.Lifetime,
                value.Navigation);
        }

        public UIOpenOptions WithFocus(bool takeFocus)
        {
            UIOpenOptions value = Normalize();
            return new UIOpenOptions(
                value.Layer,
                value.SurfaceId,
                value.InstanceId,
                value.CachePolicy,
                value.Presentation,
                takeFocus,
                value.Lifetime,
                value.Navigation);
        }

        public UIOpenOptions WithLifetime(UIPanelLifetime lifetime)
        {
            UIOpenOptions value = Normalize();
            return new UIOpenOptions(
                value.Layer,
                value.SurfaceId,
                value.InstanceId,
                value.CachePolicy,
                value.Presentation,
                value.TakeFocus,
                lifetime,
                value.Navigation);
        }

        /// <summary>设置页面导航行为；Modal 和 Overlay 会忽略此选项。</summary>
        public UIOpenOptions WithNavigation(UINavigationMode navigation)
        {
            UIOpenOptions value = Normalize();
            return new UIOpenOptions(
                value.Layer,
                value.SurfaceId,
                value.InstanceId,
                value.CachePolicy,
                value.Presentation,
                value.TakeFocus,
                value.Lifetime,
                navigation);
        }
    }
}
