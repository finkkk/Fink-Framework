using FinkFramework.Runtime.UI.Core;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace FinkFramework.Runtime.UI.Modal
{
    /// <summary>为 Modal 创建全区域遮罩，负责点击拦截、可选视觉效果与可选的点击返回。</summary>
    internal sealed class UIModalBackdropController
    {
        public void Show(UIPanelRecord record)
        {
            if (record?.Panel == null)
                return;

            if (!record.ModalBackdrop)
                record.ModalBackdrop = Create(record);

            GameObject backdrop = record.ModalBackdrop;
            Transform panelTransform = record.Panel.transform;
            if (backdrop.transform.parent != panelTransform.parent)
                backdrop.transform.SetParent(panelTransform.parent, false);

            Configure(backdrop, record);
            backdrop.SetActive(true);
            backdrop.transform.SetSiblingIndex(panelTransform.GetSiblingIndex());
            panelTransform.SetAsLastSibling();
        }

        public void Hide(UIPanelRecord record)
        {
            if (record?.ModalBackdrop)
            {
                record.ModalBackdrop.GetComponent<UIModalBackdropClickRelay>()?.Set(null);
                record.ModalBackdrop.SetActive(false);
            }
        }

        public void Remove(UIPanelRecord record)
        {
            if (record?.ModalBackdrop)
                Object.Destroy(record.ModalBackdrop);

            if (record != null)
                record.ModalBackdrop = null;
        }

        private static GameObject Create(UIPanelRecord record)
        {
            var backdrop = new GameObject(
                $"{record.Panel.name}_ModalBackdrop",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(UIModalBackdropClickRelay));

            var rect = (RectTransform)backdrop.transform;
            rect.SetParent(record.Panel.transform.parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image image = backdrop.GetComponent<Image>();
            image.color = Color.clear;
            image.raycastTarget = true;
            return backdrop;
        }

        private static void Configure(GameObject backdrop, UIPanelRecord record)
        {
            UIModalBackdrop settings = record.Panel.GetComponent<UIModalBackdrop>();
            Image image = backdrop.GetComponent<Image>();
            image.color = settings ? settings.Color : Color.clear;

            UIModalBackdropClickRelay relay = backdrop.GetComponent<UIModalBackdropClickRelay>();
            relay.Set(settings && settings.CloseOnClick
                ? () => UIManager.TryGetInstance()?.Back(record.Key.SurfaceId)
                : null);
        }
    }
}
