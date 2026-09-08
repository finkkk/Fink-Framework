using UnityEngine;

namespace FinkFramework.Runtime.Localization
{
    /// <summary>
    /// 将多语言 AudioClip 应用到同一 GameObject 上的 AudioSource。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    [AddComponentMenu("Fink Framework/Localization/Localized Audio")]
    public sealed class FinkLocalizedAudio : FinkLocalizedAsset<AudioClip>
    {
        private AudioSource audioSource;

        protected override void OnEnable()
        {
            audioSource = GetComponent<AudioSource>();
            base.OnEnable();
        }

        protected override void ApplyAsset(AudioClip asset)
        {
            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();

            if (audioSource != null)
                audioSource.clip = asset;
        }
    }
}
