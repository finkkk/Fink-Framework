using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FinkFramework.Runtime.Localization;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace FinkFramework.Editor.Modules.Localization.UI
{
    /// <summary>
    /// FinkLocalizedText 的 Key 选择器。
    /// </summary>
    [CustomEditor(typeof(FinkLocalizedText))]
    public sealed class FinkLocalizedTextEditor : UnityEditor.Editor
    {
        private const string RootGroupLabel = "（未分组）";

        private SerializedProperty keyProperty;
        private readonly List<CategoryOption> categories = new List<CategoryOption>();
        private readonly List<string> loadErrors = new List<string>();
        private string lastObservedKey;
        private int selectedCategoryIndex;
        private int selectedGroupIndex;
        private int selectedKeyIndex;
        private bool keyCatalogLoaded;

        private void OnEnable()
        {
            keyProperty = serializedObject.FindProperty("key");
            EditorApplication.projectChanged += RefreshKeyCatalog;
            LocalizationDataSyncUtility.SourceDataChanged += RefreshKeyCatalog;
            RefreshKeyCatalog();
        }

        private void OnDisable()
        {
            EditorApplication.projectChanged -= RefreshKeyCatalog;
            LocalizationDataSyncUtility.SourceDataChanged -= RefreshKeyCatalog;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            if (keyProperty == null)
            {
                EditorGUILayout.HelpBox("本地化组件字段加载失败，请重新导入脚本。", MessageType.Error);
                return;
            }

            if (!string.Equals(lastObservedKey, keyProperty.stringValue, StringComparison.Ordinal))
            {
                SyncSelectionToKey();
                lastObservedKey = keyProperty.stringValue;
            }

            EditorGUILayout.LabelField("本地化 Key");
            if (loadErrors.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    $"有 {loadErrors.Count} 个语言表文件读取失败，相关 Key 未加入下拉列表。\n" +
                    string.Join("\n", loadErrors.Take(4)),
                    MessageType.Warning);
            }

            DrawKeySelectors();

            bool keyChanged = serializedObject.ApplyModifiedProperties();
            if (keyChanged)
            {
                lastObservedKey = keyProperty.stringValue;
                foreach (UnityEngine.Object item in targets)
                {
                    if (item is FinkLocalizedText localizedText)
                        EditorUtility.SetDirty(localizedText);
                }
            }
        }

        private void DrawKeySelectors()
        {
            DrawKeyBindingWarning();

            if (categories.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "没有读取到可用 Key。请先在本地化语言表中创建 Key；若语言表刚被删除或修改，请保存语言表并同步运行时副本后再查看。",
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
            List<KeyOption> visibleKeys = category.Keys;
            string[] groupLabels = visibleKeys
                .Select(item => item.Group)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (groupLabels.Length == 0)
            {
                EditorGUILayout.HelpBox("当前分类还没有可用 Key。", MessageType.Info);
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
            List<KeyOption> groupKeys = visibleKeys
                .Where(item => GroupEquals(item.Group, selectedGroup))
                .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            selectedKeyIndex = Mathf.Clamp(selectedKeyIndex, 0, groupKeys.Count - 1);

            // 切换分类或功能分组后，Key 必须同步落在新的分组内，避免界面分组与实际 Key 不一致。
            // 未绑定 Key 时不自动选择第一项，保留为空并提示配置遗漏。
            if (groupKeys.Count > 0
                && selectionScopeChanged)
            {
                keyProperty.stringValue = groupKeys[selectedKeyIndex].FullKey;
                lastObservedKey = keyProperty.stringValue;
            }

            // 按分类的完整 Key 查找，而不是只在当前分组中查找。
            // Inspector 重建时分组索引可能短暂恢复为默认值，但序列化的 Key 仍然有效，
            // 此时也必须显示真实的 Key，不能误显示为“选择 Key”。
            string currentKey = keyProperty.stringValue?.Trim() ?? string.Empty;
            KeyOption selectedKey = visibleKeys.FirstOrDefault(
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
                        Repaint();
                    });
                dropdown.Show(keyFieldRect);
            }

            if (groupKeys.Count > 0)
            {
                EditorGUILayout.LabelField(
                    "当前完整 Key",
                    keyProperty.stringValue);
            }
        }

        /// <summary>
        /// 显示未绑定或已从当前语言表移除的 Key，避免旧序列化值被误认为仍然有效。
        /// </summary>
        private void DrawKeyBindingWarning()
        {
            string currentKey = keyProperty?.stringValue?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(currentKey))
            {
                EditorGUILayout.HelpBox(
                    "此本地化文本组件尚未绑定 Key。运行时将使用空文本。",
                    MessageType.Warning);
                return;
            }

            bool keyExists = categories.Any(category =>
                category.Keys.Any(item => KeyEquals(item.FullKey, currentKey)));
            if (!keyExists)
            {
                EditorGUILayout.HelpBox(
                    $"已绑定的文本 Key“{currentKey}”不存在于当前语言表中。请重新选择有效 Key，或清除该绑定。",
                    MessageType.Error);
            }
        }

        private void RefreshKeyCatalog()
        {
            categories.Clear();
            loadErrors.Clear();
            keyCatalogLoaded = false;

            LocalizationSettingsAsset settings = AssetDatabase.LoadAssetAtPath<LocalizationSettingsAsset>(
                LocalizationPath.SettingsAssetPath);
            if (settings == null)
            {
                lastObservedKey = keyProperty == null ? string.Empty : keyProperty.stringValue;
                Repaint();
                return;
            }

            if (settings.SupportedLocales == null)
            {
                Repaint();
                return;
            }

            keyCatalogLoaded = true;
            string dataRoot = LocalizationPath.GetSourceDataRoot();
            IReadOnlyList<string> categoryNames = LocalizationEditorDataUtility.GetCategories(settings);
            foreach (string categoryName in categoryNames)
            {
                if (!LocalizationPath.IsSafeCategory(categoryName))
                    continue;

                var keySet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (LocaleInfo locale in settings.SupportedLocales)
                {
                    if (locale == null || !LocaleInfo.TryNormalize(locale.Code, out string normalizedLocale))
                        continue;

                    string filePath = LocalizationPath.GetLanguageFilePath(
                        dataRoot,
                        categoryName,
                        normalizedLocale,
                        settings.JsonFileNamePattern);
                    if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                        continue;

                    try
                    {
                        JObject document = JObject.Parse(
                            File.ReadAllText(filePath, Encoding.UTF8).TrimStart('\uFEFF'),
                            new JsonLoadSettings
                            {
                                DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error
                            });
                        foreach (JProperty property in document.Properties())
                        {
                            if (!string.IsNullOrWhiteSpace(property.Name)
                                && property.Value.Type == JTokenType.String)
                            {
                                string fullKey = LocalizationKeyUtility.BuildFullKey(
                                    categoryName,
                                    property.Name);
                                if (!string.IsNullOrWhiteSpace(fullKey))
                                    keySet.Add(fullKey);
                            }
                        }
                    }
                    catch (Exception exception)
                    {
                        loadErrors.Add($"{Path.GetFileName(filePath)}：{exception.Message}");
                    }
                }

                var category = new CategoryOption(categoryName);
                foreach (string fullKey in keySet.OrderBy(item => item, StringComparer.Ordinal))
                    category.Keys.Add(CreateKeyOption(categoryName, fullKey));
                categories.Add(category);
            }

            // 任一语言表解析失败时目录并不完整，保留旧绑定以免读取故障误触发清除。
            keyCatalogLoaded = loadErrors.Count == 0;
            int clearedCount = ClearInvalidKeyBindings();
            serializedObject.Update();
            SyncSelectionToKey();
            lastObservedKey = keyProperty == null ? string.Empty : keyProperty.stringValue;
            if (clearedCount > 0)
                Debug.LogWarning($"[Localization] 已自动清除 {clearedCount} 个失效文本 Key。");
            Repaint();
        }

        /// <summary>
        /// 当前语言表目录已读取时，自动解除已不存在的 Key。
        /// 配置不可用时不修改组件，避免读取过程中的临时状态误清绑定。
        /// </summary>
        private int ClearInvalidKeyBindings()
        {
            if (!keyCatalogLoaded || targets == null)
                return 0;

            int clearedCount = 0;
            foreach (UnityEngine.Object o in targets)
            {
                if (!(o is FinkLocalizedText))
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
            public List<KeyOption> Keys { get; } = new List<KeyOption>();
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
