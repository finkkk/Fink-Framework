using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FinkFramework.Runtime.ResLoad.Base
{
    /// <summary>
    /// 资源加载提供器必须实现的接口
    /// </summary>
    public interface IResProvider
    {
        /// <summary>
        /// 同步加载资源方法
        /// </summary>
        /// <param name="path">路径</param>
        /// <typeparam name="T">资源类型</typeparam>
        /// <returns>返回加载完毕的资源</returns>
        T Load<T>(string path) where T : Object;

        /// <summary>
        /// 异步加载资源方法
        /// </summary>
        /// <param name="path">路径</param>
        /// <param name="callback">加载完毕执行的回调 在回调处返回加载完毕的资源</param>
        /// <typeparam name="T">资源类型</typeparam>
        UniTask<T> LoadAsync<T>(string path) where T : Object;
        
        /// <summary>
        /// 检测资源是否存在
        /// </summary>
        /// <param name="path">路径</param>
        /// <returns>是否存在</returns>
        bool Exists(string path);
        
        /// <summary>
        /// Provider 的底层卸载，不处理引用计数  (其中针对 Resources 的情况应该空实现 直接由ResManager实现卸载)
        /// </summary>
        /// <param name="path">路径</param>
        void Unload(string path);
        
        /// <summary>
        /// Provider 自己的缓存清理            (其中针对 Resources 的情况应该空实现 直接由ResManager实现清理)
        /// </summary>
        void Clear();     
        
        /// <summary>
        /// 获取一个正在加载的资源的真实进度（0~1）若 provider 不支持真实进度(如Resources)，返回 false。
        /// </summary>
        bool TryGetProgress(string path, out float progress);
    }
}