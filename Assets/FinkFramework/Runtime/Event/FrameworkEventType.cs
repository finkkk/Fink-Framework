namespace FinkFramework.Runtime.Event
{
    /// <summary>
    /// 框架内部事件（仅供 FinkFramework Runtime 使用）
    /// 不允许业务层依赖或扩展
    /// </summary>
    public enum FrameworkEventType
    {
        #region 旧版输入系统事件（仅当开启输入模块时有效）
        /// <summary>
        /// 水平热键监听（-1~1）
        /// </summary>
        E_Input_Horizontal,
        /// <summary>
        /// 垂直热键监听（-1~1）
        /// </summary>
        E_Input_Vertical,
        #endregion
    }
}