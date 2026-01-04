namespace FinkFramework.Runtime.UI.Base
{
    /// <summary>
    /// UI 面板参数初始化能力接口（可选实现）
    /// ------------------------------------------------
    /// - UIManager 不关心参数内容 面板按需实现 每次 ShowPanel 都会重新调用
    /// - 用于在执行控制面板函数(比如ShowPanel)的时候同时传入参数进行初始化
    /// </summary>
    /// <typeparam name="TParam">参数类型</typeparam>
    public interface IPanelParam<in TParam>
    {
        /// <summary>
        /// 设置面板初始化参数
        /// 调用时机：
        /// - 面板实例已存在
        /// - 在 ShowMe / OnShow 之前
        /// </summary>
        void SetParam(TParam param);
    }
}