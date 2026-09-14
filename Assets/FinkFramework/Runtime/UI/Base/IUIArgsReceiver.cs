namespace FinkFramework.Runtime.UI.Base
{
    /// <summary>
    /// 面板需要接收打开参数时实现此接口。泛型约束会在编译期保证参数与面板匹配。
    /// </summary>
    public interface IUIArgsReceiver<in TArgs>
    {
        void ApplyArgs(TArgs args);
    }
}
