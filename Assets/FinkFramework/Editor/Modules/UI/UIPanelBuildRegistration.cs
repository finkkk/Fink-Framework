using System;
using FinkFramework.Runtime.Environments;
using FinkFramework.Runtime.Settings.ScriptableObjects;
using FinkFramework.Runtime.UI;
using UnityEditor;
using UnityEngine;

namespace FinkFramework.Editor.Modules.UI
{
    /// <summary>由可选 Addressables Editor 程序集实现，核心编辑器不引用包类型。</summary>
    public interface IUIPanelAddressablesRegistration
    {
        void Validate(string groupName, string panelName, string prefabPath);
        void Register(string groupName, string panelName, string prefabPath);
    }

    /// <summary>跨脚本编译保存创建时的来源与注册参数，避免配置变化造成错配。</summary>
    [Serializable]
    internal sealed class UIPanelBuildRegistration
    {
        public EnvironmentState.ResourceBackendType Backend;
        public string Group;
        public string Bundle;

        public static UIPanelBuildRegistration Capture(GlobalSettingsAsset settings) => new()
        {
            Backend = settings.UIPanelLoadSource == UIPanelLoadSource.Custom
                ? EnvironmentState.ResourceBackendType.Custom
                : settings.UIPanelUsesResources ? EnvironmentState.ResourceBackendType.None : settings.ResourceBackend,
            Group = settings.UIPanelAddressablesGroup?.Trim(),
            Bundle = settings.UIPanelAssetBundleName?.Trim()
        };

        private static IUIPanelAddressablesRegistration GetAddressablesExtension()
        {
            Type type = Type.GetType(
                "FinkFramework.Editor.UI.Addressables.UIPanelAddressablesRegistration, FinkFramework.Editor.UI.Addressables");
            if (type == null || !typeof(IUIPanelAddressablesRegistration).IsAssignableFrom(type))
                throw new InvalidOperationException(
                    "Addressables 面板生成扩展不可用。请安装 com.unity.addressables，"
                    + "并确认 FinkFramework.Editor.UI.Addressables 程序集没有编译错误。");
            return (IUIPanelAddressablesRegistration)Activator.CreateInstance(type);
        }

        public void Validate(string panelName, string prefabPath)
        {
            switch (Backend)
            {
                case EnvironmentState.ResourceBackendType.Addressables:
                    GetAddressablesExtension().Validate(Group, panelName, prefabPath);
                    break;
                case EnvironmentState.ResourceBackendType.AssetBundle:
                    if (string.IsNullOrWhiteSpace(Bundle) || Bundle.Contains("/") || Bundle.Contains("\\")
                        || Bundle.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0
                        || Bundle != Bundle.ToLowerInvariant() || Bundle == "." || Bundle == "..")
                        throw new InvalidOperationException("UI AssetBundle 包名必须为小写且不能包含路径分隔符或非法字符。");
                    break;
            }
        }

        public void Register(string panelName, string prefabPath)
        {
            Validate(panelName, prefabPath);
            switch (Backend)
            {
                case EnvironmentState.ResourceBackendType.Addressables:
                    GetAddressablesExtension().Register(Group, panelName, prefabPath);
                    break;
                case EnvironmentState.ResourceBackendType.AssetBundle:
                    AssetImporter importer = AssetImporter.GetAtPath(prefabPath);
                    if (importer == null)
                        throw new InvalidOperationException($"无法读取面板的 AssetImporter：{prefabPath}");
                    importer.SetAssetBundleNameAndVariant(Bundle, string.Empty);
                    importer.SaveAndReimport();
                    break;
            }
        }
    }
}
