using UnityEngine.Events;

namespace Framework.Event
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

        public EventInfo(UnityAction<T> action)
        {
            actions += action;
        }
    }
    
    /// <summary>
    /// 包装无参无返回值的委托
    /// </summary>
    public class EventInfo : BaseEventInfo
    {
        // 真正观察者 对应的函数信息 记录在其中
        public UnityAction actions;

        public EventInfo(UnityAction action)
        {
            actions += action;
        }
    }
}
