using UnityEngine;
// ReSharper disable ClassWithVirtualMembersNeverInherited.Global

namespace FinkFramework.Runtime.Singleton
{
    /// <summary>
    /// 自动挂载式继承Mono的单例模式基类
    /// 继承自该基类的类会实现单例模式（前提是该类继承了Mono） 无需自行挂载至GameObject上
    /// </summary>
    /// <typeparam name="T">类名</typeparam>
    public class SingletonMonoAuto<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T _instance;
        
        /// <summary>
        /// 在退出阶段防止访问单例造成幽灵实例
        /// </summary>
        private static bool _applicationIsQuitting;

        /// <summary>
        /// 是否已经存在实例（不会创建）
        /// </summary>
        public static bool HasInstance => _instance is not null;

        public static T Instance
        {
            get
            {
                if (_applicationIsQuitting)
                    return null;

                if (_instance == null)
                {
                    CreateInstance();
                }

                return _instance;
            }
        }

        private static void CreateInstance()
        {
            var go = new GameObject(typeof(T).Name)
            {
                hideFlags = HideFlags.None,
                name = typeof(T).Name
            };
            _instance = go.AddComponent<T>();
            DontDestroyOnLoad(go);
        }

        protected virtual void Awake()
        {
            if (_instance is not null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this as T;
            DontDestroyOnLoad(gameObject);
        }
        
        protected virtual void OnApplicationQuit()
        {
            _applicationIsQuitting = true;
        }

        protected virtual void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }
    }
}