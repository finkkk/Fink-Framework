using System;
using System.Collections.Generic;
using System.Linq;
using FinkFramework.Runtime.Localization;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FinkFramework.Editor.Modules.Localization.UI
{
    /// <summary>
    /// 本地化资源组件的 Key 选择器。
    /// 交互方式与 FinkLocalizedText 保持一致：主分类 → 功能分组 → Key。
    /// </summary>
    [CustomEditor(typeof(FinkLocalizedAsset), true)]
    public sealed class FinkLocalizedAssetEditor : UnityEditor.Editor
    {
        private const string RootGroupLabel = "（未分组）";

        private SerializedProperty keyProperty;
        private readonly List<CategoryOption> categories = new List<CategoryOption>();
        private string lastObservedKey;
        private int selectedCategoryIndex;
        private int selectedGroupIndex;
        private int selectedKeyIndex;
        private bool previewRefreshQueued;
        private bool keyCatalogLoaded;

        private void OnEnable()
        {
            keyProperty = serializedObject.FindProperty("key");
            EditorApplication.projectChanged += RefreshKeyCatalog;
            LocalizationDataSyncUtility.SourceDataChanged += RefreshKeyCatalog;
            RefreshKeyCatalog();
            QueueEditorPreview();
        }

        private void OnDisable()
        {
            EditorApplication.projectChanged -= RefreshKeyCatalog;
            LocalizationDataSyncUtility.SourceDataChanged -= RefreshKeyCatalog;
            EditorApplication.delayCall -= RunQueuedEditorPreview;
            previewRefreshQueued = false;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            if (keyProperty == null)
            {
                EditorGUILayout.HelpBox("本地化资源组件字段加载失败，请重新导入脚本。", MessageType.Error);
                return;
            }

            if (!string.Equals(lastObservedKey, keyProperty.stringValue, StringComparison.Ordinal))
            {
                ClearInvalidKeyBindings();
                serializedObject.Update();
                SyncSelectionToKey();
                lastObservedKey = keyProperty.stringValue;
            }

            EditorGUILayout.LabelField("本地化 Key");
            DrawKeySelectors();

            if (serializedObject.ApplyModifiedProperties())
            {
                lastObservedKey = keyProperty.stringValue;
                foreach (UnityEngine.Object item in targets)
                {
                    if (item is FinkLocalizedAsset localizedAsset)
                        EditorUtility.SetDirty(localizedAsset);
                }

                // 等目标组件的序列化修改提交后再应用预览，避免本帧读取到旧 Key。
                QueueEditorPreview();
            }

            // 资源表保存、导入或 Inspector 重绘后统一在 GUI 事件结束时同步预览。
            QueueEditorPreview();
        }

        private void DrawKeySelectors()
        {
            DrawKeyBindingWarning();

            if (categories.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "没有读取到可用资源 Key。请先在资源本地化表中创建 Key，保存后会自动更新。",
                    MessageType.Info);
                return;
            }

            selectedCategoryIndex = Mathf.Clamp(selectedCategoryIndex, 0, categories.Count - 1);
            bool selectionScopeChanged = false;
            int nextCategoryIndex = EditorGUILayout.Popup(
                "主分类",
                selectedCategoryIndex,
                categories.Select(item => item.Name).ToArray());
            if (nextCategoryIndex != selectedCategoryIndex)
            {
                selectedCategoryIndex = nextCategoryIndex;
                selectedGroupIndex = 0;
                selectedKeyIndex = 0;
                selectionScopeChanged = true;
            }

            CategoryOption category = categories[selectedCategoryIndex];
            string[] groupLabels = category.Keys
                .Select(item => item.Group)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (groupLabels.Length == 0)
            {
                EditorGUILayout.HelpBox("当前分类还没有可用资源 Key。", MessageType.Info);
                return;
            }

            selectedGroupIndex = Mathf.Clamp(selectedGroupIndex, 0, groupLabels.Length - 1);
            int nextGroupIndex = EditorGUILayout.Popup(
                "功能分组",
                selectedGroupIndex,
                groupLabels);
            if (nextGroupIndex != selectedGroupIndex)
            {
                selectedGroupIndex = nextGroupIndex;
                selectedKeyIndex = 0;
                selectionScopeChanged = true;
            }

            string selectedGroup = groupLabels[selectedGroupIndex];
            List<KeyOption> groupKeys = category.Keys
                .Where(item => GroupEquals(item.Group, selectedGroup))
                .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            selectedKeyIndex = Mathf.Clamp(selectedKeyIndex, 0, groupKeys.Count - 1);

            // 仅在用户主动切换分类或功能分组时才同步 Key。
            // 未绑定 Key 的组件必须保持未绑定，以便 Inspector 明确提示配置遗漏，
            // 而不是在首次绘制时静默绑定列表第一项。
            if (groupKeys.Count > 0
                && selectionScopeChanged)
            {
                keyProperty.stringValue = groupKeys[selectedKeyIndex].FullKey;
                lastObservedKey = keyProperty.stringValue;
            }

            // Inspector 重建时分组索引可能短暂恢复为默认值，因此按分类完整 Key 查找，
            // 确保已保存的 Key 不会被错误显示为“选择 Key”。
            string currentKey = keyProperty.stringValue?.Trim() ?? string.Empty;
            KeyOption selectedKey = category.Keys.FirstOrDefault(
                item => KeyEquals(item.FullKey, currentKey));
            string keyLabel = selectedKey?.DisplayName ?? GetFallbackKeyLabel(category.Name, currentKey);
            Rect keyRect = EditorGUILayout.GetControlRect();
            Rect keyFieldRect = EditorGUI.PrefixLabel(keyRect, new GUIContent("Key"));
            if (EditorGUI.DropdownButton(keyFieldRect, new GUIContent(keyLabel), FocusType.Keyboard))
            {
                var options = new List<LocalizedKeyAdvancedDropdown.Option>(groupKeys.Count);
                foreach (KeyOption item in groupKeys)
                    options.Add(new LocalizedKeyAdvancedDropdown.Option(item.FullKey, item.DisplayName));
                var dropdown = new LocalizedKeyAdvancedDropdown(
                    options,
                    option =>
                    {
                        keyProperty.stringValue = option.FullKey;
                        lastObservedKey = keyProperty.stringValue;
                        serializedObject.ApplyModifiedProperties();
                        QueueEditorPreview();
                        Repaint();
                    });
                dropdown.Show(keyFieldRect);
            }

            if (groupKeys.Count > 0)
            {
                // 显示实际序列化字段，避免只显示下拉选中项而掩盖 Key 尚未写入组件的问题。
                EditorGUILayout.LabelField("当前完整 Key", keyProperty.stringValue);
            }
        }

        /// <summary>
        /// 显示未绑定或已从当前资源表移除的 Key，避免旧序列化值被误认为仍然有效。
        /// </summary>
        private void DrawKeyBindingWarning()
        {
            string currentKey = keyProperty?.stringValue?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(currentKey))
            {
                EditorGUILayout.HelpBox(
                    "此本地化资源组件尚未绑定 Key。运行时会清空目标资源。",
                    MessageType.Warning);
                return;
            }

            bool keyExists = categories.Any(category =>
                category.Keys.Any(item => KeyEquals(item.FullKey, currentKey)));
            if (!keyExists)
            {
                EditorGUILayout.HelpBox(
                    $"已绑定的资源 Key“{currentKey}”不存在于当前资源表中。请重新选择有效 Key，或清除该绑定。",
                    MessageType.Error);
            }
        }

        private void RefreshKeyCatalog()
        {
            categories.Clear();
            keyCatalogLoaded = false;

            LocalizationSettingsAsset settings = AssetDatabase.LoadAssetAtPath<LocalizationSettingsAsset>(
                LocalizationPath.SettingsAssetPath);
            LocalizationAssetTable catalog = AssetDatabase.LoadAssetAtPath<LocalizationAssetTable>(
                LocalizationPath.AssetTablePath);
            if (settings == null || catalog?.Entries == null)
            {
                Repaint();
                return;
            }

            keyCatalogLoaded = true;
            foreach (string categoryName in LocalizationEditorDataUtility.GetCategories(settings))
            {
                if (!LocalizationPath.IsSafeCategory(categoryName))
                    continue;

                var category = new CategoryOption(categoryName);
                foreach (LocalizationAssetEntry entry in catalog.Entries)
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.Key))
                        continue;

                    string fullKey = entry.Key.Trim();
                    if (LocalizationKeyUtility.IsFullKeyForCategory(categoryName, fullKey))
                        category.Keys.Add(CreateKeyOption(categoryName, fullKey));
                }

                category.Keys = category.Keys
                    .GroupBy(item => item.FullKey, StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.First())
                    .OrderBy(item => item.FullKey, StringComparer.Ordinal)
                    .ToList();
                categories.Add(category);
            }

            int clearedCount = ClearInvalidKeyBindings();
            serializedObject.Update();
            SyncSelectionToKey();
            lastObservedKey = keyProperty == null ? string.Empty : keyProperty.stringValue;
            if (clearedCount > 0)
                Debug.LogWarning($"[Localization] 已自动清除 {clearedCount} 个失效资源 Key。");
            QueueEditorPreview();
            Repaint();
        }

        /// <summary>
        /// 当前资源表已成功读取时，自动解除已不存在的 Key。
        /// 资源表不可用时不修改组件，避免导入或读取临时失败误清绑定。
        /// </summary>
        private int ClearInvalidKeyBindings()
        {
            if (!keyCatalogLoaded || targets == null)
                return 0;

            int clearedCount = 0;
            foreach (UnityEngine.Object o in targets)
            {
                if (!(o is FinkLocalizedAsset))
                    continue;

                var targetObject = new SerializedObject(o);
                SerializedProperty targetKey = targetObject.FindProperty("key");
                string currentKey = targetKey?.stringValue?.Trim() ?? string.Empty;
                if (string.IsNullOrEmpty(currentKey) || ContainsKey(currentKey))
                    continue;

                if (targetKey != null) targetKey.stringValue = string.Empty;
                targetObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(o);
                PrefabUtility.RecordPrefabInstancePropertyModifications(o);
                clearedCount++;
            }

            return clearedCount;
        }

        /// <summary>
        /// 将预览刷新延迟到当前 Inspector GUI 事件结束后执行。
        /// 资源表保存或导入完成时，Unity 可能仍在提交序列化数据，延迟一帧可避免读到旧引用。
        /// </summary>
        private void QueueEditorPreview()
        {
            if (previewRefreshQueued || Application.isPlaying)
                return;

            previewRefreshQueued = true;
            EditorApplication.delayCall += RunQueuedEditorPreview;
        }

        private void RunQueuedEditorPreview()
        {
            previewRefreshQueued = false;
            if (this == null)
                return;

            ApplyEditorPreview();
            Repaint();
        }

        /// <summary>
        /// 在编辑器中将默认语言资源预览到目标 Image。
        /// 运行时仍由 FinkLocalizedImage.OnEnable 和语言切换事件负责真正刷新。
        /// </summary>
        private void ApplyEditorPreview()
        {
            if (Application.isPlaying || keyProperty == null || targets == null)
                return;

            LocalizationSettingsAsset settings = AssetDatabase.LoadAssetAtPath<LocalizationSettingsAsset>(
                LocalizationPath.SettingsAssetPath);
            LocalizationAssetTable catalog = AssetDatabase.LoadAssetAtPath<LocalizationAssetTable>(
                LocalizationPath.AssetTablePath);
            if (settings == null || catalog == null)
                return;

            bool found = catalog.TryGetAsset(keyProperty.stringValue, settings.DefaultLocale, out var asset);
            if (!found && !string.Equals(
                    settings.DefaultLocale,
                    settings.DefaultFallbackLocale,
                    StringComparison.OrdinalIgnoreCase))
            {
                found = catalog.TryGetAsset(
                    keyProperty.stringValue,
                    settings.DefaultFallbackLocale,
                    out asset);
            }

            // 编辑器预览不能把一次暂时的资源表导入延迟误判成“没有资源”，
            // 否则会把 Image 已有的 Source Image 清空。运行时缺失资源仍由运行时组件负责清空。
            if (!found || asset == null)
                return;

            Sprite sprite = asset as Sprite;
            if (sprite == null)
                return;

            foreach (UnityEngine.Object o in targets)
            {
                if (o is not FinkLocalizedImage localizedImage)
                    continue;

                Image image = localizedImage.GetComponent<Image>();
                if (image == null)
                    continue;

                SerializedObject imageObject = new SerializedObject(image);
                SerializedProperty spriteProperty = imageObject.FindProperty("m_Sprite");
                if (spriteProperty != null)
                {
                    if (spriteProperty.objectReferenceValue == sprite)
                        continue;

                    spriteProperty.objectReferenceValue = sprite;
                    imageObject.ApplyModifiedProperties();
                }
                else
                {
                    if (image.sprite == sprite)
                        continue;

                    image.sprite = sprite;
                }

                EditorUtility.SetDirty(image);
                PrefabUtility.RecordPrefabInstancePropertyModifications(image);
                SceneView.RepaintAll();
                // Inspector 不一定会因为场景对象变脏而主动重绘，显式刷新所有编辑器视图，
                // 让 Image 的 Source Image 字段立即反映刚应用的 Sprite。
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            }
        }

        private void SyncSelectionToKey()
        {
            if (categories.Count == 0 || keyProperty == null)
                return;

            string currentKey = keyProperty.stringValue?.Trim() ?? string.Empty;
            int categoryIndex = categories.FindIndex(
                item => item.Keys.Any(key => KeyEquals(key.FullKey, currentKey)));
            if (categoryIndex < 0)
            {
                selectedCategoryIndex = Mathf.Clamp(selectedCategoryIndex, 0, categories.Count - 1);
                selectedGroupIndex = 0;
                selectedKeyIndex = 0;
                return;
            }

            selectedCategoryIndex = categoryIndex;
            CategoryOption category = categories[categoryIndex];
            KeyOption selected = category.Keys.First(
                item => KeyEquals(item.FullKey, currentKey));
            List<string> groups = category.Keys
                .Select(item => item.Group)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                .ToList();
            selectedGroupIndex = groups.FindIndex(item => GroupEquals(item, selected.Group));
            selectedKeyIndex = category.Keys
                .Where(item => GroupEquals(item.Group, selected.Group))
                .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList()
                .FindIndex(item => KeyEquals(item.FullKey, currentKey));
        }

        private static bool KeyEquals(string left, string right)
        {
            return string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private bool ContainsKey(string key)
        {
            return categories.Any(category =>
                category.Keys.Any(item => KeyEquals(item.FullKey, key)));
        }

        private static bool GroupEquals(string left, string right)
        {
            return string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static string GetFallbackKeyLabel(string category, string fullKey)
        {
            if (string.IsNullOrEmpty(fullKey))
                return "选择 Key";

            string relativeKey = LocalizationKeyUtility.GetCategoryRelativeKey(category, fullKey);
            int separatorIndex = relativeKey.LastIndexOf('.');
            return separatorIndex >= 0
                ? relativeKey.Substring(separatorIndex + 1)
                : relativeKey;
        }

        private static KeyOption CreateKeyOption(string category, string fullKey)
        {
            string[] segments = fullKey.Split('.');
            int startIndex = segments.Length > 0
                && string.Equals(segments[0], category, StringComparison.OrdinalIgnoreCase)
                ? 1
                : 0;

            if (startIndex >= segments.Length)
                return new KeyOption(fullKey, RootGroupLabel, fullKey);

            string group = segments.Length - startIndex > 1
                ? segments[startIndex]
                : RootGroupLabel;
            string displayName = segments.Length - startIndex > 1
                ? string.Join(".", segments.Skip(startIndex + 1))
                : segments[startIndex];
            return new KeyOption(fullKey, group, displayName);
        }

        private sealed class CategoryOption
        {
            public CategoryOption(string name)
            {
                Name = name;
            }

            public string Name { get; }
            public List<KeyOption> Keys { get; set; } = new List<KeyOption>();
        }

        private sealed class KeyOption
        {
            public KeyOption(string fullKey, string group, string displayName)
            {
                FullKey = fullKey;
                Group = group;
                DisplayName = displayName;
            }

            public string FullKey { get; }
            public string Group { get; }
            public string DisplayName { get; }
        }
    }
}
