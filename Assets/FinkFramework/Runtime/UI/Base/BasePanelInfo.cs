using UnityEngine;

namespace FinkFramework.Runtime.UI.Base
{
    /// <summary>
    /// 主要用于里式替换原则 在字典中 用父类容器装载子类对象
    /// </summary>
    public abstract class BasePanelInfo
    {
        public Canvas rootCanvas;
        public BasePanel panel;
        public bool isHide;
    }
}