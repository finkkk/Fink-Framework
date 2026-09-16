using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace FinkFramework.Runtime.Audio
{
    public class AudioOperation
    {
        public bool IsDone { get; internal set; }
        public bool IsFailed { get; internal set; }
        public float Progress { get; internal set; }

        public AudioSource Source { get; internal set; }
        public AudioClip Clip { get; internal set; }

        public UnityAction<AudioOperation> Completed;

        internal void SetProgress(float p)
        {
            Progress = Mathf.Clamp01(p);
        }

        internal void SetResult(AudioClip clip)
        {
            if (IsDone)
                return;

            Clip = clip;
            IsDone = true;
            Progress = 1f;
            InvokeCompletedSafely();
        }

        internal void SetFailed()
        {
            if (IsDone)
                return;

            IsFailed = true;
            IsDone = true;
            InvokeCompletedSafely();
        }

        private void InvokeCompletedSafely()
        {
            try
            {
                Completed?.Invoke(this);
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        public async UniTask WaitUntilDone()
        {
            while (!IsDone)
                await UniTask.Yield();
        }
    }
}
