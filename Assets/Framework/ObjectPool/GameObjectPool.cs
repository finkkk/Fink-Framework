using System.Collections.Generic;
using Framework.Utils;
using UnityEngine;

namespace Framework.ObjectPool
{
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
        // 判断使用中的对象数量是否超出最大值上限 若未超出则返回true 表示可以进行实例化
        public bool canCreate => UsedCount < maxNum;

        /// <summary>
        /// 初始化构造函数 用于初始化对象池
        /// </summary>
        /// <param name="root">全局对象池根物体(父对象)</param>
        /// <param name="name">对象池的名字</param>
        /// <param name="usedObj">传入动态创建的对象 存入使用中对象池 标记其为正在使用的状态</param>
        public GameObjectPool(GameObject root,string name,GameObject usedObj)
        {
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
            if (!usedObj.TryGetComponent<PoolGameObjectConfig>(out var poolObject))
            {
                throw new System.Exception($"对象 {usedObj.name} 缺失 PoolObject 组件，禁止创建对象池！");
            }
            // 从PoolObject获取上限数量值
            maxNum = poolObject.maxNum;
        }

        /// <summary>
        /// 从对象池中取出缓存对象(从栈顶弹出对象)  仅使用于GameObject
        /// </summary>
        /// <returns>取到的对象</returns>
        public GameObject Get()
        {
            GameObject obj;
            // 如果对象池中还有未被使用的缓存对象
            if (pool.Count > 0)
            {
                // 弹出栈顶的对象 直接返回给外部调用(因为对象池不需要顺序 所以直接从栈顶出栈)
                obj = pool.Pop();
                // 取出对象后需要标记为正在使用
                AddUsedList(obj);
            }
            // 若对象池中无缓存对象了 且需要获取对象 则复用 使用中对象池最早创建在使用的对象
            else if (usedList.Count > 0)
            {
                // 强制复用最久的对象(序号0即为最早创建的对象)
                obj = usedList[0];
                // 将最早创建的对象 移出使用中对象池
                RemoveUsedList(0);
                // 将这个复用的新对象重新添加进使用中对象池 变为最新地使用中对象
                AddUsedList(obj);
            }
            else
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
            usedList.Add(gameObject);
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
