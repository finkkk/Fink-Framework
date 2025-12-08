using System.Collections.Generic;
using FinkFramework.Runtime.Environments;
using FinkFramework.Runtime.ResLoad;
using FinkFramework.Runtime.Singleton;
using FinkFramework.Runtime.Utils;
using UnityEngine;

namespace FinkFramework.Runtime.Pool
{
    /// <summary>
    /// 全局缓存池(对象池)模块管理器 用于统一注册和分发不同类型的对象池（SubPools） 支持泛型访问、自动创建、对象复用与回收
    /// 初始化对象池内对象的时候尽量在OnEnable生命周期函数内初始化
    /// </summary>
    public class PoolManager : Singleton<PoolManager>
    {
        // 继承单例模式基类需要显式实现私有无参构造函数
        private PoolManager(){}
        // 全局对象池 字典统一管理 键值对中键表示不同对象池的名字(子池) 值表示存储的对象池(仅用于GameObject)
        private readonly Dictionary<string, GameObjectPool> poolDic = new();
        // 用于存储 数据结构类、逻辑类等非继承Mono的类的对象池（即泛型对象池）的字典容器
        private readonly Dictionary<string, BasePoolStorage> poolObjectDic = new();
        // 是否开启调试模式(开启后失活的对象会按照根物体进行布局管理 结构清晰 但频繁修改父子关系有性能损耗 发布时建议关闭)
        public static bool debugMode = EnvironmentState.DebugMode;
        // 全局对象池根对象
        private GameObject poolObj;

        /// <summary>
        /// 从对象池中生成对象的方法  仅适用于GameObject
        /// </summary>
        /// <param name="fullPath">预制体的完整带前缀的路径</param>
        /// <returns></returns>
        public GameObject Spawn(string fullPath)
        {
            // 若全局对象池根物体为空 则创建一个空游戏对象作为根物体（前提是开启调试模式）
            if (!poolObj && debugMode)
            {
                poolObj = new GameObject("ObjectPool");
            }
            GameObject obj;
            // 1.如果全局对象池中不存在该对象池
            if (!poolDic.TryGetValue(fullPath, out var value))
            {
                // 动态创建对象
                obj = Object.Instantiate(ResManager.Instance.Load<GameObject>(fullPath));
                if (!obj)
                {
                    LogUtil.Error($"资源路径 {fullPath} 无法加载，请检查路径是否正确！");
                    return null;
                }
                // 强制设置实例化对象名字为传入的对象池名字 方便返回对象池时直接使用对象名字查池（也避免实例化后unity自动添加的clone尾缀）
                obj.name = fullPath; 
                // 创建对象池(构造对象池的方法内部就实现了记录使用中对象的功能 即将实例化出来的这个对象存入使用中的池子内)
                poolDic.Add(fullPath,new GameObjectPool(poolObj,fullPath,obj));
            }
            // 2.有该对象池 且该对象池中存在没有使用的对象 
            else if (value.Count > 0)
            {
                // 需要从对象池中取出对象 其中是否为超限情况方法内部自有判断
                obj = value.Get();
            }
            // 3.有该对象池 且记录的使用中的对象数量超过了最大数量上限
            else if (!value.canCreate)
            {
                // 需要从对象池中取出对象 其中是否为超限情况方法内部自有判断
                obj = value.Get();
            }
            // 4. 其他情况：有该对象池 但对象池内已没有缓存对象 但也未超出最大数量上限
            else
            { 
                // 动态创建对象
                obj = Object.Instantiate(ResManager.Instance.Load<GameObject>(fullPath));
                if (!obj)
                {
                    LogUtil.Error($"资源路径 {fullPath} 无法加载，请检查路径是否正确！");
                    return null;
                }
                // 强制设置实例化对象名字为传入的对象池名字 方便返回对象池时直接使用对象名字查池（也避免实例化后unity自动添加的clone尾缀）
                obj.name = fullPath; 
                // 实例化出来的对象需要记录到使用中对象池内
                value.AddUsedList(obj);
            }
            return obj;
        }
        /// <summary>
        /// 从对象池中生成对象的方法  适用于一切类型（泛型对象池取对象）
        /// </summary>
        /// <returns>取到的对象</returns>
        public T Spawn<T>(string nameSpace = "") where T : class,IPoolable,new()
        {
            // 池子的名字是根据类的类名来决定的
            string poolName = nameSpace + "_" + typeof(T).Name;
            // 1. 存在池子的时候
            if (poolObjectDic.TryGetValue(poolName, out var value))
            {
                PoolStorage<T> pool = value as PoolStorage<T>;
                // 若池子不为空
                if (pool.poolObjs.Count > 0)
                {
                    // 从队列中取出对象进行复用
                    T obj = pool.poolObjs.Dequeue();
                    return obj;
                }
                else // 若池子为空
                {
                    T obj = new T();
                    return obj;
                }
            }
            else// 2. 不存在池子的时候
            {
                T obj = new T();
                return obj;
            }
        }

        /// <summary>
        /// 使用完该对象后 返回进对象池内 并压栈/存储对象实例(类似销毁)  仅适用于GameObject
        /// </summary>
        /// <param name="obj">要存入的对象实例</param>
        public void Despawn(GameObject obj)
        {
            // 存入对象
            poolDic[obj.name].Return(obj);
        }
        
        /// <summary>
        /// 使用完该对象后 返回进对象池内 并压栈/存储对象实例(类似销毁)  适用于一切类型（泛型对象池取对象）
        /// </summary>
        public void Despawn<T>(T obj,string nameSpace = "") where T : class,IPoolable
        {
            if (obj == null)
            {
                return;
            }
            // 池子的名字是根据类的类名来决定的
            string poolName = nameSpace + "_" + typeof(T).Name;
            PoolStorage<T> pool;
            if (poolObjectDic.TryGetValue(poolName,out var value))// 1. 存在池子的时候
            {
                // 取出池子
                pool = value as PoolStorage<T>;
            }
            else// 2. 不存在池子的时候
            {
                pool = new PoolStorage<T>();
                poolObjectDic.Add(poolName,pool);
            }
            // 在放回池子之前 先重置对象的数据（这里的重置方法为继承的对象池接口提供的）
            obj.ResetInfo();
            // 压入对象
            pool.poolObjs.Enqueue(obj);
        }
        
        /// <summary>
        /// 预加载对象的方法（提前创建若干个对象，存入对象池）
        /// 用于初始化时减少运行时实例化开销，提升性能
        /// </summary>
        /// <param name="name">传入需要预加载预制体的路径</param>
        /// <param name="count">需要预加载的个数</param>
        public void Preload(string name, int count)
        {
            // 用于暂存已生成的对象，避免立即 Despawn 后被 Spawn 重复复用
            List<GameObject> objs = new();
            // 若对象池尚未存在，调用 Spawn 自动创建一个对象池并注册
            if (!poolDic.TryGetValue(name,out var pool))
            {
                // 创建首个对象（自动创建对象池）
                var first = Spawn(name);
                // 加入暂存列表
                objs.Add(first);
                // 获取新创建的对象池引用
                pool = poolDic[name];
            }
            // 从当前已有数量开始继续预载，直到达到目标 count 或超出最大上限
            for (int i = objs.Count; i < count; i++) 
            {
                // 未超限才进行预载 否则停止预载报警告
                if (pool.canCreate)
                {
                    var obj = Spawn(name);
                    objs.Add(obj);
                }
                else
                {
                    LogUtil.Warn($"对象池 {name} 最大上限: {pool.maxNum} 本次仅成功预载 {objs.Count} 个对象");
                    break;
                }
            }
            // 全部对象预载完成后统一回收到对象池中
            foreach (var obj in objs)
            {
                Despawn(obj);
            }
        }
        
        /// <summary>
        /// 清空对象池（包含失活对象和根对象），防止内存泄漏 (主要是切换场景时调用)
        /// </summary>
        public void CleanPool()
        {
            // 如果有调试用根节点，直接销毁它（连同所有子对象）
            if (poolObj)
            {
                Object.Destroy(poolObj);
                poolObj = null;
            }
            else
            {
                // debugMode未开启时 手动销毁所有缓存对象
                foreach (var pool in poolDic.Values)
                {
                    while (pool.Count > 0)
                    {
                        Object.Destroy(pool.pool.Pop());
                    }
                }
            }
            // 清空全局对象池注册
            poolObjectDic.Clear();
            poolDic.Clear();
        }
    }
}
