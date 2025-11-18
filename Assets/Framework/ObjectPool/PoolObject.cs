using System.Collections.Generic;

namespace Framework.ObjectPool
{
    /// <summary>
    /// 方便使用里氏替换原则 来记录PoolObject
    /// </summary>
    public abstract class PoolObjectBase { }
    /// <summary>
    /// 用于存储 数据结构类 或 逻辑类 （即不继承Mono）的容器类
    /// </summary>
    /// <typeparam name="T">存储的类型</typeparam>
    public class PoolObject<T> : PoolObjectBase where T : class
    {
        public Queue<T> poolObjs = new();
    }
}