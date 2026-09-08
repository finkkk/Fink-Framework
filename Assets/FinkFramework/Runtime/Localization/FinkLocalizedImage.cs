using UnityEngine;
using UnityEngine.UI;

namespace FinkFramework.Runtime.Localization
{
    /// <summary>
    /// 将多语言 Sprite 应用到同一 GameObject 上的 Unity UI Image。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    [AddComponentMenu("Fink Framework/Localization/Localized Image")]
    public sealed class FinkLocalizedImage : FinkLocalizedAsset<Sprite>
    {
        private Image image;

        protected override void OnEnable()
        {
            image = GetComponent<Image>();
            base.OnEnable();
        }

        protected override void ApplyAsset(Sprite asset)
        {
            if (image == null)
                image = GetComponent<Image>();

            if (image != null)
                image.sprite = asset;
        }
    }
}
