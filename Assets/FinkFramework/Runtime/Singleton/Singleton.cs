using System;
using System.Reflection;
using UnityEngine;

namespace FinkFramework.Runtime.Singleton
{
    /// <summary>
    /// 不继承Mono的单例模式基类 继承该基类的类可实现单例模式 但要求有私有的无参构造函数
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class Singleton<T> where T : class
    {
        // ReSharper disable once StaticMemberInGenericType
        private static T instance;
        // ReSharper disable once StaticMemberInGenericType
        protected static readonly object lockObj = new();
        
        /// <summary>
        /// 是否已经创建实例。该属性不会触发创建。
        /// </summary>
        public static bool HasInstance => instance != null;

        public static T TryGetInstance()
        {
            return instance;
        }
        
        public static T Instance
        {
            get
            {
                if (instance == null)
                {
                    lock (lockObj)
                    {
                        if (instance == null)
                        {
                            Type type = typeof(T);
                            ConstructorInfo info = type.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null,
                                Type.EmptyTypes, null);
                            if (info != null)
                            {
                                try
                                {
                                    instance = info.Invoke(null) as T;
                                }
                                catch (TargetInvocationException exception) when (exception.InnerException != null)
                                {
                                    // 反射会把构造函数的真实异常包装起来；保留原始异常，便于定位初始化依赖错误。
                                    throw exception.InnerException;
                                }
                            }
                            else
                            {
                                throw new InvalidOperationException(
                                    $"{type.Name} 必须显式实现私有无参构造函数。");
                            }
                        }
                    }
                }
                return instance;
            }
        }

        /// <summary>
        /// 支持关闭 Domain Reload 的编辑器播放模式，避免跨次 Play 保留旧的静态实例。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
        }
    }
}
