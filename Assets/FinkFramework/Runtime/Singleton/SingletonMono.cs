using UnityEngine;

namespace FinkFramework.Runtime.Singleton
{
    /// <summary>
    /// 挂载式继承Mono的单例模式基类 继承自该基类的类会实现单例模式（前提是该类继承了Mono） 需要自行挂载至GameObject上
    /// </summary>
    /// <typeparam name="T"></typeparam>
    // ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
    public abstract class SingletonMono<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T _instance;

        public static T Instance
        {
            get
            {
                if (!_instance)
                {
                    // 先找场景里有没有
                    _instance = FindObjectOfType<T>();

                    if (!_instance)
                    {
                        // 再强制创建
                        var go = new GameObject(typeof(T).Name);
                        _instance = go.AddComponent<T>();
                        DontDestroyOnLoad(go);
                    }
                }
                return _instance;
            }
        }

        protected virtual void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this as T;
            DontDestroyOnLoad(gameObject);
        }
    }
}