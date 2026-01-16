using FinkFramework.Runtime.Audio;
using FinkFramework.Runtime.Event;
using FinkFramework.Runtime.Pool;
using FinkFramework.Runtime.ResLoad;
using FinkFramework.Runtime.Settings.Loaders;
using FinkFramework.Runtime.Singleton;
using FinkFramework.Runtime.UI;
using UnityEngine;
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
        
        
        #region 场景生命周期事件

        /// <summary>切换场景前
        /// （LoadScene / LoadSceneAsync 调用前）
        /// </summary>
        public event UnityAction OnBeforeSceneLoad;

        #endregion
        #region 同步切换场景

        public void LoadScene(string sceneName)
        {
            OnBeforeSceneLoad?.Invoke();
            PreSceneClean();

            SceneManager.LoadScene(sceneName);

        }

        public void LoadScene(int sceneId)
        {
            OnBeforeSceneLoad?.Invoke();
            PreSceneClean();

            SceneManager.LoadScene(sceneId);

        }

        #endregion

        #region 异步切换场景（官方 AsyncOperation）

        public AsyncOperation LoadSceneAsync(string sceneName)
        {
            OnBeforeSceneLoad?.Invoke();
            PreSceneClean();

            return SceneManager.LoadSceneAsync(sceneName);
        }

        public AsyncOperation LoadSceneAsync(int sceneId)
        {
            OnBeforeSceneLoad?.Invoke();
            PreSceneClean();

            return SceneManager.LoadSceneAsync(sceneId);
        }

        #endregion

        #region 清理资源
        private static void PreSceneClean()
        {
            if (GlobalSettingsRuntimeLoader.Current.EnableAudioModule)
            {
                AudioManager.Instance.ClearSound();
            }
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
    }
}
