namespace FinkFramework.Runtime.Event 
{
    /// <summary>
    /// 事件类型 枚举
    /// </summary>
    public enum E_EventType 
    {
        /// <summary>
        /// 测试用事件 —— 参数：无
        /// </summary>
        E_Test,
        /// <summary>
        /// 场景切换时的进度条变化获取
        /// </summary>
        E_SceneLoadChange,
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
