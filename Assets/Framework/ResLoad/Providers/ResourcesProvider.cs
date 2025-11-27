using System.Collections;
using Framework.Mono;
using Framework.ResLoad.Base;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;

namespace Framework.ResLoad.Providers
{
    /// <summary>
    /// Resources 加载资源方式的提供器
    /// </summary>
    public class ResourcesProvider : IResProvider
    {
        #region 同步加载资源
        /// <summary>
        /// 同步加载Resources下资源的方法
        /// </summary>
        /// <typeparam name="T">需要加载的资源类型</typeparam>
        /// <param name="path">需要加载的资源路径</param>
        /// <returns>返回同步加载好的资源</returns>
        public T Load<T>(string path) where T : Object
        {
            return Resources.Load<T>(path);
        }
        #endregion
        
        #region 异步加载资源
        /// <summary>
        /// 异步加载资源的方法(泛型)
        /// </summary>
        /// <typeparam name="T">资源类型</typeparam>
        /// <param name="path">资源路径（Resources下的）</param>
        /// <param name="callback">加载结束后的回调函数 当异步加载资源结束后才会调用</param>
        public void LoadAsync<T>(string path, UnityAction<T> callback) where T : Object
        {
            MonoManager.Instance.StartCoroutine(LoadAsyncCoroutine(path, callback));
        }
        
        /// <summary>
        /// 异步加载资源的协程(泛型)
        /// </summary>
        /// <param name="path">资源路径</param>
        /// <param name="callback">加载完毕的回调 返回异步加载好的资源</param>
        /// <typeparam name="T">资源类型</typeparam>
        /// <returns></returns>
        private IEnumerator LoadAsyncCoroutine<T>(string path, UnityAction<T> callback) where T : Object
        {
            var rq = Resources.LoadAsync<T>(path);
            yield return rq;
            callback?.Invoke(rq.asset as T);
        }
        #endregion

        #region 检测资源存在
        /// <summary>
        /// 检测资源文件是否存在
        /// </summary>
        /// <param name="path">路径</param>
        /// <returns>返回是否存在</returns>
        public bool Exists(string path)
        {
            return Resources.Load(path) != null;
        }
        #endregion

        #region 卸载资源和清空引用
        /// <summary>
        /// 卸载资源 (只负责执行底层 Unload 真正判断是否能卸载由 ResManager 决定)
        /// </summary>
        /// <param name="path">路径</param>
        public void Unload(string path)
        {
            // ResourcesProvider 不能仅依靠资源路径就卸载 需要具体对象
            // Resources 类型空实现即可  因此真正的卸载动作全部由 ResManager 根据引用计数和 asset 对象执行
        }

        /// <summary>
        /// 清空缓存(只负责执行底层 Clear 真正判断是否能卸载由 ResManager 决定)
        /// </summary>
        public void Clear()
        {
            // ResourcesProvider 不持有任何独立缓存
            // Resources 类型空实现即可 因此真正的卸载动作全部由 ResManager 根据引用计数和 asset 对象执行
        }

        #endregion
    }
}