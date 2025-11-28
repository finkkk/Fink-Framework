using FinkFramework.Pool;
using UnityEngine.Events;

namespace FinkFramework.Timer
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
        /// <param name="keyID">唯一ID</param>
        /// <param name="allTime">需要计时的总时间</param>
        /// <param name="onOver">计时结束的回调</param>
        /// <param name="intervalTime">需要间隔执行的时间</param>
        /// <param name="onInterval">每次间隔结束执行的回调</param>
        /// <param name="isRunning">是否启动 默认启动</param>
        public void InitInfo(int keyID,int allTime,UnityAction onOver, int intervalTime = 0, UnityAction onInterval = null, bool isRunning = true)
        {
            this.keyID = keyID;
            maxAllTime = this.allTime = allTime;
            this.onOver = onOver;
            maxIntervalTime = this.intervalTime = intervalTime;
            this.onInterval = onInterval;
            this.isRunning = isRunning;
        }

        /// <summary>
        /// 重置计时器
        /// </summary>
        /// <param name="isRunning">是否启动 默认启动</param>
        public void ResetTimer(bool isRunning = true)
        {
            allTime = maxAllTime;
            intervalTime = maxIntervalTime;
            this.isRunning = isRunning;
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
