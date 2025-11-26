using System.Collections;
using Framework.Audio;
using Framework.Event;
using Framework.Mono;
using Framework.ObjectPool;
using Framework.ResLoad;
using Framework.Singleton;
using Framework.UI;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace Framework.Scene
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

        #region 异步切换场景
        /// <summary>
        /// 异步切换场景（通过场景名）
        /// </summary>
        /// <param name="name">场景名称</param>
        /// <param name="onProgress">加载进度回调(0~1)</param>
        /// <param name="onComplete">切换完成的回调函数</param>
        public void LoadSceneAsync(string name, UnityAction onComplete = null, UnityAction<float> onProgress = null)
        {
            MonoManager.Instance.StartCoroutine(ReallyLoadSceneAsync(name, onComplete, onProgress));
        }

        /// <summary>
        /// 异步切换场景的协程（通过场景名）
        /// </summary>
        /// <param name="name">场景名称</param>
        /// <param name="onComplete">切换完成的回调函数</param>
        /// <param name="onProgress">加载进度回调(0~1)</param>
        /// <returns>返回加载场景的进度(0-1 float)</returns>
        private static IEnumerator ReallyLoadSceneAsync(string name, UnityAction onComplete, UnityAction<float> onProgress)
        {
            PreSceneClean();
            AsyncOperation ao = SceneManager.LoadSceneAsync(name);
            //不停的在协同程序中每帧检测是否加载结束 只有当加载完毕后才会跳出该循环
            while (!ao.isDone)
            {
                // 将Unity 的 ao.progress 参数更平滑
                float progress = Mathf.Clamp01(ao.progress / 0.9f);
                // 传入加载进度回调里
                onProgress?.Invoke(progress);
                yield return null;
            }
            // 保证最终进度回调到 1
            onProgress?.Invoke(1f);
            // 延迟一帧再回调，保证 XR Origin Awake/Start 都跑完
            yield return null;
            // 场景切换完成回调
            onComplete?.Invoke();
        }
        
        /// <summary>
        /// 异步切换场景（通过场景ID）
        /// </summary>
        /// <param name="sceneId">场景ID</param>
        /// <param name="onProgress">加载进度回调(0~1)</param>
        /// <param name="onComplete">切换完成的回调函数</param>
        public void LoadSceneAsync(int sceneId, UnityAction onComplete = null, UnityAction<float> onProgress = null)
        {
            MonoManager.Instance.StartCoroutine(ReallyLoadSceneAsync(sceneId, onComplete, onProgress));
        }

        /// <summary>
        /// 异步切换场景的协程（通过场景ID）
        /// </summary>
        /// <param name="sceneId">场景ID</param>
        /// <param name="onComplete">切换完成的回调函数</param>
        /// <param name="onProgress">加载进度回调(0~1)</param>
        /// <returns>返回加载场景的进度(0-1 float)</returns>
        private static IEnumerator ReallyLoadSceneAsync(int sceneId, UnityAction onComplete, UnityAction<float> onProgress)
        {
            PreSceneClean();
            AsyncOperation ao = SceneManager.LoadSceneAsync(sceneId);
            //不停的在协同程序中每帧检测是否加载结束 只有当加载完毕后才会跳出该循环
            while (!ao.isDone)
            {
                // 将Unity 的 ao.progress 参数更平滑
                float progress = Mathf.Clamp01(ao.progress / 0.9f);
                // 传入加载进度回调里
                onProgress?.Invoke(progress);
                yield return null;
            }
            // 保证最终进度回调到 1
            onProgress?.Invoke(1f);
            // 延迟一帧再回调，保证 XR Origin Awake/Start 都跑完
            yield return null;
            // 场景切换完成回调
            onComplete?.Invoke();
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
