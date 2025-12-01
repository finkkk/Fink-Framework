using UnityEngine;

namespace FinkFramework.Runtime.Singleton
{
    /// <summary>
    /// 挂载式继承Mono的单例模式基类 继承自该基类的类会实现单例模式（前提是该类继承了Mono） 需要自行挂载至GameObject上
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class SingletonMono<T> : MonoBehaviour where T : MonoBehaviour
    {
        public static T Instance { get; private set; }

        private void Awake()
        {
            if (Instance)
            {
                Destroy(this);
                return;
            }
            Instance = this as T;
            DontDestroyOnLoad(gameObject);
        }
    }
}
