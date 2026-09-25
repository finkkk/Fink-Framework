using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using FinkFramework.Runtime.Audio;
using FinkFramework.Runtime.Event;
using FinkFramework.Runtime.Pool;
using FinkFramework.Runtime.ResLoad;
using FinkFramework.Runtime.Singleton;
using FinkFramework.Runtime.UI;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityScene = UnityEngine.SceneManagement.Scene;

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

        #region 叠加场景

        /// <summary>
        /// 以叠加模式加载场景。
        ///
        /// 该路径不会执行完整换场景的全局清理；加载完成后会明确把新场景设为活动场景，
        /// 避免调用方依赖 Unity 当时碰巧的活动场景。
        /// </summary>
        public Task<SceneHandle> LoadAdditiveAsync(string sceneName, CancellationToken cancellationToken = default)
        {
            return LoadAdditiveCoreAsync(sceneName, cancellationToken);
        }

        /// <summary>将指定的已加载场景设为活动场景。</summary>
        public bool SetActiveScene(SceneHandle handle)
        {
            if (handle == null)
                throw new ArgumentNullException(nameof(handle));

            if (!handle.IsLoaded)
                throw new InvalidOperationException($"场景句柄已失效，无法设为活动场景：{handle}");

            return SceneManager.SetActiveScene(handle.Scene);
        }

        /// <summary>
        /// 卸载指定的一次场景加载，并执行该场景的局部清理。
        ///
        /// 调用方应在此方法前完成 Scope 自己持有的事件、实体、计时器和资源引用清理；
        /// ScenesManager 只负责场景级 UI 和实际的 Unity 场景卸载。
        /// </summary>
        public Task UnloadAsync(
            SceneHandle handle,
            SceneCleanupOptions options,
            CancellationToken cancellationToken = default)
        {
            return UnloadCoreAsync(handle, options, cancellationToken);
        }

        private static async Task<SceneHandle> LoadAdditiveCoreAsync(
            string sceneName,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
                throw new ArgumentException("场景名不能为空。", nameof(sceneName));

            cancellationToken.ThrowIfCancellationRequested();
            UnityScene[] loadedBefore = SnapshotLoadedScenes();
            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            if (operation == null)
                throw new InvalidOperationException($"无法开始叠加加载场景：{sceneName}");

            try
            {
                await operation.ToUniTask(cancellationToken: cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Unity 不支持取消已经开始的场景加载。等待底层操作完成后，
                // 仅卸载这次加载产生的新场景，避免取消调用留下孤儿场景。
                UnloadSceneWhenLoadCompletes(operation, sceneName, loadedBefore);
                throw;
            }

            cancellationToken.ThrowIfCancellationRequested();
            UnityScene loadedScene = FindNewlyLoadedScene(sceneName, loadedBefore);
            if (!loadedScene.IsValid() || !loadedScene.isLoaded)
                throw new InvalidOperationException($"叠加场景加载完成，但找不到场景实例：{sceneName}");

            var handle = new SceneHandle(loadedScene);
            if (!SceneManager.SetActiveScene(loadedScene))
                throw new InvalidOperationException($"叠加场景加载完成，但无法设为活动场景：{handle}");

            return handle;
        }

        private static async Task UnloadCoreAsync(
            SceneHandle handle,
            SceneCleanupOptions options,
            CancellationToken cancellationToken)
        {
            if (handle == null)
                throw new ArgumentNullException(nameof(handle));

            cancellationToken.ThrowIfCancellationRequested();
            if (!handle.IsLoaded)
                throw new InvalidOperationException($"场景句柄已失效，无法卸载：{handle}");

            // UIManager 自己也监听 sceneUnloaded；这里提前关闭一次是为了让 UI
            // 在卸载等待期间立即进入关闭态，监听器则负责处理场景级 Surface 的移除。
            UIManager.TryGetInstance()?.CloseScenePanels(handle.Scene);

            AsyncOperation operation = SceneManager.UnloadSceneAsync(handle.Scene);
            if (operation == null)
                throw new InvalidOperationException($"无法开始卸载场景：{handle}");

            await operation.ToUniTask(cancellationToken: cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            if (options.UnloadUnusedAssets)
                await UnloadUnusedAssetsAsync(cancellationToken);

#if !UNITY_EDITOR
            if (options.CollectGarbage)
                GC.Collect();
#endif
        }

        private static async Task UnloadUnusedAssetsAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ResManager resManager = ResManager.TryGetInstance();
            if (resManager != null)
                await resManager.UnloadUnusedAssets();
            else
                await Resources.UnloadUnusedAssets().ToUniTask(cancellationToken: cancellationToken);
        }

        private static UnityScene[] SnapshotLoadedScenes()
        {
            var scenes = new List<UnityScene>(SceneManager.sceneCount);
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                UnityScene scene = SceneManager.GetSceneAt(index);
                if (scene.IsValid() && scene.isLoaded)
                    scenes.Add(scene);
            }

            return scenes.ToArray();
        }

        private static UnityScene FindNewlyLoadedScene(string sceneName, UnityScene[] loadedBefore)
        {
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                UnityScene scene = SceneManager.GetSceneAt(index);
                if (!scene.IsValid()
                    || !scene.isLoaded
                    || !SceneMatches(scene, sceneName)
                    || ContainsScene(loadedBefore, scene))
                    continue;

                return scene;
            }

            // 正常情况下上面的“新实例”匹配足够精确；这个回退兼容场景加载器在
            // Unity 内部复用场景实例的情况，同时仍然返回 Unity 的 Scene 而非场景名。
            UnityScene byName = SceneManager.GetSceneByName(sceneName);
            if (byName.IsValid() && byName.isLoaded)
                return byName;

            UnityScene byPath = SceneManager.GetSceneByPath(sceneName);
            return byPath.IsValid() && byPath.isLoaded ? byPath : default;
        }

        private static bool SceneMatches(UnityScene scene, string sceneName) =>
            string.Equals(scene.name, sceneName, StringComparison.Ordinal)
            || string.Equals(scene.path, sceneName, StringComparison.Ordinal);

        private static bool ContainsScene(UnityScene[] scenes, UnityScene candidate)
        {
            foreach (UnityScene scene in scenes)
            {
                if (scene.handle == candidate.handle)
                    return true;
            }

            return false;
        }

        private static void UnloadSceneWhenLoadCompletes(
            AsyncOperation operation,
            string sceneName,
            UnityScene[] loadedBefore)
        {
            void UnloadLoadedScene(AsyncOperation completedOperation)
            {
                completedOperation.completed -= UnloadLoadedScene;
                UnityScene scene = FindNewlyLoadedScene(sceneName, loadedBefore);
                if (scene.IsValid() && scene.isLoaded)
                    SceneManager.UnloadSceneAsync(scene);
            }

            if (operation.isDone)
                UnloadLoadedScene(operation);
            else
                operation.completed += UnloadLoadedScene;
        }

        #endregion

        #region 清理资源
        private static void PreSceneClean()
        {
            AudioManager.TryGetInstance()?.ClearSound();
            PoolManager.TryGetInstance()?.CleanPool();
            UIManager.TryGetInstance()?.CloseScenePanels(SceneManager.GetActiveScene());
            EventManager.TryGetInstance()?.ClearAllEvent();
            ResManager.TryGetInstance()?.ClearDic();
            // 手动触发GC
#if !UNITY_EDITOR
            System.GC.Collect();
#endif
        }
        #endregion
    }
}
