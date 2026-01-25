using System;
using System.Reflection;
using FinkFramework.Runtime.Utils;

namespace FinkFramework.Runtime.Singleton
{
    /// <summary>
    /// 不继承Mono的单例模式基类 继承该基类的类可实现单例模式 但要求有私有的无参构造函数
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class Singleton<T> where T : class
    {
        private static T instance;
        // ReSharper disable once StaticMemberInGenericType
        protected static readonly object lockObj = new();
        
        public static bool HasInstance => instance != null;

        public static T TryGetInstance()
        {
            return instance;
        }
        
        public static T Instance
        {
            get
            {
                // 当两个线程都需要访问该实例时如果只是返回实例还需要等待线程完毕的话效率太低 因此直接判空  若实例存在直接返回 不需要等待线程
                if (instance == null)
                {
                    // 锁住 防止多线程并发访问该实例的时候出现线程不安全的问题 锁住即可保证在访问完毕后再允许下一个线程访问
                    lock (lockObj)
                    {
                        if (instance == null)
                        {
                            // 使用反射获取继承该类的类(即T)的私有无参构造函数
                            Type type = typeof(T);
                            ConstructorInfo info = type.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null,
                                Type.EmptyTypes, null);
                            if (info != null)
                            {
                                // 如果能获取到私有无参构造函数 就执行该函数 并将构造出来的实例赋值给instance
                                instance = info.Invoke(null) as T;
                            }
                            else
                            {
                                LogUtil.Error("没有显式实现私有无参构造函数");
                            }
                        }
                    }
                }
                return instance;
            }
        }
    }
}
