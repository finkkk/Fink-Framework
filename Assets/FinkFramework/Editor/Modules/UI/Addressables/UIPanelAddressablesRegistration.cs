using System;
using FinkFramework.Editor.Modules.UI;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;

namespace FinkFramework.Editor.UI.Addressables
{
    /// <summary>只在安装 Addressables 时编译的面板资源注册扩展。</summary>
    public sealed class UIPanelAddressablesRegistration : IUIPanelAddressablesRegistration
    {
        public void Validate(string groupName, string panelName, string prefabPath)
        {
            if (string.IsNullOrWhiteSpace(groupName)
                || groupName.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0)
                throw new InvalidOperationException("请配置有效的 UI Addressables Group 名称。");

            var settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
            if (settings == null)
                return;

            string targetGuid = AssetDatabase.AssetPathToGUID(prefabPath);
            foreach (var group in settings.groups)
            {
                if (group == null)
                    continue;
                foreach (var entry in group.entries)
                {
                    if (entry.address == panelName && entry.guid != targetGuid)
                        throw new InvalidOperationException(
                            $"Addressables Address 已被占用：{panelName}（{entry.AssetPath}）。面板类名必须唯一。");
                }
            }
            var targetGroup = settings.FindGroup(groupName);
            if (targetGroup != null && (targetGroup.ReadOnly
                || !targetGroup.HasSchema<BundledAssetGroupSchema>()
                || !targetGroup.HasSchema<ContentUpdateGroupSchema>()))
                throw new InvalidOperationException(
                    $"UI 资源组 {groupName} 需要可写，并包含 BundledAssetGroupSchema 和 ContentUpdateGroupSchema。");
        }

        public void Register(string groupName, string panelName, string prefabPath)
        {
            Validate(groupName, panelName, prefabPath);
            string guid = AssetDatabase.AssetPathToGUID(prefabPath);
            if (string.IsNullOrEmpty(guid))
                throw new InvalidOperationException($"面板预制体尚未导入：{prefabPath}");

            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            if (settings == null)
                throw new InvalidOperationException("无法创建 Addressables Settings。");
            var group = settings.FindGroup(groupName) ?? settings.CreateGroup(groupName,
                false, false, true, null, typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            var entry = settings.CreateOrMoveEntry(guid, group);
            if (entry == null)
                throw new InvalidOperationException($"无法注册 Addressables 面板：{prefabPath}");
            entry.SetAddress(panelName);
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, entry, true, true);
            AssetDatabase.SaveAssets();
        }
    }
}
