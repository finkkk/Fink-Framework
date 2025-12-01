using UnityEngine;

namespace FinkFramework.Runtime.UI.Canva
{
        
    /// <summary>
    /// 每个 CanvasInfo 的封装
    /// </summary>
    public class CanvasInfo
    {
        public E_UIRoot type;
        public Canvas canvas;
        public string canvasId;
        public Transform panelParent; // 用于放置面板的根节点
    }
}