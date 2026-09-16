using FinkFramework.Runtime.Utils;
using UnityEngine;

namespace FinkFramework.Runtime.Singleton
{
    /// <summary>
    /// 手动挂载式继承Mono的单例模式基类
    /// 继承自该基类的类会实现单例模式（前提是该类继承了Mono） 需要自行挂载至GameObject上
    /// </summary>
    /// <typeparam name="T">类名</typeparam>
    // ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
    public abstract class SingletonMono<T> : MonoBehaviour where T : MonoBehaviour
    {
        // ReSharper disable once StaticMemberInGenericType
        private static T _instance;
        
        /// <summary>
        /// 在退出阶段防止访问单例造成幽灵实例
        /// </summary>
        // ReSharper disable once StaticMemberInGenericType
        private static bool _applicationIsQuitting;

        /// <summary>
        /// 是否已经存在实例（不会创建）
        /// </summary>
        public static bool HasInstance => !_applicationIsQuitting && _instance != null;

        /// <summary>
        /// 获取已经由场景挂载的实例；不存在时返回 null，不会创建。
        /// </summary>
        public static T TryGetInstance()
        {
            return _applicationIsQuitting || _instance == null ? null : _instance;
        }
        
        public static T Instance
        {
            get
            {
                if (_applicationIsQuitting)
                    return null;

                if (_instance == null)
                {
                    LogUtil.Error($"{typeof(T).Name} 未在场景中实例化。");
                }

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

        /// <summary>
        /// 支持关闭 Domain Reload 的编辑器播放模式，避免退出 Play 后保留旧静态引用。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
            _applicationIsQuitting = false;
        }
    }
}
