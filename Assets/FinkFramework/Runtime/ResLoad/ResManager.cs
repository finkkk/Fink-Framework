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
            // 注册默认 provider（无前缀 / res://）
            AddProvider("", new ResourcesProvider());
            
            // 注册 Resources 加载模块
            AddProvider("res", new ResourcesProvider());
            
            // 注册 File 加载模块
            AddProvider("file",new FileProvider());
            
            // 注册 Web 加载模块
            AddProvider("http", new WebProvider());
            AddProvider("https", new WebProvider());
            
            // 注册 AssetBundle 加载模块
            if (GlobalSettingsRuntimeLoader.Current.ResourceBackend == EnvironmentState.ResourceBackendType.AssetBundle)
            {
                var settings = GlobalSettingsRuntimeLoader.Current.AssetBundleSettings;
                var provider = new ABProvider();
                provider.Initialize(settings);
                AddProvider("ab", provider);
            }
            // 注册 addressables 加载模块
#if ENABLE_ADDRESSABLES
            if (GlobalSettingsRuntimeLoader.Current.ResourceBackend == EnvironmentState.ResourceBackendType.Addressables)
            {
                var addrProvider = new AddressablesProvider();
                AddProvider("addr", addrProvider);
                AddProvider("addressables", addrProvider);
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
            providers[prefix] = provider;
        }

        /// <summary>
        /// 解析提供器专用路径
        /// </summary>
        /// <param name="fullPath">完整路径</param>
        /// <returns>解析后的提供器可识别的路径</returns>
        private (string prefix, string realPath) ParsePath(string fullPath)
        {
            // 规范化路径
            fullPath = PathUtil.NormalizePath(fullPath);
            // 1) 禁止以 :// 开头（前缀为空导致格式异常）
            if (fullPath.StartsWith("://"))
            {
                LogUtil.Error($"非法资源路径（前缀为空但包含 :// ）: {fullPath}");
                return ("", fullPath);
            }
            // 2) 禁止以 : 开头（本质也是非法格式）
            if (fullPath.StartsWith(":"))
            {
                LogUtil.Error($"非法资源路径（不能以 : 开头）: {fullPath}");
                return ("", fullPath);
            }
            // 3) 判断是否包含前缀
            int idx = fullPath.IndexOf("://", StringComparison.Ordinal);
            if (idx < 0)
            {
                // 无前缀 → ResourcesProvider
                return ("", fullPath);
            }
            // 4) 解析合法前缀
            string prefix = fullPath.Substring(0, idx);
            string real = fullPath.Substring(idx + 3);
            real = PathUtil.NormalizePath(real);
            // 5) 返回前缀和真实路径
            return (prefix, real);
        }

        /// <summary>
        /// 传入一个完整路径来获取解析出的提供器
        /// </summary>
        /// <param name="fullPath">完整路径</param>
        /// <returns>提供器</returns>
        private IResProvider GetProvider(string fullPath)
        {
            var (prefix, _) = ParsePath(fullPath);
           
            // 前缀存在且已注册 → 正常返回
            if (providers.TryGetValue(prefix, out var provider))
                return provider;

            // 前缀为空 → 默认 ResourcesProvider
            if (string.IsNullOrEmpty(prefix))
            {
                return providers[""];
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
            // 解析前缀与实际资源路径
            var (prefix, realPath) = ParsePath(fullPath);
            // 资源的唯一ID 通过 路径名_资源类型 规则命名
            string resName = BuildResKey<T>(fullPath);
            // ============================
            // ① 缓存中无记录 → 首次同步加载
            // ============================
            if (!resDic.TryGetValue(resName,out var value))
            {
                // 使用 Provider 同步加载资源
                IResProvider provider = GetProvider(fullPath);
                if (provider == null)
                {
                    LogUtil.Error($"[ResManager] Provider 不存在，加载终止 => {fullPath}");
                    return null;
                }
                T res = provider.Load<T>(realPath);
                // 加载失败，直接返回，不写入缓存
                if (!res)
                {
                    LogUtil.Error($"[ResManager] 同步加载失败 => path: {realPath}, type: {typeof(T).Name}");
                    return null;
                }
                // 创建资源信息结构 ResInfo（只有成功加载才创建）
                var info = new ResInfo<T>();
                // 把加载好的资源传给资源信息
                info.asset = res;
                // 引用计数增加
                info.AddRefCount();
                // 写入缓存（使用索引器避免重复 Add 崩溃）
                resDic[resName] = info; 
                return res;
            }
            // ============================
            // ② 缓存中已有记录
            // ============================
            // 从字典中取出该资源的记录
            var infoExist = value as ResInfo<T>;
            // 引用计数增加
            infoExist.AddRefCount();
            // 如果已加载完毕 则直接返回加载的资源
            if (infoExist.asset)
                return infoExist.asset;
            // ================================================================
            // ③ asset=null && task=null → 异常状态
            // ================================================================
            LogUtil.Warn($"[ResManager] 异常状态：asset=null & task=null => {fullPath}");
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
            // 解析前缀与实际资源路径
            var (prefix, realPath) = ParsePath(fullPath);
            // 资源的唯一ID 通过 路径名_资源类型 规则命名
            string resName = BuildResKey<T>(fullPath);
            
            // ============================
            // ① 缓存中无记录 → 首次加载
            // ============================
            if (!resDic.TryGetValue(resName,out var value))
            {
                // 缓存中没有 → 创建新记录
                var info = new ResInfo<T>();
                // 引用计数增加
                info.AddRefCount();
                // 将资源信息添加到字典中(此时资源还未初始化且还未加载完毕) 
                resDic[resName] = info;  // 使用索引器比add更稳健，不会因旧数据残留崩溃
                // 开始Provider 异步加载
                var provider = GetProvider(fullPath);
                if (provider == null)
                {
                    LogUtil.Error($"[ResManager] Provider 不存在，加载终止 => {fullPath}");
                    return null;
                }
                // 开始真正的加载任务
                var loadTask = provider.LoadAsync<T>(realPath);
                info.task = loadTask;
                // 等待加载完成
                T result = await loadTask;
                // 记录资源 清除 task
                info.asset = result;
                info.task = null;
                // 如果加载失败
                if (!result)
                {
                    LogUtil.Error($"[ResManager] 加载失败 => path: {realPath}, type: {typeof(T).Name}");
                    resDic.Remove(resName);
                    return null;
                }
                // 若在加载结束期间被标记为删除
                if (info.refCount == 0 && info.isDel)
                {
                    UnloadAsset<T>(fullPath, true, isSub: false);
                }
                return result;
            }
            // ============================
            // ② 缓存中已有记录
            // ============================
            // 从字典中取出资源信息
            var existInfo = value as ResInfo<T>;
            // 引用计数增加
            existInfo.AddRefCount();
            // 资源已经加载完成 → 直接返回
            if (existInfo.asset)
                return existInfo.asset;
            // 资源正在异步加载中 → 等待任务完成
            if (existInfo.task.HasValue)
                return await existInfo.task.Value;
            
            // ============================
            // ③ 既没有 asset，也没有 task 则代表任务丢失（异常状态） → 启动托底重新加载机制
            // ============================
            LogUtil.Warn($"[ResManager] Task lost (asset=null & task=null)，启动托底重新加载 => {fullPath}");
            // ===== 以下部分为托底重新加载机制 =====
            // 获取 Provider
            var providerReload = GetProvider(fullPath);
            if (providerReload == null)
            {
                LogUtil.Error($"[ResManager] Provider 不存在，加载终止 => {fullPath}");
                return null;
            }
            // 重新启动异步加载
            var reloadTask = providerReload.LoadAsync<T>(realPath);
            existInfo.task = reloadTask;
            // 等待重新加载
            T reloadResult = await reloadTask;
            // 清理任务引用
            existInfo.task = null;
            existInfo.asset = reloadResult;
            // 如果仍然加载失败 → 移除缓存
            if (!reloadResult)
            {
                LogUtil.Error($"[ResManager] 重新加载失败 => path: {realPath}, type: {typeof(T).Name}");
                resDic.Remove(resName);
                return null;
            }
            return reloadResult;
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
            // 解析 prefix 和 realPath
            var (prefix, realPath) = ParsePath(fullPath);
            var provider = GetProvider(fullPath);
            if (provider == null)
            {
                LogUtil.Error($"[ResManager] Provider 不存在，加载终止 => {fullPath}");
                return;
            }
            // 启动真正的加载任务
            var task = LoadAsync<T>(fullPath);
            
            // 只用于“是否完成”的轮询 
            // 若 Provider 支持真实进度，进行实时进度绑定；若 Resources 无真实进度 → 使用平滑插值模拟
            while (task.Status == UniTaskStatus.Pending)
            {
                // 若 Provider 支持真实进度
                if (provider.TryGetProgress(realPath, out float p))
                {
                    op.SetProgress(p);
                }
                else
                {
                    // 使用 Lerp 模拟更自然的进度（最多到 0.9）
                    op.SetProgress(MathUtil.SmoothProgress(op.Progress));
                }
                await UniTask.Yield();
            }
            // 等待最终结果
            var result = await task;
            // 设置结果（会自动把进度设为 1.0）
            op.SetResult(result);
        }

        #endregion

        #region 批量加载资源
        
        /// <summary>
        /// 批量异步加载资源（用于整体进度条 / Loading 界面）
        /// 调用后立即返回 <see cref="BatchOperation"/> 操作句柄，不阻塞主线程，适合作为场景切换加载页的整体任务。
        /// </summary>
        /// <param name="paths">需要批量加载的完整路径列表（支持带前缀）</param>
        /// <returns>批量加载操作句柄 <see cref="BatchOperation"/></returns>
        public BatchOperation BatchLoadAsync(List<string> paths)
        {
            var op = new BatchOperation();
            _ = LoadGroupAsyncWrapper(paths, op);
            return op;
        }

        /// <summary>
        /// 批量加载异步处理器（内部实现）
        /// --------------------------------------------------------------------
        /// 对每个资源依次调用底层 <see cref="LoadAsync{T}"/>，并根据完成数量更新 BatchOperation 的 Progress。
        /// 注意：
        /// - 若某个资源加载失败，将返回 null，并仍然计入 Results，确保顺序一致。
        /// - 当前实现针对resources考虑按顺序依次加载，不会并行加载（更安全，不产生资源竞争）
        /// </summary>
        private async UniTask LoadGroupAsyncWrapper(List<string> paths, BatchOperation op)
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
                // 利用底层 LoadAsync，支持任何类型，只需要自动推断
                var asset = await LoadAsync<Object>(path);
                // 无论成功或失败，都加入结果列表，使调用者自己判断
                op.AddResult(asset);

                loaded++;
                op.SetProgress((float)loaded / total);
            }

            op.Finish();
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
            // 解析前缀与实际资源路径
            var (prefix, path) = ParsePath(fullPath);
            // 资源的唯一ID 通过 路径名_资源类型 规则命名
            string resName = BuildResKey<T>(fullPath);
            // 若字典内无记录该资源信息说明资源已经不存在 无需删除 直接返回
            if (!resDic.TryGetValue(resName, out var value))
                return;
            // 获取资源信息
            var resInfo = value as ResInfo<T>;
            // 引用计数减 1（若 isSub=false 则跳过）
            if (isSub)
            {
                resInfo.SubRefCount();
            }
            // 记录该资源是否被标记了需要马上移除的标签
            resInfo.isDel = isDel;
            // ================================
            // ① 若资源已加载完成 refCount==0 && isDel==true → 立即卸载
            // ================================
            if (resInfo.asset && resInfo.refCount == 0 && resInfo.isDel)
            {
                // 使用 Provider 移除内存加载的资源
                var provider = GetProvider(fullPath);
                if (provider == null)
                {
                    LogUtil.Error($"[ResManager] Provider 不存在，加载终止 => {fullPath}");
                    return;
                }
                if (provider is ResourcesProvider)
                {
                    // Resources 必须通过对象卸载
                    if (resInfo.asset)
                        Resources.UnloadAsset(resInfo.asset);
                }
                else
                {
                    // AB / Web / File等其余情况 由 provider 自己卸载
                    provider.Unload(path);
                }
                // 从字典移除记录
                resDic.Remove(resName);
                return;
            }
            // ================================
            // ② 若资源仍在异步加载中（task!=null）
            //    不立即卸载，让 LoadAsync 完成后判断 refCount & isDel
            // ================================
            if (!resInfo.asset && resInfo.task.HasValue)
            {
                // 不做额外操作：任务完成后 LoadAsync 内部会自动判断
                return;
            }
            // ================================
            // ③ 资源未加载且无任务 → 不需要卸载（缓存状态异常）
            // ================================
            if (!resInfo.asset && !resInfo.task.HasValue)
            {
                // 数值异常（几乎不会达到这一分支）
                LogUtil.Warn($"[ResManager] 尝试卸载异常状态资源 => {fullPath}");
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
                {
                    Object asset = baseInfo.GetAsset();
                    // 直接反推出 fullPath
                    string fullPath = key.Substring(0, key.LastIndexOf('_'));
                    // 解析 prefix 和 realPath
                    var (prefix, realPath) = ParsePath(fullPath);
                    // 使用 Provider 移除内存加载的资源
                    var provider = GetProvider(fullPath);
                    if (provider == null)
                    {
                        LogUtil.Error($"[ResManager] Provider 不存在，加载终止 => {fullPath}");
                        return;
                    }
                    if (provider is ResourcesProvider)
                    {
                        // Resources 必须通过对象卸载
                        if (asset) Resources.UnloadAsset(asset);
                    }
                    else
                    {
                        // AB / Web / File等其余情况 由 provider 自己卸载
                        provider.Unload(realPath);
                    }
                }
                resDic.Remove(key);
            }
            // Step 3：系统清理未引用资源
            await Resources.UnloadUnusedAssets();
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
            // 资源的唯一ID 通过 路径名_资源类型 规则命名
            string resName = BuildResKey<T>(fullPath);
            // 从字典中查询该资源 若查不到直接返回0
            return resDic.TryGetValue(resName,out var info) ? (info as ResInfo<T>).refCount : 0;
        }

        /// <summary>
        /// 异步调用：手动清空字典记录数据(适用于不想使用手动卸载资源时)
        /// </summary>
        public async UniTask ClearDicAsync()
        {
            // 清 provider 自己的缓存（主要针对 ABProvider、WebProvider 等非Resources的提供器 必须执行）
            foreach (var p in providers.Values)
            {
                p.Clear();
            }
            resDic.Clear();
            // 释放 Unity 未引用资源
            await Resources.UnloadUnusedAssets();
        }
        
        /// <summary>
        /// 同步调用：手动清空字典记录数据(适用于不想使用手动卸载资源时 比如切换场景时执行)
        /// </summary>
        /// <param name="callback"></param>
        public void ClearDic(UnityAction callback = null)
        {
            // 清 provider 自己的缓存（主要针对 ABProvider、WebProvider 等非Resources的提供器 必须执行）
            foreach (var p in providers.Values)
                p.Clear();
            resDic.Clear();
            Resources.UnloadUnusedAssets();
            //卸载完毕后 通知外部
            if (callback != null)
            {
                callback();
            }
        }

        #endregion

        #region 工具方法

        /// <summary>
        /// 创建资源唯一id
        /// </summary>
        /// <param name="fullPath">带前缀的完整路径</param>
        /// <typeparam name="T">资源的类名</typeparam>
        /// <returns></returns>
        private string BuildResKey<T>(string fullPath)
        {
            return $"{fullPath}_{typeof(T).Name}";
        }
        
        #endregion
    }
}
