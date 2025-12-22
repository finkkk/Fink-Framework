using Cysharp.Threading.Tasks;
using UnityEngine.Events;

namespace FinkFramework.Runtime.Scene
{
    public class SceneOperation
    {
        public bool IsDone { get; internal set; }
        public float Progress { get; internal set; }
        public bool IsCancelled { get; private set; }
        public bool IsFinished => IsDone || IsCancelled;
        
        public UnityAction<SceneOperation> Completed;

        internal void SetProgress(float p) => Progress = p;

        internal void Finish()
        {
            if (IsFinished)
                return;
            IsDone = true;
            Progress = 1f;
            Completed?.Invoke(this);
        }
        
        internal void Cancel()
        {
            if (IsDone || IsCancelled)
                return;

            IsCancelled = true;
            Progress = 0f;
            Completed?.Invoke(this);
        }

        public async UniTask WaitUntilDone()
        {
            while (!IsFinished)
                await UniTask.Yield();
        }
    }
}