using System;
using System.Collections.Generic;
using System.Linq;
using FinkFramework.Editor.Common;
using FinkFramework.Runtime.Localization;
using UnityEditor;
using UnityEngine;

namespace FinkFramework.Editor.Modules.Localization.UI
{
    /// <summary>
    /// 本地化语言表模式：负责筛选栏、编辑行和语言表格布局。
    /// </summary>
    public sealed partial class LocalizationWindow
    {
        #region 语言表界面与表格布局
        
        /// <summary>
        /// 绘制语言表的筛选工具栏。筛选状态与资源表模式共享。
        /// </summary>
        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(
                EditorStyles.toolbar,
                GUILayout.Height(FFEditorStyles.ToolbarHeight));
        
            DrawTableModeSelector();
            GUILayout.Space(FFEditorStyles.ControlSpacing);
        
            IReadOnlyList<string> categories = LocalizationEditorDataUtility.GetCategories(settings);
            string[] categoryLabels = categories == null
                ? new[] { "无分类" }
                : categories.ToArray();
            if (categoryLabels.Length == 0)
                categoryLabels = new[] { "无分类" };
        
            selectedCategoryIndex = Mathf.Clamp(selectedCategoryIndex, 0, categoryLabels.Length - 1);
            EditorGUILayout.LabelField(
                "分类",
                GUILayout.Width(42f));
            int nextCategoryIndex = EditorGUILayout.Popup(
                selectedCategoryIndex,
                categoryLabels,
                GUILayout.Width(180f));
            if (nextCategoryIndex != selectedCategoryIndex)
            {
                if (ConfirmDiscardChanges())
                {
                    selectedCategoryIndex = nextCategoryIndex;
                    LoadSelectedCategory();
                }
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
                ShowLocaleColumnMenu();
            EditorGUI.BeginDisabledGroup(!hasPendingChanges);
            if (GUILayout.Button("差异预览", EditorStyles.toolbarButton, GUILayout.Width(68f)))
                ShowUnsavedChangesPreview();
            EditorGUI.EndDisabledGroup();
        
            EditorGUI.BeginDisabledGroup(undoStates.Count == 0);
            if (GUILayout.Button("撤销", EditorStyles.toolbarButton, GUILayout.Width(44f)))
                UndoEdit();
            EditorGUI.EndDisabledGroup();
        
            EditorGUI.BeginDisabledGroup(redoStates.Count == 0);
            if (GUILayout.Button("重做", EditorStyles.toolbarButton, GUILayout.Width(44f)))
                RedoEdit();
            EditorGUI.EndDisabledGroup();
        
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("重新读取", EditorStyles.toolbarButton, GUILayout.Width(80f)))
            {
                if (ConfirmDiscardChanges())
                    LoadSelectedCategory();
            }
        
            EditorGUILayout.EndHorizontal();
        }
        
        /// <summary>
        /// 绘制新增 Key、外部工具和保存操作区。
        /// </summary>
        private void DrawEditActions()
        {
            EditorGUILayout.BeginHorizontal(
                GetTableRowStyle(),
                GUILayout.Height(EditActionRowHeight));
        
            Rect rowRect = GUILayoutUtility.GetRect(
                0f,
                EditActionRowHeight,
                GUILayout.ExpandWidth(true));
            float controlY = rowRect.y + (rowRect.height - EditActionControlHeight) * 0.5f;
            float x = rowRect.x + 8f;
            float gap = FFEditorStyles.ControlSpacing;
            float labelWidth = 64f;
            float addButtonWidth = 58f;
            float toolButtonWidth = 82f;
            float saveButtonWidth = 160f;
        
            Rect labelRect = new Rect(x, controlY, labelWidth, EditActionControlHeight);
            GUI.Label(labelRect, "新增 Key", EditorStyles.miniLabel);
            x += labelWidth + gap;
        
            float saveX = rowRect.xMax - saveButtonWidth - 8f;
            float excelX = saveX - gap - toolButtonWidth;
            float folderX = excelX - gap - toolButtonWidth;
            float jsonX = folderX - gap - toolButtonWidth;
            float maxKeyWidth = jsonX - gap - addButtonWidth - 2f - x;
            float keyWidth = Mathf.Clamp(
                maxKeyWidth,
                180f,
                MaxNewKeyFieldWidth);
            Rect keyRect = new Rect(x, controlY, keyWidth, EditActionControlHeight);
            newKeyDraft = EditorGUI.TextField(keyRect, newKeyDraft, EditorStyles.miniTextField);
            x = keyRect.xMax + 2f;
        
            Rect addButtonRect = new Rect(x, controlY, addButtonWidth, EditActionControlHeight);
            if (GUI.Button(addButtonRect, "新增", EditorStyles.miniButton))
                AddKey();
        
            Rect openJsonButtonRect = new Rect(jsonX, controlY, toolButtonWidth, EditActionControlHeight);
            if (GUI.Button(openJsonButtonRect, "打开 JSON", EditorStyles.miniButton))
                OpenJsonSource();
        
            Rect openFolderButtonRect = new Rect(folderX, controlY, toolButtonWidth, EditActionControlHeight);
            if (GUI.Button(openFolderButtonRect, "打开目录", EditorStyles.miniButton))
                OpenJsonDirectory();
        
            Rect excelButtonRect = new Rect(excelX, controlY, toolButtonWidth, EditActionControlHeight);
            if (GUI.Button(excelButtonRect, "Excel 工具", EditorStyles.miniButton))
                ShowExcelMenu();
        
            Rect saveButtonRect = new Rect(
                saveX,
                controlY,
                saveButtonWidth,
                EditActionControlHeight);
            if (GUI.Button(saveButtonRect, "保存并同步运行时副本", EditorStyles.miniButton))
                SaveSelectedCategory();
            EditorGUILayout.EndHorizontal();
        }
        
        /// <summary>
        /// 绘制当前分类下的文本 Key 和多语言内容。
        /// </summary>
        private void DrawTable()
        {
            tableScrollPosition = EditorGUILayout.BeginScrollView(tableScrollPosition, false, false, GUILayout.ExpandHeight(true));
            float tableWidth = GetTableWidth();
            EditorGUILayout.BeginHorizontal(GetTableRowStyle(), GUILayout.Width(tableWidth), GUILayout.Height(TableRowHeight));
            DrawTableHeaderCell("Key", KeyColumnWidth);
            foreach (var t in locales)
            {
                if (!IsLocaleColumnVisible(t))
                    continue;
                string label = LocaleCatalog.GetEditorLabel(t);
                DrawTableHeaderCell(label, LocaleColumnWidth);
            }
            DrawTableHeaderCell("操作", OperationColumnWidth);
            EditorGUILayout.EndHorizontal();
        
            int visibleKeyCount = 0;
            for (int i = 0; i < keys.Count; i++)
            {
                string key = keys[i];
                if (!IsKeyVisible(key))
                    continue;
        
                visibleKeyCount++;
                EditorGUILayout.BeginHorizontal(
                    GetTableRowStyle(),
                    GUILayout.Width(tableWidth),
                    GUILayout.Height(TableRowHeight));
                Rect keyCellRect = GetTableCellRect(KeyColumnWidth);
                DrawTableCellFrame(keyCellRect);
                EditorGUI.BeginChangeCheck();
                string editedKey = EditorGUI.DelayedTextField(
                    GetTableCellContentRect(keyCellRect),
                    key,
                    EditorStyles.textField);
                if (EditorGUI.EndChangeCheck() && !string.Equals(editedKey, key, StringComparison.Ordinal))
                {
                    RenameKey(key, editedKey);
                    EditorGUILayout.EndHorizontal();
                    break;
                }
        
                for (int localeIndex = 0; localeIndex < locales.Count; localeIndex++)
                {
                    if (!IsLocaleColumnVisible(locales[localeIndex]))
                        continue;
                    Dictionary<string, string> localeValues = valuesByLocale[locales[localeIndex].Code];
                    localeValues.TryGetValue(key, out var value);
                    bool isMissing = string.IsNullOrWhiteSpace(value);
                    Color previousBackgroundColor = GUI.backgroundColor;
                    if (isMissing)
                        GUI.backgroundColor = new Color(1f, 0.9f, 0.6f);
        
                    Rect valueCellRect = GetTableCellRect(LocaleColumnWidth);
                    DrawTableCellFrame(valueCellRect);
                    EditorGUI.BeginChangeCheck();
                    string editedValue = EditorGUI.TextArea(
                        GetTableCellContentRect(valueCellRect),
                        value ?? string.Empty,
                        EditorStyles.textArea);
                    if (EditorGUI.EndChangeCheck())
                    {
                        RecordUndoState();
                        localeValues[key] = editedValue;
                        hasPendingChanges = true;
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
                if (GUI.Button(deleteButtonRect, "删除", EditorStyles.miniButton))
                {
                    DeleteKey(key);
                    EditorGUILayout.EndHorizontal();
                    break;
                }
        
                EditorGUILayout.EndHorizontal();
            }
        
            if (visibleKeyCount == 0)
            {
                EditorGUILayout.HelpBox(
                    showMissingOnly
                        ? "当前筛选条件下没有缺失翻译。"
                        : "没有匹配当前搜索条件的 Key。",
                    MessageType.Info);
            }
        
            EditorGUILayout.EndScrollView();
        
            string status = $"当前分类：{GetSelectedCategory()}    显示：{visibleKeyCount}/{keys.Count}    语言：{locales.Count}";
            int missingCellCount = CountMissingCells();
            if (missingCellCount > 0)
                status += $"    缺失翻译：{missingCellCount}";
            if (missingFileCount > 0)
                status += $"    缺少语言表：{missingFileCount}";
            if (externalFilesChanged)
                status += "    ! 外部文件已变化";
            if (hasPendingChanges)
                status += "    * 有未保存修改";
            EditorGUILayout.LabelField(status, EditorStyles.centeredGreyMiniLabel);
        }
        
        private void DrawTableHeaderCell(string text, float width)
        {
            Rect cellRect = GetTableCellRect(width);
            DrawTableCellFrame(cellRect);
            GUI.Label(cellRect, text, GetLocaleHeaderStyle());
        }
        
        private Rect GetTableCellRect(float width)
        {
            return EditorGUILayout.GetControlRect(
                false,
                TableRowHeight,
                GUILayout.Width(width),
                GUILayout.Height(TableRowHeight));
        }
        
        private float GetTableWidth()
        {
            return KeyColumnWidth + GetVisibleLocaleCount() * LocaleColumnWidth + OperationColumnWidth;
        }
        
        private static Rect GetTableCellContentRect(Rect cellRect)
        {
            float inset = TableCellContentInset;
            return new Rect(
                cellRect.x + inset,
                cellRect.y + inset,
                Mathf.Max(0f, cellRect.width - inset * 2f),
                Mathf.Max(0f, cellRect.height - inset * 2f));
        }
        
        private static void DrawTableCellFrame(Rect cellRect)
        {
            Color dividerColor = EditorGUIUtility.isProSkin
                ? new Color(0.12f, 0.12f, 0.12f, 0.95f)
                : new Color(0.55f, 0.55f, 0.55f, 0.95f);
        
            EditorGUI.DrawRect(new Rect(cellRect.x, cellRect.y, TableCellInset, cellRect.height), dividerColor);
            EditorGUI.DrawRect(new Rect(cellRect.xMax - TableCellInset, cellRect.y, TableCellInset, cellRect.height), dividerColor);
            EditorGUI.DrawRect(new Rect(cellRect.x, cellRect.y, cellRect.width, TableCellInset), dividerColor);
            EditorGUI.DrawRect(new Rect(cellRect.x, cellRect.yMax - TableCellInset, cellRect.width, TableCellInset), dividerColor);
        }
        
        private GUIStyle GetLocaleHeaderStyle()
        {
            if (localeHeaderStyle != null)
                return localeHeaderStyle;
        
            localeHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false,
                clipping = TextClipping.Clip
            };
            return localeHeaderStyle;
        }
        
        private GUIStyle GetTableKeyStyle()
        {
            if (tableKeyStyle != null)
                return tableKeyStyle;
        
            tableKeyStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleLeft,
                wordWrap = false,
                clipping = TextClipping.Clip,
                padding = new RectOffset(4, 4, 0, 0)
            };
            return tableKeyStyle;
        }
        
        private GUIStyle GetTableRowStyle()
        {
            if (tableRowStyle != null)
                return tableRowStyle;
        
            tableRowStyle = FFEditorStyles.RowBox;
            return tableRowStyle;
        }
        
        #endregion
    }
}
