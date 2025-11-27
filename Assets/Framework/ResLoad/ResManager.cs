using System;
using System.Collections;
using System.Collections.Generic;
using Framework.Data.Runtime;
using Framework.Mono;
using Framework.ResLoad.Base;
using Framework.ResLoad.Providers;
using Framework.Singleton;
using Framework.Utils;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;
// ReSharper disable All

namespace Framework.ResLoad
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
            AddProvider("res", new ResourcesProvider());
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
            fullPath = FilesUtil.NormalizePath(fullPath);
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
            if (providers.TryGetValue(prefix, out var p))
            {
                return p;
            }
            else
            {
                LogUtil.Error($"未知的资源前缀: {prefix}");
                return providers[""];
            }
        }

        #endregion
        
        #region 同步加载资源
        
        /// <summary>
        /// 同步加载资源的方法
        /// </summary>
        /// <typeparam name="T">需要加载的资源类型</typeparam>
        /// <param name="fullPath">带前缀的资源路径（若无前缀默认Resources）</param>
        /// <returns>返回同步加载好的资源</returns>
        public T Load<T>(string fullPath) where T : Object
        {
            // 解析前缀与实际资源路径
            var (prefix, path) = ParsePath(fullPath);
            // 资源的唯一ID 通过 路径名_资源类型 规则命名
            string resName = BuildResKey<T>(fullPath);
            ResInfo<T> info;
            // 若字典内无该资源的记录
            if (!resDic.TryGetValue(resName,out var value))
            {
                // 使用 Provider 同步加载资源 并记录资源信息放入字典内
                IResProvider provider = GetProvider(fullPath);
                T res = provider.Load<T>(path);
                // 构造该资源信息
                info = new ResInfo<T>
                {
                    asset = res
                };
                // 引用计数增加
                info.AddRefCount();
                resDic.Add(resName,info);
                return res;
            }
            // 若字典内有该资源的记录 
            else
            {
                // 从字典中取出该资源的记录
                info = value as ResInfo<T>;
                // 引用计数增加
                info.AddRefCount();
                // 若存在异步加载该资源的协程在执行中(即处于未能加载完毕的状态) → 强制终止异步协程，转同步
                if (!info.asset)
                {
                    // 停止异步加载协程
                    MonoManager.Instance.StopCoroutine(info.coroutine);
                    // 直接使用 Provider 同步加载资源 
                    IResProvider provider = GetProvider(fullPath);
                    T res = provider.Load<T>(path);
                    info.asset = res;
                    // 对加载的资源做判空
                    if (!res)
                    {
                        LogUtil.Error($"[ResManager] 加载失败 => path: {path}, type: {typeof(T).Name}");
                        resDic.Remove(resName); // 自动移除无效缓存
                    }
                    // 已同步加载完毕资源 因此手动调用一下加载完毕的回调函数
                    info.callback?.Invoke(res);
                    // 回调结束 异步加载也被停止 清除无用的引用
                    info.coroutine = null;
                    info.callback = null;
                    return res;
                }
                // 资源异步加载完毕
                return info.asset;
            }
        }
        
        #endregion

        #region 异步加载资源
        
        /// <summary>
        /// 异步加载资源的方法(泛型)
        /// </summary>
        /// <typeparam name="T">资源类型</typeparam>
        /// <param name="fullPath">带前缀的资源路径（若无前缀默认Resources）</param>
        /// <param name="callback">加载结束后的回调函数 当异步加载资源结束后才会调用</param>
        public void LoadAsync<T>(string fullPath, UnityAction<T> callback) where T: Object
        {
            // 解析前缀与实际资源路径
            var (prefix, path) = ParsePath(fullPath);
            // 资源的唯一ID 通过 路径名_资源类型 规则命名
            string resName = BuildResKey<T>(fullPath);
            // 若字典内无该资源的记录
            if (!resDic.TryGetValue(resName,out var value))
            {
                // 声明或者实例化一个资源信息对象
                var info = new ResInfo<T>();
                // 引用计数增加
                info.AddRefCount();
                // 将资源信息添加到字典中(此时资源还未初始化且还未加载完毕)
                resDic.Add(resName,info);
                // 记录传入的委托函数 等待加载完毕再调用
                info.callback += callback;
                //开启协程进行Provider 异步加载 并记录协程(用于之后可能的停止)
                // --------  --------
                info.coroutine = MonoManager.Instance.StartCoroutine(
                    ReallyLoadAsync(fullPath, path, info)
                );
                return;
            }
            // 若字典内有该资源的记录 
            else
            {
                // 从字典中取出资源信息
                var info = value as ResInfo<T>;
                // 引用计数增加
                info.AddRefCount();
                // 若该资源还未完全加载完毕——正在加载中（由于asset是在加载完毕才记录的 因此此时asset为空等于还未加载完毕）
                if (!info.asset)
                {
                    info.callback += callback;
                }
                // asset不为空说明已经加载完毕
                else
                {
                    // 直接执行回调函数返回加载好的资源
                    callback?.Invoke(info.asset);
                }
            }
        }

        /// <summary>
        /// 异步加载资源的协程(泛型)
        /// </summary>
        /// <param name="fullPath">完整路径（含前缀）</param>
        /// <param name="realPath">解析后的真实路径</param>
        /// <param name="info">缓存的 ResInfo</param>
        /// <typeparam name="T">资源类型</typeparam>
        /// <returns>返回异步加载好的资源</returns>
        private IEnumerator ReallyLoadAsync<T>(string fullPath, string realPath, ResInfo<T> info) where T : Object
        {
            // 记录加载结果
            T result = null;
            // 记录是否加载完毕
            bool done = false;
            // 使用 provider 异步加载资源
            IResProvider provider = GetProvider(fullPath);
            provider.LoadAsync<T>(realPath, r =>
            {
                result = r;
                done = true;
            });
            //等待资源加载结束后 才会继续执行yield return后面的代码
            while (!done) yield return null;
            // 资源的唯一ID 通过 路径名_资源类型 规则命名
            string resName = BuildResKey<T>(fullPath);
            // 记录资源
            info.asset = result;
            // 如果加载失败
            if (!result)
            {
                Debug.LogError($"[ResManager] 异步加载失败 => path: {realPath}");
                resDic.Remove(resName);
                yield break;
            }
            // 已经被标记删除，且 refCount==0
            if (info.refCount == 0 && info.isDel)
            {
                UnloadAsset<T>(fullPath, true);
                yield break;
            }
            // 调用回调
            info.callback?.Invoke(result);
            // 清理无用引用
            info.callback = null;
            info.coroutine = null;
        }
        
        #endregion

        #region 指定卸载单个资源

        /// <summary>
        /// 指定卸载一个资源
        /// </summary>
        /// <param name="fullPath">带前缀的完整的需要卸载的资源路径</param>
        /// <param name="isDel">标记是否需要马上移除</param>
        /// <param name="callback">回调函数</param>
        /// <param name="isSub">为了防止减两次 在内部调用的时候标记为不减</param>
        /// <typeparam name="T">需要卸载的资源类型</typeparam>
        public void UnloadAsset<T>(string fullPath,bool isDel = false, UnityAction<T> callback = null, bool isSub = true) where T : Object
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
            // 引用计数减一
            if (isSub)
            {
                resInfo.SubRefCount();
            }
            // 记录该资源是否被标记了需要马上移除的标签
            resInfo.isDel = isDel;
            // 资源已经加载完毕 且引用计数为零 即未被使用的状态时
            if (resInfo.asset && resInfo.refCount == 0 && resInfo.isDel)
            {
                // 使用 Provider 移除内存加载的资源
                var provider = GetProvider(fullPath);
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
            }
            // 资源正在异步加载中
            else if(!resInfo.asset)
            {
                // 当异步加载不想使用时 应该移除回调记录 而不是直接卸载资源
                if (callback != null)
                {
                    resInfo.callback -= callback;
                }
            }
        }
        
        #endregion

        #region 异步卸载全部未使用资源
        
        /// <summary>
        /// 异步卸载所有未使用资源 (资源引用数为0 且被标记为需要删除)
        /// </summary>
        /// <param name="callBack">回调函数</param>
        public void UnloadUnusedAssets(UnityAction callBack)
        {
            MonoManager.Instance.StartCoroutine(ReallyUnloadUnusedAssets(callBack));
        }

        /// <summary>
        /// 异步卸载所有未使用资源 的协程
        /// </summary>
        /// <param name="callback">回调函数</param>
        /// <returns></returns>
        private IEnumerator ReallyUnloadUnusedAssets(UnityAction callback)
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
            // Step 3：系统级卸载（清理未引用资源）
            AsyncOperation op = Resources.UnloadUnusedAssets();
            yield return op;
            //卸载完毕后 通知外部
            callback?.Invoke();
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
        /// <param name="callback"></param>
        public void ClearDicAsync(UnityAction callback = null)
        {
            MonoManager.Instance.StartCoroutine(ReallyClearDic(callback));
        }

        private IEnumerator ReallyClearDic(UnityAction callback)
        {
            // 清 provider 自己的缓存（主要针对 ABProvider、WebProvider 等非Resources的提供器 必须执行）
            foreach (var p in providers.Values)
            {
                p.Clear();
            }
            resDic.Clear();
            AsyncOperation ao = Resources.UnloadUnusedAssets();
            yield return ao;
            //卸载完毕后 通知外部
            callback();
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
            callback();
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
