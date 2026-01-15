using System;
using System.Collections.Generic;
using System.Linq;
using FinkFramework.Runtime.Singleton;
using FinkFramework.Runtime.Utils;
using UnityEngine.Events;

namespace FinkFramework.Runtime.Event
{
    /// <summary>
    /// 事件全局管理器(事件中心模块)
    /// </summary>
    public class EventManager : Singleton<EventManager>
    {
        private EventManager(){}
        // 全局事件记录字典 用于记录对应事件关联的对应函数
        private readonly Dictionary<(Enum eventType, Type param1, Type param2), BaseEventInfo> eventDic = new();
        
        /// <summary>
        /// 构建稳定 Key 用于被字典记录事件
        /// </summary>
        private (Enum, Type, Type) MakeKey(Enum eventType, Type t1, Type t2)
        {
            return (eventType, t1 ?? typeof(void), t2 ?? typeof(void));
        }
        
        #region 无参无返回类型的委托
        /// <summary>
        /// 触发无参无返回监听函数
        /// </summary>
        /// <param name="eventName">事件名字</param>
        public void EventTrigger(Enum eventName)
        {
            var key = MakeKey(eventName, typeof(void), typeof(void));
            // 存在可以被监听的该事件 才允许触发事件
            if (eventDic.TryGetValue(key,out var action) && action is EventInfo info)
            {
                info.hasFired = true;
                // 执行监听函数
                var invocationList = info.actions?.GetInvocationList();
                if (invocationList == null)
                    return;

                foreach (var @delegate in invocationList)
                {
                    var cb = (UnityAction)@delegate;
                    cb.Invoke();
                }
            }
        }
        
        /// <summary>
        /// 向事件添加无参无返回值的监听函数
        /// </summary>
        /// <param name="eventName">事件名字</param>
        /// <param name="func">事件委托:需要添加的监听函数</param>
        public void AddEventListener(Enum eventName, UnityAction func, bool sticky = false)
        {
#if UNITY_EDITOR
            CheckSignatureConflict(eventName, typeof(void), typeof(void));
#endif
            var key = MakeKey(eventName, typeof(void), typeof(void));
            // 若已经存在监听事件的委托记录 直接添加监听函数即可
            if (eventDic.TryGetValue(key,out var action) && action is EventInfo info)
            {
                if (info.actions != null && info.actions.GetInvocationList().Contains(func))
                {
                    LogUtil.Warn("EventManager", $"重复注册事件监听：{eventName}（无参）");
                    return;
                }
                // 添加监听函数
                info.actions += func;
                if (sticky && !info.isSticky)
                    info.isSticky = true;
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
        public void RemoveEventListener(Enum eventName, UnityAction func)
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
        public void EventTrigger<T>(Enum eventName,T info)
        {
            var key = MakeKey(eventName, typeof(T), typeof(void));
            // 存在可以被监听的该事件 才允许触发事件
            // 执行监听函数 类型检查（模式匹配写法 更安全 防止空引用）
            if (eventDic.TryGetValue(key,out var action) && action is EventInfo<T> eventInfo)
            {
                eventInfo.lastValue = info;
                eventInfo.hasValue = true;
                // 执行监听函数 传入T类型参数
                var invocationList = eventInfo.actions?.GetInvocationList();
                if (invocationList == null)
                    return;

                foreach (var @delegate in invocationList)
                {
                    var cb = (UnityAction<T>)@delegate;
                    cb.Invoke(info);
                }
            }
        }
        
        /// <summary>
        /// 向事件添加有参无返回值的监听函数（1个参数）
        /// </summary>
        /// <param name="eventName">事件名字</param>
        /// <param name="func">事件委托:需要添加的监听函数</param>
        /// <typeparam name="T">委托需要传参的类型（有参数的委托）</typeparam>
        public void AddEventListener<T>(Enum eventName, UnityAction<T> func, bool sticky = false)
        {
#if UNITY_EDITOR
            CheckSignatureConflict(eventName, typeof(T),  typeof(void));
#endif
            var key = MakeKey(eventName, typeof(T), typeof(void));
            // 若已经存在监听事件的委托记录 直接添加监听函数即可
            if (eventDic.TryGetValue(key,out var action) && action is EventInfo<T> info)
            {
                if (info.actions != null && info.actions.GetInvocationList().Contains(func))
                {
                    LogUtil.Warn( "EventManager", $"重复注册事件监听：{eventName}<{typeof(T).Name}>");
                    return;
                }
                // 添加监听函数
                info.actions += func;
                if (sticky && !info.isSticky)
                    info.isSticky = true;
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
        public void RemoveEventListener<T>(Enum eventName, UnityAction<T> func)
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
        public void EventTrigger<T1, T2>(Enum eventName,T1 a, T2 b)
        {
            var key = MakeKey(eventName, typeof(T1), typeof(T2));
            // 存在可以被监听的该事件 才允许触发事件
            // 执行监听函数 类型检查（模式匹配写法 更安全 防止空引用）
            if (eventDic.TryGetValue(key,out var action) && action is EventInfo<T1,T2> eventInfo)
            {
                eventInfo.lastValue = (a, b);
                eventInfo.hasValue = true;
                // 执行监听函数 传入T类型参数
                var invocationList = eventInfo.actions?.GetInvocationList();
                if (invocationList == null)
                    return;

                foreach (var @delegate in invocationList)
                {
                    var cb = (UnityAction<T1, T2>)@delegate;
                    cb.Invoke(a, b);
                }
            }
        }
        
        /// <summary>
        /// 向事件添加有参无返回值的监听函数（2个参数）
        /// </summary>
        /// <param name="eventName">事件名字</param>
        /// <param name="func">事件委托:需要添加的监听函数</param>
        /// <typeparam name="T1">委托需要传参的类型1（有参数的委托）</typeparam>
        /// <typeparam name="T2">委托需要传参的类型2（有参数的委托）</typeparam>
        public void AddEventListener<T1, T2>(Enum eventName, UnityAction<T1, T2> func, bool sticky = false)
        {
#if UNITY_EDITOR
            CheckSignatureConflict(eventName, typeof(T1), typeof(T2));
#endif
            var key = MakeKey(eventName, typeof(T1), typeof(T2));
            // 若已经存在监听事件的委托记录 直接添加监听函数即可
            if (eventDic.TryGetValue(key,out var action) && action is EventInfo<T1, T2> info)
            {
                // 防止重复注册
                if (info.actions != null && info.actions.GetInvocationList().Contains(func))
                {
                    LogUtil.Warn("EventManager", $"重复注册事件监听：{eventName}<{typeof(T1).Name},{typeof(T2).Name}>");
                    return;
                }
                // 添加监听函数
                info.actions += func;
                if (sticky && !info.isSticky)
                    info.isSticky = true;
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
        public void RemoveEventListener<T1, T2>(Enum eventName, UnityAction<T1, T2> func)
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
        /// 将清除该事件名下所有参数签名的监听
        /// </summary>
        public void ClearAllEvent()
        {
            eventDic.Clear();
        }

        /// <summary>
        /// 清除指定事件名的所有监听
        /// </summary>
        public void ClearEvent(Enum eventName)
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

        #region 工具方法

#if UNITY_EDITOR
        private void CheckSignatureConflict(Enum eventName, Type t1, Type t2)
        {
            foreach (var key in eventDic.Keys)
            {
                // 同一个事件 ID
                if (!key.Item1.Equals(eventName))
                    continue;

                // 参数签名不一致 → 非法
                if (key.Item2 != (t1 ?? typeof(void)) ||
                    key.Item3 != (t2 ?? typeof(void)))
                {
                    LogUtil.Error(
                        "EventManager",
                        "检测到事件参数签名冲突！" +
                        $"事件名：{eventName}\n\n" +
                        $"已注册签名：({key.Item2.Name}, {key.Item3.Name})\n" +
                        $"当前尝试注册：({(t1 ?? typeof(void)).Name}, {(t2 ?? typeof(void)).Name})\n\n" +
                        "同一个事件只能使用一种参数签名。请检查是否对同一事件使用了不同参数类型。"
                    );
                }
            }
        }
#endif

        #endregion
        
    }
}
