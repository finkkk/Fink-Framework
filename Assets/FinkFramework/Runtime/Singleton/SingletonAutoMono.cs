using UnityEngine;

namespace FinkFramework.Runtime.Singleton
{
    /// <summary>
    /// 自动挂载式继承Mono的单例模式基类 继承自该基类的类会实现单例模式（前提是该类继承了Mono） 无需挂载 会自动实例化一个挂载该脚本的游戏对象
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class SingletonAutoMono<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T instance;
        public static T Instance
        {
            get
            {
                if (!instance)
                {
                    // 动态创建一个空游戏对象 并自动挂载该脚本
                    GameObject obj = new()
                    {
                        // 自动按照类名给该游戏对象命名
                        name = typeof(T).Name
                    };
                    instance = obj.AddComponent<T>();
                    DontDestroyOnLoad(obj);
                }
                return instance;
            }
        }
    }
}
