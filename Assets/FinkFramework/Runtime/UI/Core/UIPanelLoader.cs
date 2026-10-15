using System;
using Cysharp.Threading.Tasks;
using FinkFramework.Runtime.Environments;
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
        public Func<Type, string> CustomAssetPathResolver { get; set; }

        public string GetAssetPath<T>() where T : BasePanel
        {
            if (GlobalSettingsRuntimeLoader.TryGet(out var settings))
            {
                switch (settings.UIPanelLoadSource)
                {
                    case UIPanelLoadSource.Resources:
                        break;
                    case UIPanelLoadSource.Custom:
                        return ResolveCustomPath<T>();
                    case UIPanelLoadSource.FollowResourceBackend:
                        switch (settings.ResourceBackend)
                        {
                            case EnvironmentState.ResourceBackendType.None:
                                break;
                            case EnvironmentState.ResourceBackendType.Addressables:
                                return $"addr://{typeof(T).Name}";
                            case EnvironmentState.ResourceBackendType.AssetBundle:
                                string bundle = settings.UIPanelAssetBundleName?.Trim();
                                if (string.IsNullOrWhiteSpace(bundle) || bundle.Contains("/") || bundle.Contains("\\"))
                                    throw new InvalidOperationException("UI AssetBundle 包名不能为空或包含斜杠，请在 UI Settings 中配置。");
                                return $"ab://{bundle}/{typeof(T).Name}";
                            case EnvironmentState.ResourceBackendType.Custom:
                                return ResolveCustomPath<T>();
                            default:
                                throw new InvalidOperationException($"不支持的资源后端：{settings.ResourceBackend}");
                        }
                        break;
                    default:
                        throw new InvalidOperationException($"不支持的 UI 面板加载来源：{settings.UIPanelLoadSource}");
                }
            }

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

        private string ResolveCustomPath<T>() where T : BasePanel
        {
            if (CustomAssetPathResolver == null)
                throw new InvalidOperationException(
                    "UI 使用自定义资源来源，请先设置 UIManager.Instance.CustomPanelAssetPathResolver，"
                    + "并通过 ResManager.AddProvider 注册对应 Provider。");

            string path = CustomAssetPathResolver(typeof(T))?.Trim();
            if (string.IsNullOrWhiteSpace(path) || path.IndexOf("://", StringComparison.Ordinal) <= 0
                || path.EndsWith("://", StringComparison.Ordinal))
                throw new InvalidOperationException($"自定义 UI 地址无效：{typeof(T).FullName}。请返回带协议的完整资源地址。");
            return path;
        }

        public T Load<T>(string assetPath, Transform parent, bool isPreload) where T : BasePanel
        {
            bool isResources = assetPath.StartsWith("res://", StringComparison.OrdinalIgnoreCase);
            bool isAddressables = assetPath.StartsWith("addr://", StringComparison.OrdinalIgnoreCase)
                || assetPath.StartsWith("addressables://", StringComparison.OrdinalIgnoreCase);
            // 只允许显式开启同步策略的 Addressables Preload 发起阻塞加载。
            // Open 仍只复用已加载实例；这里读取的是框架配置类型，无可选包编译依赖。
            bool allowSyncPreload = isPreload && isAddressables
                && ResBackendSettingsRuntimeLoader.Addressables?.AllowSyncLoad == true;
            if (!isResources && !allowSyncPreload)
                throw new InvalidOperationException(
                    isPreload
                        ? $"同步 Preload<{typeof(T).Name}>() 失败（{assetPath}）："
                          + (isAddressables
                              ? "Addressables 同步加载未获允许，请在资源后端配置中选择 Addressables 并开启 Allow Sync Load。本次未完成预加载。"
                              : "UI 当前不支持该来源的同步首次加载，本次未完成预加载。")
                          + $"请改用 await UIManager.Instance.PreloadAsync<{typeof(T).Name}>()，等待完成且返回面板非 null 后再调用 Open。"
                          + "预加载与打开需使用相同的 Surface 和 InstanceId。"
                        : $"同步 Open<{typeof(T).Name}>() 失败：当前 Surface / InstanceId 下没有已加载的面板实例（{assetPath}）。非 Resources 面板需要先完成预加载。"
                          + $"请直接使用 await UIManager.Instance.OpenAsync<{typeof(T).Name}>()，"
                          + $"或先 await UIManager.Instance.PreloadAsync<{typeof(T).Name}>()，等待完成且返回面板非 null 后再调用 Open。"
                          + (isAddressables ? "开启 Addressables 的 Allow Sync Load 后，也可以先调用同步 Preload<T>()。" : string.Empty)
                          + "预加载与打开需使用相同的 Surface 和 InstanceId。");

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
