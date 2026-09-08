using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FinkFramework.Runtime.Localization;
using UnityEditor;

namespace FinkFramework.Editor.Modules.Localization.UI
{
    /// <summary>
    /// 本地化表的编辑历史：负责撤销、重做和未保存差异快照。
    /// </summary>
    public sealed partial class LocalizationWindow
    {
        #region 编辑历史与 Key 操作
        
        /// <summary>
        /// 在修改 Key 或翻译内容前保存一个可恢复的编辑快照。
        /// </summary>
        private void RecordUndoState()
        {
            undoStates.Add(CaptureEditorState());
            if (undoStates.Count > MaxUndoStates)
                undoStates.RemoveAt(0);
        
            redoStates.Clear();
        }
        
        /// <summary>
        /// 恢复最近一次编辑，并将当前状态压入重做栈。
        /// </summary>
        private void UndoEdit()
        {
            if (undoStates.Count == 0)
                return;
        
            int lastIndex = undoStates.Count - 1;
            redoStates.Add(CaptureEditorState());
            EditorStateSnapshot previousState = undoStates[lastIndex];
            undoStates.RemoveAt(lastIndex);
            RestoreEditorState(previousState);
            Repaint();
        }
        
        /// <summary>
        /// 应用最近一次撤销的编辑状态。
        /// </summary>
        private void RedoEdit()
        {
            if (redoStates.Count == 0)
                return;
        
            int lastIndex = redoStates.Count - 1;
            undoStates.Add(CaptureEditorState());
            EditorStateSnapshot nextState = redoStates[lastIndex];
            redoStates.RemoveAt(lastIndex);
            RestoreEditorState(nextState);
            Repaint();
        }
        
        private EditorStateSnapshot CaptureEditorState()
        {
            var snapshotValues = new Dictionary<string, Dictionary<string, string>>(
                StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, Dictionary<string, string>> localePair in valuesByLocale)
            {
                snapshotValues[localePair.Key] = new Dictionary<string, string>(
                    localePair.Value,
                    StringComparer.Ordinal);
            }
        
            return new EditorStateSnapshot(
                new List<string>(keys),
                snapshotValues,
                hasPendingChanges);
        }
        
        private void RestoreEditorState(EditorStateSnapshot snapshot)
        {
            keys.Clear();
            keys.AddRange(snapshot.Keys);
            valuesByLocale.Clear();
            foreach (KeyValuePair<string, Dictionary<string, string>> localePair in snapshot.ValuesByLocale)
            {
                valuesByLocale[localePair.Key] = new Dictionary<string, string>(
                    localePair.Value,
                    StringComparer.Ordinal);
            }
        
            hasPendingChanges = snapshot.HasUnsavedChanges;
        }
        
        private void RenameKey(string oldKey, string proposedKey)
        {
            string category = GetSelectedCategory();
            string newKey = LocalizationKeyUtility.GetCategoryRelativeKey(
                category,
                proposedKey);
            if (string.IsNullOrEmpty(newKey))
            {
                EditorUtility.DisplayDialog("重命名 Key", "Key 不能为空。", "确定");
                return;
            }
        
            if (string.Equals(oldKey, newKey, StringComparison.Ordinal))
                return;
        
            if (keys.Contains(newKey, StringComparer.Ordinal))
            {
                EditorUtility.DisplayDialog("重命名 Key", $"Key 已存在：{newKey}", "确定");
                return;
            }
        
            string oldFullKey = LocalizationKeyUtility.BuildFullKey(category, oldKey);
            IReadOnlyList<string> references =
                LocalizationQualityChecker.FindKeyReferences(settings, oldFullKey);
            var message = new StringBuilder();
            message.AppendLine($"确认将 Key 重命名：");
            message.AppendLine($"{oldKey}");
            message.AppendLine("↓");
            message.AppendLine(newKey);
            message.AppendLine();
            if (references.Count == 0)
            {
                message.AppendLine("未扫描到工程引用。");
            }
            else
            {
                message.AppendLine($"发现 {references.Count} 处工程引用：");
                for (int i = 0; i < references.Count && i < 6; i++)
                    message.AppendLine($"- {references[i]}");
                if (references.Count > 6)
                    message.AppendLine($"其余 {references.Count - 6} 处引用省略。");
                message.AppendLine();
                message.AppendLine("确认后只会修改语言表，旧引用不会自动改写，请在迁移后运行质量检查。");
            }
        
            if (!EditorUtility.DisplayDialog("确认重命名 Key", message.ToString(), "重命名", "取消"))
                return;
        
            RecordUndoState();
            keys.Remove(oldKey);
            keys.Add(newKey);
            keys.Sort(StringComparer.Ordinal);
            foreach (Dictionary<string, string> localeValues in valuesByLocale.Values)
            {
                localeValues.TryGetValue(oldKey, out string value);
                localeValues.Remove(oldKey);
                localeValues[newKey] = value ?? string.Empty;
            }
        
            hasPendingChanges = true;
        }
        
        /// <summary>
        /// 删除当前分类中的一个相对 Key；保存时会同步移除所有配置语言的对应条目。
        /// 删除操作不再弹出二次确认，可通过窗口的 Undo/Redo 恢复。
        /// </summary>
        private void DeleteKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return;
        
            RecordUndoState();
            keys.Remove(key);
            foreach (Dictionary<string, string> localeValues in valuesByLocale.Values)
                localeValues.Remove(key);
            hasPendingChanges = true;
        }
        
        #region 差异预览与快照

        private void CaptureGenerationSnapshot()
        {
            loadedKeySnapshot.Clear();
            loadedKeySnapshot.UnionWith(keys);
            loadedValuesSnapshot.Clear();

            foreach (LocaleInfo locale in locales)
            {
                if (!valuesByLocale.TryGetValue(locale.Code, out Dictionary<string, string> values))
                    continue;

                loadedValuesSnapshot[locale.Code] = new Dictionary<string, string>(
                    values,
                    StringComparer.Ordinal);
            }
        }

        /// <summary>
        /// 对比当前编辑状态与最近一次读取/保存的快照。
        /// </summary>
        private void ShowUnsavedChangesPreview()
        {
            if (!hasPendingChanges)
            {
                LocalizationDiffPreviewWindow.Open(
                    "语言表差异预览",
                    $"分类：{GetSelectedCategory()}    当前没有未保存修改。",
                    new[]
                    {
                        new LocalizationDiffPreviewWindow.Section(
                            "当前状态",
                            LocalizationDiffPreviewWindow.SectionType.Summary,
                            new[] { "没有需要保存的变化。" })
                    });
                return;
            }

            var addedKeys = keys
                .Except(loadedKeySnapshot, StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList();
            var removedKeys = loadedKeySnapshot
                .Except(keys, StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList();
            var changedValues = new List<LocalizationDiffPreviewWindow.Section.Change>();
            foreach (LocaleInfo locale in locales)
            {
                loadedValuesSnapshot.TryGetValue(
                    locale.Code,
                    out Dictionary<string, string> oldValues);
                valuesByLocale.TryGetValue(
                    locale.Code,
                    out Dictionary<string, string> currentValues);
                foreach (string key in keys.Union(loadedKeySnapshot, StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal))
                {
                    string oldValue = string.Empty;
                    string currentValue = string.Empty;
                    if (oldValues != null)
                        oldValues.TryGetValue(key, out oldValue);
                    if (currentValues != null)
                        currentValues.TryGetValue(key, out currentValue);
                    oldValue ??= string.Empty;
                    currentValue ??= string.Empty;
                    if (!string.Equals(oldValue, currentValue, StringComparison.Ordinal))
                    {
                        changedValues.Add(
                            new LocalizationDiffPreviewWindow.Section.Change(
                                $"{locale.Code} / {key}",
                                FormatDiffValue(oldValue),
                                FormatDiffValue(currentValue)));
                    }
                }
            }

            LocalizationDiffPreviewWindow.Open(
                "语言表差异预览",
                $"分类：{GetSelectedCategory()}    显示当前内容与上次读取/保存内容的差异。",
                new[]
                {
                    new LocalizationDiffPreviewWindow.Section(
                        "新增 Key",
                        LocalizationDiffPreviewWindow.SectionType.Added,
                        addedKeys),
                    new LocalizationDiffPreviewWindow.Section(
                        "删除 Key",
                        LocalizationDiffPreviewWindow.SectionType.Removed,
                        removedKeys),
                    LocalizationDiffPreviewWindow.Section.CreateChanges(
                        "翻译内容变化",
                        LocalizationDiffPreviewWindow.SectionType.Changed,
                        changedValues)
                });
        }

        private static string FormatDiffValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "（空）";

            string normalized = value
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
            return normalized.Length > 80
                ? normalized.Substring(0, 80) + "..."
                : normalized;
        }

        #endregion
        #endregion
    }
}
