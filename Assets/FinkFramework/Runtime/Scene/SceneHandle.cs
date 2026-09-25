using UnityScene = UnityEngine.SceneManagement.Scene;

namespace FinkFramework.Runtime.Scene
{
    /// <summary>
    /// 一次实际加载出来的 Unity 场景句柄。
    /// </summary>
    public sealed class SceneHandle
    {
        internal SceneHandle(UnityScene scene)
        {
            Scene = scene;
        }

        /// <summary>Unity 为这一次加载分配的场景实例。</summary>
        public UnityScene Scene { get; }

        /// <summary>加载时的场景名。</summary>
        public string Name => Scene.name;

        /// <summary>Unity 场景实例句柄，可用于日志和诊断。</summary>
        public int InstanceId => Scene.handle;

        /// <summary>Unity 场景实例句柄的简短别名。</summary>
        public int Handle => Scene.handle;

        /// <summary>场景当前是否仍然有效且已加载。</summary>
        public bool IsLoaded => Scene.IsValid() && Scene.isLoaded;

        public override string ToString() =>
            $"{Name} (handle: {InstanceId}, loaded: {IsLoaded})";
    }
}
