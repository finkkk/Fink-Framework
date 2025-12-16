using System;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using UnityEngine;
using FinkFramework.Runtime.ResLoad.Base;
using FinkFramework.Runtime.Utils;
using Object = UnityEngine.Object;

namespace FinkFramework.Runtime.ResLoad.Providers
{
    /// <summary>
    /// AssetBundle 资源加载 Provider
    /// </summary>
    public sealed class ABProvider : IResProvider
    {
        private readonly string rootPath;
        private AssetBundleManifest manifest;

        private readonly Dictionary<string, ABBundleInfo> bundleInfos = new();
        private readonly Dictionary<string, float> loadingProgress = new();
        private readonly Dictionary<string, UniTask> loadingTasks = new();

        public ABProvider(string rootPath)
        {
            this.rootPath = rootPath;
            LoadMainManifest();
        }

        #region 初始化

        private void LoadMainManifest()
        {
            string mainName = GetPlatformName();
            string path = Path.Combine(rootPath, mainName);

            var bundle = AssetBundle.LoadFromFile(path);
            if (!bundle)
            {
                LogUtil.Error("ABProvider",$"主包加载失败: {path}");
                return;
            }

            manifest = bundle.LoadAsset<AssetBundleManifest>("AssetBundleManifest");

            bundleInfos[mainName] = new ABBundleInfo
            {
                bundle = bundle,
                refCount = int.MaxValue,
                dependencies = Array.Empty<string>(),
                permanent = true
            };
        }

        #endregion

        #region IResProvider

        public T Load<T>(string path) where T : Object
        {
            ParsePath(path, out var bundleName, out var assetName);

            // 1. 确保 AB 及依赖已物理加载
            EnsureBundleLoaded(bundleName);

            // 2. 引用计数 +1（逻辑 retain）
            RetainBundle(bundleName);

            // 3. 加载资源
            return bundleInfos[bundleName].bundle.LoadAsset<T>(assetName);
        }

        public async UniTask<T> LoadAsync<T>(string path) where T : Object
        {
            ParsePath(path, out var bundleName, out var assetName);

            await EnsureBundleLoadedAsync(bundleName);
            RetainBundle(bundleName);

            var req = bundleInfos[bundleName].bundle.LoadAssetAsync<T>(assetName);
            await req;

            return req.asset as T;
        }


        public bool Exists(string path)
        {
            ParsePath(path, out var bundleName, out _);
            return File.Exists(Path.Combine(rootPath, bundleName));
        }

        public void Unload(string path)
        {
            ParsePath(path, out var bundleName, out _);
            ReleaseBundle(bundleName);
        }

        public void Clear()
        {
            var keys = new List<string>(bundleInfos.Keys);

            foreach (var key in keys)
            {
                var info = bundleInfos[key];
                if (!info.permanent)
                {
                    info.bundle.Unload(false);
                    bundleInfos.Remove(key);
                }
            }

            loadingProgress.Clear();
            loadingTasks.Clear();
        }

      
        public bool TryGetProgress(string path, out float progress)
        {
            ParsePath(path, out var bundleName, out _);
            return loadingProgress.TryGetValue(bundleName, out progress);
        }

        #endregion

        #region 核心加载逻辑

        private void EnsureBundleLoaded(string bundleName)
        {
            if (bundleInfos.ContainsKey(bundleName))
                return;
            
            if (loadingTasks.TryGetValue(bundleName, out var task))
            {
                // 同步等待异步加载完成（初始化阶段可接受）
                task.GetAwaiter().GetResult();
                return;
            }

            LoadBundleInternal(bundleName);
        }

        private async UniTask EnsureBundleLoadedAsync(string bundleName)
        {
            if (bundleInfos.ContainsKey(bundleName))
                return;

            if (loadingTasks.TryGetValue(bundleName, out var task))
            {
                await task;
                return;
            }

            var loadTask = LoadBundleInternalAsync(bundleName);
            loadingTasks[bundleName] = loadTask;

            await loadTask;

            loadingTasks.Remove(bundleName);
        }

        private void LoadBundleInternal(string bundleName)
        {
            string path = Path.Combine(rootPath, bundleName);
            var bundle = AssetBundle.LoadFromFile(path);

            if (!bundle)
            {
                LogUtil.Error("ABProvider",$"AB 加载失败: {bundleName}");
                return;
            }

            string[] deps = manifest != null
                ? manifest.GetAllDependencies(bundleName)
                : Array.Empty<string>();

            bundleInfos[bundleName] = new ABBundleInfo
            {
                bundle = bundle,
                refCount = 0,
                dependencies = deps
            };

            foreach (var dep in deps)
                EnsureBundleLoaded(dep);
        }

        private async UniTask LoadBundleInternalAsync(string bundleName)
        {
            string path = Path.Combine(rootPath, bundleName);
            var req = AssetBundle.LoadFromFileAsync(path);

            while (!req.isDone)
            {
                loadingProgress[bundleName] = req.progress;
                await UniTask.Yield();
            }

            if (!req.assetBundle)
            {
                LogUtil.Error("ABProvider",$"AB 异步加载失败: {bundleName}");
                return;
            }

            string[] deps = manifest != null
                ? manifest.GetAllDependencies(bundleName)
                : Array.Empty<string>();

            bundleInfos[bundleName] = new ABBundleInfo
            {
                bundle = req.assetBundle,
                refCount = 0,
                dependencies = deps
            };

            loadingProgress.Remove(bundleName);

            foreach (var dep in deps)
                await EnsureBundleLoadedAsync(dep);
        }

        #endregion
        
        #region 引用计数

        private void RetainBundle(string bundleName)
        {
            if (!bundleInfos.TryGetValue(bundleName, out var info))
                return;

            foreach (var dep in info.dependencies)
                RetainBundle(dep);

            if (!info.permanent)
                info.refCount++;
        }

        private void ReleaseBundle(string bundleName)
        {
            if (!bundleInfos.TryGetValue(bundleName, out var info))
                return;

            if (!info.permanent)
                info.refCount--;

            if (!info.permanent && info.refCount <= 0)
            {
                info.bundle.Unload(false);
                bundleInfos.Remove(bundleName);

                foreach (var dep in info.dependencies)
                    ReleaseBundle(dep);
            }
        }

        #endregion
        
        #region 工具方法

        private static void ParsePath(string path, out string bundleName, out string assetName)
        {
            if (string.IsNullOrEmpty(path))
            {
                LogUtil.Error("ABProvider", "AB 路径为空");
                bundleName = string.Empty;
                assetName = string.Empty;
                return;
            }

            // 不允许以 / 开头或结尾
            if (path[0] == '/' || path[^1] == '/')
            {
                LogUtil.Error("ABProvider", $"非法 AB 路径（不能以 / 开头或结尾）: {path}");
                bundleName = string.Empty;
                assetName = string.Empty;
                return;
            }

            int index = path.IndexOf('/');
            if (index <= 0 || index == path.Length - 1)
            {
                LogUtil.Error("ABProvider", $"非法 AB 路径（格式应为 bundle/asset）: {path}");
                bundleName = string.Empty;
                assetName = string.Empty;
                return;
            }

            bundleName = path[..index];
            assetName  = path[(index + 1)..];
        }

        private static string GetPlatformName()
        {
#if UNITY_ANDROID
            return "Android";
#elif UNITY_IOS
            return "iOS";
#else
            return "StandaloneWindows64";
#endif
        }

        #endregion
        
        private class ABBundleInfo
        {
            public AssetBundle bundle;
            public int refCount;
            public string[] dependencies;
            public bool permanent;
        }
    }
}
