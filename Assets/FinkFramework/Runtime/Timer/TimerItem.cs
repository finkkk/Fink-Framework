using FinkFramework.Runtime.Pool;
using UnityEngine.Events;

namespace FinkFramework.Runtime.Timer
{
    /// <summary>
    /// 计时器对象 存储计时器相关数据
    /// </summary>
    public class TimerItem : IPoolable
    {
        /// <summary>
        /// 唯一ID
        /// </summary>
        public int keyID;
        /// <summary>
        /// 延迟结束时调用的委托函数
        /// </summary>
        public UnityAction onOver;
        /// <summary>
        /// 间隔一定时间调用的委托函数
        /// </summary>
        public UnityAction onInterval;
        /// <summary>
        /// 单位：毫秒 表示计时器的总时间
        /// </summary>
        public int allTime;
        /// <summary>
        /// 单位：毫秒 记录 一开始需要计时的总时间 用于计时器重置
        /// </summary>
        public int maxAllTime;
        /// <summary>
        /// 单位：毫秒 间隔执行回调的时间
        /// </summary>
        public int intervalTime;
        /// <summary>
        /// 单位：毫秒 记录 一开始间隔的时间
        /// </summary>
        public int maxIntervalTime;
        /// <summary>
        /// 是否在进行计时
        /// </summary>
        public bool isRunning;

        /// <summary>
        /// 初始化计时器数据
        /// </summary>
        /// <param name="keyId">唯一 ID</param>
        /// <param name="totalTime">需要计时的总时间</param>
        /// <param name="onComplete">计时结束的回调</param>
        /// <param name="intervalMs">需要间隔执行的时间</param>
        /// <param name="onIntervalCallback">每次间隔结束执行的回调</param>
        /// <param name="startImmediately">是否启动，默认启动</param>
        public void InitInfo(
            int keyId,
            int totalTime,
            UnityAction onComplete,
            int intervalMs = 0,
            UnityAction onIntervalCallback = null,
            bool startImmediately = true)
        {
            keyID = keyId;
            maxAllTime = allTime = totalTime;
            onOver = onComplete;
            maxIntervalTime = intervalTime = intervalMs;
            onInterval = onIntervalCallback;
            isRunning = startImmediately;
        }

        /// <summary>
        /// 重置计时器
        /// </summary>
        /// <param name="startImmediately">是否启动，默认启动</param>
        public void ResetTimer(bool startImmediately = true)
        {
            allTime = maxAllTime;
            intervalTime = maxIntervalTime;
            isRunning = startImmediately;
        }
        
        /// <summary>
        /// 缓存池回收时 清除回调函数引用
        /// </summary>
        public void ResetInfo()
        {
            onOver = null;
            onInterval = null;
        }
    }
}
