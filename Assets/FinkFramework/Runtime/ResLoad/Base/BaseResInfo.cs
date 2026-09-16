using UnityEngine;

using Cysharp.Threading.Tasks;

namespace FinkFramework.Runtime.ResLoad.Base
{
    /// <summary>
    /// 资源缓存记录的非泛型部分。
    ///
    /// 除了引用计数，还保存 Provider 和 Provider 路径，避免在卸载时从缓存 key
    /// 反推原始路径。原先通过最后一个下划线截断 key 的做法对路径格式有隐含要求，
    /// 也会让缓存 key 和实际请求路径不一致时很难排查。
    /// </summary>
    public abstract class BaseResInfo
    {
        /// <summary>当前由框架持有的引用数量。</summary>
        public int refCount;

        /// <summary>引用归零后是否允许移除缓存并释放底层资源。</summary>
        public bool isDel;

        /// <summary>创建该记录时使用的 Provider。</summary>
        public IResProvider provider;

        /// <summary>不带协议前缀的 Provider 路径。</summary>
        public string providerPath;

        /// <summary>规范化后的完整资源路径，仅用于日志和诊断。</summary>
        public string fullPath;

        /// <summary>统一获取已加载资源。</summary>
        public abstract Object GetAsset();

        /// <summary>统一设置已加载资源。</summary>
        public abstract void SetAsset(Object obj);

        /// <summary>等待该记录的底层加载结束，用于清理时避免中断进行中的请求。</summary>
        public abstract UniTask WaitForLoadAsync();

        /// <summary>是否存在尚未完成的底层加载任务。</summary>
        public abstract bool IsLoading { get; }
    }
}
