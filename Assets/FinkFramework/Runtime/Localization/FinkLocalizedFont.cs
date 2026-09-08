using UnityEngine;
using UnityEngine.UI;

namespace FinkFramework.Runtime.Localization
{
    /// <summary>
    /// 将多语言 Font 应用到同一 GameObject 上的 Unity UI Text。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Text))]
    [AddComponentMenu("Fink Framework/Localization/Localized Font")]
    public sealed class FinkLocalizedFont : FinkLocalizedAsset<Font>
    {
        private Text text;

        protected override void OnEnable()
        {
            text = GetComponent<Text>();
            base.OnEnable();
        }

        protected override void ApplyAsset(Font asset)
        {
            if (text == null)
                text = GetComponent<Text>();

            if (text != null)
                text.font = asset;
        }
    }
}
