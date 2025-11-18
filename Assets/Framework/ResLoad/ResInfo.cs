using Framework.Utils;
using UnityEngine;
using UnityEngine.Events;

namespace Framework.ResLoad
{
    /// <summary>
    /// 资源信息的抽象基类，用于记录资源加载时里氏替换原则 记录时是记录父类 实际调用时转换为子类
    /// </summary>
    public abstract class BaseResInfo
    {
        // 引用计数 
        public int refCount;
    }
    /// <summary>
    /// 资源信息对象 主要用于存储资源信息 异步加载委托信息 异步加载协程信息
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ResInfo<T> : BaseResInfo
    {
        // 资源信息
        public T asset;
        // 主要用于异步加载结束后 传递资源到外部的委托
        public UnityAction<T> callback;
        // 用于存储异步加载时开启的协同程序
        public Coroutine coroutine;
        // 当引用计数为0时 是否被标记为需要移除
        public bool isDel;
        public void AddRefCount()
        {
            ++refCount;
        }

        public void SubRefCount()
        {
            --refCount;
            if (refCount < 0)
            {
                LogUtil.Error("引用计数变为负数，请检查使用与卸载是否配对执行");
            }
        }
    }
}
