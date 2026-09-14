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
    /// Addressables 资源加载 Provider。
    /// 此类位于独立的可选程序集，仅在安装 Addressables 包时参与编译。
    /// </summary>
    public sealed class AddressablesProvider : IResProvider
    {
        private readonly Dictionary<string, HandleInfo> handles = new();
        private readonly HashSet<string> knownKeys = new();

        public T Load<T>(string path) where T : Object
        {
            if (!ResBackendSettingsRuntimeLoader.Addressables.AllowSyncLoad)
            {
                LogUtil.Error("AddressablesProvider", $"禁止同步加载 Addressables 资源: {path}");
                return null;
            }

            if (handles.TryGetValue(path, out HandleInfo info))
            {
                info.RefCount++;
                return info.Handle.Result as T;
            }

            AsyncOperationHandle<T> handle = Addressables.LoadAssetAsync<T>(path);
            handle.WaitForCompletion();
            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                LogUtil.Error("AddressablesProvider", $"同步加载失败: {path}");
                if (handle.IsValid())
                    Addressables.Release(handle);
                return null;
            }

            handles[path] = new HandleInfo(handle, 1);
            knownKeys.Add(path);
            return handle.Result;
        }

        public async UniTask<T> LoadAsync<T>(string path) where T : Object
        {
            if (handles.TryGetValue(path, out HandleInfo info))
            {
                info.RefCount++;
                return info.Handle.Result as T;
            }

            AsyncOperationHandle<T> handle = Addressables.LoadAssetAsync<T>(path);
            handles[path] = new HandleInfo(handle, 1);
            knownKeys.Add(path);
            await handle.Task;

            if (handle.Status == AsyncOperationStatus.Succeeded)
                return handle.Result;

            LogUtil.Error("AddressablesProvider", $"异步加载失败: {path}");
            handles.Remove(path);
            if (handle.IsValid())
                Addressables.Release(handle);
            return null;
        }

        public bool Exists(string path)
        {
            if (knownKeys.Contains(path))
                return true;

            foreach (var locator in Addressables.ResourceLocators)
            {
                if (!locator.Locate(path, typeof(Object), out _))
                    continue;

                knownKeys.Add(path);
                return true;
            }

            return false;
        }

        public void Unload(string path)
        {
            if (!handles.TryGetValue(path, out HandleInfo info))
                return;

            info.RefCount--;
            if (info.RefCount > 0)
                return;

            if (info.Handle.IsValid())
                Addressables.Release(info.Handle);

            handles.Remove(path);
        }

        public void Clear()
        {
            foreach (HandleInfo info in handles.Values)
            {
                if (info.Handle.IsValid())
                    Addressables.Release(info.Handle);
            }

            handles.Clear();
            knownKeys.Clear();
        }

        public bool TryGetProgress(string path, out float progress)
        {
            if (!handles.TryGetValue(path, out HandleInfo info))
            {
                progress = 0f;
                return false;
            }

            progress = info.Handle.IsDone ? 1f : info.Handle.PercentComplete;
            return true;
        }

        private sealed class HandleInfo
        {
            public AsyncOperationHandle Handle { get; }
            public int RefCount { get; set; }

            public HandleInfo(AsyncOperationHandle handle, int refCount)
            {
                Handle = handle;
                RefCount = refCount;
            }
        }
    }
}
