using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using FinkFramework.Editor.Common;
using FinkFramework.Runtime.Localization;
using UnityEditor;
using UnityEngine;
#if ENABLE_TEXTMESHPRO
using TMPro;
#endif

namespace FinkFramework.Editor.Modules.Localization.UI
{
    /// <summary>
    /// 本地化表的资源模式：负责 Unity 资源引用的多语言配置。
    /// </summary>
    public sealed partial class LocalizationWindow
    {
        private enum LocalizationTableMode
        {
            Text,
            Asset
        }

        private const float AssetTypeColumnWidth = 125f;
        private const float AssetSaveButtonWidth = 160f;
        private static readonly Regex AssetFullKeyPattern = new(@"^[a-z0-9_]+(?:\.[a-z0-9_]+)+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        // 表模式和资源数据属于窗口专属状态；筛选条件则统一放在主文件中共享。
        private LocalizationTableMode tableMode;
        private LocalizationAssetTable assetTable;
        private Vector2 assetTableScrollPosition;
        private string assetNewKeyDraft = string.Empty;
        private bool assetHasUnsavedChanges;
        private string assetValidationMessage;

        #region 表模式切换

        private void DrawTableModeSelector()
        {
            EditorGUILayout.LabelField(
                "表类型",
                GUILayout.Width(48f));
            int nextMode = EditorGUILayout.Popup(
                (int)tableMode,
                new[] { "语言表", "资源表" },
                GUILayout.Width(92f));
            if (nextMode == (int)tableMode)
                return;

            bool canSwitch = tableMode == LocalizationTableMode.Asset
                ? ConfirmAssetDiscardChanges()
                : ConfirmDiscardChanges();
            if (!canSwitch)
                return;

            tableMode = (LocalizationTableMode)nextMode;
            if (tableMode == LocalizationTableMode.Asset)
                ReloadAssetMode();
            else
                ReloadSettings();
        }

        #endregion

        #region 资源表界面

        /// <summary>
        /// 绘制资源引用表模式；资源引用保存在 LocalizationAssetTable 资产中。
        /// </summary>
        private void DrawAssetMode()
        {
            if (assetTable == null)
            {
                EditorGUILayout.HelpBox(
                    "资源本地化表资产不存在，无法打开资源表模式。",
                    MessageType.Error);
                if (GUILayout.Button("重新加载资源表"))
                    ReloadAssetMode();
                return;
            }

            DrawAssetToolbar();
            DrawAssetEditActions();
            GUILayout.Space(6f);

            if (!string.IsNullOrEmpty(assetValidationMessage))
            {
                EditorGUILayout.HelpBox(assetValidationMessage, MessageType.Error);
                assetValidationMessage = null;
            }

            DrawAssetTable();
        }

        private void DrawAssetToolbar()
        {
            EditorGUILayout.BeginHorizontal(
                EditorStyles.toolbar,
                GUILayout.Height(FFEditorStyles.ToolbarHeight));

            DrawTableModeSelector();
            GUILayout.Space(FFEditorStyles.ControlSpacing);

            string[] categoryLabels = GetAssetCategoryLabels();
            selectedCategoryIndex = Mathf.Clamp(
                selectedCategoryIndex,
                0,
                categoryLabels.Length - 1);
            EditorGUILayout.LabelField(
                "分类",
                GUILayout.Width(42f));
            int nextCategoryIndex = EditorGUILayout.Popup(
                selectedCategoryIndex,
                categoryLabels,
                GUILayout.Width(180f));
            if (nextCategoryIndex != selectedCategoryIndex)
            {
                selectedCategoryIndex = nextCategoryIndex;
                Repaint();
            }

            GUILayout.Space(FFEditorStyles.ControlSpacing);
            EditorGUILayout.LabelField("搜索", GUILayout.Width(35f));
            searchText = EditorGUILayout.TextField(
                searchText,
                EditorStyles.toolbarTextField,
                GUILayout.Width(Mathf.Clamp(position.width * 0.18f, 150f, 220f)));
            showMissingOnly = GUILayout.Toggle(
                showMissingOnly,
                "仅看缺失",
                EditorStyles.toolbarButton,
                GUILayout.Width(78f));

            if (GUILayout.Button("语言列", EditorStyles.toolbarButton, GUILayout.Width(60f)))
                ShowAssetLocaleColumnMenu();

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("重新读取", EditorStyles.toolbarButton, GUILayout.Width(80f)))
            {
                if (ConfirmAssetDiscardChanges())
                    ReloadAssetMode();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawAssetEditActions()
        {
            EditorGUILayout.BeginHorizontal(
                GetTableRowStyle(),
                GUILayout.Height(EditActionRowHeight));

            Rect rowRect = GUILayoutUtility.GetRect(
                0f,
                EditActionRowHeight,
                GUILayout.ExpandWidth(true));
            float controlY = rowRect.y
                + (rowRect.height - EditActionControlHeight) * 0.5f;
            float x = rowRect.x + 8f;
            float gap = FFEditorStyles.ControlSpacing;
            float labelWidth = 64f;
            float addButtonWidth = 58f;
            float saveX = rowRect.xMax - AssetSaveButtonWidth - 8f;
            float maxKeyWidth = saveX - gap - addButtonWidth - 2f - x;
            float keyWidth = Mathf.Clamp(
                maxKeyWidth,
                180f,
                MaxNewKeyFieldWidth);

            GUI.Label(
                new Rect(x, controlY, labelWidth, EditActionControlHeight),
                "新增 Key",
                EditorStyles.miniLabel);
            x += labelWidth + gap;

            Rect keyRect = new Rect(x, controlY, keyWidth, EditActionControlHeight);
            assetNewKeyDraft = EditorGUI.TextField(
                keyRect,
                assetNewKeyDraft,
                EditorStyles.miniTextField);
            x = keyRect.xMax + 2f;

            Rect addButtonRect = new Rect(
                x,
                controlY,
                addButtonWidth,
                EditActionControlHeight);
            if (GUI.Button(addButtonRect, "新增", EditorStyles.miniButton))
                AddAssetEntryFromDraft();

            Rect saveButtonRect = new Rect(
                saveX,
                controlY,
                AssetSaveButtonWidth,
                EditActionControlHeight);
            if (GUI.Button(
                    saveButtonRect,
                    "保存资源表",
                    EditorStyles.miniButton))
                SaveAssetTable();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawAssetTable()
        {
            if (locales.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "当前没有有效的支持语言，请检查本地化配置中的固定语言列表。",
                    MessageType.Warning);
                return;
            }

            float tableWidth = GetAssetTableWidth();
            assetTableScrollPosition = EditorGUILayout.BeginScrollView(
                assetTableScrollPosition,
                false,
                false,
                GUILayout.ExpandHeight(true));

            EditorGUILayout.BeginHorizontal(
                GetTableRowStyle(),
                GUILayout.Width(tableWidth),
                GUILayout.Height(TableRowHeight));
            DrawAssetHeaderCell("Key", KeyColumnWidth);
            DrawAssetHeaderCell("类型", AssetTypeColumnWidth);
            foreach (LocaleInfo locale in locales)
            {
                if (!IsAssetLocaleColumnVisible(locale))
                    continue;

                string label = LocaleCatalog.GetEditorLabel(locale);
                DrawAssetHeaderCell(label, LocaleColumnWidth);
            }
            DrawAssetHeaderCell("操作", OperationColumnWidth);
            EditorGUILayout.EndHorizontal();

            int visibleCount = 0;
            IReadOnlyList<LocalizationAssetEntry> entries = assetTable.Entries;
            if (entries != null)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    LocalizationAssetEntry entry = entries[i];
                    if (!IsAssetEntryVisible(entry))
                        continue;

                    visibleCount++;
                    if (DrawAssetEntry(entry, tableWidth))
                        // 删除当前条目后列表会前移，回退索引避免跳过下一条。
                        i--;
                }
            }

            if (visibleCount == 0)
            {
                EditorGUILayout.HelpBox(
                    showMissingOnly
                        ? "当前筛选条件下没有缺失资源。"
                        : "没有匹配当前搜索条件的资源 Key。",
                    MessageType.Info);
            }

            EditorGUILayout.EndScrollView();

            string status =
                $"当前分类：{(string.IsNullOrEmpty(GetSelectedAssetCategory()) ? "全部" : GetSelectedAssetCategory())}    " +
                $"显示：{visibleCount}/{entries?.Count ?? 0}    语言：{locales.Count}";
            int missingCount = CountAssetMissingCells();
            if (missingCount > 0)
                status += $"    缺失资源：{missingCount}";
            if (assetHasUnsavedChanges)
                status += "    * 有未保存修改";
            EditorGUILayout.LabelField(status, EditorStyles.centeredGreyMiniLabel);
        }

        private bool DrawAssetEntry(
            LocalizationAssetEntry entry,
            float tableWidth)
        {
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.BeginHorizontal(
                GetTableRowStyle(),
                GUILayout.Width(tableWidth),
                GUILayout.Height(TableRowHeight));

            Rect keyCellRect = GetTableCellRect(KeyColumnWidth);
            DrawTableCellFrame(keyCellRect);
            string category = GetSelectedAssetCategory();
            string displayedKey = LocalizationKeyUtility.GetCategoryRelativeKey(
                category,
                entry.Key);
            string editedKey = EditorGUI.DelayedTextField(
                GetTableCellContentRect(keyCellRect),
                displayedKey,
                EditorStyles.textField);
            if (!string.Equals(editedKey, displayedKey, StringComparison.Ordinal))
                entry.SetKey(LocalizationKeyUtility.BuildFullKey(category, editedKey));

            Rect typeCellRect = GetTableCellRect(AssetTypeColumnWidth);
            DrawTableCellFrame(typeCellRect);
            LocalizationAssetType editedType = (LocalizationAssetType)EditorGUI.EnumPopup(
                GetTableCellContentRect(typeCellRect),
                entry.AssetType);
            if (editedType != entry.AssetType)
                entry.SetAssetType(editedType);

            foreach (LocaleInfo locale in locales)
            {
                if (!IsAssetLocaleColumnVisible(locale))
                    continue;

                LocalizationAssetLocaleValue value = FindAssetLocaleValue(entry, locale.Code);
                Rect assetCellRect = GetTableCellRect(LocaleColumnWidth);
                DrawTableCellFrame(assetCellRect);
                Color previousBackgroundColor = GUI.backgroundColor;
                if (value?.Asset == null)
                    GUI.backgroundColor = new Color(1f, 0.9f, 0.6f);

                Rect objectFieldRect = GetTableCellContentRect(assetCellRect);
                objectFieldRect.height = Mathf.Min(
                    objectFieldRect.height,
                    EditorGUIUtility.singleLineHeight);
                objectFieldRect.y = assetCellRect.y
                    + (assetCellRect.height - objectFieldRect.height) * 0.5f;
                UnityEngine.Object asset = EditorGUI.ObjectField(
                    objectFieldRect,
                    value?.Asset,
                    GetAssetFieldType(entry.AssetType),
                    false);
                if (asset != value?.Asset)
                {
                    value ??= entry.GetOrCreateLocaleValue(locale.Code);
                    value.SetAsset(asset);
                }
                GUI.backgroundColor = previousBackgroundColor;
            }

            Rect operationCellRect = GetTableCellRect(OperationColumnWidth);
            DrawTableCellFrame(operationCellRect);
            Rect deleteButtonRect = new Rect(
                operationCellRect.x + DeleteButtonHorizontalInset,
                operationCellRect.y + DeleteButtonVerticalInset,
                Mathf.Max(0f, operationCellRect.width - DeleteButtonHorizontalInset * 2f),
                DeleteButtonHeight);
            bool delete = GUI.Button(deleteButtonRect, "删除", EditorStyles.miniButton);
            EditorGUILayout.EndHorizontal();

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(assetTable);
                assetHasUnsavedChanges = true;
            }

            bool removed = delete && EditorUtility.DisplayDialog(
                    "删除资源条目",
                    $"确定删除资源 Key：{entry.Key}",
                    "删除",
                    "取消");
            if (removed)
            {
                assetTable.RemoveEntry(entry);
                EditorUtility.SetDirty(assetTable);
                assetHasUnsavedChanges = true;
                return true;
            }

            string error = GetAssetEntryValidationError(entry);
            if (!string.IsNullOrEmpty(error))
            {
                EditorGUILayout.BeginHorizontal(GUILayout.Width(tableWidth));
                EditorGUILayout.HelpBox(error, MessageType.Error);
                EditorGUILayout.EndHorizontal();
            }

            return false;
        }

        private void DrawAssetHeaderCell(string label, float width)
        {
            Rect cellRect = GetTableCellRect(width);
            DrawTableCellFrame(cellRect);
            GUI.Label(cellRect, label, GetLocaleHeaderStyle());
        }

        #endregion

        #region 资源筛选与语言列

        private string[] GetAssetCategoryLabels()
        {
            string[] categories = LocalizationEditorDataUtility.GetCategories(settings)
                .Where(category => !string.IsNullOrWhiteSpace(category))
                .Select(category => category.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return categories.Length == 0 ? new[] { "无分类" } : categories;
        }

        private string GetSelectedAssetCategory()
        {
            string[] categories = GetAssetCategoryLabels();
            if (categories.Length == 0 || string.Equals(categories[0], "无分类", StringComparison.Ordinal))
                return string.Empty;

            int index = Mathf.Clamp(selectedCategoryIndex, 0, categories.Length - 1);
            return categories[index];
        }

        private bool IsAssetEntryVisible(LocalizationAssetEntry entry)
        {
            if (entry == null)
                return false;

            string category = GetSelectedAssetCategory();
            if (!string.IsNullOrEmpty(category)
                && !LocalizationKeyUtility.IsFullKeyForCategory(category, entry.Key))
                return false;

            return (string.IsNullOrWhiteSpace(searchText)
                    || (entry.Key != null
                        && entry.Key.IndexOf(
                            searchText.Trim(),
                            StringComparison.OrdinalIgnoreCase) >= 0))
                && (!showMissingOnly || HasMissingAsset(entry));
        }

        private bool IsAssetLocaleColumnVisible(LocaleInfo locale)
        {
            return locale != null && !hiddenLocaleCodes.Contains(locale.Code);
        }

        private int GetVisibleAssetLocaleCount()
        {
            return locales.Count(IsAssetLocaleColumnVisible);
        }

        private float GetAssetTableWidth()
        {
            return KeyColumnWidth
                + AssetTypeColumnWidth
                + GetVisibleAssetLocaleCount() * LocaleColumnWidth
                + OperationColumnWidth;
        }

        private void ShowAssetLocaleColumnMenu()
        {
            var menu = new GenericMenu();
            foreach (LocaleInfo locale in locales)
            {
                bool isVisible = IsAssetLocaleColumnVisible(locale);
                string label = LocaleCatalog.GetEditorLabel(locale);
                menu.AddItem(
                    new GUIContent(label),
                    isVisible,
                    () =>
                    {
                        if (isVisible)
                        {
                            if (GetVisibleAssetLocaleCount() <= 1)
                            {
                                EditorUtility.DisplayDialog(
                                    "语言列",
                                    "至少需要保留一列语言，不能隐藏全部语言列。",
                                    "确定");
                                return;
                            }

                            hiddenLocaleCodes.Add(locale.Code);
                        }
                        else
                        {
                            hiddenLocaleCodes.Remove(locale.Code);
                        }

                        Repaint();
                    });
            }

            if (locales.Count == 0)
                menu.AddDisabledItem(new GUIContent("没有配置支持语言"));
            menu.ShowAsContext();
        }

        private bool HasMissingAsset(LocalizationAssetEntry entry)
        {
            foreach (LocaleInfo locale in locales)
            {
                if (GetAssetValue(entry, locale.Code) == null)
                    return true;
            }

            return false;
        }

        private int CountAssetMissingCells()
        {
            int count = 0;
            foreach (LocalizationAssetEntry entry in assetTable?.Entries ?? Array.Empty<LocalizationAssetEntry>())
            {
                if (entry == null)
                    continue;

                foreach (LocaleInfo locale in locales)
                {
                    if (GetAssetValue(entry, locale.Code) == null)
                        count++;
                }
            }

            return count;
        }

        private static UnityEngine.Object GetAssetValue(
            LocalizationAssetEntry entry,
            string localeCode)
        {
            return FindAssetLocaleValue(entry, localeCode)?.Asset;
        }

        private static LocalizationAssetLocaleValue FindAssetLocaleValue(
            LocalizationAssetEntry entry,
            string localeCode)
        {
            foreach (LocalizationAssetLocaleValue value in entry?.LocaleValues
                     ?? Array.Empty<LocalizationAssetLocaleValue>())
            {
                if (value != null && AreLocaleCodesEqual(value.LocaleCode, localeCode))
                    return value;
            }

            return null;
        }

        private static bool AreLocaleCodesEqual(string left, string right)
        {
            if (LocaleInfo.TryNormalize(left, out string normalizedLeft)
                && LocaleInfo.TryNormalize(right, out string normalizedRight))
                return string.Equals(
                    normalizedLeft,
                    normalizedRight,
                    StringComparison.OrdinalIgnoreCase);

            return string.Equals(
                left?.Trim(),
                right?.Trim(),
                StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region 资源编辑、校验与保存

        /// <summary>
        /// 在当前分类下新增资源 Key，默认资源类型为 Sprite。
        /// </summary>
        private void AddAssetEntryFromDraft()
        {
            string category = GetSelectedAssetCategory();
            string relativeKey = assetNewKeyDraft?.Trim();
            if (string.IsNullOrEmpty(category))
            {
                assetValidationMessage = "请先在本地化配置中添加主分类。";
                return;
            }

            string fullKey = LocalizationKeyUtility.BuildFullKey(category, relativeKey);
            if (string.IsNullOrWhiteSpace(relativeKey)
                || string.IsNullOrWhiteSpace(fullKey)
                || !AssetFullKeyPattern.IsMatch(fullKey))
            {
                assetValidationMessage =
                    "请输入小写英文、数字、下划线和点号组成的 Key，例如 logo.main。";
                return;
            }

            if (assetTable.FindEntry(fullKey) != null)
            {
                assetValidationMessage = $"资源 Key 已存在：{fullKey}";
                return;
            }

            LocalizationAssetEntry entry = assetTable.AddEntry();
            entry.SetKey(fullKey);
            entry.SetAssetType(LocalizationAssetType.Sprite);
            foreach (LocaleInfo locale in locales)
                entry.GetOrCreateLocaleValue(locale.Code);

            EditorUtility.SetDirty(assetTable);
            assetHasUnsavedChanges = true;
            assetNewKeyDraft = string.Empty;
            assetValidationMessage = null;
        }

        /// <summary>
        /// 校验资源类型和 Key 后保存资源本地化资产。
        /// </summary>
        private void SaveAssetTable()
        {
            if (!ValidateAssetTable(out string message))
            {
                assetValidationMessage = message;
                return;
            }

            EditorUtility.SetDirty(assetTable);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            // 资源表不是 JSON，同步工具不会覆盖它；保存成功后主动通知 Inspector 重新读取 Key。
            LocalizationDataSyncUtility.NotifySourceDataChanged();
            assetHasUnsavedChanges = false;
            ShowNotification(new GUIContent("本地化资源表已保存"));
        }

        private bool ValidateAssetTable(out string message)
        {
            var errors = new List<string>();
            var hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var entries = assetTable?.Entries;
            if (entries != null)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    LocalizationAssetEntry entry = entries[i];
                    if (entry == null)
                    {
                        errors.Add($"第 {i + 1} 行资源条目为空。");
                        continue;
                    }

                    string key = entry.Key?.Trim();
                    if (string.IsNullOrEmpty(key))
                        errors.Add($"第 {i + 1} 行 Key 不能为空。");
                    else if (!AssetFullKeyPattern.IsMatch(key))
                        errors.Add($"第 {i + 1} 行 Key 不符合完整 Key 规则：{key}");
                    else if (!hashSet.Add(key))
                        errors.Add($"发现重复资源 Key：{key}");

                    string entryError = GetAssetEntryValidationError(entry);
                    if (!string.IsNullOrEmpty(entryError))
                        errors.Add($"第 {i + 1} 行：{entryError}");
                }
            }

            message = errors.Count == 0
                ? null
                : "资源本地化表保存失败：\n- " + string.Join("\n- ", errors);
            return errors.Count == 0;
        }

        private string GetAssetEntryValidationError(LocalizationAssetEntry entry)
        {
            foreach (LocalizationAssetLocaleValue value in entry?.LocaleValues
                     ?? Array.Empty<LocalizationAssetLocaleValue>())
            {
                if (value == null || value.Asset == null)
                    continue;

                if (entry != null)
                {
                    string error = GetAssetValidationError(entry.AssetType, value.Asset);
                    if (!string.IsNullOrEmpty(error))
                        return $"语言 {value.LocaleCode} 的资源类型不匹配：{error}";
                }
            }

            return null;
        }

        private static string GetAssetValidationError(
            LocalizationAssetType assetType,
            UnityEngine.Object asset)
        {
            switch (assetType)
            {
                case LocalizationAssetType.Sprite:
                    return asset is Sprite ? null : "需要 Sprite。";
                case LocalizationAssetType.AudioClip:
                    return asset is AudioClip ? null : "需要 AudioClip。";
                case LocalizationAssetType.Font:
                    return asset is Font ? null : "需要 Font。";
                case LocalizationAssetType.Prefab:
                    if (!(asset is GameObject))
                        return "需要 Prefab 资源。";
                    return PrefabUtility.GetPrefabAssetType(asset) == PrefabAssetType.NotAPrefab
                        ? "需要项目中的 Prefab 资产，不能使用场景对象。"
                        : null;
                case LocalizationAssetType.ScriptableObject:
                    return asset is ScriptableObject ? null : "需要 ScriptableObject。";
                case LocalizationAssetType.TMPFontAsset:
#if ENABLE_TEXTMESHPRO
                    return asset is TMP_FontAsset ? null : "需要 TMP Font Asset。";
#else
                    return "当前项目未安装 TextMeshPro，无法使用 TMP Font Asset。";
#endif
                case LocalizationAssetType.Other:
                default:
                    return null;
            }
        }

        private static Type GetAssetFieldType(LocalizationAssetType assetType)
        {
            switch (assetType)
            {
                case LocalizationAssetType.Sprite:
                    return typeof(Sprite);
                case LocalizationAssetType.AudioClip:
                    return typeof(AudioClip);
                case LocalizationAssetType.Font:
                    return typeof(Font);
                case LocalizationAssetType.Prefab:
                    return typeof(GameObject);
                case LocalizationAssetType.ScriptableObject:
                    return typeof(ScriptableObject);
                case LocalizationAssetType.TMPFontAsset:
#if ENABLE_TEXTMESHPRO
                    return typeof(TMP_FontAsset);
#else
                    return typeof(UnityEngine.Object);
#endif
                case LocalizationAssetType.Other:
                default:
                    return typeof(UnityEngine.Object);
            }
        }

        #endregion

        #region 资源表加载

        /// <summary>
        /// 重新读取资源目录、配置语言和资源目录资产，不在打开界面时静默修改资产。
        /// </summary>
        private void ReloadAssetMode()
        {
            settings = LocalizationSettingsEditorLoader.LoadOrCreate();
            assetTable = LocalizationAssetTableEditorLoader.LoadOrCreate();

            locales.Clear();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (LocaleInfo locale in settings?.SupportedLocales ?? Array.Empty<LocaleInfo>())
            {
                if (locale == null || !LocaleInfo.TryNormalize(locale.Code, out string code))
                    continue;
                if (seen.Add(code))
                    locales.Add(new LocaleInfo(code, locale.DisplayName));
            }

            string[] categories = GetAssetCategoryLabels();
            selectedCategoryIndex = Mathf.Clamp(
                selectedCategoryIndex,
                0,
                categories.Length - 1);
            hiddenLocaleCodes.RemoveWhere(code =>
                !locales.Any(locale => string.Equals(locale.Code, code, StringComparison.OrdinalIgnoreCase)));
            if (locales.Count > 0 && GetVisibleAssetLocaleCount() == 0)
                hiddenLocaleCodes.Clear();
            assetHasUnsavedChanges = false;
            assetValidationMessage = null;
            Repaint();
        }

        private bool ConfirmAssetDiscardChanges()
        {
            if (!assetHasUnsavedChanges)
                return true;

            return EditorUtility.DisplayDialog(
                "存在未保存修改",
                "重新读取或切换表类型会丢失当前资源表修改，是否继续？",
                "放弃修改",
                "取消");
        }

        #endregion

    }
}
