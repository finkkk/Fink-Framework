#if UNITY_EDITOR
using Cysharp.Threading.Tasks;
using FinkFramework.Runtime.ResLoad.Base;
using FinkFramework.Runtime.Utils;
using UnityEditor;
using Object = UnityEngine.Object;

namespace FinkFramework.Runtime.ResLoad.Providers
{
    /// <summary>
    /// Editor 环境下使用 AssetDatabase 加载任意 Assets 路径资源的 Provider
    /// 仅用于开发期，打包前请使用其他 Provider（Resources / AB / Addressables）
    /// </summary>
    public sealed class EditorProvider : IResProvider
    {
        private static readonly string[] TryExtensions =
        {
            "",
            ".prefab",
            ".asset",
            ".mat",
            ".png",
            ".jpg",
            ".mp3",
            ".wav"
        };
        
        /// <summary>
        /// 同步加载资源
        /// </summary>
        public T Load<T>(string path) where T : Object
        {
            foreach (var ext in TryExtensions)
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(path + ext);
                if (asset)
                    return asset;
            }

            LogUtil.Warn("EditorProvider", $"资源不存在: {path}");
            return null;
        }

        /// <summary>
        /// 异步加载（AssetDatabase 没有异步，这里用 UniTask 模拟）
        /// </summary>
        public async UniTask<T> LoadAsync<T>(string path) where T : Object
        {
            await UniTask.Yield();
            return Load<T>(path);
        }

        /// <summary>
        /// 检测资源是否存在
        /// </summary>
        public bool Exists(string path)
        {
            foreach (var ext in TryExtensions)
            {
                if (AssetDatabase.LoadAssetAtPath<Object>(path + ext))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 卸载（Editor 下不需要卸载，交给 ResManager）
        /// </summary>
        public void Unload(string path)
        {
            // Editor 模式资源不会真正持有实例，不做任何处理
        }

        public void Clear()
        {
            // Editor 没有缓存，不需要清理
        }

        /// <summary>
        /// Editor 无真实进度，统一返回 false
        /// </summary>
        public bool TryGetProgress(string path, out float progress)
        {
            progress = 0f;
            return false;
        }
    }
}
#endif
