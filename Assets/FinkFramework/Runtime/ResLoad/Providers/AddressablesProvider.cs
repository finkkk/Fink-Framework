#if ENABLE_ADDRESSABLES

using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using FinkFramework.Runtime.ResLoad.Base;
using FinkFramework.Runtime.Settings.Loaders;
using FinkFramework.Runtime.Utils;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace FinkFramework.Runtime.ResLoad.Providers
{
    /// <summary>
    /// Addressables 资源加载 Provider
    /// ------------------------------------------------------------
    /// 基于 Unity Addressables 系统
    /// - 自动处理依赖
    /// - 自动管理生命周期
    /// </summary>
    public sealed class AddressablesProvider : IResProvider
    {
        // 正在加载或已加载的资源句柄缓存
        private readonly Dictionary<string, HandleInfo> handles = new();
        private readonly HashSet<string> knownKeys = new();
        
        #region IResProvider

        /// <summary>
        /// 同步加载（可能阻塞主线程，仅建议在 Editor / 初始化阶段使用）
        /// </summary>
        public T Load<T>(string path) where T : Object
        {
            if (!ResBackendSettingsRuntimeLoader.Addressables.AllowSyncLoad)
            {
                LogUtil.Error(
                    "AddressablesProvider",
                    $"禁止同步加载 Addressables 资源: {path}"
                );
                return null;
            }
            if (handles.TryGetValue(path, out var info))
            {
                info.refCount++;
                return info.handle.Result as T;
            }

            var handle = Addressables.LoadAssetAsync<T>(path);
            handle.WaitForCompletion();

            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                LogUtil.Error("AddressablesProvider",$"同步加载失败: {path}");
                if (handle.IsValid())
                    Addressables.Release(handle);
                return null;
            }

            handles[path] = new HandleInfo
            {
                handle = handle,
                refCount = 1
            };

            knownKeys.Add(path);
            return handle.Result;
        }

        public async UniTask<T> LoadAsync<T>(string path) where T : Object
        {
            if (handles.TryGetValue(path, out var info))
            {
                info.refCount++;
                return info.handle.Result as T;
            }

            var handle = Addressables.LoadAssetAsync<T>(path);

            handles[path] = new HandleInfo
            {
                handle = handle,
                refCount = 1
            };

            knownKeys.Add(path);

            await handle.Task;

            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                LogUtil.Error("AddressablesProvider",$"异步加载失败: {path}");
                handles.Remove(path);
                return null;
            }

            return handle.Result;
        }

        public bool Exists(string path)
        {
            if (knownKeys.Contains(path))
                return true;

            foreach (var locator in Addressables.ResourceLocators)
            {
                if (locator.Locate(path, typeof(Object), out _))
                {
                    knownKeys.Add(path);
                    return true;
                }
            }
            return false;
        }

        public void Unload(string path)
        {
            if (!handles.TryGetValue(path, out var info))
                return;

            info.refCount--;

            if (info.refCount > 0)
                return;

            if (info.handle.IsValid())
                Addressables.Release(info.handle);

            handles.Remove(path);
        }

        public void Clear()
        {
            foreach (var info in handles.Values)
            {
                if (info.handle.IsValid())
                    Addressables.Release(info.handle);
            }

            handles.Clear();
            knownKeys.Clear();
        }

        public bool TryGetProgress(string path, out float progress)
        {
            if (handles.TryGetValue(path, out var info))
            {
                progress = info.handle.IsDone
                    ? 1f
                    : info.handle.PercentComplete;
                return true;
            }

            progress = 0f;
            return false;
        }

        #endregion

        private sealed class HandleInfo
        {
            public AsyncOperationHandle handle;
            public int refCount;
        }
    }
}

#endif