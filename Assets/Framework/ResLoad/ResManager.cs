using System.Collections;
using System.Collections.Generic;
using Framework.Mono;
using Framework.Singleton;
using Framework.Utils;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;
// ReSharper disable All

namespace Framework.ResLoad
{
    /// <summary>
    /// 资源加载模块管理器
    /// </summary>
    public class ResManager : Singleton<ResManager>
    {
        private ResManager() { }
        // 用于存储加载过或者加载中的资源信息
        private readonly Dictionary<string, BaseResInfo> resDic = new();

        #region 异步加载资源
        
        /// <summary>
        /// 异步加载资源的方法(泛型)
        /// </summary>
        /// <typeparam name="T">资源类型</typeparam>
        /// <param name="path">资源路径（Resources下的）</param>
        /// <param name="callback">加载结束后的回调函数 当异步加载资源结束后才会调用</param>
        public void LoadAsync<T>(string path, UnityAction<T> callback) where T: Object
        {
            ResInfo<T> info;
            // 资源的唯一ID 通过 路径名_资源类型 规则命名
            string resName = path + "_" + typeof(T).Name;
            if (!resDic.TryGetValue(resName,out var value))
            {
                // 声明或者实例化一个资源信息对象
                info = new ResInfo<T>();
                // 引用计数增加
                info.AddRefCount();
                // 将资源信息添加到字典中(此时资源还未初始化且还未加载完毕)
                resDic.Add(resName,info);
                // 记录传入的委托函数 等待加载完毕再调用
                info.callback += callback;
                //开启协程进行异步加载 并记录协程(用于之后可能的停止)
                info.coroutine = MonoManager.Instance.StartCoroutine(ReallyLoadAsync<T>(path));
            }
            else
            {
                // 从字典中取出资源信息
                info = value as ResInfo<T>;
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
        /// <param name="path">资源路径</param>
        /// <typeparam name="T">资源类型</typeparam>
        /// <returns>返回异步加载好的资源</returns>
        private IEnumerator ReallyLoadAsync<T>(string path) where T : Object
        {
            //异步加载资源
            ResourceRequest rq = Resources.LoadAsync<T>(path);
            //等待资源加载结束后 才会继续执行yield return后面的代码
            yield return rq;
            // 资源的唯一ID 通过 路径名_资源类型 规则命名
            string resName = path + "_" + typeof(T).Name;
            //资源加载结束 将资源传到外部的委托函数去进行使用
            if (resDic.TryGetValue(resName,out var info))
            {
                // 取出资源信息
                ResInfo<T> resInfo = info as ResInfo<T>;
                // 记录加载完毕的资源
                resInfo.asset = rq.asset as T;
                // 对加载的资源做判空(Resources本身的异步加载方法若路径不对或者类型错误是不会抛出错误的)
                if (!resInfo.asset)
                {
                    Debug.LogError($"[ResManager] 加载失败：路径不存在或类型错误 => path: {path}, type: {typeof(T).Name}");
                    resDic.Remove(resName); // 自动移除无效缓存
                }
                // 判断该资源是否是待删除状态（即引用计数是否是0 也就是说是否处于未被使用的状态）
                if (resInfo.refCount == 0)
                {
                    UnloadAsset<T>(path,resInfo.isDel,null,false);
                }
                else
                {
                    // 将加载完成的资源传递出去
                    resInfo.callback?.Invoke(resInfo.asset);
                    // 加载完毕后 清空回调和协程的引用 防止内存泄漏(这两个变量的作用就是判断是否加载完毕)
                    resInfo.coroutine = null;
                    resInfo.callback = null;
                }
            }
        }
        #endregion

        #region 同步加载资源
        /// <summary>
        /// 同步加载Resources下资源的方法
        /// </summary>
        /// <typeparam name="T">需要加载的资源类型</typeparam>
        /// <param name="path">需要加载的资源路径</param>
        /// <returns>返回同步加载好的资源</returns>
        public T Load<T>(string path) where T : Object
        {
            // 资源的唯一ID 通过 路径名_资源类型 规则命名
            string resName = path + "_" + typeof(T).Name;
            ResInfo<T> info;
            // 若字典内无该资源的记录
            if (!resDic.TryGetValue(resName,out var value))
            {
                // 直接同步加载出资源 并记录资源信息放入字典内
                T res = Resources.Load<T>(path);
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
                // 存在异步加载该资源的协程在执行中(即处于未能加载完毕的状态)
                if (!info.asset)
                {
                    // 停止异步加载协程
                    MonoManager.Instance.StopCoroutine(info.coroutine);
                    // 直接同步加载出资源 
                    T res = Resources.Load<T>(path);
                    info.asset = res;
                    // 对加载的资源做判空(Resources本身的异步加载方法若路径不对或者类型错误是不会抛出错误的)
                    if (!info.asset)
                    {
                        LogUtil.Error($"[ResManager] 加载失败：路径不存在或类型错误 => path: {path}, type: {typeof(T).Name}");
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

        #region 指定卸载资源

        /// <summary>
        /// 指定卸载一个资源
        /// </summary>
        /// <param name="path">需要卸载的资源路径</param>
        /// <param name="isDel">标记是否需要马上移除</param>
        /// <param name="callback">回调函数</param>
        /// <param name="isSub">为了防止减两次 在内部调用的时候标记为不减</param>
        /// <typeparam name="T">需要卸载的资源类型</typeparam>
        public void UnloadAsset<T>(string path,bool isDel = false, UnityAction<T> callback = null, bool isSub = true) where T : Object
        {
            // 资源的唯一ID 通过 路径名_资源类型 规则命名
            string resName = path + "_" + typeof(T).Name;
            // 若字典内已记录该资源信息
            if (resDic.TryGetValue(resName,out var info))
            {
                ResInfo<T> resInfo = info as ResInfo<T>;
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
                    // 从字典移除记录
                    resDic.Remove(resName);
                    // 移除内存加载的资源
                    Resources.UnloadAsset(resInfo.asset);
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
            
        }
        #endregion

        #region 异步卸载资源
        /// <summary>
        /// 异步卸载 没有使用到的Resources相关的资源
        /// </summary>
        /// <param name="callBack">回调函数</param>
        public void UnloadUnusedAssets(UnityAction callBack)
        {
            MonoManager.Instance.StartCoroutine(ReallyUnloadUnusedAssets(callBack));
        }

        /// <summary>
        /// 异步卸载 未被使用到的Resources相关的资源 的协程
        /// </summary>
        /// <param name="callback">回调函数</param>
        /// <returns></returns>
        private IEnumerator ReallyUnloadUnusedAssets(UnityAction callback)
        {
            // 在真正移除不使用的资源之前 应该先将引用计数为0且没有被移除记录的资源删除
            List<string> list = new();
            // 需要记录待移除的资源路径列表
            foreach (string path in resDic.Keys)
            {
                if (resDic[path].refCount == 0)
                {
                    list.Add(path);
                }
            }
            // 逐个对待删除的资源删除
            foreach (string path in list)
            {
                resDic.Remove(path);
            }
            AsyncOperation ao = Resources.UnloadUnusedAssets();
            yield return ao;
            //卸载完毕后 通知外部
            callback();
        }
        #endregion
        
        /// <summary>
        /// 获取当前资源的引用计数
        /// </summary>
        /// <param name="path">资源路径</param>
        /// <typeparam name="T">资源类型</typeparam>
        /// <returns></returns>
        public int GetRefCount<T>(string path)
        {
            // 资源的唯一ID 通过 路径名_资源类型 规则命名
            string resName = path + "_" + typeof(T).Name;
            // 从字典中查询该资源 若查不到直接返回0
            return resDic.TryGetValue(resName,out var info) ? (info as ResInfo<T>).refCount : 0;
        }

        /// <summary>
        /// 手动清空字典记录数据(适用于不想使用手动卸载资源时 比如切换场景时执行)
        /// </summary>
        /// <param name="callback"></param>
        public void ClearDic(UnityAction callback)
        {
            MonoManager.Instance.StartCoroutine(ReallyClearDic(callback));
        }

        private IEnumerator ReallyClearDic(UnityAction callback)
        {
            resDic.Clear();
            AsyncOperation ao = Resources.UnloadUnusedAssets();
            yield return ao;
            //卸载完毕后 通知外部
            callback();
        }
    }
}
