using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FinkFramework.Editor.Common;
using FinkFramework.Runtime.Localization;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace FinkFramework.Editor.Modules.Localization.UI
{
    /// <summary>
    /// 统一本地化表编辑窗口，支持文本 JSON 表和资源引用表两种模式。
    /// </summary>
    public sealed partial class LocalizationWindow : EditorWindow
    {
        #region 窗口状态与公共入口

        private const float KeyColumnWidth = 260f;
        private const float LocaleColumnWidth = 190f;
        private const float OperationColumnWidth = 64f;
        private const float TableRowHeight = 26f;
        private const float EditActionRowHeight = 28f;
        private const float EditActionControlHeight = 20f;
        private const float TableCellInset = 1f;
        private const float TableCellContentInset = 3f;
        private const float DeleteButtonHorizontalInset = 4f;
        private const float DeleteButtonVerticalInset = 3f;
        private const float DeleteButtonHeight = TableRowHeight - DeleteButtonVerticalInset * 2f;
        private const float MaxNewKeyFieldWidth = 280f;
        private const int MaxUndoStates = 50;
        private LocalizationSettingsAsset settings;

        // 语言表和资源表共用这一组筛选状态，切换表类型时保持同一套视图条件。
        private int selectedCategoryIndex;
        private Vector2 tableScrollPosition;
        private string newKeyDraft = string.Empty;
        private string searchText = string.Empty;
        private bool showMissingOnly;
        // 本窗口自己的待保存状态，避免遮蔽 EditorWindow.hasUnsavedChanges。
        private bool hasPendingChanges;
        private readonly List<EditorStateSnapshot> undoStates = new();
        private readonly List<EditorStateSnapshot> redoStates = new();
        private readonly List<LocaleInfo> locales = new();
        private readonly List<string> keys = new();
        private readonly HashSet<string> loadedKeySnapshot = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Dictionary<string, string>> loadedValuesSnapshot = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Dictionary<string, string>> valuesByLocale = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> hiddenLocaleCodes = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, FileStamp> loadedFileStamps = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> loadErrors = new();
        private GUIStyle localeHeaderStyle;
        private GUIStyle tableKeyStyle;
        private GUIStyle tableRowStyle;
        private int missingFileCount;
        private bool externalFilesChanged;

        #endregion

        #region 窗口生命周期

        /// <summary>
        /// 打开统一本地化表编辑器，默认进入语言表模式。
        /// </summary>
        [MenuItem("Fink Framework/本地化系统/本地化语言表", false, 100)]
        public static void Open()
        {
            var window = GetWindow<LocalizationWindow>("本地化语言表");
            window.minSize = new Vector2(860f, 560f);
            window.tableMode = LocalizationTableMode.Text;
            window.ReloadSettings();
            window.Show();
        }

        /// <summary>
        /// 从菜单直接打开本地化资源表。
        /// 与文本语言表共用同一个窗口，但进入后固定显示资源引用模式。
        /// </summary>
        [MenuItem("Fink Framework/本地化系统/本地化资源表", false, 110)]
        public static void OpenAsset()
        {
            var window = GetWindow<LocalizationWindow>("本地化资源表");
            bool canSwitch = window.tableMode == LocalizationTableMode.Asset
                ? window.ConfirmAssetDiscardChanges()
                : window.ConfirmDiscardChanges();
            if (!canSwitch)
                return;

            window.minSize = new Vector2(860f, 560f);
            window.tableMode = LocalizationTableMode.Asset;
            window.ReloadAssetMode();
            window.Show();
        }

        private void OnEnable()
        {
            ReloadSettings();
        }

        private void OnGUI()
        {
            if (settings == null)
            {
                EditorGUILayout.HelpBox(
                    "LocalizationSettingsAsset 缺失，请先打开本地化设置面板重新创建配置。",
                    MessageType.Error);
                if (GUILayout.Button("重新加载配置"))
                    ReloadSettings();
                GUILayout.FlexibleSpace();
                FFEditorGUI.DrawFrameworkFooter(8f);
                return;
            }

            if (tableMode == LocalizationTableMode.Asset)
            {
                DrawAssetMode();
                FFEditorGUI.DrawFrameworkFooter(6f);
                return;
            }

            externalFilesChanged = HasExternalFileChanges();
            DrawToolbar();
            DrawEditActions();
            GUILayout.Space(6);

            if (loadErrors.Count > 0)
                EditorGUILayout.HelpBox(BuildErrorMessage(), MessageType.Warning);

            if (externalFilesChanged)
            {
                EditorGUILayout.HelpBox(
                    "当前分类的语言表已被外部修改。保存前必须选择重新载入或明确覆盖外部修改。",
                    MessageType.Warning);
            }

            if (keys.Count == 0)
            {
                EditorGUILayout.BeginVertical(
                    FFEditorStyles.SectionBox,
                    GUILayout.ExpandWidth(true));
                bool hasMissingFiles = missingFileCount > 0;
                EditorGUILayout.HelpBox(
                    locales.Count == 0
                        ? "当前没有配置支持语言，请检查本地化配置中的固定语言列表。"
                        : hasMissingFiles
                            ? "当前分类缺少语言表文件，请先在本地化配置中点击“保存并应用配置”创建缺失文件。"
                            : "当前分类的语言表文件已经存在，但还没有 Key。请使用上方的“新增 Key”创建内容。",
                    MessageType.Info);

                EditorGUILayout.EndVertical();
                GUILayout.FlexibleSpace();
                FFEditorGUI.DrawFrameworkFooter(8f);
                return;
            }

            DrawTable();
            FFEditorGUI.DrawFrameworkFooter(6f);
        }

        #endregion

        #region 文本数据加载与筛选

        /// <summary>
        /// 重新读取配置，并按当前共享分类筛选条件加载语言表。
        /// </summary>
        private void ReloadSettings()
        {
            settings = LocalizationSettingsEditorLoader.LoadOrCreate();
            LoadSelectedCategory();
        }

        /// <summary>
        /// 加载当前分类下的所有语言 JSON，并建立编辑器内存快照。
        /// </summary>
        private void LoadSelectedCategory()
        {
            locales.Clear();
            keys.Clear();
            valuesByLocale.Clear();
            loadedFileStamps.Clear();
            loadErrors.Clear();
            undoStates.Clear();
            redoStates.Clear();
            missingFileCount = 0;
            hasPendingChanges = false;
            externalFilesChanged = false;
            loadedKeySnapshot.Clear();
            loadedValuesSnapshot.Clear();
            newKeyDraft = string.Empty;

            if (settings == null)
                return;

            settings.NormalizeConfiguration();
            if (!LocalizationDataSyncUtility.PrepareSourceDataRoot(
                    out string prepareMessage))
            {
                loadErrors.Add(prepareMessage);
                return;
            }

            foreach (LocaleInfo locale in settings.SupportedLocales)
            {
                if (locale == null || !LocaleInfo.TryNormalize(locale.Code, out string normalizedCode))
                    continue;

                var normalizedLocale = new LocaleInfo(normalizedCode, locale.DisplayName);
                locales.Add(normalizedLocale);
                valuesByLocale[normalizedCode] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            hiddenLocaleCodes.RemoveWhere(code =>
                !locales.Any(locale => string.Equals(locale.Code, code, StringComparison.OrdinalIgnoreCase)));
            if (locales.Count > 0 && GetVisibleLocaleCount() == 0)
                hiddenLocaleCodes.Clear();

            string category = GetSelectedCategory();
            if (string.IsNullOrEmpty(category) || locales.Count == 0)
                return;

            string dataRoot = LocalizationPath.GetSourceDataRoot();
            foreach (LocaleInfo locale in locales)
            {
                string filePath = LocalizationPath.GetLanguageFilePath(
                    dataRoot,
                    category,
                    locale.Code,
                    settings.JsonFileNamePattern);
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                {
                    missingFileCount++;
                    continue;
                }

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
                        if (property.Value.Type != JTokenType.String)
                        {
                            loadErrors.Add($"{Path.GetFileName(filePath)}：Key {property.Name} 的值不是字符串。");
                            continue;
                        }

                        if (!string.IsNullOrWhiteSpace(property.Name))
                        {
                            string editorKey = LocalizationKeyUtility.GetCategoryRelativeKey(
                                category,
                                property.Name);
                            if (string.IsNullOrWhiteSpace(editorKey))
                                continue;

                            if (valuesByLocale[locale.Code].ContainsKey(editorKey))
                            {
                                loadErrors.Add(
                                    $"{Path.GetFileName(filePath)}：Key {property.Name} 与当前分类的另一个 Key 映射为同一个完整 Key。\n"
                                    + "请手动合并后再保存语言表。");
                                continue;
                            }

                            valuesByLocale[locale.Code][editorKey] =
                                property.Value.Value<string>() ?? string.Empty;
                            if (!keys.Contains(editorKey, StringComparer.OrdinalIgnoreCase))
                                keys.Add(editorKey);
                            if (!LocalizationKeyUtility.IsFullKeyForCategory(category, property.Name))
                            {
                                hasPendingChanges = true;
                            }
                        }
                    }
                }
                catch (Exception exception)
                {
                    loadErrors.Add($"{Path.GetFileName(filePath)}：{exception.Message}");
                }
            }

            keys.Sort(StringComparer.Ordinal);
            CaptureGenerationSnapshot();
            CaptureFileStamps(dataRoot, category);
            Repaint();
        }

        /// <summary>
        /// 按当前分类新增一个相对 Key，并为所有支持语言创建空值。
        /// </summary>
        private void AddKey()
        {
            string category = GetSelectedCategory();
            string key = LocalizationKeyUtility.GetCategoryRelativeKey(
                category,
                newKeyDraft);
            if (string.IsNullOrEmpty(key))
            {
                EditorUtility.DisplayDialog("新增 Key", "Key 不能为空。", "确定");
                return;
            }

            if (keys.Contains(key, StringComparer.OrdinalIgnoreCase))
            {
                EditorUtility.DisplayDialog("新增 Key", $"Key 已存在：{key}", "确定");
                return;
            }

            RecordUndoState();
            keys.Add(key);
            keys.Sort(StringComparer.Ordinal);
            foreach (LocaleInfo locale in locales)
                valuesByLocale[locale.Code][key] = string.Empty;

            newKeyDraft = string.Empty;
            hasPendingChanges = true;
        }

        private bool IsKeyVisible(string key)
        {
            if (!string.IsNullOrWhiteSpace(searchText)
                && key.IndexOf(searchText.Trim(), StringComparison.OrdinalIgnoreCase) < 0)
                return false;

            return !showMissingOnly || HasMissingTranslation(key);
        }

        private bool IsLocaleColumnVisible(LocaleInfo locale)
        {
            return locale != null && !hiddenLocaleCodes.Contains(locale.Code);
        }

        private int GetVisibleLocaleCount()
        {
            int count = 0;
            foreach (LocaleInfo locale in locales)
            {
                if (IsLocaleColumnVisible(locale))
                    count++;
            }

            return count;
        }

        private void ShowLocaleColumnMenu()
        {
            var menu = new GenericMenu();
            foreach (var locale in locales)
            {
                string label = LocaleCatalog.GetEditorLabel(locale);
                bool isVisible = IsLocaleColumnVisible(locale);
                var locale1 = locale;
                menu.AddItem(new GUIContent(label), isVisible,
                    () =>
                    {
                        if (isVisible)
                        {
                            if (GetVisibleLocaleCount() <= 1)
                            {
                                EditorUtility.DisplayDialog(
                                    "语言列",
                                    "至少需要保留一列语言，不能隐藏全部语言列。",
                                    "确定");
                                return;
                            }

                            hiddenLocaleCodes.Add(locale1.Code);
                        }
                        else
                        {
                            hiddenLocaleCodes.Remove(locale1.Code);
                        }

                        Repaint();
                    });
            }

            if (locales.Count == 0)
                menu.AddDisabledItem(new GUIContent("没有配置支持语言"));
            menu.ShowAsContext();
        }

        private bool HasMissingTranslation(string key)
        {
            foreach (LocaleInfo locale in locales)
            {
                if (!valuesByLocale[locale.Code].TryGetValue(key, out string value)
                    || string.IsNullOrWhiteSpace(value))
                    return true;
            }

            return false;
        }

        private int CountMissingCells()
        {
            int count = 0;
            foreach (string key in keys)
            {
                if (!IsKeyVisible(key))
                    continue;

                foreach (LocaleInfo locale in locales)
                {
                    if (!valuesByLocale[locale.Code].TryGetValue(key, out string value)
                        || string.IsNullOrWhiteSpace(value))
                        count++;
                }
            }

            return count;
        }

        #endregion




        #region 公共筛选与错误提示

        private bool ConfirmDiscardChanges()
        {
            if (!hasPendingChanges)
                return true;

            return EditorUtility.DisplayDialog(
                "存在未保存修改",
                "重新读取或切换分类会丢失当前修改，是否继续？",
                "放弃修改",
                "取消");
        }

        private string GetSelectedCategory()
        {
            IReadOnlyList<string> categories = LocalizationEditorDataUtility.GetCategories(settings);
            if (categories.Count == 0)
                return string.Empty;

            int index = Mathf.Clamp(selectedCategoryIndex, 0, categories.Count - 1);
            return categories[index];
        }

        private string BuildErrorMessage()
        {
            var builder = new StringBuilder("读取语言表时发现问题：\n");
            for (int i = 0; i < loadErrors.Count && i < 6; i++)
                builder.AppendLine(loadErrors[i]);

            if (loadErrors.Count > 6)
                builder.AppendLine($"其余 {loadErrors.Count - 6} 个问题已省略。");

            return builder.ToString();
        }

        private sealed class EditorStateSnapshot
        {
            public EditorStateSnapshot(
                List<string> keys,
                Dictionary<string, Dictionary<string, string>> valuesByLocale,
                bool hasPendingChanges)
            {
                Keys = keys;
                ValuesByLocale = valuesByLocale;
                HasUnsavedChanges = hasPendingChanges;
            }

            public List<string> Keys { get; }
            public Dictionary<string, Dictionary<string, string>> ValuesByLocale { get; }
            public bool HasUnsavedChanges { get; }
        }

        private readonly struct FileStamp
        {
            public FileStamp(bool exists, long length, long lastWriteTicks)
            {
                Exists = exists;
                Length = length;
                LastWriteTicks = lastWriteTicks;
            }

            private bool Exists { get; }
            private long Length { get; }
            private long LastWriteTicks { get; }

            public bool Matches(FileStamp other)
            {
                return Exists == other.Exists
                    && Length == other.Length
                    && LastWriteTicks == other.LastWriteTicks;
            }
        }

        #endregion
    }
}
