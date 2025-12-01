using Cysharp.Threading.Tasks;
using UnityEngine.Events;

namespace FinkFramework.Runtime.Scene
{
    public class SceneOperation
    {
        public bool IsDone { get; internal set; }
        public float Progress { get; internal set; }
        public UnityAction<SceneOperation> Completed;

        internal void SetProgress(float p) => Progress = p;

        internal void Finish()
        {
            IsDone = true;
            Progress = 1f;
            Completed?.Invoke(this);
        }

        public async UniTask WaitUntilDone()
        {
            while (!IsDone)
                await UniTask.Yield();
        }
    }
}