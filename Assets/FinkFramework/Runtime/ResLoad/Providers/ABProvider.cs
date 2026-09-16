using System;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using UnityEngine;
using FinkFramework.Runtime.ResLoad.Base;
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
        private string platformName;

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
            platformName   = settings.PlatformName;

            if (LoadMainManifest())
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

        private bool LoadMainManifest()
        {
            if (string.IsNullOrEmpty(platformName))
            {
                LogUtil.Error("ABProvider", "AssetBundle PlatformName 未配置");
                return false;
            }
            string path = Path.Combine(builtInRootPath, platformName);

            var bundle = AssetBundle.LoadFromFile(path);
            if (!bundle)
            {
                LogUtil.Error("ABProvider", $"主包加载失败: {path}");
                return false;
            }

            manifest = bundle.LoadAsset<AssetBundleManifest>("AssetBundleManifest");

            bundleInfos[platformName] = new ABBundleInfo
            {
                bundle = bundle,
                refCount = int.MaxValue,
                dependencies = Array.Empty<string>(),
                permanent = true
            };
            return true;
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
            
            if (!TryParsePath(path, out var bundleName, out var assetName))
                return null;

            // 1. 确保 AB 及依赖已物理加载
            if (!EnsureBundleLoaded(bundleName))
                return null;
            if (!bundleInfos.TryGetValue(bundleName, out var bundleInfo))
                return null;

            // 2. 引用计数 +1（逻辑 retain）
            RetainBundle(bundleName);

            // 3. 加载资源
            try
            {
                T asset = bundleInfo.bundle.LoadAsset<T>(assetName);
                if (!asset)
                    ReleaseBundle(bundleName);
                return asset;
            }
            catch
            {
                ReleaseBundle(bundleName);
                throw;
            }
        }

        public async UniTask<T> LoadAsync<T>(string path) where T : Object
        {
            if (!initialized)
            {
                LogUtil.Error("ABProvider", "ABProvider 未初始化");
                return null;
            }
            
            if (!TryParsePath(path, out var bundleName, out var assetName))
                return null;

            await EnsureBundleLoadedAsync(bundleName);
            if (!bundleInfos.TryGetValue(bundleName, out var bundleInfo))
                return null;

            RetainBundle(bundleName);

            try
            {
                var req = bundleInfo.bundle.LoadAssetAsync<T>(assetName);
                await req;
                T asset = req.asset as T;
                if (!asset)
                    ReleaseBundle(bundleName);
                return asset;
            }
            catch
            {
                ReleaseBundle(bundleName);
                throw;
            }
        }


        public bool Exists(string path)
        {
            if (!TryParsePath(path, out var bundleName, out _))
                return false;
            return File.Exists(ResolveBundlePath(bundleName));
        }

        public void Unload(string path)
        {
            if (!TryParsePath(path, out var bundleName, out _))
                return;
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
            if (!TryParsePath(path, out var bundleName, out _))
            {
                progress = 0f;
                return false;
            }

            return loadingProgress.TryGetValue(bundleName, out progress);
        }

        #endregion

        #region 核心加载逻辑

        private bool EnsureBundleLoaded(string bundleName)
        {
            if (bundleInfos.ContainsKey(bundleName))
                return true;

            if (loadingTasks.ContainsKey(bundleName))
            {
                // 不能在主线程同步等待异步 AssetBundle 请求，否则请求无法推进而形成死锁。
                LogUtil.Warn("ABProvider", $"AssetBundle 正在异步加载，跳过本次同步请求：{bundleName}");
                return false;
            }

            return LoadBundleInternal(bundleName);
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

            try
            {
                await loadTask;
            }
            finally
            {
                loadingTasks.Remove(bundleName);
            }
        }

        private bool LoadBundleInternal(string bundleName)
        {
            string path = ResolveBundlePath(bundleName);
            var bundle = AssetBundle.LoadFromFile(path);

            if (!bundle)
            {
                LogUtil.Error("ABProvider",$"AB 加载失败: {bundleName}");
                return false;
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
            {
                if (!EnsureBundleLoaded(dep) || !bundleInfos.ContainsKey(dep))
                {
                    bundle.Unload(false);
                    bundleInfos.Remove(bundleName);
                    LogUtil.Error("ABProvider", $"AssetBundle 依赖加载失败：{bundleName} -> {dep}");
                    return false;
                }
            }

            return true;
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
                loadingProgress.Remove(bundleName);
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
            {
                await EnsureBundleLoadedAsync(dep);
                if (!bundleInfos.ContainsKey(dep))
                {
                    req.assetBundle.Unload(false);
                    bundleInfos.Remove(bundleName);
                    LogUtil.Error("ABProvider", $"AssetBundle 依赖加载失败：{bundleName} -> {dep}");
                    return;
                }
            }
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

            if (info.permanent)
                return;

            if (info.refCount <= 0)
            {
                LogUtil.Warn("ABProvider", $"忽略重复释放 AssetBundle：{bundleName}");
                return;
            }

            info.refCount--;

            if (info.refCount == 0)
            {
                info.bundle.Unload(false);
                bundleInfos.Remove(bundleName);

                foreach (var dep in info.dependencies)
                    ReleaseBundle(dep);
            }
        }

        #endregion
        
        #region 工具方法

        private static bool TryParsePath(string path, out string bundleName, out string assetName)
        {
            if (string.IsNullOrEmpty(path))
            {
                LogUtil.Error("ABProvider", "AB 路径为空");
                bundleName = string.Empty;
                assetName = string.Empty;
                return false;
            }

            // 不允许以 / 开头或结尾
            if (path[0] == '/' || path[^1] == '/')
            {
                LogUtil.Error("ABProvider", $"非法 AB 路径（不能以 / 开头或结尾）: {path}");
                bundleName = string.Empty;
                assetName = string.Empty;
                return false;
            }

            int index = path.IndexOf('/');
            if (index <= 0 || index == path.Length - 1)
            {
                LogUtil.Error("ABProvider", $"非法 AB 路径（格式应为 bundle/asset）: {path}");
                bundleName = string.Empty;
                assetName = string.Empty;
                return false;
            }

            bundleName = path[..index];
            assetName  = path[(index + 1)..];
            return true;
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
