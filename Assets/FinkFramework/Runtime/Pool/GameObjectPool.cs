using System.Collections.Generic;
using FinkFramework.Runtime.Utils;
using UnityEngine;

namespace FinkFramework.Runtime.Pool
{
    /// <summary>
    /// GameObject 池（GameObject Pool）
    /// ------------------------------------------------------------
    /// 本类为框架内置的 GameObject 级对象池，主要用于：
    /// - 避免频繁 Instantiate / Destroy 带来的性能开销
    /// - 提供高效的对象复用机制（缓存 + LRU 复用）
    /// - 支持池上限、调试模式布局、使用中对象统计
    ///
    /// 特点：
    /// 1. 使用栈结构保存未使用对象（LIFO）
    /// 2. 使用列表记录正在使用中的对象
    /// 3. 当池已满时，自动复用“最早使用的对象”（LRU）
    /// 4. 在调试模式下，会自动将闲置对象归类到池根节点下，便于观察
    ///
    /// 通常由 PoolManager 管理，不建议手动实例化。
    /// ------------------------------------------------------------
    /// </summary>
    public class GameObjectPool
    {
        // 对象池中的对象合集(栈)  记录的是没有使用的对象
        public readonly Stack<GameObject> pool = new();
        // 正在使用中的对象合集
        private readonly List<GameObject> usedList = new();
        // 该对象池的根物体 用于布局管理的对象
        private readonly GameObject rootObj;
        // 对象池数量上限 即场景上最多允许同时出现的对象的个数
        public readonly int maxNum;
        // 获取对象池容器中的缓存对象个数
        public int Count => pool.Count;
        // 获取正在使用中的对象个数
        public int UsedCount => usedList.Count;
        // 对象池持有的一份预制体资源引用，供 PoolManager 在池销毁时释放。
        public readonly string PrefabPath;
        public readonly GameObject Prefab;
        // 判断使用中的对象数量是否超出最大值上限 若未超出则返回true 表示可以进行实例化
        public bool canCreate => UsedCount < maxNum;

        /// <summary>
        /// 初始化构造函数 用于初始化对象池
        /// </summary>
        /// <param name="root">全局对象池根物体(父对象)</param>
        /// <param name="name">对象池的名字</param>
        /// <param name="usedObj">传入动态创建的对象 存入使用中对象池 标记其为正在使用的状态</param>
        /// <param name="prefab"></param>
        /// <param name="prefabPath"></param>
        public GameObjectPool(GameObject root,string name,GameObject usedObj,GameObject prefab,string prefabPath)
        {
            if (!usedObj)
                throw new System.ArgumentNullException(nameof(usedObj));

            Prefab = prefab;
            PrefabPath = prefabPath;
            // 只有当开启调试模式的时候 才会启用布局功能(即根据父子关系布局)
            if (PoolManager.debugMode)
            {
                // 创建对象池父对象 
                rootObj = new GameObject(name);
                // 和全局父对象创建父子关系
                rootObj.transform.SetParent(root.transform);
            }
            // 创建对象池时 动态创建的对象需要存入正在使用中对象的容器内 用以记录使用中对象
            AddUsedList(usedObj);
            // 获取被对象池管理的预制体对象身上的PoolGameObject脚本
            if (!usedObj.TryGetComponent<PoolablePrefab>(out var poolObject))
            {
                throw new System.Exception($"对象 {usedObj.name} 缺失 PoolObject 组件，禁止创建对象池！");
            }
            // 从PoolObject获取上限数量值
            maxNum = Mathf.Max(1, poolObject.maxNum);
            if (poolObject.maxNum <= 0)
                LogUtil.Warn($"对象池 {name} 的最大数量必须大于 0，已按 1 处理。");
        }

        /// <summary>使用池持有的预制体创建实例，不重复向资源系统申请引用。</summary>
        public GameObject Create()
        {
            return Prefab ? Object.Instantiate(Prefab) : null;
        }

        /// <summary>
        /// 销毁池内所有实例，包括当前仍在使用中的实例。
        /// 场景切换时如果只销毁闲置对象，会遗留活动对象和其组件引用。
        /// </summary>
        public void DestroyAll()
        {
            while (pool.Count > 0)
                Object.Destroy(pool.Pop());

            foreach (GameObject obj in usedList)
            {
                if (obj)
                    Object.Destroy(obj);
            }

            usedList.Clear();
            if (rootObj)
                Object.Destroy(rootObj);
        }

        /// <summary>
        /// 从对象池中取出缓存对象(从栈顶弹出对象)  仅使用于GameObject
        /// </summary>
        /// <returns>取到的对象</returns>
        public GameObject Get()
        {
            GameObject obj = null;
            // 忽略业务代码误删后残留在缓存栈中的对象。
            while (pool.Count > 0 && !obj)
                obj = pool.Pop();

            if (obj)
            {
                // 取出对象后需要标记为正在使用
                AddUsedList(obj);
            }
            // 若池中无缓存对象，则复用最早进入使用列表的有效对象。
            while (!obj && usedList.Count > 0)
            {
                if (!usedList[0])
                {
                    RemoveUsedList(0);
                    continue;
                }

                obj = usedList[0];
                RemoveUsedList(0);
                AddUsedList(obj);
            }

            if (!obj)
            {
                LogUtil.Warn("对象池为空，且无可复用对象，请确认是否初始化或限制合理。");
                return null;
            }
            // 在获取到栈顶的对象后 将该对象激活
            obj.SetActive(true);
            // 只有当开启调试模式的时候 才会启用布局功能(即根据父子关系布局)
            if (PoolManager.debugMode)
            {
                // 取出缓存池的对象时需要断开父子关系 使其无父对象
                obj.transform.SetParent(null);
            }
            // 返回对象
            return obj;
        }

        /// <summary>
        /// 使用完该对象后 返回进对象池内 并压栈/存储对象实例(类似销毁)  仅使用于GameObject
        /// </summary>
        /// <param name="obj">要存入的对象实例</param>
        public void Return(GameObject obj)
        {
            // 对需要传入的对象实例进行判空
            if (!obj)
            {
                return;
            }

            if (pool.Contains(obj))
            {
                LogUtil.Warn($"对象 {obj.name} 已经在对象池中，忽略重复回收。");
                return;
            }

            if (!usedList.Contains(obj))
            {
                LogUtil.Warn($"对象 {obj.name} 不属于当前对象池，忽略回收。");
                return;
            }
        
            // 隐藏对象 使对象失活 进入对象池待命(来代替销毁)
            obj.SetActive(false);
            // 只有当开启调试模式的时候 才会启用布局功能(即根据父子关系布局)
            if (PoolManager.debugMode)
            {
                // 把对象池的缓存对象(即暂存的失活对象)的父对象设置为对象池根对象
                obj.transform.SetParent(rootObj.transform);
            }
            // 存入对象到对象池内
            pool.Push(obj);
            // 这个对象已不再使用 被返回至缓存池 因此应该从使用中对象池中删除
            RemoveUsedList(obj);
        }
        
        /// <summary>
        /// 将对象存入使用中对象的容器中
        /// </summary>
        /// <param name="gameObject">使用中的对象</param>
        public void AddUsedList(GameObject gameObject)
        {
            if (gameObject && !usedList.Contains(gameObject))
                usedList.Add(gameObject);
        }

        /// <summary>判断实例是否由当前池持有，供管理器处理改名和重复回收。</summary>
        public bool Contains(GameObject gameObject)
        {
            return gameObject && (usedList.Contains(gameObject) || pool.Contains(gameObject));
        }
        
        /// <summary>
        /// 将对象从使用中对象的容器中删除（重载函数 参数为索引）
        /// </summary>
        /// <param name="index">需要删除的对象的索引</param>
        public void RemoveUsedList(int index)
        {
            usedList.RemoveAt(index);
        }
        
        /// <summary>
        /// 将对象从使用中对象的容器中删除（重载函数 参数为对象）
        /// </summary>
        /// <param name="obj">不再使用的对象</param>
        public void RemoveUsedList(GameObject obj)
        {
            usedList.Remove(obj);
        }
    }
}
