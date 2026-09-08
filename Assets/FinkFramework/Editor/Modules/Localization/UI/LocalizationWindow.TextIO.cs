using System;
using System.Collections.Generic;
using System.IO;
using FinkFramework.Runtime.Localization;
using FinkFramework.Runtime.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace FinkFramework.Editor.Modules.Localization.UI
{
    /// <summary>
    /// 本地化表的文本数据访问：负责 JSON 文件、保存和外部文件变更检测。
    /// </summary>
    public sealed partial class LocalizationWindow
    {
        #region JSON 文件、保存与外部变更检测
        
        /// <summary>
        /// 打开当前分类的默认语言 JSON 文件。
        /// </summary>
        private void OpenJsonSource()
        {
            string category = GetSelectedCategory();
            if (string.IsNullOrEmpty(category))
            {
                EditorUtility.DisplayDialog("打开 JSON", "当前没有可打开的分类。", "确定");
                return;
            }
        
            string dataRoot = LocalizationPath.GetSourceDataRoot();
            string categoryDirectory = LocalizationPath.GetCategoryDirectory(dataRoot, category);
            if (!Directory.Exists(categoryDirectory))
            {
                EditorUtility.DisplayDialog(
                    "打开 JSON",
                    $"当前分类目录不存在：\n{categoryDirectory}\n\n请先在本地化配置中点击“保存并应用配置”。",
                    "确定");
                return;
            }
        
            var menu = new GenericMenu();
            foreach (LocaleInfo locale in locales)
            {
                string localeCode = locale.Code;
                string fileName = LocalizationPath.GetLanguageFileName(
                    localeCode,
                    settings.JsonFileNamePattern);
                string filePath = Path.Combine(categoryDirectory, fileName);
                string label = LocaleCatalog.GetEditorLabel(locale);
                menu.AddItem(
                    new GUIContent(label),
                    false,
                    () => OpenJsonFile(filePath, localeCode));
            }
        
            if (locales.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("没有配置支持语言"));
            }
        
            menu.ShowAsContext();
        }
        
        private void OpenJsonFile(string filePath, string localeCode)
        {
            if (!File.Exists(filePath))
            {
                EditorUtility.DisplayDialog(
                    "打开 JSON",
                    $"语言表文件不存在：\n{filePath}\n\n请先在本地化配置中点击“保存并应用配置”。",
                    "确定");
                return;
            }
        
            EditorUtility.OpenWithDefaultApp(filePath);
            LogUtil.Info("LocalizationJsonTool", $"已打开 {localeCode} JSON 源文件：{filePath}");
        }
        
        private void OpenJsonDirectory()
        {
            string category = GetSelectedCategory();
            if (string.IsNullOrEmpty(category))
            {
                EditorUtility.DisplayDialog("打开目录", "当前没有可打开的分类。", "确定");
                return;
            }
        
            string dataRoot = LocalizationPath.GetSourceDataRoot();
            string categoryDirectory = LocalizationPath.GetCategoryDirectory(dataRoot, category);
            if (!Directory.Exists(categoryDirectory))
            {
                EditorUtility.DisplayDialog(
                    "打开目录",
                    $"当前分类目录不存在：\n{categoryDirectory}\n\n请先在本地化配置中点击“保存并应用配置”。",
                    "确定");
                return;
            }
        
            EditorUtility.RevealInFinder(categoryDirectory);
            LogUtil.Info("LocalizationJsonTool", $"已打开本地化 JSON 目录：{categoryDirectory}");
        }
        
        private void SaveSelectedCategory()
        {
            string category = GetSelectedCategory();
            if (string.IsNullOrEmpty(category))
            {
                EditorUtility.DisplayDialog("保存失败", "当前没有可保存的分类。", "确定");
                return;
            }
        
            if (!ConfirmExternalChangesBeforeSave())
                return;
        
            if (!LocalizationPlaceholderValidator.Validate(
                    locales,
                    keys,
                    valuesByLocale,
                    out string validationMessage))
            {
                EditorUtility.DisplayDialog("占位符校验失败", validationMessage, "确定");
                return;
            }
        
            try
            {
                string dataRoot = LocalizationPath.GetSourceDataRoot();
                foreach (LocaleInfo locale in locales)
                {
                    string filePath = LocalizationPath.GetLanguageFilePath(
                        dataRoot,
                        category,
                        locale.Code,
                        settings.JsonFileNamePattern);
                    string directory = Path.GetDirectoryName(filePath);
                    if (!string.IsNullOrEmpty(directory))
                        Directory.CreateDirectory(directory);
        
                    var document = new JObject();
                    Dictionary<string, string> localeValues = valuesByLocale[locale.Code];
                    foreach (string key in keys)
                    {
                        localeValues.TryGetValue(key, out string value);
                        string fullKey = LocalizationKeyUtility.BuildFullKey(category, key);
                        if (string.IsNullOrWhiteSpace(fullKey))
                            throw new InvalidDataException($"Key 无法补充分类前缀：{key}");
                        document[fullKey] = value ?? string.Empty;
                    }
        
                    LocalizationEditorFileUtil.WriteUtf8TextAtomic(
                        filePath,
                        document.ToString(Formatting.Indented) + "\n");
                }

                if (!LocalizationDataSyncUtility.Sync(
                        settings,
                        out string syncMessage))
                {
                    // 源语言表已经写入，但运行时副本可能仍是旧版本，明确提示用户不要误以为同步完成。
                    AssetDatabase.Refresh();
                    LoadSelectedCategory();
                    EditorUtility.DisplayDialog(
                        "本地化语言表已保存，但同步失败",
                        $"本地化语言表已保存，运行时副本和清单同步失败：\n\n{syncMessage}",
                        "确定");
                    return;
                }

                CaptureFileStamps(dataRoot, category);
                externalFilesChanged = false;
                hasPendingChanges = false;
                LoadSelectedCategory();
                ShowNotification(new GUIContent("本地化语言表已保存"));
            }
            catch (Exception exception)
            {
                EditorUtility.DisplayDialog("保存失败", exception.Message, "确定");
            }
        }
        
        private bool ConfirmExternalChangesBeforeSave()
        {
            if (!HasExternalFileChanges())
            {
                externalFilesChanged = false;
                return true;
            }
        
            externalFilesChanged = true;
            if (!hasPendingChanges)
            {
                int reloadResult = EditorUtility.DisplayDialogComplex(
                    "语言表已在外部修改",
                    "当前分类的语言表文件已被外部修改，是否重新载入？",
                    "重新载入",
                    "取消",
                    "取消");
                if (reloadResult == 0)
                    LoadSelectedCategory();
                return false;
            }
        
            int result = EditorUtility.DisplayDialogComplex(
                "检测到外部修改",
                "当前分类的语言表文件已被外部修改。重新载入会丢失当前编辑器中的修改；覆盖会用当前编辑器内容覆盖外部文件。",
                "重新载入",
                "覆盖外部修改",
                "取消");
            if (result == 0)
            {
                LoadSelectedCategory();
                return false;
            }
        
            return result == 1;
        }
        
        private bool HasExternalFileChanges()
        {
            if (settings == null || loadedFileStamps.Count == 0)
                return false;
        
            string category = GetSelectedCategory();
            if (string.IsNullOrEmpty(category))
                return false;
        
            string dataRoot = LocalizationPath.GetSourceDataRoot();
            foreach (LocaleInfo locale in locales)
            {
                string filePath = LocalizationPath.GetLanguageFilePath(
                    dataRoot,
                    category,
                    locale.Code,
                    settings.JsonFileNamePattern);
                if (string.IsNullOrEmpty(filePath))
                    continue;
        
                if (!loadedFileStamps.TryGetValue(filePath, out FileStamp loadedStamp)
                    || !loadedStamp.Matches(GetFileStamp(filePath)))
                    return true;
            }
        
            return false;
        }
        
        private void CaptureFileStamps(string dataRoot, string category)
        {
            loadedFileStamps.Clear();
            if (string.IsNullOrEmpty(category))
                return;
        
            foreach (LocaleInfo locale in locales)
            {
                string filePath = LocalizationPath.GetLanguageFilePath(
                    dataRoot,
                    category,
                    locale.Code,
                    settings.JsonFileNamePattern);
                if (!string.IsNullOrEmpty(filePath))
                    loadedFileStamps[filePath] = GetFileStamp(filePath);
            }
        }
        
        private static FileStamp GetFileStamp(string filePath)
        {
            if (!File.Exists(filePath))
                return new FileStamp(false, 0L, 0L);
        
            FileInfo fileInfo = new FileInfo(filePath);
            return new FileStamp(
                true,
                fileInfo.Length,
                fileInfo.LastWriteTimeUtc.Ticks);
        }
        
        #endregion
    }
}
