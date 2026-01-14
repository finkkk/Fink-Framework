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
        
        #region 场景防重复加载标志

        private bool _isSceneLoading;

        #endregion
        
        #region 场景生命周期事件

        public event UnityAction OnBeforeSceneLoad;
        public event UnityAction OnAfterSceneLoad;

        public event UnityAction<object> OnBeforeSceneLoadWithParam;
        public event UnityAction<object> OnAfterSceneLoadWithParam;

        #endregion
        
        #region 同步切换场景(无参)
        /// <summary>
        /// 同步切换场景（通过场景名）
        /// </summary>
        /// <param name="name">场景名称</param>
        /// <param name="onComplete">切换完成的回调函数</param>
        public void LoadScene(string name, UnityAction onComplete = null)
        {
            if (_isSceneLoading)
            {
                LogUtil.Warn("ScenesManager", $"场景正在加载中，已忽略本次场景加载:{name}");
                return;
            }

            _isSceneLoading = true;
            OnBeforeSceneLoad?.Invoke();
            PreSceneClean();
            //切换场景
            SceneManager.LoadScene(name);
            //调用回调
            MonoManager.Instance.StartCoroutine(InvokeAfterLoad(null, onComplete));
        }
        /// <summary>
        /// 同步切换场景（通过场景 ID）
        /// </summary>
        /// <param name="sceneId">场景ID</param>
        /// <param name="onComplete">切换完成的回调函数</param>
        public void LoadScene(int sceneId, UnityAction onComplete = null)
        {
            if (_isSceneLoading)
            {
                LogUtil.Warn("ScenesManager", $"场景正在加载中，已忽略本次场景加载:{sceneId}");
                return;
            }

            _isSceneLoading = true;
            OnBeforeSceneLoad?.Invoke();
            PreSceneClean();
            //切换场景
            SceneManager.LoadScene(sceneId);
            //调用回调
            MonoManager.Instance.StartCoroutine(InvokeAfterLoad(null, onComplete));
        }
        #endregion
        
        #region 同步切换场景(有参)
        /// <summary>
        /// 同步切换场景（通过场景名）
        /// </summary>
        /// <param name="name">场景名称</param>
        /// <param name="param">委托事件传入的参数</param>
        /// <param name="onComplete">切换完成的回调函数</param>
        public void LoadScene(string name, object param, UnityAction onComplete = null)
        {
            if (_isSceneLoading)
            {
                LogUtil.Warn("ScenesManager", $"场景正在加载中，已忽略本次场景加载:{name}");
                return;
            }

            _isSceneLoading = true;
            OnBeforeSceneLoad?.Invoke();
            OnBeforeSceneLoadWithParam?.Invoke(param);

            PreSceneClean();
            SceneManager.LoadScene(name);

            MonoManager.Instance.StartCoroutine(InvokeAfterLoad(param, onComplete));
        }
        /// <summary>
        /// 同步切换场景（通过场景 ID）
        /// </summary>
        /// <param name="sceneId">场景ID</param>
        /// <param name="param">委托事件传入的参数</param>
        /// <param name="onComplete">切换完成的回调函数</param>
        public void LoadScene(int sceneId, object param, UnityAction onComplete = null)
        {
            if (_isSceneLoading)
            {
                LogUtil.Warn("ScenesManager", $"场景正在加载中，已忽略本次场景加载:{sceneId}");
                return;
            }

            _isSceneLoading = true;
            OnBeforeSceneLoad?.Invoke();
            OnBeforeSceneLoadWithParam?.Invoke(param);
            PreSceneClean();
            //切换场景
            SceneManager.LoadScene(sceneId);
            //调用回调
            MonoManager.Instance.StartCoroutine(InvokeAfterLoad(param, onComplete));
        }
        #endregion

        #region 异步切换场景核心

        /// <summary>
        /// 异步核心封装（通过场景名）
        /// </summary>
        /// <param name="name">场景名称</param>
        /// <param name="op">句柄</param>
        private async UniTask LoadSceneHandleWrapper(string name, SceneOperation op, object param = null)
        {
            if (_isSceneLoading)
            {
                LogUtil.Warn("ScenesManager", $"场景正在加载中，已忽略本次场景加载:{name}");
                op.Cancel();
                return;
            }

            _isSceneLoading = true;
            try
            {
                OnBeforeSceneLoad?.Invoke();
                if (param != null)
                    OnBeforeSceneLoadWithParam?.Invoke(param);
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
                OnAfterSceneLoad?.Invoke();
                if (param != null)
                    OnAfterSceneLoadWithParam?.Invoke(param);
                // 切换场景完成
                op.Finish();
            }
            finally
            {
                _isSceneLoading = false;
            }
        }
        
        /// <summary>
        /// 异步核心封装（通过场景Id）
        /// </summary>
        /// <param name="id">场景id</param>
        /// <param name="op">句柄</param>
        private async UniTask LoadSceneHandleWrapper(int id, SceneOperation op, object param = null)
        {
            if (_isSceneLoading)
            {
                LogUtil.Warn("ScenesManager", $"场景正在加载中，已忽略本次场景加载:{id}");
                op.Cancel();
                return;
            }

            _isSceneLoading = true;
            try
            {
                OnBeforeSceneLoad?.Invoke();
                if (param != null)
                    OnBeforeSceneLoadWithParam?.Invoke(param);
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
                OnAfterSceneLoad?.Invoke();
                if (param != null)
                    OnAfterSceneLoadWithParam?.Invoke(param);
                // 切换场景完成
                op.Finish();
            }
            finally
            {
                _isSceneLoading = false;
            }
        }
        
        /// <summary>
        /// 追踪场景切换进度的协程 专用于回调方式的异步切换
        /// </summary>
        private IEnumerator TrackSceneProgress(SceneOperation op, UnityAction<float> onProgress)
        {
            while (!op.IsFinished)
            {
                onProgress?.Invoke(op.Progress);
                yield return null;
            }
            onProgress?.Invoke(op.Progress);
        }
        #endregion

        #region 通过场景名异步切换(无参)
        /// <summary>
        /// 异步切换场景：句柄方式（通过场景名）
        /// </summary>
        public SceneOperation LoadSceneAsyncHandle(string name)
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
            var op = LoadSceneAsyncHandle(name);
            await op.WaitUntilDone();
        }
        
        /// <summary>
        /// 异步切换场景：回调方式（通过场景名）
        /// </summary>
        public void LoadSceneAsyncCallback(string name, UnityAction onComplete = null, UnityAction<float> onProgress = null)
        {
            var op = LoadSceneAsyncHandle(name);

            // 完成监听
            op.Completed += _ => onComplete?.Invoke();

            // 进度监听：用协程转发
            MonoManager.Instance.StartCoroutine(TrackSceneProgress(op, onProgress));
        }
        #endregion
        
        #region 通过场景ID异步切换(无参)
        /// <summary>
        /// 异步切换场景：句柄方式（通过场景ID）
        /// </summary>
        public SceneOperation LoadSceneAsyncHandle(int id)
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
            var op = LoadSceneAsyncHandle(id);
            await op.WaitUntilDone();
        }
        
        /// <summary>
        /// 异步切换场景：回调方式（通过场景ID）
        /// </summary>
        public void LoadSceneAsyncCallback(int id, UnityAction onComplete = null, UnityAction<float> onProgress = null)
        {
            var op = LoadSceneAsyncHandle(id);

            // 完成监听
            op.Completed += _ => onComplete?.Invoke();

            // 进度监听：用协程转发
            MonoManager.Instance.StartCoroutine(TrackSceneProgress(op, onProgress));
        }
        #endregion
        
        #region 通过场景名异步切换(有参)
        /// <summary>
        /// 异步切换场景：句柄方式（通过场景名）
        /// </summary>
        public SceneOperation LoadSceneAsyncHandle(string name, object param)
        {
            var op = new SceneOperation();
            _ = LoadSceneHandleWrapper(name, op, param);
            return op;
        }
        
        /// <summary>
        /// 异步切换场景：await 方式（通过场景名）
        /// </summary>
        public async UniTask LoadSceneAsync(string name, object param)
        {
            var op = LoadSceneAsyncHandle(name, param);
            await op.WaitUntilDone();
        }
        
        /// <summary>
        /// 异步切换场景：回调方式（通过场景名）
        /// </summary>
        public void LoadSceneAsyncCallback(string name, object param, UnityAction onComplete = null, UnityAction<float> onProgress = null)
        {
            var op = LoadSceneAsyncHandle(name, param);

            // 完成监听
            op.Completed += _ => onComplete?.Invoke();

            // 进度监听：用协程转发
            MonoManager.Instance.StartCoroutine(TrackSceneProgress(op, onProgress));
        }
        #endregion
        
        #region 通过场景ID异步切换(有参)
        /// <summary>
        /// 异步切换场景：句柄方式（通过场景ID）
        /// </summary>
        public SceneOperation LoadSceneAsyncHandle(int id, object param)
        {
            var op = new SceneOperation();
            _ = LoadSceneHandleWrapper(id, op, param);
            return op;
        }
        
        /// <summary>
        /// 异步切换场景：await 方式（通过场景ID）
        /// </summary>
        public async UniTask LoadSceneAsync(int id, object param)
        {
            var op = LoadSceneAsyncHandle(id, param);
            await op.WaitUntilDone();
        }
        
        /// <summary>
        /// 异步切换场景：回调方式（通过场景ID）
        /// </summary>
        public void LoadSceneAsyncCallback(int id, object param, UnityAction onComplete = null, UnityAction<float> onProgress = null)
        {
            var op = LoadSceneAsyncHandle(id, param);

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
#if !UNITY_EDITOR
            System.GC.Collect();
#endif
        }
        #endregion

        #region 工具方法
        
        private IEnumerator InvokeAfterLoad(object param, UnityAction onComplete)
        {
            // 等一帧，保证新场景 Awake / Start 跑完
            yield return null;

            OnAfterSceneLoad?.Invoke();

            if (param != null)
                OnAfterSceneLoadWithParam?.Invoke(param);

            onComplete?.Invoke();
            // 场景加载完毕
            _isSceneLoading = false;
        }

        #endregion
    }
}
