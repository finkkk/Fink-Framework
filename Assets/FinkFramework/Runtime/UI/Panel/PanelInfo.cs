using FinkFramework.Runtime.UI.Base;

namespace FinkFramework.Runtime.UI.Panel
{
    /// <summary>
    /// 泛型 Panel 信息。用于存储面板实例与状态
    /// </summary>
    /// <typeparam name="T">面板的类型</typeparam>
    public class PanelInfo<T> : BasePanelInfo where T:BasePanel
    {
        // 是否初始化
        public bool isInit = false;
        public new T panel  // 泛型 panel，隐藏基类
        {
            get => base.panel as T;
            set => base.panel = value;
        }
    }
}