using FinkFramework.Runtime.Utils;
using UnityEngine;

namespace FinkFramework.Runtime.Singleton
{
    /// <summary>
    /// 自动创建、跨场景常驻的 MonoBehaviour 单例。
    /// 只应由运行时全局基础服务继承，例如协程和帧更新桥接器。
    /// 不要手动挂载继承者；首次访问 Instance 时会创建专用对象。
    /// </summary>
    public abstract class SingletonAutoMono<T> : MonoBehaviour where T : MonoBehaviour
    {
        // ReSharper disable once StaticMemberInGenericType
        private static T _instance;
        // ReSharper disable once StaticMemberInGenericType
        private static bool _applicationIsQuitting;

        /// <summary>是否已经有实例；不会自动创建。</summary>
        public static bool HasInstance => _instance is not null;

        /// <summary>获取已有实例；不存在时返回 null，不会自动创建。</summary>
        public static T TryGetInstance()
        {
            return _applicationIsQuitting ? null : _instance;
        }

        /// <summary>
        /// 获取实例；首次运行时访问会创建专用 GameObject，并使其跨场景常驻。
        /// 只能在 Unity 主线程、运行期间调用。
        /// </summary>
        public static T Instance
        {
            get
            {
                if (_applicationIsQuitting)
                    return null;

                if (_instance != null)
                    return _instance;

                if (!Application.isPlaying)
                {
                    LogUtil.Error($"{typeof(T).Name} 只能在运行时自动创建。");
                    return null;
                }

                var singletonObject = new GameObject($"[Singleton] {typeof(T).Name}");
                _instance = singletonObject.AddComponent<T>();
                return _instance;
            }
        }

        protected virtual void Awake()
        {
            if (_instance != null && _instance != this)
            {
                LogUtil.Error($"场景中存在多个 {typeof(T).Name}");
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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
            _applicationIsQuitting = false;
        }
    }
}
