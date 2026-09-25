namespace FinkFramework.Runtime.Scene
{
    /// <summary>
    /// 指定场景卸载完成后的可选内存整理策略。
    /// 场景级 UI 清理始终执行，不会触发框架的全局事件、对象池、音频或资源缓存清理。
    /// </summary>
    public readonly struct SceneCleanupOptions
    {
        public SceneCleanupOptions(
            bool unloadUnusedAssets = false,
            bool collectGarbage = false)
        {
            UnloadUnusedAssets = unloadUnusedAssets;
            CollectGarbage = collectGarbage;
        }

        /// <summary>卸载完成后调用 ResManager.UnloadUnusedAssets。</summary>
        public bool UnloadUnusedAssets { get; }

        /// <summary>卸载完成后请求一次托管堆 GC。仅在非 Editor 环境执行。</summary>
        public bool CollectGarbage { get; }

        /// <summary>只做场景级 UI 清理。</summary>
        public static SceneCleanupOptions None => new();

        /// <summary>只做场景级 UI 清理。</summary>
        public static SceneCleanupOptions Default => None;

        /// <summary>清理框架引用计数为零的资源并调用 Unity 资源回收。</summary>
        public static SceneCleanupOptions ReleaseUnusedAssets => new(unloadUnusedAssets: true);

        /// <summary>释放未使用资源，并在非 Editor 环境执行 GC。</summary>
        public static SceneCleanupOptions ReleaseUnusedAssetsAndCollectGarbage =>
            new(unloadUnusedAssets: true, collectGarbage: true);
    }
}
