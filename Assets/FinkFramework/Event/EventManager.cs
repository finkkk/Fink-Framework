using System.Collections.Generic;
using System.Linq;
using FinkFramework.Singleton;
using UnityEngine.Events;

namespace FinkFramework.Event
{
    /// <summary>
    /// 事件全局管理器(事件中心模块)
    /// </summary>
    public class EventManager : Singleton<EventManager>
    {
        private EventManager(){}
        // 全局事件记录字典 用于记录对应事件关联的对应函数
        private readonly Dictionary<(E_EventType eventType, System.Type param1, System.Type param2), BaseEventInfo> eventDic = new();
        
        /// <summary>
        /// 构建稳定 Key 用于被字典记录事件
        /// </summary>
        private (E_EventType, System.Type, System.Type) MakeKey(E_EventType eventType, System.Type t1, System.Type t2)
        {
            return (eventType, t1 ?? typeof(void), t2 ?? typeof(void));
        }
        
        #region 无参无返回类型的委托
        /// <summary>
        /// 触发无参无返回监听函数
        /// </summary>
        /// <param name="eventName">事件名字</param>
        public void EventTrigger(E_EventType eventName)
        {
            var key = MakeKey(eventName, typeof(void), typeof(void));
            // 存在可以被监听的该事件 才允许触发事件
            if (eventDic.TryGetValue(key,out var action) && action is EventInfo info)
            {
                info.hasFired = true;
                // 执行监听函数
                info.actions?.Invoke();
            }
        }
        
        /// <summary>
        /// 向事件添加无参无返回值的监听函数
        /// </summary>
        /// <param name="eventName">事件名字</param>
        /// <param name="func">事件委托:需要添加的监听函数</param>
        public void AddEventListener(E_EventType eventName, UnityAction func, bool sticky = false)
        {
            var key = MakeKey(eventName, typeof(void), typeof(void));
            // 若已经存在监听事件的委托记录 直接添加监听函数即可
            if (eventDic.TryGetValue(key,out var action) && action is EventInfo info)
            {
                // 添加监听函数
                info.actions += func;
                // 如果是粘性事件且之前触发过 → 新监听者立即触发一次
                if (info.isSticky && info.hasFired)
                    func.Invoke();
            }
            // 若无此事件的记录
            else
            {
                // 向全局事件字典里添加事件记录
                eventDic.Add(key,new EventInfo(func,sticky));
            }
        }
        
        /// <summary>
        /// 向事件移除无参无返回值的监听函数
        /// </summary>
        /// <param name="eventName">事件名字</param>
        /// <param name="func">事件委托:需要移除的监听函数</param>
        public void RemoveEventListener(E_EventType eventName, UnityAction func)
        {
            var key = MakeKey(eventName, typeof(void), typeof(void));
            if (eventDic.TryGetValue(key,out var action) && action is EventInfo info)
            {
                // 移除监听函数
                info.actions -= func;
                // 删除监听函数后判断 若此时事件无任何监听函数了 可直接删除记录
                if (info.actions == null)
                {
                    eventDic.Remove(key);
                }
                    
            }
        }
        #endregion

        #region 有参无返回类型的委托（1个参数）
        /// <summary>
        /// 触发有参无返回的监听事件的函数（1个参数）
        /// 若事件未注册(无监听函数)，则不会执行任何操作，不抛出异常
        /// </summary>
        /// <param name="eventName">事件名字</param>
        /// <param name="info">具体监听函数的传参</param>
        /// <typeparam name="T">委托需要传参的类型（有参数的委托）</typeparam>
        public void EventTrigger<T>(E_EventType eventName,T info)
        {
            var key = MakeKey(eventName, typeof(T), typeof(void));
            // 存在可以被监听的该事件 才允许触发事件
            // 执行监听函数 类型检查（模式匹配写法 更安全 防止空引用）
            if (eventDic.TryGetValue(key,out var action) && action is EventInfo<T> eventInfo)
            {
                eventInfo.lastValue = info;
                eventInfo.hasValue = true;
                // 执行监听函数 传入T类型参数
                eventInfo.actions?.Invoke(info);
            }
        }
        
        /// <summary>
        /// 向事件添加有参无返回值的监听函数（1个参数）
        /// </summary>
        /// <param name="eventName">事件名字</param>
        /// <param name="func">事件委托:需要添加的监听函数</param>
        /// <typeparam name="T">委托需要传参的类型（有参数的委托）</typeparam>
        public void AddEventListener<T>(E_EventType eventName, UnityAction<T> func, bool sticky = false)
        {
            var key = MakeKey(eventName, typeof(T), typeof(void));
            // 若已经存在监听事件的委托记录 直接添加监听函数即可
            if (eventDic.TryGetValue(key,out var action) && action is EventInfo<T> info)
            {
                // 添加监听函数
                info.actions += func;
                // 如果是粘性事件且之前触发过 → 新监听者立即触发一次
                if (info.isSticky && info.hasValue)
                    func.Invoke(info.lastValue);
            }
            // 若无此事件的记录
            else
            {
                // 向全局事件字典里添加事件记录
                eventDic.Add(key,new EventInfo<T>(func,sticky));
            }
        }
        
        /// <summary>
        /// 向事件移除有参无返回值的监听函数（1个参数）
        /// </summary>
        /// <param name="eventName">事件名字</param>
        /// <param name="func">事件委托:需要移除的监听函数</param>
        /// <typeparam name="T">委托需要传参的类型（有参数的委托）</typeparam>
        public void RemoveEventListener<T>(E_EventType eventName, UnityAction<T> func)
        {
            var key = MakeKey(eventName, typeof(T), typeof(void));
            if (eventDic.TryGetValue(key,out var action) && action is EventInfo<T> info)
            {
                // 移除监听函数
                info.actions -= func;
                // 删除监听函数后判断 若此时事件无任何监听函数了 可直接删除记录
                if (info.actions == null)
                {
                    eventDic.Remove(key);
                }
            }
        }
        #endregion
        
        #region 有参无返回类型的委托（2个参数）
        /// <summary>
        /// 触发有参无返回的监听事件的函数（2个参数）
        /// 若事件未注册(无监听函数)，则不会执行任何操作，不抛出异常
        /// </summary>
        /// <param name="eventName">事件名字</param>
        /// <param name="info">具体监听函数的传参</param>
        /// <typeparam name="T1">委托需要传参的类型1（有参数的委托）</typeparam>
        /// <typeparam name="T2">委托需要传参的类型2（有参数的委托）</typeparam>
        public void EventTrigger<T1, T2>(E_EventType eventName,T1 a, T2 b)
        {
            var key = MakeKey(eventName, typeof(T1), typeof(T2));
            // 存在可以被监听的该事件 才允许触发事件
            // 执行监听函数 类型检查（模式匹配写法 更安全 防止空引用）
            if (eventDic.TryGetValue(key,out var action) && action is EventInfo<T1,T2> eventInfo)
            {
                eventInfo.lastValue = (a, b);
                eventInfo.hasValue = true;
                // 执行监听函数 传入T类型参数
                eventInfo.actions?.Invoke(a,b);
            }
        }
        
        /// <summary>
        /// 向事件添加有参无返回值的监听函数（2个参数）
        /// </summary>
        /// <param name="eventName">事件名字</param>
        /// <param name="func">事件委托:需要添加的监听函数</param>
        /// <typeparam name="T1">委托需要传参的类型1（有参数的委托）</typeparam>
        /// <typeparam name="T2">委托需要传参的类型2（有参数的委托）</typeparam>
        public void AddEventListener<T1, T2>(E_EventType eventName, UnityAction<T1, T2> func, bool sticky = false)
        {
            var key = MakeKey(eventName, typeof(T1), typeof(T2));
            // 若已经存在监听事件的委托记录 直接添加监听函数即可
            if (eventDic.TryGetValue(key,out var action) && action is EventInfo<T1, T2> info)
            {
                // 添加监听函数
                info.actions += func;
                // 如果是粘性事件且之前触发过 → 新监听者立即触发一次
                if (info.isSticky && info.hasValue)
                {
                    var (v1, v2) = info.lastValue;
                    func.Invoke(v1, v2);
                }
            }
            // 若无此事件的记录
            else
            {
                // 向全局事件字典里添加事件记录
                eventDic.Add(key,new EventInfo<T1, T2>(func,sticky));
            }
        }
        
        /// <summary>
        /// 向事件移除有参无返回值的监听函数（2个参数）
        /// </summary>
        /// <param name="eventName">事件名字</param>
        /// <param name="func">事件委托:需要移除的监听函数</param>
        /// <typeparam name="T1">委托需要传参的类型1（有参数的委托）</typeparam>
        /// <typeparam name="T2">委托需要传参的类型2（有参数的委托）</typeparam>
        public void RemoveEventListener<T1, T2>(E_EventType eventName, UnityAction<T1, T2> func)
        {
            var key = MakeKey(eventName, typeof(T1), typeof(T2));
            if (eventDic.TryGetValue(key,out var action) && action is EventInfo<T1, T2> info)
            {
                // 移除监听函数
                info.actions -= func;
                // 删除监听函数后判断 若此时事件无任何监听函数了 可直接删除记录
                if (info.actions == null)
                {
                    eventDic.Remove(key);
                }
            }
        }
        #endregion

        #region 清除事件监听

        /// <summary>
        /// 清除所有事件的监听
        /// </summary>
        public void ClearAllEvent()
        {
            eventDic.Clear();
        }

        /// <summary>
        /// 清除指定事件名的所有监听
        /// </summary>
        public void ClearEvent(E_EventType eventName)
        {
            // 找到所有匹配 eventName 的 key
            var keysToRemove = eventDic.Keys.Where(key => key.Item1.Equals(eventName)).ToList();

            // 批量删除
            foreach (var key in keysToRemove)
            {
                eventDic.Remove(key);
            }
        }

        #endregion
        
    }
}
