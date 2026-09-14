using System;
using Cysharp.Threading.Tasks;
using FinkFramework.Runtime.ResLoad;
using FinkFramework.Runtime.Settings.Loaders;
using FinkFramework.Runtime.UI.Base;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FinkFramework.Runtime.UI.Core
{
    /// <summary>面板资源路径解析、加载与实例化的唯一入口。</summary>
    internal sealed class UIPanelLoader
    {
        public string GetAssetPath<T>() where T : BasePanel
        {
            string root = GlobalSettingsRuntimeLoader.UIPanelResourcesPath;
            if (string.IsNullOrWhiteSpace(root))
            {
                throw new InvalidOperationException(
                    "UI 面板资源目录为空。请在 Project Settings > Fink Framework > UI Settings 中配置。");
            }

            root = root.Trim('/');
            if (root.Length == 0)
            {
                throw new InvalidOperationException(
                    "UI 面板资源目录不能只包含斜杠。请在 Project Settings > Fink Framework > UI Settings 中配置。");
            }

            return $"res://{root}/{typeof(T).Name}";
        }

        public T Load<T>(string assetPath, Transform parent) where T : BasePanel
        {
            GameObject prefab = ResManager.Instance.Load<GameObject>(assetPath);
            try
            {
                return InstantiateAndValidate<T>(prefab, assetPath, parent);
            }
            catch
            {
                if (prefab)
                    Release(assetPath);
                throw;
            }
        }

        public async UniTask<T> LoadAsync<T>(string assetPath, Transform parent) where T : BasePanel
        {
            GameObject prefab = await ResManager.Instance.LoadAsync<GameObject>(assetPath);
            await UniTask.SwitchToMainThread();
            try
            {
                return InstantiateAndValidate<T>(prefab, assetPath, parent);
            }
            catch
            {
                if (prefab)
                    Release(assetPath);
                throw;
            }
        }

        public void Release(string assetPath)
        {
            ResManager.Instance.UnloadAsset<GameObject>(assetPath, true);
        }

        private static T InstantiateAndValidate<T>(GameObject prefab, string assetPath, Transform parent)
            where T : BasePanel
        {
            if (!prefab)
                throw new InvalidOperationException($"未找到 UI 面板预制体：{assetPath}");

            GameObject instance = Object.Instantiate(prefab, parent, false);
            instance.name = typeof(T).Name;
            T panel = instance.GetComponent<T>();
            if (panel)
            {
                instance.SetActive(false);
                return panel;
            }

            Object.Destroy(instance);
            throw new InvalidOperationException(
                $"UI 预制体 {assetPath} 的根对象没有挂载 {typeof(T).FullName}。"
                + "面板脚本必须挂在预制体根对象上。");
        }
    }
}
