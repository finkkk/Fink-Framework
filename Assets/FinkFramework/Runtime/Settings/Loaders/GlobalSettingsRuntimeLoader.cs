using System;
using System.Collections.Generic;
using FinkFramework.Runtime.Settings.ScriptableObjects;
using UnityEngine;

namespace FinkFramework.Runtime.Settings.Loaders
{
    /// <summary>
    /// 框架配置统一入口，必须在 Unity 主线程访问。已有配置直接复用；
    /// 编辑器通过注册的加载器补建资产，Player 缺失配置时缓存内存默认值。
    /// </summary>
    public static class GlobalSettingsRuntimeLoader
    {
        private const string ResourcesDirectory = "FinkFramework/Settings/";
        private static GlobalSettingsAsset _instance;
        private static List<ScriptableObject> runtimeDefaults;

#if UNITY_EDITOR
        private static Func<GlobalSettingsAsset> editorLoader;

        /// <summary>由编辑器程序集注册资产补建入口，避免运行时依赖 AssetDatabase 或编辑器程序集。</summary>
        /// <param name="loader">复用或创建根配置并补齐模块引用的编辑器入口。</param>
        public static void RegisterEditorLoader(Func<GlobalSettingsAsset> loader)
        {
            editorLoader = loader ?? throw new ArgumentNullException(nameof(loader));
            _instance = null;
        }
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache()
        {
            _instance = null;
            if (runtimeDefaults == null) return;
            // 仅销毁自行创建的内存默认值；项目中的配置资产不由加载器释放。
            foreach (var asset in runtimeDefaults)
            {
                if (!asset) continue;
                if (Application.isPlaying) UnityEngine.Object.Destroy(asset);
                else UnityEngine.Object.DestroyImmediate(asset);
            }
            runtimeDefaults = null;
        }

        /// <summary>当前根配置。缺失时自动创建默认值，不因缺少配置文件返回 null。</summary>
        public static GlobalSettingsAsset Current
        {
            get
            {
                if (_instance) return _instance;
#if UNITY_EDITOR
                if (editorLoader != null) _instance = editorLoader();
#endif
                if (!_instance) _instance = LoadOrCreateDefault<GlobalSettingsAsset>();
                return _instance;
            }
        }

        /// <summary>获取已有或默认根配置，必须在 Unity 主线程调用。</summary>
        /// <param name="settings">缓存的根配置。</param>
        /// <returns>成功取得配置时为 true，文件缺失会自动补齐。</returns>
        public static bool TryGet(out GlobalSettingsAsset settings)
        {
            settings = Current;
            return settings;
        }

        /// <summary>复用模块引用，缺失时尝试编辑器补建，Player 则沿默认 Resources 路径查找或创建内存默认值。</summary>
        internal static T GetOrCreateModule<T>(ref T settings) where T : ScriptableObject
        {
            if (settings) return settings;
#if UNITY_EDITOR
            // 补建入口会绑定持久化根配置及所选后端的引用，不修改已有模块参数。
            editorLoader?.Invoke();
            if (settings) return settings;
#endif
            settings = LoadOrCreateDefault<T>();
            return settings;
        }

        private static T LoadOrCreateDefault<T>() where T : ScriptableObject
        {
            string resourceName = typeof(T) == typeof(AssetBundleBackendSettingsAsset)
                ? "AssetBundleBackendSettings"
                : typeof(T) == typeof(AddressablesBackendSettingsAsset)
                    ? "AddressablesBackendSettings" : typeof(T).Name;
            var asset = Resources.Load<T>(ResourcesDirectory + resourceName);
            if (asset) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            asset.name = resourceName;
            asset.hideFlags = HideFlags.HideAndDontSave;
            (runtimeDefaults ??= new List<ScriptableObject>()).Add(asset);
            return asset;
        }

        /// <summary>所有脚本生成器共用的 Assets 下相对脚本根目录，内容无效时使用默认目录。</summary>
        public static string ScriptRootDirectory =>
            GlobalSettingsAsset.NormalizeScriptRootDirectory(
                Current.ScriptRootDirectory);

    }
}
