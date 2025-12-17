using System;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using UnityEngine;
using FinkFramework.Runtime.ResLoad.Base;
using FinkFramework.Runtime.Settings.Loaders;
using FinkFramework.Runtime.Settings.ScriptableObjects;
using FinkFramework.Runtime.Utils;
using Object = UnityEngine.Object;

namespace FinkFramework.Runtime.ResLoad.Providers
{
    /// <summary>
    /// AssetBundle 资源加载 Provider
    /// ------------------------------------
    /// Experimental / Advanced Backend
    ///
    /// - 仅负责本地 AssetBundle 加载
    /// - 不包含资源下载、版本管理、校验逻辑
    /// - 不推荐新项目首选
    ///
    /// 推荐方案：Addressables / Custom Backend
    /// </summary>
    public sealed class ABProvider : IResProvider
    {
        private AssetBundleManifest manifest;
        private bool initialized;
        
        private string builtInRootPath;
        private string hotfixRootPath;
        private bool enableHotfix;

        private readonly Dictionary<string, ABBundleInfo> bundleInfos = new();
        private readonly Dictionary<string, float> loadingProgress = new();
        private readonly Dictionary<string, UniTask> loadingTasks = new();

        #region 初始化
        
        /// <summary>
        /// 初始化 ABProvider（必须先调用）
        /// </summary>
        public void Initialize(AssetBundleBackendSettingsAsset settings)
        {
            if (initialized)
                return;

            if (!settings)
            {
                LogUtil.Error("ABProvider", "AssetBundleBackendSettingsAsset 为 null");
                return;
            }

            builtInRootPath = Path.Combine(
                Application.streamingAssetsPath,
                settings.BuiltInRootPath
            );

            hotfixRootPath = settings.HotfixRootPath;
            enableHotfix   = settings.EnableHotfix;

            LoadMainManifest();
            initialized = true;
        }
        
        private string ResolveBundlePath(string bundleName)
        {
            if (enableHotfix && !string.IsNullOrEmpty(hotfixRootPath))
            {
                string hotfixPath = Path.Combine(hotfixRootPath, bundleName);
                if (File.Exists(hotfixPath))
                    return hotfixPath;
            }

            return Path.Combine(builtInRootPath, bundleName);
        }

        private void LoadMainManifest()
        {
            var platformName = ResBackendSettingsRuntimeLoader.AssetBundle.PlatformName;
            if (string.IsNullOrEmpty(platformName))
            {
                LogUtil.Error("ABProvider", "AssetBundle PlatformName 未配置");
                return;
            }
            string path = Path.Combine(builtInRootPath, platformName);

            var bundle = AssetBundle.LoadFromFile(path);
            if (!bundle)
            {
                LogUtil.Error("ABProvider", $"主包加载失败: {path}");
                return;
            }

            manifest = bundle.LoadAsset<AssetBundleManifest>("AssetBundleManifest");

            bundleInfos[platformName] = new ABBundleInfo
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
            if (!initialized)
            {
                LogUtil.Error("ABProvider", "ABProvider 未初始化");
                return null;
            }
            
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
            if (!initialized)
            {
                LogUtil.Error("ABProvider", "ABProvider 未初始化");
                return null;
            }
            
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
            return File.Exists(ResolveBundlePath(bundleName));
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
            string path = ResolveBundlePath(bundleName);
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
            string path = ResolveBundlePath(bundleName);
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
