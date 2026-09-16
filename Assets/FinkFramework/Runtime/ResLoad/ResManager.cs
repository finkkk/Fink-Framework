using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using FinkFramework.Runtime.Environments;
using FinkFramework.Runtime.ResLoad.Base;
using FinkFramework.Runtime.ResLoad.Providers;
using FinkFramework.Runtime.Settings.Loaders;
using FinkFramework.Runtime.Singleton;
using FinkFramework.Runtime.Utils;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;
// ReSharper disable All

namespace FinkFramework.Runtime.ResLoad
{
    /// <summary>
    /// 资源加载模块管理器 （Provider 插件系统 + 引用计数 + 缓存）
    /// </summary>
    public class ResManager : Singleton<ResManager>
    {
        #region 变量定义与初始化

        /// <summary>
        /// 构造函数 初始化资源加载模块
        /// </summary>
        private ResManager()
        {
            // 无前缀和 res:// 共享同一个 Provider，避免清理时重复操作。
            var resourcesProvider = new ResourcesProvider();
            AddProvider("", resourcesProvider);
            AddProvider("res", resourcesProvider);

            AddProvider("file", new FileProvider());

            // http / https 共享同一个请求管理器，进度和清理行为保持一致。
            var webProvider = new WebProvider();
            AddProvider("http", webProvider);
            AddProvider("https", webProvider);
            
            // 注册 AssetBundle / Addressables 加载模块。
            // 全局配置首次导入或被移除时允许资源管理器先以基础 Provider 工作，
            // 避免直接访问空配置导致整个本地化初始化链抛出 NullReferenceException。
            if (!GlobalSettingsRuntimeLoader.TryGet(out var globalSettings))
            {
                LogUtil.Warn(
                    "FinkFramework",
                    "未加载到外部 GlobalSettingsAsset，已跳过 AssetBundle/Addressables Provider 注册。"
                    + "请确认 Assets/FinkFramework_Assets/Resources/FinkFramework/Settings/Global/GlobalSettingsAsset.asset"
                    + " 已被 Unity 导入。");
            }
            else if (globalSettings.ResourceBackend == EnvironmentState.ResourceBackendType.AssetBundle)
            {
                var settings = globalSettings.AssetBundleSettings;
                var provider = new ABProvider();
                provider.Initialize(settings);
                AddProvider("ab", provider);
            }
            // Addressables 位于独立程序集。核心程序集不直接引用该包，
            // 以便未安装 Addressables 的项目仍可正常编译和使用 Resources / AB。
#if ENABLE_ADDRESSABLES
            if (globalSettings?.ResourceBackend == EnvironmentState.ResourceBackendType.Addressables)
            {
                RegisterAddressablesProvider();
            }
#else
            if (globalSettings?.ResourceBackend == EnvironmentState.ResourceBackendType.Addressables)
            {
                LogUtil.Error(
                    "ResManager",
                    "当前资源后端配置为 Addressables，但项目未安装 com.unity.addressables 包。"
                    + "请安装该包，或在全局配置中改用 Resources / AssetBundle / Custom。");
            }
#endif
            // 注册 Editor 加载模块
#if UNITY_EDITOR
            AddProvider("editor", new EditorProvider());
#endif
        }
        
        // Provider 插件路由字典
        private readonly Dictionary<string, IResProvider> providers = new();
        
        // 资源缓存字典 用于记录加载过或者加载中的资源信息
        private readonly Dictionary<string, BaseResInfo> resDic = new();

        #endregion
        
        #region Provider 插件管理

        /// <summary>
        /// 注册Provider插件
        /// </summary>
        /// <param name="prefix">前缀</param>
        /// <param name="provider">提供器</param>
        public void AddProvider(string prefix, IResProvider provider)
        {
            prefix = string.IsNullOrWhiteSpace(prefix)
                ? string.Empty
                : prefix.Trim().ToLowerInvariant();

            if (provider == null)
                throw new ArgumentNullException(nameof(provider));

            providers[prefix] = provider;
        }

#if ENABLE_ADDRESSABLES
        /// <summary>
        /// 从可选的 Addressables 程序集创建 Provider。
        /// Runtime 核心不持有 Addressables 的编译期引用，避免包缺失时整个框架无法编译。
        /// </summary>
        private void RegisterAddressablesProvider()
        {
            const string providerTypeName =
                "FinkFramework.Runtime.ResLoad.Providers.AddressablesProvider, FinkFramework.Addressables";
            Type providerType = Type.GetType(providerTypeName);
            if (providerType == null || !typeof(IResProvider).IsAssignableFrom(providerType))
            {
                LogUtil.Error(
                    "ResManager",
                    "已检测到 Addressables 包，但 FinkFramework.Addressables 扩展程序集不可用。"
                    + "请确认框架的 Addressables 扩展未被删除且没有编译错误。");
                return;
            }

            try
            {
                if (Activator.CreateInstance(providerType) is not IResProvider provider)
                {
                    LogUtil.Error("ResManager", "无法创建 Addressables Provider。");
                    return;
                }

                AddProvider("addr", provider);
                AddProvider("addressables", provider);
            }
            catch (Exception exception)
            {
                LogUtil.Error("ResManager", $"Addressables Provider 初始化失败：{exception.Message}");
            }
        }
#endif

        /// <summary>
        /// 解析并规范化资源路径。
        /// </summary>
        /// <param name="fullPath">带协议前缀的完整路径；无前缀时使用 Resources。</param>
        /// <param name="normalizedPath"></param>
        /// <param name="prefix"></param>
        /// <param name="realPath"></param>
        /// <returns>是否为合法路径。</returns>
        private bool TryParsePath(
            string fullPath,
            out string normalizedPath,
            out string prefix,
            out string realPath)
        {
            normalizedPath = PathUtil.NormalizePath(fullPath);
            prefix = string.Empty;
            realPath = normalizedPath;

            if (string.IsNullOrEmpty(normalizedPath))
            {
                LogUtil.Error("ResManager", "资源路径不能为空。");
                return false;
            }

            int separatorIndex = normalizedPath.IndexOf("://", StringComparison.Ordinal);
            if (separatorIndex < 0)
            {
                return true;
            }

            // 协议名和协议路径都必须存在。路径正文可能包含查询参数或重定向 URL，
            // 因此这里只解析第一个协议分隔符，不限制正文再次出现 "://"。
            if (separatorIndex == 0 || separatorIndex == normalizedPath.Length - 3)
            {
                LogUtil.Error("ResManager", $"非法资源路径：{normalizedPath}");
                return false;
            }

            prefix = normalizedPath.Substring(0, separatorIndex).ToLowerInvariant();
            realPath = PathUtil.NormalizePath(normalizedPath.Substring(separatorIndex + 3));
            if (string.IsNullOrEmpty(realPath))
            {
                LogUtil.Error("ResManager", $"资源路径不能为空：{normalizedPath}");
                return false;
            }

            // 统一缓存 key 的大小写和分隔符，避免同一资源出现多条缓存记录。
            normalizedPath = prefix + "://" + realPath;
            return true;
        }

        /// <summary>
        /// 按协议前缀获取 Provider。
        /// </summary>
        private IResProvider GetProvider(string prefix)
        {
            if (providers.TryGetValue(prefix, out var provider))
                return provider;

            if (string.IsNullOrEmpty(prefix))
            {
                providers.TryGetValue(string.Empty, out provider);
                return provider;
            }

            // 前缀非空但未注册 → 明确报错
            if (prefix == "ab")
            {
                LogUtil.Error("请前往全局配置中设置资源后端系统类型为 AssetBundle！");
            }
            else if (prefix == "addr" || prefix == "addressables")
            {
                LogUtil.Error("请前往全局配置中设置资源后端系统类型为 Addressables！");
            }
            else
            {
                LogUtil.Error($"未知的资源前缀: {prefix}");
            }

            return null;
        }

        #endregion
        
        #region 同步加载资源
        
        /// <summary>
        /// 同步加载资源的方法
        /// </summary>
        /// <typeparam name="T">资源类型</typeparam>
        /// <param name="fullPath">带前缀的资源路径（若无前缀默认走 ResourcesProvider）</param>
        /// <returns>加载成功返回资源实例；失败返回 null</returns>
        public T Load<T>(string fullPath) where T : Object
        {
            if (!TryParsePath(fullPath, out string normalizedPath, out string prefix, out string realPath))
                return null;

            string resKey = BuildResKey<T>(normalizedPath);
            if (!resDic.TryGetValue(resKey, out BaseResInfo baseInfo))
            {
                IResProvider provider = GetProvider(prefix);
                if (provider == null)
                {
                    LogUtil.Error("ResManager", $"Provider 不存在，加载终止：{normalizedPath}");
                    return null;
                }

                try
                {
                    T asset = provider.Load<T>(realPath);
                    if (!asset)
                    {
                        // Provider 必须在失败返回前回滚自己的 Bundle/句柄引用。
                        LogUtil.Error("ResManager", $"同步加载失败：path={normalizedPath}, type={typeof(T).Name}");
                        return null;
                    }

                    var info = CreateResInfo<T>(normalizedPath, realPath, provider, asset);
                    resDic[resKey] = info;
                    return asset;
                }
                catch (Exception exception)
                {
                    LogUtil.Error("ResManager", $"同步加载异常：path={normalizedPath}, error={exception}");
                    return null;
                }
            }

            if (baseInfo is not ResInfo<T> infoExist)
            {
                LogUtil.Error("ResManager", $"同一路径使用了不一致的缓存类型：{normalizedPath}");
                return null;
            }

            if (infoExist.asset)
            {
                infoExist.AddRefCount();
                return infoExist.asset;
            }

            // 同步 API 不会阻塞等待异步任务，调用方应改用 LoadAsync。
            LogUtil.Warn("ResManager", $"资源正在异步加载中，同步调用返回 null：{normalizedPath}");
            return null;
        }
        
        #endregion

        #region 异步加载资源（底层实现）
        
        /// <summary>
        /// 核心异步加载方法（框架底层实现）
        /// 所有外部异步加载形式（await / callback / operation handle） 最终都基于此方法。
        /// </summary>
        /// <typeparam name="T">资源类型</typeparam>
        /// <param name="fullPath">带前缀路径（无前缀则默认 ResourcesProvider）</param>
        public async UniTask<T> LoadAsync<T>(string fullPath) where T: Object
        {
            if (!TryParsePath(fullPath, out string normalizedPath, out string prefix, out string realPath))
                return null;

            string resKey = BuildResKey<T>(normalizedPath);
            IResProvider provider = GetProvider(prefix);
            if (provider == null)
            {
                LogUtil.Error("ResManager", $"Provider 不存在，加载终止：{normalizedPath}");
                return null;
            }

            if (!resDic.TryGetValue(resKey, out BaseResInfo baseInfo))
            {
                var newInfo = CreateResInfo<T>(normalizedPath, realPath, provider, null);
                resDic[resKey] = newInfo;
                return await StartLoadAsync(resKey, newInfo, provider, normalizedPath, realPath);
            }

            if (baseInfo is not ResInfo<T> info)
            {
                LogUtil.Error("ResManager", $"同一路径使用了不一致的缓存类型：{normalizedPath}");
                return null;
            }

            info.AddRefCount();
            if (info.asset)
                return info.asset;

            if (info.task.HasValue)
                return await AwaitLoadSafely(info.task.Value, normalizedPath);

            LogUtil.Warn("ResManager", $"缓存记录缺少异步任务，重新加载：{normalizedPath}");
            return await StartLoadAsync(
                resKey,
                info,
                info.provider ?? provider,
                normalizedPath,
                info.providerPath ?? realPath);
        }

        /// <summary>
        /// 创建一条缓存记录。记录 Provider 信息后，卸载不再依赖缓存 key 的字符串格式。
        /// </summary>
        private static ResInfo<T> CreateResInfo<T>(
            string normalizedPath,
            string providerPath,
            IResProvider provider,
            T asset) where T : Object
        {
            var info = new ResInfo<T>
            {
                asset = asset,
                provider = provider,
                providerPath = providerPath,
                fullPath = normalizedPath
            };
            info.AddRefCount();
            return info;
        }

        /// <summary>
        /// 启动 Provider 加载并负责统一收尾：保存可复用任务、清理异常状态、处理失败卸载。
        /// </summary>
        private async UniTask<T> StartLoadAsync<T>(
            string resKey,
            ResInfo<T> info,
            IResProvider provider,
            string normalizedPath,
            string providerPath) where T : Object
        {
            UniTask<T> loadTask;
            try
            {
                // UniTask 默认不保证可被多个调用方重复 await；Preserve 是并发缓存的关键。
                loadTask = provider.LoadAsync<T>(providerPath).Preserve();
                info.task = loadTask;
            }
            catch (Exception exception)
            {
                RemoveInfoIfCurrent(resKey, info);
                LogUtil.Error("ResManager", $"启动异步加载异常：path={normalizedPath}, error={exception}");
                return null;
            }

            T result;
            try
            {
                result = await loadTask;
            }
            catch (Exception exception)
            {
                info.task = null;
                RemoveInfoIfCurrent(resKey, info);
                LogUtil.Error("ResManager", $"异步加载异常：path={normalizedPath}, error={exception}");
                return null;
            }

            info.task = null;
            info.asset = result;
            if (!result)
            {
                RemoveInfoIfCurrent(resKey, info);
                LogUtil.Error("ResManager", $"异步加载失败：path={normalizedPath}, type={typeof(T).Name}");
                return null;
            }

            // 加载结束前若所有使用方都请求了删除，则立即释放本次加载结果。
            if (info.refCount == 0 && info.isDel)
                ReleaseInfo(resKey, info);

            return result;
        }

        private static async UniTask<T> AwaitLoadSafely<T>(UniTask<T> task, string normalizedPath)
            where T : Object
        {
            try
            {
                return await task;
            }
            catch (Exception exception)
            {
                LogUtil.Error("ResManager", $"等待异步加载异常：path={normalizedPath}, error={exception}");
                return null;
            }
        }

        private void RemoveInfoIfCurrent(string resKey, BaseResInfo info)
        {
            if (resDic.TryGetValue(resKey, out BaseResInfo current)
                && ReferenceEquals(current, info))
            {
                resDic.Remove(resKey);
            }
        }

        private static void SafeProviderUnload(IResProvider provider, string providerPath)
        {
            if (provider == null)
                return;

            try
            {
                provider.Unload(providerPath);
            }
            catch (Exception exception)
            {
                LogUtil.Error("ResManager", $"Provider 卸载异常：path={providerPath}, error={exception}");
            }
        }

        private void ReleaseInfo(string resKey, BaseResInfo info)
        {
            if (info.provider is ResourcesProvider)
                ReleaseResourcesAsset(info.GetAsset());
            else
            {
                SafeProviderUnload(info.provider, info.providerPath);
                ReleaseProviderOwnedAsset(info.provider, info.GetAsset());
            }

            RemoveInfoIfCurrent(resKey, info);
        }

        /// <summary>
        /// 释放 File/Web Provider 在运行时创建的 Unity 对象。
        /// Resources、AssetBundle 和 Addressables 的对象由各自系统管理，不能在这里 Destroy。
        /// </summary>
        private static void ReleaseProviderOwnedAsset(IResProvider provider, Object asset)
        {
            if (asset == null || (provider is not FileProvider && provider is not WebProvider))
                return;

            if (asset is AssetBundle bundle)
                bundle.Unload(false);
            else
                Object.Destroy(asset);
        }
        
        #endregion
        
        #region 异步加载资源（回调封装）

        /// <summary>
        /// 异步加载（回调形式，无需 async/await）
        /// 用于 UI 层、热更逻辑、老项目兼容等场景。
        /// </summary>
        public void LoadAsyncCallback<T>(string fullPath, Action<T> callback) where T : Object
        {
            if (callback == null)
            {
                LogUtil.Warn("ResManager",$"异步加载资源的回调为空. path={fullPath}");
                return;
            }

            _ = LoadAsyncCallbackWrapper(fullPath, callback);
        }

        /// <summary>
        /// 内部 异步加载处理器回调封装
        /// </summary>
        private async UniTask LoadAsyncCallbackWrapper<T>(string fullPath, Action<T> callback) where T : Object
        {
            T result = await LoadAsync<T>(fullPath);

            try
            {
                callback?.Invoke(result);
            }
            catch (Exception ex)
            {
                LogUtil.Error("ResManager",$"异步加载回调报错: {ex}");
            }
        }

        #endregion

        #region 异步加载资源（句柄封装）

        /// <summary>
        /// 异步加载（返回操作句柄 ResOperation）
        /// 可用于：
        /// ✔ 加载进度条（Progress）
        /// ✔ 监听完成事件（Completed）
        /// ✔ 无需 async/await
        /// </summary>
        public ResOperation<T> LoadAsyncHandle<T>(string fullPath) where T : Object
        {
            var op = new ResOperation<T>();
            _ = LoadAsyncHandleWrapper(fullPath, op);
            return op;
        }
        
        /// <summary>
        /// 内部 异步加载处理器句柄封装
        /// </summary>
        private async UniTask LoadAsyncHandleWrapper<T>(string fullPath, ResOperation<T> op) where T : Object
        {
            op.SetProgress(0f);
            // 保证调用方拿到句柄并注册 Completed 后，完成事件不会被同步路径抢先触发。
            await UniTask.Yield();
            try
            {
                if (!TryParsePath(fullPath, out string normalizedPath, out string prefix, out string realPath))
                {
                    CompleteOperationSafely(op, null);
                    return;
                }

                var provider = GetProvider(prefix);
                if (provider == null)
                {
                    CompleteOperationSafely(op, null);
                    return;
                }

                // LoadAsync 内部会负责缓存和并发合并；这里仅轮询 Provider 进度。
                var task = LoadAsync<T>(normalizedPath);
                while (task.Status == UniTaskStatus.Pending)
                {
                    if (provider.TryGetProgress(realPath, out float progress))
                        op.SetProgress(Mathf.Clamp01(progress));
                    else
                        op.SetProgress(MathUtil.SmoothProgress(op.Progress));

                    await UniTask.Yield();
                }

                CompleteOperationSafely(op, await task);
            }
            catch (Exception exception)
            {
                LogUtil.Error("ResManager", $"句柄加载异常：path={fullPath}, error={exception}");
                CompleteOperationSafely(op, null);
            }
        }

        private static void CompleteOperationSafely<T>(ResOperation<T> operation, T result)
            where T : Object
        {
            try
            {
                operation.SetResult(result);
            }
            catch (Exception exception)
            {
                // Completed 回调属于业务代码，不能让回调异常破坏句柄的完成状态。
                LogUtil.Error("ResManager", $"资源句柄完成回调异常：{exception}");
            }
        }

        #endregion

        #region 批量加载资源
        
        /// <summary>
        /// 批量异步加载 UnityEngine.Object 资源（用于整体进度条 / Loading 界面）。
        /// 该兼容重载无法推断每个路径的真实类型；File/Web 等需要明确类型的 Provider
        /// 请使用同名泛型重载。
        /// 调用后立即返回 <see cref="BatchOperation"/> 操作句柄，不阻塞主线程，适合作为场景切换加载页的整体任务。
        /// </summary>
        /// <param name="paths">需要批量加载的完整路径列表（支持带前缀）</param>
        /// <returns>批量加载操作句柄 <see cref="BatchOperation"/></returns>
        public BatchOperation BatchLoadAsync(List<string> paths)
        {
            var op = new BatchOperation();
            _ = LoadGroupAsyncWrapper<Object>(paths, op);
            return op;
        }

        /// <summary>
        /// 按指定类型批量加载资源。相比无类型重载，这个版本可用于 File/Web 等
        /// 需要明确资源类型的 Provider。
        /// </summary>
        public BatchOperation BatchLoadAsync<T>(List<string> paths) where T : Object
        {
            var op = new BatchOperation();
            _ = LoadGroupAsyncWrapper<T>(paths, op);
            return op;
        }

        /// <summary>
        /// 批量加载异步处理器（内部实现）
        /// --------------------------------------------------------------------
        /// 对每个资源依次调用底层 <see cref="LoadAsync{T}"/>，并根据完成数量更新 BatchOperation 的 Progress。
        /// 注意：
        /// - 若某个资源加载失败，将返回 null，并仍然计入 Results，确保顺序一致。
        /// - 当前实现按顺序依次加载，不会并行加载，保证 Results 与输入路径顺序一致。
        /// </summary>
        private async UniTask LoadGroupAsyncWrapper<T>(List<string> paths, BatchOperation op)
            where T : Object
        {
            // 空列表也异步完成，确保调用方可以先注册 Completed。
            await UniTask.Yield();
            try
            {
                // 无资源可加载 → 直接完成
                if (paths == null || paths.Count == 0)
                {
                    op.Finish();
                    return;
                }

                int total = paths.Count;
                int loaded = 0;

                foreach (string path in paths)
                {
                    var asset = await LoadAsync<T>(path);
                    // 无论成功或失败，都加入结果列表，使调用者可以保持输入顺序。
                    op.AddResult(asset);

                    loaded++;
                    op.SetProgress((float)loaded / total);
                }

                op.Finish();
            }
            catch (Exception exception)
            {
                LogUtil.Error("ResManager", $"批量加载异常：{exception}");
                op.Finish();
            }
        }

        #endregion

        #region 指定卸载单个资源

        /// <summary>
        /// 指定卸载一个资源（引用计数 -1；若资源已加载且 refCount=0 且 isDel=true 则立即卸载）
        /// </summary>
        /// <param name="fullPath">带前缀的完整的需要卸载的资源路径</param>
        /// <param name="isDel">标记是否需要马上移除</param>
        /// <param name="isSub">为了防止减两次 在内部调用的时候标记为不减</param>
        /// <typeparam name="T">需要卸载的资源类型</typeparam>
        public void UnloadAsset<T>(string fullPath,bool isDel = false, bool isSub = true) where T : Object
        {
            if (!TryParsePath(fullPath, out string normalizedPath, out string prefix, out string providerPath))
                return;

            string resName = BuildResKey<T>(normalizedPath);
            // 若字典内无记录该资源信息说明资源已经不存在 无需删除 直接返回
            if (!resDic.TryGetValue(resName, out var value))
                return;
            // 获取资源信息
            if (value is not ResInfo<T> resInfo)
            {
                LogUtil.Error("ResManager", $"卸载时发现缓存类型不一致：{normalizedPath}");
                return;
            }
            // 引用计数减 1（若 isSub=false 则跳过）
            if (isSub && !resInfo.TrySubRefCount())
                return;

            // 删除标记是单向的，避免后续普通卸载把待删除状态意外清除。
            resInfo.isDel |= isDel;
            // ================================
            // ① 若资源已加载完成 refCount==0 && isDel==true → 立即卸载
            // ================================
            if (resInfo.asset && resInfo.refCount == 0 && resInfo.isDel)
            {
                ReleaseInfo(resName, resInfo);
                return;
            }
            // ================================
            // ② 若资源仍在异步加载中（task!=null）
            //    不立即卸载，让 LoadAsync 完成后判断 refCount & isDel
            // ================================
            if (!resInfo.asset && resInfo.task.HasValue)
                return;
            // ================================
            // ③ 资源未加载且无任务 → 不需要卸载（缓存状态异常）
            // ================================
            if (!resInfo.asset && !resInfo.task.HasValue)
            {
                // 移除损坏的缓存记录，避免后续调用反复命中空状态。
                LogUtil.Warn("ResManager", $"移除异常缓存记录：{normalizedPath}");
                SafeProviderUnload(resInfo.provider ?? GetProvider(prefix), resInfo.providerPath ?? providerPath);
                RemoveInfoIfCurrent(resName, resInfo);
            }
        }
        
        #endregion

        #region 异步卸载全部未使用资源
        
        /// <summary>
        /// 异步卸载所有未使用资源 (资源引用数为0 且被标记为需要删除)
        /// </summary>
        public async UniTask UnloadUnusedAssets()
        {
            // 在真正移除不使用的资源之前 应该先将引用计数为0且被标记为需要删除的资源删除
            List<string> removeList = new();
            // Step 1：收集所有可以卸载的资源（资源引用数为0 且被标记为需要删除）
            foreach (var kv in resDic)
            {
                if (kv.Value.refCount == 0 && kv.Value.isDel)
                    removeList.Add(kv.Key);
            }
            // Step 2：统一卸载记录在待卸载列表里的资源本体 + 移除缓存
            foreach (string key in removeList)
            {
                if (resDic.TryGetValue(key, out var baseInfo))
                    ReleaseInfo(key, baseInfo);
            }
            // Step 3：系统清理未引用资源
            await Resources.UnloadUnusedAssets();
        }

        /// <summary>
        /// 释放可单独卸载的 Resources 资源。
        /// GameObject、Component 与 AssetBundle 不能传入 Resources.UnloadAsset；
        /// 移除框架引用后交由 Resources.UnloadUnusedAssets 在合适时机统一回收。
        /// </summary>
        private static void ReleaseResourcesAsset(Object asset)
        {
            if (!asset
                || asset is GameObject
                || asset is Component
                || asset is AssetBundle)
                return;

            Resources.UnloadAsset(asset);
        }
        
        #endregion

        #region 清空 / 统计

        /// <summary>
        /// 获取当前资源的引用计数
        /// </summary>
        /// <param name="fullPath">完整的带前缀的资源路径</param>
        /// <typeparam name="T">资源类型</typeparam>
        /// <returns></returns>
        public int GetRefCount<T>(string fullPath) where T : Object
        {
            if (!TryParsePath(fullPath, out string normalizedPath, out _, out _))
                return 0;

            string resName = BuildResKey<T>(normalizedPath);
            return resDic.TryGetValue(resName, out BaseResInfo baseInfo)
                && baseInfo is ResInfo<T> info
                ? info.refCount
                : 0;
        }

        /// <summary>
        /// 异步调用：清理当前缓存和 Provider 资源。进行中的请求会先完成，避免中断底层加载。
        /// </summary>
        public async UniTask ClearDicAsync()
        {
            var records = new List<KeyValuePair<string, BaseResInfo>>(resDic);
            var pendingRecords = new List<KeyValuePair<string, BaseResInfo>>();
            foreach (var record in records)
            {
                if (record.Value.IsLoading)
                {
                    record.Value.refCount = 0;
                    record.Value.isDel = true;
                    pendingRecords.Add(record);
                }
                else
                {
                    ReleaseInfo(record.Key, record.Value);
                }
            }

            if (pendingRecords.Count > 0)
            {
                await ClearDicAfterPendingLoadsAsync(pendingRecords, null);
                return;
            }

            ClearProviders();
            resDic.Clear();
            // 释放 Unity 未引用资源
            await Resources.UnloadUnusedAssets();
        }
        
        /// <summary>
        /// 同步调用：清理当前缓存（适用于场景切换）。若存在进行中的请求，会在请求完成后
        /// 延迟清理本次调用捕获的记录，并在完成后触发 callback。
        /// </summary>
        /// <param name="callback"></param>
        public void ClearDic(UnityAction callback = null)
        {
            var records = new List<KeyValuePair<string, BaseResInfo>>(resDic);
            var pendingRecords = new List<KeyValuePair<string, BaseResInfo>>();
            foreach (var record in records)
            {
                if (record.Value.IsLoading)
                {
                    // 清理边界已经到达：旧调用方不再持有引用，加载完成后可安全释放。
                    record.Value.refCount = 0;
                    record.Value.isDel = true;
                    pendingRecords.Add(record);
                }
                else
                {
                    ReleaseInfo(record.Key, record.Value);
                }
            }

            if (pendingRecords.Count > 0)
            {
                // 同步 API 不能安全地中断 Provider 请求，延迟处理本次捕获的记录。
                _ = ClearDicAfterPendingLoadsAsync(pendingRecords, callback);
                return;
            }

            ClearProviders();
            resDic.Clear();
            AsyncOperation unloadOperation = Resources.UnloadUnusedAssets();
            if (callback != null)
                unloadOperation.completed += _ => callback();
        }

        private async UniTask ClearDicAfterPendingLoadsAsync(
            List<KeyValuePair<string, BaseResInfo>> records,
            UnityAction callback)
        {
            foreach (var record in records)
                await record.Value.WaitForLoadAsync();

            // 给 StartLoadAsync 一次机会完成缓存状态回写和按标记释放。
            await UniTask.Yield();
            foreach (var record in records)
            {
                if (resDic.TryGetValue(record.Key, out BaseResInfo current)
                    && ReferenceEquals(current, record.Value)
                    && record.Value.refCount == 0)
                {
                    if (record.Value.GetAsset())
                        ReleaseInfo(record.Key, record.Value);
                    else
                        resDic.Remove(record.Key);
                }
            }

            await Resources.UnloadUnusedAssets().ToUniTask();
            try
            {
                callback?.Invoke();
            }
            catch (Exception exception)
            {
                LogUtil.Error("ResManager", $"清理完成回调异常：{exception}");
            }
        }

        private void ClearProviders()
        {
            // 同一个 Provider 可能注册了多个协议别名，只清理一次。
            var uniqueProviders = new HashSet<IResProvider>(providers.Values);
            foreach (IResProvider provider in uniqueProviders)
                provider.Clear();
        }

        #endregion

        #region 工具方法

        /// <summary>
        /// 创建资源唯一id
        /// </summary>
        /// <typeparam name="T">资源的类名</typeparam>
        /// <returns></returns>
        private static string BuildResKey<T>(string normalizedPath)
        {
            return $"{normalizedPath}_{typeof(T).AssemblyQualifiedName}";
        }
        
        #endregion
    }
}
