using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FinkFramework.Runtime.UI.Modal
{
    /// <summary>运行时遮罩的点击转发器；不暴露给业务层，也不依赖 Button 的颜色过渡。</summary>
    internal sealed class UIModalBackdropClickRelay : MonoBehaviour, IPointerClickHandler
    {
        private Action clicked;

        public void Set(Action action) => clicked = action;

        public void OnPointerClick(PointerEventData eventData) => clicked?.Invoke();
    }
}
