using UnityEngine;
using TMPro;

namespace FinkFramework.Runtime.Localization
{
    /// <summary>
    /// 将多语言 TMP Font Asset 应用到同一 GameObject 上的 TMP_Text。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    [AddComponentMenu("Fink Framework/Localization/Localized TMP Font")]
    public sealed class FinkLocalizedTMPFont : FinkLocalizedAsset<TMP_FontAsset>
    {
        private TMP_Text text;

        protected override void OnEnable()
        {
            text = GetComponent<TMP_Text>();
            base.OnEnable();
        }

        protected override void ApplyAsset(TMP_FontAsset asset)
        {
            if (text == null)
                text = GetComponent<TMP_Text>();

            if (text != null)
                text.font = asset;
        }
    }
}
