using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using FinkFramework.Runtime.ResLoad.Base;
using FinkFramework.Runtime.Settings.Loaders;
using FinkFramework.Runtime.Settings.ScriptableObjects;
using FinkFramework.Runtime.Utils;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

namespace FinkFramework.Runtime.ResLoad.Providers
{
    /// <summary>
    /// Addressables 资源 Provider。
    /// 此类位于独立的可选程序集，仅在安装 Addressables 包时参与编译。
    /// 句柄按路径缓存，以匹配 IResProvider 按路径卸载的接口约束。
    /// </summary>
    public sealed class AddressablesProvider : IResProvider
    {
        private readonly Dictionary<string, HandleInfo> handles = new();
        private readonly HashSet<string> knownKeys = new();

        public T Load<T>(string path) where T : Object
        {
            AddressablesBackendSettingsAsset settings = ResBackendSettingsRuntimeLoader.Addressables;
            if (settings == null || !settings.AllowSyncLoad)
            {
                LogUtil.Error("AddressablesProvider", $"禁止同步加载 Addressables 资源：{path}");
                return null;
            }

            if (handles.TryGetValue(path, out HandleInfo cached))
            {
                if (cached.AssetType != typeof(T))
                {
                    LogUtil.Error(
                        "AddressablesProvider",
                        $"同一路径不能按不同类型重复加载：{path}（已有 {cached.AssetType.Name}，请求 {typeof(T).Name}）");
                    return null;
                }

                if (!cached.Handle.IsValid()
                    || cached.Handle.Status != AsyncOperationStatus.Succeeded)
                {
                    RemoveFailedHandle(path, cached.Handle);
                    return null;
                }

                cached.RefCount++;
                return cached.Handle.Result as T;
            }

            AsyncOperationHandle<T> handle = Addressables.LoadAssetAsync<T>(path);
            handle.WaitForCompletion();
            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                LogUtil.Error("AddressablesProvider", $"同步加载失败：{path}");
                ReleaseHandle(handle);
                return null;
            }

            handles[path] = new HandleInfo(handle, 1, typeof(T));
            knownKeys.Add(path);
            return handle.Result;
        }

        public async UniTask<T> LoadAsync<T>(string path) where T : Object
        {
            if (handles.TryGetValue(path, out HandleInfo cached))
            {
                if (cached.AssetType != typeof(T))
                {
                    LogUtil.Error(
                        "AddressablesProvider",
                        $"同一路径不能按不同类型重复加载：{path}（已有 {cached.AssetType.Name}，请求 {typeof(T).Name}）");
                    return null;
                }

                try
                {
                    await cached.Handle.Task;
                }
                catch (Exception exception)
                {
                    RemoveFailedHandle(path, cached.Handle);
                    LogUtil.Error("AddressablesProvider", $"异步加载异常：{path} => {exception.Message}");
                    return null;
                }

                if (cached.Handle.Status != AsyncOperationStatus.Succeeded)
                {
                    RemoveFailedHandle(path, cached.Handle);
                    LogUtil.Error("AddressablesProvider", $"异步加载失败：{path}");
                    return null;
                }

                cached.RefCount++;
                return cached.Handle.Result as T;
            }

            AsyncOperationHandle<T> handle = Addressables.LoadAssetAsync<T>(path);
            var info = new HandleInfo(handle, 1, typeof(T));
            handles[path] = info;

            try
            {
                await handle.Task;
            }
            catch (Exception exception)
            {
                RemoveFailedHandle(path, handle);
                LogUtil.Error("AddressablesProvider", $"异步加载异常：{path} => {exception.Message}");
                return null;
            }

            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                RemoveFailedHandle(path, handle);
                LogUtil.Error("AddressablesProvider", $"异步加载失败：{path}");
                return null;
            }

            knownKeys.Add(path);
            return handle.Result;
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
            if (!handles.TryGetValue(path, out HandleInfo info) || info.RefCount <= 0)
                return;

            info.RefCount--;
            if (info.RefCount > 0)
                return;

            ReleaseHandle(info.Handle);
            handles.Remove(path);
        }

        public void Clear()
        {
            foreach (HandleInfo info in handles.Values)
                ReleaseHandle(info.Handle);

            handles.Clear();
            knownKeys.Clear();
        }

        public bool TryGetProgress(string path, out float progress)
        {
            if (handles.TryGetValue(path, out HandleInfo info) && info.Handle.IsValid())
            {
                progress = info.Handle.IsDone ? 1f : info.Handle.PercentComplete;
                return true;
            }

            progress = 0f;
            return false;
        }

        private void RemoveFailedHandle(string path, AsyncOperationHandle handle)
        {
            if (handles.TryGetValue(path, out HandleInfo current) && current.Handle.Equals(handle))
                handles.Remove(path);

            ReleaseHandle(handle);
        }

        private static void ReleaseHandle(AsyncOperationHandle handle)
        {
            if (handle.IsValid())
                Addressables.Release(handle);
        }

        private sealed class HandleInfo
        {
            public AsyncOperationHandle Handle { get; }
            public int RefCount { get; set; }
            public Type AssetType { get; }

            public HandleInfo(AsyncOperationHandle handle, int refCount, Type assetType)
            {
                Handle = handle;
                RefCount = refCount;
                AssetType = assetType;
            }
        }
    }
}
