using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

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
            Progress = p;
        }

        internal void SetResult(AudioClip clip)
        {
            Clip = clip;
            IsDone = true;
            Progress = 1f;
            Completed?.Invoke(this);
        }

        internal void SetFailed()
        {
            IsFailed = true;
            IsDone = true;
            Completed?.Invoke(this);
        }

        public async UniTask WaitUntilDone()
        {
            while (!IsDone)
                await UniTask.Yield();
        }
    }
}