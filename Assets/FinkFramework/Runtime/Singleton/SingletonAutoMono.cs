using UnityEngine;

namespace FinkFramework.Runtime.Singleton
{
    /// <summary>
    /// 自动挂载式继承Mono的单例模式基类 继承自该基类的类会实现单例模式（前提是该类继承了Mono） 无需挂载 会自动实例化一个挂载该脚本的游戏对象
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class SingletonAutoMono<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T instance;
        public static T Instance
        {
            get
            {
                if (!instance)
                {
                    // 先尝试在场景中找
                    instance = FindObjectOfType<T>();

                    if (!instance)
                    {
                        var obj = new GameObject(typeof(T).Name);
                        instance = obj.AddComponent<T>();
                        DontDestroyOnLoad(obj);
                    }
                }
                return instance;
            }
        }
        
        protected virtual void Awake()
        {
            if (!instance)
            {
                instance = this as T;
                DontDestroyOnLoad(gameObject);
            }
            else if (instance != this)
            {
                // 防止重复
                Destroy(gameObject);
            }
        }
    }
}
