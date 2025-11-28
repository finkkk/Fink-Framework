using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FinkFramework.Mono;
using FinkFramework.Pool;
using FinkFramework.Singleton;
using UnityEngine;
using UnityEngine.Events;

namespace FinkFramework.Timer
{
    /// <summary>
    /// 计时器管理器 主要用于 开启 停止 重置 等操作来管理计时器
    /// </summary>
    public class TimerManager : Singleton<TimerManager>
    {
        private TimerManager()
        {
            // 默认计时器就是开启的
            Start();
        }
        // 重复调用会启动多个协程，加一个标志位防止重复启动
        private bool isRunningTimer;
        
        /// <summary>
        /// 用于记录当前将要创建的唯一ID的
        /// </summary>
        private int TIMER_KEY;
        
        /// <summary>
        /// 用于存储管理所有计时器的字典容器（受Time.timeScale影响的计时器）
        /// </summary>
        private readonly Dictionary<int, TimerItem> timerDic = new();
        
        /// <summary>
        /// 用于存储管理所有计时器的字典容器（不受Time.timeScale影响的计时器）
        /// </summary>
        private readonly Dictionary<int, TimerItem> realTimerDic = new();
        /// <summary>
        /// 待移除列表（为了防止边遍历边删除 只能先记录要删除的元素后续执行删除）
        /// </summary>
        private readonly List<TimerItem> delList = new();
        /// <summary>
        /// 计时器协同程序(受scale影响)
        /// </summary>
        private Coroutine timer;
        /// <summary>
        /// 计时器协同程序(不受scale影响)
        /// </summary>
        private Coroutine realTimer;
        /// <summary>
        /// 计时器管理器中的唯一计时用的协同程序的 间隔时间（即最小分度值）
        /// </summary>
        private const float countInterval = 0.1f;
        //为了避免内存的浪费 每次while都会生成 
        //我们直接将其声明为成员变量
        private readonly WaitForSecondsRealtime waitForSecondsRealtime = new(countInterval);
        private readonly WaitForSeconds waitForSeconds = new(countInterval);

        /// <summary>
        /// 开启计时器
        /// </summary>
        public void Start()
        {
            if (!isRunningTimer)
            {
                timer = MonoManager.Instance.StartCoroutine(StartTiming(false, timerDic));
                realTimer = MonoManager.Instance.StartCoroutine(StartTiming(true, realTimerDic));
                isRunningTimer = true;
            }
        }

        /// <summary>
        /// 关闭计时器
        /// </summary>
        public void Stop()
        {
            MonoManager.Instance.StopCoroutine(timer);
            MonoManager.Instance.StopCoroutine(realTimer);
        }

        /// <summary>
        /// 开启计时器的协程
        /// </summary>
        /// <returns></returns>
        private IEnumerator StartTiming(bool isRealTime, Dictionary<int, TimerItem> timerDic)
        {
            while (true)
            {
                // 每间隔一百毫秒进行计时（即最小分度值为100毫秒）
                if (isRealTime)
                {
                    yield return waitForSecondsRealtime;
                }
                else
                {
                    yield return waitForSeconds;
                }
               
                foreach (var item in timerDic.Values.ToList())
                {
                    if (!item.isRunning)
                    {
                        continue;
                    }
                    // -------------------------------
                    // 处理间隔回调
                    if (item.onInterval != null)
                    {
                        // 若有间隔需求 则每次计时最小分度值的时候记录一次（乘以1000是秒转换为毫秒）
                        item.intervalTime -= (int)(countInterval * 1000);
                        // 满足一次间隔时间执行
                        while (item.intervalTime <= 0)
                        {
                            // 执行间隔时间的回调
                            item.onInterval?.Invoke();
                            // 重置间隔时间
                            item.intervalTime += item.maxIntervalTime;
                        }
                    }
                    // -------------------------------
                    // 处理总时间
                    if (item.allTime > 0)// 只有正数才倒计时
                    {
                        item.allTime -= (int)(countInterval * 1000);
                        // 计时时间到 需要执行计时完毕的回调
                        if (item.allTime <= 0)
                        {
                            item.onOver?.Invoke();
                            delList.Add(item);
                        }
                    }  
                    
                }
                // 移除 待移除列表中的计时器
                foreach (var t in delList)
                {
                    // 从字典中移除
                    timerDic.Remove(t.keyID);
                    // 计时完毕 返回对象池内
                    PoolManager.Instance.Despawn(t);
                }
                // 清除完毕 清空待移除列表
                delList.Clear();
            }
            // ReSharper disable once IteratorNeverReturns
        }

        /// <summary>
        /// 创建单个计时器 若想做无限时间的间隔计时器只需传入总时间为-1或其他负数即可
        /// </summary>
        /// <param name="isRealTimer">若是true则不受scaleTime的影响</param>
        /// <param name="allTime">总的时间 毫秒 1s=1000ms</param>
        /// <param name="onOver">总时间结束回调</param>
        /// <param name="intervalTime">间隔计时时间 毫秒 1s=1000ms</param>
        /// <param name="onInterval">间隔计时时间结束 回调</param>
        /// <param name="isRunning">是否创建完毕后自动开启 默认开启</param>
        /// <returns>返回唯一ID 用于外部控制对应计时器</returns>
        public int CreateTimer(bool isRealTimer,int allTime, UnityAction onOver, int intervalTime = 0, UnityAction onInterval = null,bool isRunning = true)
        {
            //构建唯一ID
            int keyID = ++TIMER_KEY;
            //从缓存池取出对应的计时器
            TimerItem timerItem = PoolManager.Instance.Spawn<TimerItem>();
            //初始化数据
            timerItem.InitInfo(keyID, allTime, onOver, intervalTime, onInterval,isRunning);
            //记录到字典中 进行数据更新
            if (isRealTimer)
            {
                realTimerDic.Add(keyID,timerItem);
            }
            else
            {
                timerDic.Add(keyID, timerItem);
            }
            return keyID;
        }
        
        /// <summary>
        /// 移除单个计时器
        /// </summary>
        /// <param name="keyID">唯一ID</param>
        public void RemoveTimer(int keyID)
        {
            if(timerDic.ContainsKey(keyID))
            {
                //移除对应id计时器 放入缓存池
                PoolManager.Instance.Despawn(timerDic[keyID]);
                //从字典中移除
                timerDic.Remove(keyID);
            }
            else if(realTimerDic.ContainsKey(keyID))
            {
                //移除对应id计时器 放入缓存池
                PoolManager.Instance.Despawn(realTimerDic[keyID]);
                //从字典中移除
                realTimerDic.Remove(keyID);
            }
        }

        /// <summary>
        /// 重置单个计时器
        /// </summary>
        /// <param name="keyID">计时器唯一ID</param>
        /// <param name="isRunning">是否重置完后自动开启 默认开启</param>
        public void ResetTimer(int keyID, bool isRunning = true)
        {
            if (timerDic.TryGetValue(keyID, out var value))
            {
                value.ResetTimer(isRunning);
            }
            else if (realTimerDic.TryGetValue(keyID, out var r))
            {
                r.ResetTimer(isRunning);
            }
        }

        /// <summary>
        /// 开启单个计时器 主要用于暂停后重新开始
        /// </summary>
        /// <param name="keyID">计时器唯一ID</param>
        public void StartTimer(int keyID)
        {
            if (timerDic.TryGetValue(keyID, out var value))
            {
                value.isRunning = true;
            }
            else if (realTimerDic.TryGetValue(keyID,out var r))
            {
                r.isRunning = true;
            }
        }

        /// <summary>
        /// 停止单个计时器 主要用于暂停
        /// </summary>
        /// <param name="keyID">计时器唯一ID</param>
        public void StopTimer(int keyID)
        {
            if (timerDic.TryGetValue(keyID, out var value))
            {
                value.isRunning = false;
            }
            else if (realTimerDic.TryGetValue(keyID,out var r))
            {
                r.isRunning = false;
            }
        }

        /// <summary>
        /// 创建无限间隔计时器
        /// </summary>
        /// <param name="isRealTimer">若是true则不受scaleTime的影响</param>
        /// <param name="intervalTime">间隔时间</param>
        /// <param name="onInterval">间隔回调</param>
        /// <param name="isRunning">是否创建完毕就自动开启 默认开启</param>
        /// <returns></returns>
        public int CreateInfiniteTimer(bool isRealTimer,int intervalTime, UnityAction onInterval, bool isRunning = true)
        {
            return CreateTimer(isRealTimer,-1, null, intervalTime, onInterval, isRunning);
        }
        
        /// <summary>
        /// 一次性计时器（计时结束自动销毁）
        /// 类似于 JavaScript 的 setTimeout
        /// </summary>
        /// <param name="delay">延迟时间（毫秒）</param>
        /// <param name="callback">执行的回调函数</param>
        /// <param name="isRealTimer">是否使用真实时间，不受 Time.timeScale 影响</param>
        /// <returns>TimerID，可选</returns>
        public int SetTimeout(int delay, UnityAction callback, bool isRealTimer = false)
        {
            // Delay <= 0 则立即执行
            if (delay <= 0)
            {
                callback?.Invoke();
                return -1;
            }

            // 创建计时器，计时结束后执行 callback 并自动移除 Timer
            int id = 0;
            id = CreateTimer(
                isRealTimer,
                delay,
                () =>
                {
                    callback?.Invoke();
                    // ReSharper disable once AccessToModifiedClosure
                    RemoveTimer(id);
                }
            );

            return id;
        }
        
        /// <summary>
        /// 暂停所有计时器（受 TimeScale 与 RealTime 两种计时器）
        /// </summary>
        public void PauseAll()
        {
            foreach (var t in timerDic.Values)
                t.isRunning = false;

            foreach (var t in realTimerDic.Values)
                t.isRunning = false;
        }

        /// <summary>
        /// 恢复所有计时器
        /// </summary>
        public void ResumeAll()
        {
            foreach (var t in timerDic.Values)
                t.isRunning = true;

            foreach (var t in realTimerDic.Values)
                t.isRunning = true;
        }
    }
}