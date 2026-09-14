using UnityEngine;

namespace FinkFramework.Runtime.UI.Modal
{
    /// <summary>
    /// Modal 面板的可选遮罩配置。挂在面板根节点上，仅在以 Modal 方式打开时生效。
    /// 不挂载时仍会创建透明遮罩并拦截点击，保持框架默认行为。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UIModalBackdrop : MonoBehaviour
    {
        [Header("视觉效果")]
        [SerializeField] private Color color = new(0f, 0f, 0f, 0.45f);

        [Header("交互")]
        [Tooltip("点击面板外的遮罩时，等同于业务主动调用 UIManager.Back()。"
                 + "若面板实现 IUIBackHandler，仍会优先由面板决定是否消费该请求。")]
        [SerializeField] private bool closeOnClick;

        internal Color Color => color;
        internal bool CloseOnClick => closeOnClick;
    }
}
