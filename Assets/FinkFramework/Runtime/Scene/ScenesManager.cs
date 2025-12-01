using System.Collections;
using Cysharp.Threading.Tasks;
using FinkFramework.Runtime.Audio;
using FinkFramework.Runtime.Event;
using FinkFramework.Runtime.Mono;
using FinkFramework.Runtime.Pool;
using FinkFramework.Runtime.ResLoad;
using FinkFramework.Runtime.Singleton;
using FinkFramework.Runtime.UI;
using FinkFramework.Runtime.Utils;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace FinkFramework.Runtime.Scene
{
    /// <summary>
    /// 场景管理器 (封装切换场景的方法)
    /// </summary>
    public class ScenesManager : Singleton<ScenesManager>
    {
        private ScenesManager(){}
        
        #region 同步切换场景
        /// <summary>
        /// 同步切换场景（通过场景名）
        /// </summary>
        /// <param name="name">场景名称</param>
        /// <param name="onComplete">切换完成的回调函数</param>
        public void LoadScene(string name, UnityAction onComplete = null)
        {
            PreSceneClean();
            //切换场景
            SceneManager.LoadScene(name);
            //调用回调
            onComplete?.Invoke();
        }
        /// <summary>
        /// 同步切换场景（通过场景 ID）
        /// </summary>
        /// <param name="sceneId">场景ID</param>
        /// <param name="onComplete">切换完成的回调函数</param>
        public void LoadScene(int sceneId, UnityAction onComplete = null)
        {
            PreSceneClean();
            //切换场景
            SceneManager.LoadScene(sceneId);
            //调用回调
            onComplete?.Invoke();
        }
        #endregion

        #region 异步切换场景核心
        /// <summary>
        /// 异步核心封装（通过场景名）
        /// </summary>
        /// <param name="name">场景名称</param>
        /// <param name="op">句柄</param>
        private async UniTask LoadSceneHandleWrapper(string name, SceneOperation op)
        {
            PreSceneClean();
            var asyncOp = SceneManager.LoadSceneAsync(name);
            //不停的在协同程序中每帧检测是否加载结束 只有当加载完毕后才会跳出该循环
            while (!asyncOp.isDone)
            {
                // 将Unity 的 ao.progress 参数更平滑
                float p = MathUtil.Percent01(asyncOp.progress, 0f, 0.9f);
                op.SetProgress(p);
                await UniTask.Yield();
            }
            // 保证最终进度回调到 1
            op.SetProgress(1f);
            // 延迟一帧，保证 XR/Start 生命周期运行完毕
            await UniTask.Yield();
            // 切换场景完成
            op.Finish();
        }
        
        /// <summary>
        /// 异步核心封装（通过场景名）
        /// </summary>
        /// <param name="id">场景id</param>
        /// <param name="op">句柄</param>
        private async UniTask LoadSceneHandleWrapper(int id, SceneOperation op)
        {
            PreSceneClean();
            var asyncOp = SceneManager.LoadSceneAsync(id);
            //不停的在协同程序中每帧检测是否加载结束 只有当加载完毕后才会跳出该循环
            while (!asyncOp.isDone)
            {
                // 将Unity 的 ao.progress 参数更平滑
                float p = MathUtil.Percent01(asyncOp.progress, 0f, 0.9f);
                op.SetProgress(p);
                await UniTask.Yield();
            }
            // 保证最终进度回调到 1
            op.SetProgress(1f);
            // 延迟一帧，保证 XR/Start 生命周期运行完毕
            await UniTask.Yield();
            // 切换场景完成
            op.Finish();
        }
        
        /// <summary>
        /// 追踪场景切换进度的协程 专用于回调方式的异步切换
        /// </summary>
        private IEnumerator TrackSceneProgress(SceneOperation op, UnityAction<float> onProgress)
        {
            while (!op.IsDone)
            {
                onProgress?.Invoke(op.Progress);
                yield return null;
            }

            // 最终到 1
            onProgress?.Invoke(1f);
        }
        #endregion

        #region 通过场景名异步切换
        /// <summary>
        /// 异步切换场景：句柄方式（通过场景名）
        /// </summary>
        public SceneOperation LoadSceneHandle(string name)
        {
            var op = new SceneOperation();
            _ = LoadSceneHandleWrapper(name, op);
            return op;
        }
        
        /// <summary>
        /// 异步切换场景：await 方式（通过场景名）
        /// </summary>
        public async UniTask LoadSceneAsync(string name)
        {
            var op = LoadSceneHandle(name);
            await op.WaitUntilDone();
        }
        
        /// <summary>
        /// 异步切换场景：回调方式（通过场景名）
        /// </summary>
        public void LoadSceneAsyncCallback(string name, UnityAction onComplete = null, UnityAction<float> onProgress = null)
        {
            var op = LoadSceneHandle(name);

            // 完成监听
            op.Completed += _ => onComplete?.Invoke();

            // 进度监听：用协程转发
            MonoManager.Instance.StartCoroutine(TrackSceneProgress(op, onProgress));
        }
        #endregion
        
        #region 通过场景ID异步切换
        /// <summary>
        /// 异步切换场景：句柄方式（通过场景ID）
        /// </summary>
        public SceneOperation LoadSceneHandle(int id)
        {
            var op = new SceneOperation();
            _ = LoadSceneHandleWrapper(id, op);
            return op;
        }
        
        /// <summary>
        /// 异步切换场景：await 方式（通过场景ID）
        /// </summary>
        public async UniTask LoadSceneAsync(int id)
        {
            var op = LoadSceneHandle(id);
            await op.WaitUntilDone();
        }
        
        /// <summary>
        /// 异步切换场景：回调方式（通过场景ID）
        /// </summary>
        public void LoadSceneAsyncCallback(int id, UnityAction onComplete = null, UnityAction<float> onProgress = null)
        {
            var op = LoadSceneHandle(id);

            // 完成监听
            op.Completed += _ => onComplete?.Invoke();

            // 进度监听：用协程转发
            MonoManager.Instance.StartCoroutine(TrackSceneProgress(op, onProgress));
        }
        #endregion

        #region 清理资源
        private static void PreSceneClean()
        {
            AudioManager.Instance.ClearSound();
            PoolManager.Instance.CleanPool();
            UIManager.Instance.ClearAllPanels();
            EventManager.Instance.ClearAllEvent();
            ResManager.Instance.ClearDic();
            // 手动触发GC
            System.GC.Collect();
        }
        #endregion
    }
}
