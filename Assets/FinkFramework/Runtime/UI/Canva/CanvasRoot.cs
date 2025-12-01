using UnityEngine;

namespace FinkFramework.Runtime.UI.Canva
{
    /// <summary>
    /// 标记一个可被 UIManager 管理的“UI根画布”。
    /// 用途：
    /// - 在场景中声明不同类型的 UI Canvas（HUD / World Canvas / HandMenu 等）
    /// - 由 UIManager 自动扫描并注册，使得 ShowPanelAsync 能知道面板挂在哪个画布下
    /// 注意：
    /// - canvasId 必须唯一（同一个 rootType 下不能重复）
    /// - panelParent 如果为空，自动使用 Canvas.transform
    /// 使用示例：
    ///   LeftHandMenuCanvas:
    ///       rootType = HandMenu
    ///       canvasId = "LeftHand"
    ///       panelParent = (自动绑定)
    /// </summary>
    public class CanvasRoot : MonoBehaviour
    {
        public E_UIRoot rootType;        // HUD / HandMenu / WorldPanel
        public Transform panelParent;    // 可选，指定挂面板的父节点
        public string canvasId;          // 可选唯一标识，自定义命名，不可重复，例如 "LeftHand", "QuestPanel1"
        
        private void Reset()
        {
            // 在 Inspector 拖拽时，自动用Canvas自身作为父节点
            if (!panelParent)
                panelParent = GetComponent<Canvas>()?.transform ?? transform;
            // 在 Inspector 拖拽时，自动用对象名作为默认 ID
            if (string.IsNullOrEmpty(canvasId))
                canvasId = gameObject.name;
        }
    }
}