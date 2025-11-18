using System.Collections.Generic;
using Framework.Singleton;
using UnityEngine.Events;

namespace Framework.Event
{
    /// <summary>
    /// 事件全局管理器(事件中心模块)
    /// </summary>
    public class EventManager : Singleton<EventManager>
    {
        private EventManager(){}
        // 全局事件记录字典 用于记录对应事件关联的对应函数
        private readonly Dictionary<E_EventType, BaseEventInfo> eventDic = new();
        
        #region 无参无返回类型的委托
        /// <summary>
        /// 触发无参无返回监听函数
        /// </summary>
        /// <param name="eventName">事件名字</param>
        public void EventTrigger(E_EventType eventName)
        {
            // 存在可以被监听的该事件 才允许触发事件
            if (eventDic.TryGetValue(eventName,out var action) && action is EventInfo info)
            {
                // 执行监听函数
                info.actions?.Invoke();
            }
        }
        
        /// <summary>
        /// 向事件添加无参无返回值的监听函数
        /// </summary>
        /// <param name="eventName">事件名字</param>
        /// <param name="func">事件委托:需要添加的监听函数</param>
        public void AddEventListener(E_EventType eventName, UnityAction func)
        {
            // 若已经存在监听事件的委托记录 直接添加监听函数即可
            if (eventDic.TryGetValue(eventName,out var action) && action is EventInfo info)
            {
                // 添加监听函数
                info.actions += func;
            }
            // 若无此事件的记录
            else
            {
                // 向全局事件字典里添加事件记录
                eventDic.Add(eventName,new EventInfo(func));
            }
        }
        
        /// <summary>
        /// 向事件移除无参无返回值的监听函数
        /// </summary>
        /// <param name="eventName">事件名字</param>
        /// <param name="func">事件委托:需要移除的监听函数</param>
        public void RemoveEventListener(E_EventType eventName, UnityAction func)
        {
            if (eventDic.TryGetValue(eventName,out var action) && action is EventInfo info)
            {
                // 移除监听函数
                info.actions -= func;
                // 删除监听函数后判断 若此时事件无任何监听函数了 可直接删除记录
                if (info.actions == null)
                {
                    eventDic.Remove(eventName);
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
            // 存在可以被监听的该事件 才允许触发事件
            // 执行监听函数 类型检查（模式匹配写法 更安全 防止空引用）
            if (eventDic.TryGetValue(eventName,out var action) && action is EventInfo<T> eventInfo)
            {
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
        public void AddEventListener<T>(E_EventType eventName, UnityAction<T> func)
        {
            // 若已经存在监听事件的委托记录 直接添加监听函数即可
            if (eventDic.TryGetValue(eventName,out var action) && action is EventInfo<T> info)
            {
                // 添加监听函数
                info.actions += func;
            }
            // 若无此事件的记录
            else
            {
                // 向全局事件字典里添加事件记录
                eventDic.Add(eventName,new EventInfo<T>(func));
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
            if (eventDic.TryGetValue(eventName,out var action) && action is EventInfo<T> info)
            {
                // 移除监听函数
                info.actions -= func;
                // 删除监听函数后判断 若此时事件无任何监听函数了 可直接删除记录
                if (info.actions == null)
                {
                    eventDic.Remove(eventName);
                }
            }
        }
        #endregion
        
        /// <summary>
        /// 清除所有事件的监听
        /// </summary>
        public void ClearAllEvent()
        {
            eventDic.Clear();
        }

        /// <summary>
        /// 清除指定事件的监听
        /// </summary>
        /// <param name="name">需要清除的事件名字</param>
        public void ClearEvent(E_EventType name)
        {
            // Remove方法只有当存在的时候才会执行
            eventDic.Remove(name);
        }
    }
}
