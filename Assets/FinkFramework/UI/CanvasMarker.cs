using UnityEngine;

namespace FinkFramework.UI
{
    public class CanvasMarker : MonoBehaviour
    {
        public E_UIRoot rootType;        // HUD / HandMenu / WorldPanel
        public Transform panelParent;    // 可选，指定挂面板的父节点
        public string canvasId;          // 可选唯一标识，例如 "LeftHand", "QuestPanel1"
        
        private void Reset()
        {
            // 在 Inspector 拖拽时，自动用Canvas
            if (!panelParent)
                panelParent = GetComponent<Canvas>()?.transform ?? transform;
            // 在 Inspector 拖拽时，自动用对象名作为默认 ID
            if (string.IsNullOrEmpty(canvasId))
                canvasId = gameObject.name;
        }
    }
}