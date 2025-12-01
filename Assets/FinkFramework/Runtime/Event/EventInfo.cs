using UnityEngine.Events;

namespace FinkFramework.Runtime.Event
{
    /// <summary>
    /// 事件信息的抽象基类，用于统一管理不同类型事件的容器
    /// </summary>
    public abstract class BaseEventInfo { }
    
    /// <summary>
    /// 包装有参无返回值的委托（1个参数）
    /// </summary>
    /// <typeparam name="T">委托需要传参的类型</typeparam>
    public class EventInfo<T> : BaseEventInfo
    {
        // 真正观察者 对应的函数信息 记录在其中
        public UnityAction<T> actions;
        
        // 是否为粘性事件
        public bool isSticky;
        // 是否已经触发过
        public bool hasValue;
        // 最近一次的参数值
        public T lastValue;

        public EventInfo(UnityAction<T> action, bool sticky = false)
        {
            actions += action;
            isSticky = sticky;
        }
    }
    
    /// <summary>
    /// 包装有参无返回值的委托（2个参数）
    /// </summary>
    /// <typeparam name="T">委托需要传参的类型</typeparam>
    public class EventInfo<T1, T2> : BaseEventInfo
    {
        public UnityAction<T1, T2> actions;
        
        // 是否为粘性事件
        public bool isSticky;
        // 是否已经触发过
        public bool hasValue;
        // 最近一次的参数值
        public (T1, T2) lastValue;

        public EventInfo(UnityAction<T1, T2> action, bool sticky = false)
        {
            actions += action;
            isSticky = sticky;
        }
    }
    
    /// <summary>
    /// 包装无参无返回值的委托
    /// </summary>
    public class EventInfo : BaseEventInfo
    {
        // 真正观察者 对应的函数信息 记录在其中
        public UnityAction actions;
        
        // 是否为粘性事件
        public bool isSticky;
        // 是否已经触发过
        public bool hasFired;

        public EventInfo(UnityAction action, bool sticky = false)
        {
            actions += action;
            isSticky = sticky;
        }
    }
}
