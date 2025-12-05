#if UNITY_EDITOR
using System.IO;
using Cysharp.Threading.Tasks;
using FinkFramework.Runtime.ResLoad.Base;
using UnityEditor;
using Object = UnityEngine.Object;

namespace FinkFramework.Runtime.ResLoad.Providers
{
    /// <summary>
    /// 编辑器下使用 AssetDatabase 来加载资源的 Provider
    /// 开发期使用，打包前请替换为其他 Provider（如 Resources / AB / Web）
    /// </summary>
    public class EditorProvider : IResProvider
    {
        // 你的编辑器资源根目录，可自由修改
        private const string ROOT = "Assets/Editor/";

        // 拼接完整路径
        private string BuildPath(string path)
        {
            // 例如 path=UI/MainPanel 会得到：
            // Assets/Editor/ArtRes/UI/MainPanel
            string full = Path.Combine(ROOT, path).Replace("\\", "/");

            return full;
        }

        /// <summary>
        /// 同步加载资源
        /// </summary>
        public T Load<T>(string path) where T : Object
        {
            string fullPath = BuildPath(path);

            T asset = AssetDatabase.LoadAssetAtPath<T>(fullPath);

            if (!asset)
            {
                // 尝试无后缀查一次后缀资源
                asset = AssetDatabase.LoadAssetAtPath<T>(fullPath + ".prefab");
                if (!asset) asset = AssetDatabase.LoadAssetAtPath<T>(fullPath + ".asset");
                if (!asset) asset = AssetDatabase.LoadAssetAtPath<T>(fullPath + ".mat");
                if (!asset) asset = AssetDatabase.LoadAssetAtPath<T>(fullPath + ".png");
                if (!asset) asset = AssetDatabase.LoadAssetAtPath<T>(fullPath + ".jpg");
                if (!asset) asset = AssetDatabase.LoadAssetAtPath<T>(fullPath + ".mp3");
                if (!asset) asset = AssetDatabase.LoadAssetAtPath<T>(fullPath + ".wav");
            }

            return asset;
        }

        /// <summary>
        /// 异步加载（AssetDatabase 没有异步，这里用 UniTask 模拟）
        /// </summary>
        public async UniTask<T> LoadAsync<T>(string path) where T : Object
        {
            // Editor 不支持真正异步，仍然立即加载，但对外保持异步接口一致
            await UniTask.Yield();
            return Load<T>(path);
        }

        /// <summary>
        /// 检测资源是否存在
        /// </summary>
        public bool Exists(string path)
        {
            string fullPath = BuildPath(path);
            return AssetDatabase.LoadAssetAtPath<Object>(fullPath) != null ||
                   AssetDatabase.LoadAssetAtPath<Object>(fullPath + ".prefab") != null ||
                   AssetDatabase.LoadAssetAtPath<Object>(fullPath + ".asset") != null ||
                   AssetDatabase.LoadAssetAtPath<Object>(fullPath + ".png") != null ||
                   AssetDatabase.LoadAssetAtPath<Object>(fullPath + ".jpg") != null ||
                   AssetDatabase.LoadAssetAtPath<Object>(fullPath + ".wav") != null ||
                   AssetDatabase.LoadAssetAtPath<Object>(fullPath + ".mp3") != null;
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
