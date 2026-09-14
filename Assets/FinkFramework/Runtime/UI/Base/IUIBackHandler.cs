namespace FinkFramework.Runtime.UI.Base
{
    /// <summary>
    /// 可选的返回处理能力。
    /// 当业务显式调用 <see cref="UIManager.Back"/> 时，栈顶面板可先处理返回请求。
    /// 返回 <c>true</c> 表示请求已被面板消费，UIManager 不会自动关闭该面板；
    /// 返回 <c>false</c> 则继续执行默认的关闭和页面恢复逻辑。
    /// </summary>
    public interface IUIBackHandler
    {
        bool TryHandleBack();
    }
}
