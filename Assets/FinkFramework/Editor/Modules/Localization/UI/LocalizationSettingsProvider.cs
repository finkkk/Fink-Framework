using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FinkFramework.Editor.Common;
using FinkFramework.Runtime.Localization;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FinkFramework.Editor.Modules.Localization.UI
{
    /// <summary>
    /// 本地化模块的 Project Settings 页面。
    /// </summary>
    public sealed class LocalizationSettingsProvider : SettingsProvider
    {
        private LocalizationSettingsAsset asset;
        private SerializedObject serializedAsset;
        private readonly List<string> savedCategoryNames = new List<string>();
        private readonly List<string> savedLocaleCodes = new List<string>();
        private string savedJsonFileNamePattern = "{0}.json";
        private string localeCodeToAdd;
        private sealed class PendingDataDeletion
        {
            public readonly Dictionary<string, string> Files =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            public readonly List<string> RemovedCategories = new List<string>();

            public bool HasChanges => Files.Count > 0 || RemovedCategories.Count > 0;

            public void AddFile(string path, string reason)
            {
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                    Files[path] = reason;
            }
        }

        /// <summary>
        /// 删除确认使用自定义中文窗口，避免 Unity 的 DisplayDialog 在长文本后追加英文日志提示。
        /// </summary>
        private sealed class PendingDeletionWindow : EditorWindow
        {
            private string message;
            private Vector2 scrollPosition;
            private bool confirmed;

            public static bool Show(string message)
            {
                PendingDeletionWindow window = CreateInstance<PendingDeletionWindow>();
                window.message = message;
                window.titleContent = new GUIContent("确认删除本地化数据");
                // 固定为紧凑尺寸，避免 Unity 根据历史窗口大小留下大块空白。
                const float windowWidth = 560f;
                const float windowHeight = 340f;
                Rect mainWindow = EditorGUIUtility.GetMainWindowPosition();
                float centeredX = mainWindow.x + (mainWindow.width - windowWidth) * 0.5f;
                float centeredY = mainWindow.y + (mainWindow.height - windowHeight) * 0.5f;
                window.position = new Rect(
                    centeredX,
                    centeredY,
                    windowWidth,
                    windowHeight);
                window.minSize = new Vector2(520f, 340f);
                window.maxSize = new Vector2(760f, 340f);
                window.ShowModalUtility();
                bool result = window.confirmed;
                DestroyImmediate(window);
                return result;
            }

            private void OnGUI()
            {
                GUILayout.Space(12f);
                EditorGUILayout.LabelField(
                    "检测到本次配置会移除已有本地化数据。",
                    EditorStyles.boldLabel);
                EditorGUILayout.LabelField(
                    "以下配置删除可能导致翻译数据丢失，删除后通常无法恢复：",
                    EditorStyles.wordWrappedLabel);
                GUILayout.Space(6f);

                int messageLineCount = string.IsNullOrEmpty(message)
                    ? 1
                    : message.Split('\n').Length;
                float listHeight = Mathf.Clamp(56f + messageLineCount * 18f, 150f, 210f);
                scrollPosition = EditorGUILayout.BeginScrollView(
                    scrollPosition,
                    GUILayout.Height(listHeight));
                EditorGUILayout.HelpBox(message, MessageType.Warning);
                EditorGUILayout.EndScrollView();

                GUILayout.Space(8f);
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("确认删除", GUILayout.Width(110f), GUILayout.Height(28f)))
                {
                    confirmed = true;
                    Close();
                }

                if (GUILayout.Button("取消并保留文件", GUILayout.Width(130f), GUILayout.Height(28f)))
                {
                    confirmed = false;
                    Close();
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(10f);
            }
        }

        /// <summary>
        /// 创建本地化 Project Settings 页面。
        /// </summary>
        public LocalizationSettingsProvider(string path, SettingsScope scope)
            : base(path, scope) { }

        /// <summary>
        /// 从 Fink Framework 菜单直接打开本地化配置页面。
        /// </summary>
        [MenuItem("Fink Framework/本地化系统/本地化配置", false, 120)]
        public static void Open()
        {
            // SettingsService 可能复用已打开页面而不再次触发 OnActivate；
            // 菜单入口本身也要保证核心资产存在。
            LocalizationSettingsEditorLoader.LoadOrCreate();
            LocalizationAssetTableEditorLoader.LoadOrCreate();
            SettingsService.OpenProjectSettings("Project/Fink Framework/Localization");
        }

        [SettingsProvider]
        public static SettingsProvider CreateProvider()
        {
            return new LocalizationSettingsProvider(
                "Project/Fink Framework/Localization",
                SettingsScope.Project)
            {
                keywords = new[]
                {
                    "Fink",
                    "Framework",
                    "Localization",
                    "Locale",
                    "Language",
                    "Translation",
                    "本地化",
                    "语言",
                    "翻译"
                }
            };
        }

        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            ReloadSettings();
        }

        public override void OnGUI(string searchContext)
        {
            if (asset == null || serializedAsset == null)
            {
                EditorGUILayout.HelpBox(
                    "LocalizationSettingsAsset 缺失，请点击按钮自动创建配置。",
                    MessageType.Error);
                if (GUILayout.Button("重新创建配置文件"))
                    ReloadSettings();
                return;
            }

            // 有未提交的 Inspector 草稿时不要调用 Update，否则 Unity 会丢弃用户正在输入的内容。
            // 所有配置只在“保存并应用配置”按钮中提交、补齐语言文件并同步运行时副本。
            if (!serializedAsset.hasModifiedProperties)
                serializedAsset.Update();

            FFEditorGUI.BeginWindowContent();
            GUILayout.Space(4);
            EditorGUILayout.LabelField("本地化配置", FFEditorStyles.Title);
            GUILayout.Space(4);
            EditorGUILayout.LabelField(
                "管理项目语言、翻译表路径和运行时加载方式，并从这里打开语言表、质量检查等工具。",
                FFEditorStyles.Description);
            GUILayout.Space(14);

            DrawTextDirectionSettings();
            GUILayout.Space(FFEditorStyles.SectionSpacing);
            DrawLocaleSettings();
            GUILayout.Space(FFEditorStyles.SectionSpacing);
            DrawDataSettings();
            GUILayout.Space(FFEditorStyles.SectionSpacing);
            DrawCategories();
            GUILayout.Space(FFEditorStyles.SectionSpacing);
            DrawQualityCheck();
            GUILayout.Space(FFEditorStyles.SectionSpacing);
            DrawSaveActions();
            FFEditorGUI.DrawFrameworkFooter(18f);

            FFEditorGUI.EndWindowContent();
        }

        private void DrawTextDirectionSettings()
        {
            GUILayout.BeginVertical(FFEditorStyles.SectionBox);
            FFEditorGUI.DrawSectionHeader(
                "文字方向设置",
                "配置从右向左语言的文本排版适配。"
            );
            GUILayout.Space(6);
            EditorGUILayout.PropertyField(
                serializedAsset.FindProperty("enableRtlSupport"),
                new GUIContent(
                    "启用 RTL 文字方向适配",
                    "仅对希伯来语、阿拉伯语等少数从右向左书写的语言生效。"));
            EditorGUILayout.HelpBox(
                "RTL 只适用于少数从右向左书写的语言，例如希伯来语和阿拉伯语；" +
                "普通中文、英文等语言不会受到影响。",
                MessageType.Info);
            GUILayout.EndVertical();
        }

        private void DrawLocaleSettings()
        {
            GUILayout.BeginVertical(FFEditorStyles.SectionBox);
            FFEditorGUI.DrawSectionHeader(
                "语言设置",
                "从框架预置语言目录中选择项目需要支持的语言，并设置默认显示语言和回退语言。"
            );
            GUILayout.Space(6);

            SerializedProperty supportedLocalesProperty =
                serializedAsset.FindProperty("supportedLocales");
            SerializedProperty defaultLocaleProperty =
                serializedAsset.FindProperty("defaultLocale");
            SerializedProperty defaultFallbackLocaleProperty =
                serializedAsset.FindProperty("defaultFallbackLocale");
            EditorGUILayout.BeginHorizontal();
            DrawLocaleSelectionCard(
                "默认语言",
                defaultLocaleProperty,
                supportedLocalesProperty);
            GUILayout.Space(FFEditorStyles.ControlSpacing);
            DrawLocaleSelectionCard(
                "默认 Fallback",
                defaultFallbackLocaleProperty,
                supportedLocalesProperty);
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(8);
            DrawSupportedLocales(
                supportedLocalesProperty,
                defaultLocaleProperty,
                defaultFallbackLocaleProperty);
            GUILayout.EndVertical();
        }

        private void DrawSupportedLocales(
            SerializedProperty localesProperty,
            SerializedProperty defaultLocaleProperty,
            SerializedProperty defaultFallbackLocaleProperty)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(
                $"支持语言  ({localesProperty.arraySize})",
                FFEditorStyles.SectionTitle);
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(
                "语言表和资源表使用已启用的语言列",
                FFEditorStyles.Description,
                GUILayout.Width(220f));
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(3);
            DrawAddSupportedLocale(localesProperty);
            GUILayout.Space(3);
            for (int i = 0; i < localesProperty.arraySize; i++)
            {
                SerializedProperty locale = localesProperty.GetArrayElementAtIndex(i);
                SerializedProperty code = locale.FindPropertyRelative("code");
                SerializedProperty displayName = locale.FindPropertyRelative("displayName");

                EditorGUILayout.BeginHorizontal(GUILayout.Height(FFEditorStyles.ActionHeight));
                EditorGUILayout.LabelField(
                    (i + 1).ToString("00"),
                    FFEditorStyles.CenteredLabel,
                    GUILayout.Width(34f));
                string codeValue = code.stringValue;
                string displayValue = displayName.stringValue;
                string value = string.IsNullOrWhiteSpace(displayValue)
                    ? codeValue
                    : $"{codeValue}（{displayValue}）";
                EditorGUILayout.LabelField(value, EditorStyles.label);

                bool isDefaultLocale = string.Equals(
                    codeValue,
                    defaultLocaleProperty.stringValue,
                    StringComparison.OrdinalIgnoreCase);
                bool isFallbackLocale = string.Equals(
                    codeValue,
                    defaultFallbackLocaleProperty.stringValue,
                    StringComparison.OrdinalIgnoreCase);
                bool isInUse = isDefaultLocale || isFallbackLocale;
                string removeTooltip = isInUse
                    ? "请先将默认语言和默认 Fallback 切换为其他支持语言。"
                    : "从支持语言列表中移除该语言。保存时会检查并确认关联数据。";
                using (new EditorGUI.DisabledScope(isInUse))
                {
                    if (GUILayout.Button(
                            new GUIContent("移除", removeTooltip),
                            GUILayout.Width(58f)))
                    {
                        RemoveSupportedLocale(localesProperty, i);
                        EditorGUILayout.EndHorizontal();
                        break;
                    }
                }
                EditorGUILayout.EndHorizontal();
            }

        }

        private void DrawAddSupportedLocale(SerializedProperty localesProperty)
        {
            List<LocaleInfo> availableLocales = GetAvailableBuiltInLocales(localesProperty);
            if (availableLocales.Count == 0)
                return;

            var labels = new string[availableLocales.Count + 1];
            labels[0] = "选择要添加的预置语言...";
            for (int i = 0; i < availableLocales.Count; i++)
                labels[i + 1] = LocaleCatalog.GetEditorLabel(availableLocales[i]);

            EditorGUILayout.BeginHorizontal();
            int selectedIndex = FindLocaleIndex(availableLocales, localeCodeToAdd) + 1;
            int nextIndex = EditorGUILayout.Popup(
                new GUIContent("添加支持语言"),
                selectedIndex,
                labels);
            localeCodeToAdd = nextIndex > 0 ? availableLocales[nextIndex - 1].Code : null;

            using (new EditorGUI.DisabledScope(nextIndex == 0))
            {
                if (GUILayout.Button("添加", GUILayout.Width(58f)))
                {
                    AddSupportedLocale(localesProperty, availableLocales[nextIndex - 1]);
                    localeCodeToAdd = null;
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private static List<LocaleInfo> GetAvailableBuiltInLocales(
            SerializedProperty localesProperty)
        {
            var configuredCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < localesProperty.arraySize; i++)
            {
                string code = localesProperty
                    .GetArrayElementAtIndex(i)
                    .FindPropertyRelative("code")
                    .stringValue;
                if (LocaleInfo.TryNormalize(code, out string normalized))
                    configuredCodes.Add(normalized);
            }

            return LocaleCatalog.BuiltInLocales
                .Where(locale => !configuredCodes.Contains(locale.Code))
                .ToList();
        }

        private static void AddSupportedLocale(
            SerializedProperty localesProperty,
            LocaleInfo locale)
        {
            if (locale == null || FindLocaleIndex(GetConfiguredLocales(localesProperty), locale.Code) >= 0)
                return;

            int newIndex = localesProperty.arraySize;
            localesProperty.arraySize++;
            SerializedProperty newLocale = localesProperty.GetArrayElementAtIndex(newIndex);
            newLocale.FindPropertyRelative("code").stringValue = locale.Code;
            newLocale.FindPropertyRelative("displayName").stringValue = locale.DisplayName;
        }

        private static void RemoveSupportedLocale(SerializedProperty localesProperty, int index)
        {
            int originalSize = localesProperty.arraySize;
            localesProperty.DeleteArrayElementAtIndex(index);
            if (localesProperty.arraySize == originalSize)
                localesProperty.DeleteArrayElementAtIndex(index);
        }

        private static List<LocaleInfo> GetConfiguredLocales(SerializedProperty localesProperty)
        {
            var locales = new List<LocaleInfo>();
            for (int i = 0; i < localesProperty.arraySize; i++)
            {
                SerializedProperty locale = localesProperty.GetArrayElementAtIndex(i);
                string code = locale.FindPropertyRelative("code").stringValue;
                if (!LocaleInfo.TryNormalize(code, out string normalized)
                    || FindLocaleIndex(locales, normalized) >= 0)
                {
                    continue;
                }

                string displayName = locale.FindPropertyRelative("displayName").stringValue;
                locales.Add(new LocaleInfo(normalized, displayName));
            }

            return locales;
        }

        private void DrawLocaleSelectionCard(
            string title,
            SerializedProperty codeProperty,
            SerializedProperty supportedLocalesProperty)
        {
            EditorGUILayout.BeginVertical(
                EditorStyles.helpBox,
                GUILayout.ExpandWidth(true));
            EditorGUILayout.LabelField(title, EditorStyles.miniBoldLabel);
            DrawLocalePopup(string.Empty, codeProperty, supportedLocalesProperty);
            EditorGUILayout.EndVertical();
        }

        private void DrawDataSettings()
        {
            GUILayout.BeginVertical(FFEditorStyles.SectionBox);
            FFEditorGUI.DrawSectionHeader(
                "数据与加载",
                "管理翻译源文件目录和运行时加载策略。"
            );
            GUILayout.Space(6);

            EditorGUILayout.PropertyField(
                serializedAsset.FindProperty("jsonFileNamePattern"),
                new GUIContent("JSON 文件名格式"));

            EditorGUILayout.PropertyField(
                serializedAsset.FindProperty("runtimeLoadingMode"),
                new GUIContent("运行时加载模式"));

            EditorGUILayout.HelpBox(
                $"翻译源 JSON 目录：{LocalizationPath.ToProjectDisplayPath(LocalizationPath.GetSourceDataRoot())}\n" +
                $"运行时副本目录：{LocalizationPath.ToProjectDisplayPath(LocalizationPath.GetStreamingDataRoot())}\n" +
                "Key 直接从语言表读取，无需生成额外 C# 文件。",
                MessageType.Info);
            GUILayout.EndVertical();
        }

        private void DrawCategories()
        {
            GUILayout.BeginVertical(FFEditorStyles.SectionBox);
            FFEditorGUI.DrawSectionHeader(
                "语言表分类",
                "每个分类对应一组语言 JSON 文件，也会成为完整 Key 的前缀。"
            );
            GUILayout.Space(6);
            SerializedProperty categoriesProperty =
                serializedAsset.FindProperty("categories");
            EditorGUILayout.PropertyField(
                categoriesProperty,
                new GUIContent("主分类"),
                true);

            List<string> categoryErrors = CollectCategoryValidationErrors(
                ReadCategoryValues(categoriesProperty));
            bool hasIncompleteDraft = HasIncompleteCategoryDraft(categoriesProperty);
            if (categoryErrors.Count > 0 && !hasIncompleteDraft)
            {
                EditorGUILayout.HelpBox(
                    "主分类配置存在问题，保存配置前必须修正：\n- "
                    + string.Join("\n- ", categoryErrors),
                    MessageType.Error);
            }
            else if (hasIncompleteDraft)
            {
                EditorGUILayout.HelpBox(
                    "已添加新的主分类，请直接在列表空白行中输入名称。完成后点击“保存并应用配置”。",
                    MessageType.Info);
            }

            List<string> missingSourceCategories = asset.Categories == null
                ? new List<string>()
                : asset.Categories
                    .Where(LocalizationPath.IsSafeCategory)
                    .Where(category => !Directory.Exists(
                        LocalizationPath.GetCategoryDirectory(
                            LocalizationPath.GetSourceDataRoot(),
                            category)))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            if (missingSourceCategories.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    "配置中的分类在源目录中不存在："
                    + string.Join("、", missingSourceCategories)
                    + "。系统会保留配置，不会因源目录暂时缺失而自动删除。",
                    MessageType.Warning);
            }

            List<string> missingSourceFiles = GetMissingSourceFiles();
            if (missingSourceFiles.Count > 0)
            {
                string preview = string.Join(
                    "、",
                    missingSourceFiles.Take(8));
                string suffix = missingSourceFiles.Count > 8
                    ? $"，其余 {missingSourceFiles.Count - 8} 个省略"
                    : string.Empty;
                EditorGUILayout.HelpBox(
                    $"配置中的语言文件尚未全部存在：{preview}{suffix}。保存并应用配置可补齐缺失文件。",
                    MessageType.Warning);
            }

            EditorGUILayout.LabelField(
                "例如 UI、Common、Battle。分类会对应数据目录和语言 Key 的前缀。",
                FFEditorStyles.Description);

            GUILayout.Space(FFEditorStyles.ControlSpacing);
            if (DrawTwoButtonRow(
                    "打开本地化语言表",
                    FFEditorStyles.ActionButton,
                    "打开本地化资源表",
                    FFEditorStyles.ActionButton,
                    out bool openAssetTableClicked))
            {
                OpenLanguageTableWindow();
                GUIUtility.ExitGUI();
            }

            if (openAssetTableClicked)
            {
                OpenAssetTableWindow();
                GUIUtility.ExitGUI();
            }

            GUILayout.EndVertical();
        }

        private static bool HasIncompleteCategoryDraft(SerializedProperty categoriesProperty)
        {
            if (categoriesProperty == null)
                return false;

            for (int i = 0; i < categoriesProperty.arraySize; i++)
            {
                if (string.IsNullOrWhiteSpace(
                        categoriesProperty.GetArrayElementAtIndex(i).stringValue))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 查找配置要求但源目录中尚未存在的分类语言文件，用于非破坏性同步提示。
        /// </summary>
        private List<string> GetMissingSourceFiles()
        {
            var missing = new List<string>();
            if (asset?.Categories == null || asset.SupportedLocales == null)
                return missing;

            string sourceRoot = LocalizationPath.GetSourceDataRoot();
            foreach (string category in asset.Categories)
            {
                if (!LocalizationPath.IsSafeCategory(category))
                    continue;

                foreach (LocaleInfo locale in asset.SupportedLocales)
                {
                    if (locale == null || !LocaleInfo.TryNormalize(locale.Code, out string normalizedLocale))
                        continue;

                    string filePath = LocalizationPath.GetLanguageFilePath(
                        sourceRoot,
                        category,
                        normalizedLocale,
                        asset.JsonFileNamePattern);
                    if (!string.IsNullOrEmpty(filePath) && !File.Exists(filePath))
                    {
                        missing.Add($"{category}/{LocaleInfo.ToFileName(normalizedLocale)}");
                    }
                }
            }

            return missing;
        }

        private void DrawQualityCheck()
        {
            GUILayout.BeginVertical(FFEditorStyles.SectionBox);
            FFEditorGUI.DrawSectionHeader(
                "质量检查",
                "扫描本地化配置、语言表、资源表以及工程中的绑定引用。"
            );
            GUILayout.Space(6);
            if (HasUnappliedChanges())
            {
                EditorGUILayout.HelpBox(
                    "当前有尚未应用的配置修改。请先保存并应用，再执行质量检查。",
                    MessageType.Info);
            }

            EditorGUILayout.LabelField("快速检查", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField(
                "检查配置、语言表、资源表、脚本引用和已挂载的本地化组件；跳过普通静态文本普查。",
                FFEditorStyles.Description);
            GUILayout.Space(4);
            EditorGUILayout.LabelField("完整检查", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField(
                "包含快速检查的全部内容，并额外扫描场景和 Prefab 中未接入本地化的 Text/TMP 静态文本。",
                FFEditorStyles.Description);
            GUILayout.Space(10);

            if (DrawTwoButtonRow(
                    "执行快速质量检查",
                    FFEditorStyles.ActionButton,
                    "执行完整质量检查",
                    FFEditorStyles.ActionButton,
                    out bool runFullQualityCheckClicked))
            {
                RunQualityCheck(false);
                GUIUtility.ExitGUI();
            }

            if (runFullQualityCheckClicked)
            {
                RunQualityCheck(true);
                GUIUtility.ExitGUI();
            }

            GUILayout.EndVertical();
        }

        private void DrawSaveActions()
        {
            GUILayout.BeginVertical(FFEditorStyles.SectionBox);
            FFEditorGUI.DrawSectionHeader(
                "保存与应用",
                "保存配置、补齐缺失语言文件，并同步运行时副本。"
            );
            GUILayout.Space(6);
            if (HasUnappliedChanges())
            {
                EditorGUILayout.HelpBox(
                    "当前有尚未应用的配置修改。修改只保留在编辑器草稿中，保存后才会生效。",
                    MessageType.Info);
            }

            if (GUILayout.Button(
                    "保存并应用配置",
                    FFEditorStyles.PrimaryButton,
                    GUILayout.ExpandWidth(true),
                    GUILayout.Height(FFEditorStyles.ActionHeight)))
            {
                SaveConfigurationAndApply();
                // 保存流程可能显示删除确认/结果对话框，避免回到已失效的布局栈。
                GUIUtility.ExitGUI();
            }
            GUILayout.EndVertical();
        }

        private static bool DrawTwoButtonRow(
            string leftLabel,
            GUIStyle leftStyle,
            string rightLabel,
            GUIStyle rightStyle,
            out bool rightClicked)
        {
            Rect rowRect = EditorGUILayout.GetControlRect(
                false,
                FFEditorStyles.ActionHeight);
            float gap = FFEditorStyles.ControlSpacing;
            float buttonWidth = (rowRect.width - gap) * 0.5f;

            bool leftClicked = GUI.Button(
                new Rect(rowRect.x, rowRect.y, buttonWidth, rowRect.height),
                leftLabel,
                leftStyle);
            rightClicked = GUI.Button(
                new Rect(
                    rowRect.x + buttonWidth + gap,
                    rowRect.y,
                    buttonWidth,
                    rowRect.height),
                rightLabel,
                rightStyle);
            return leftClicked;
        }

        private void SaveConfigurationAndApply()
        {
            if (!ApplyAndSaveSettings())
                return;

            if (asset.EnableLocalization
                && !LocalizationDataInitializer.Initialize(
                    asset,
                    out var initializationMessage))
            {
                EditorUtility.DisplayDialog(
                    "配置已保存，但语言文件未补齐",
                    $"本地化配置已保存，但缺失语言文件创建或运行时同步失败：\n\n{initializationMessage}",
                    "确定");
                return;
            }

            if (!asset.EnableLocalization
                && !LocalizationDataSyncUtility.Sync(asset, out string syncMessage))
            {
                EditorUtility.DisplayDialog(
                    "配置已保存，但运行时副本未同步",
                    $"本地化模块已关闭，但源文件与 StreamingAssets 副本同步失败：\n\n{syncMessage}",
                    "确定");
                return;
            }

            if (Application.isPlaying)
                LocalizationManager.Initialize(asset);
        }

        private void OpenLanguageTableWindow()
        {
            if (HasUnappliedChanges())
            {
                ShowSaveRequiredDialog();
                return;
            }

            LocalizationWindow.Open();
        }

        private void OpenAssetTableWindow()
        {
            if (HasUnappliedChanges())
            {
                ShowSaveRequiredDialog();
                return;
            }

            LocalizationWindow.OpenAsset();
        }

        private void RunQualityCheck(bool scanUnlocalizedStaticText)
        {
            if (HasUnappliedChanges())
            {
                ShowSaveRequiredDialog();
                return;
            }

            LocalizationQualityChecker.Run(asset, scanUnlocalizedStaticText);
        }

        private bool HasUnappliedChanges()
        {
            return serializedAsset is { hasModifiedProperties: true };
        }

        private static void ShowSaveRequiredDialog()
        {
            EditorUtility.DisplayDialog(
                "配置尚未应用",
                "检测到尚未保存的配置修改。请先点击“保存并应用配置”，再打开本地化表或执行质量检查。",
                "确定");
        }

        private bool ApplyAndSaveSettings()
        {
            List<string> categoryErrors = CollectCategoryValidationErrors(
                ReadCategoryValues(serializedAsset.FindProperty("categories")));
            if (categoryErrors.Count > 0)
            {
                EditorUtility.DisplayDialog(
                    "分类配置无效",
                    "请先修正以下问题：\n- "
                    + string.Join("\n- ", categoryErrors),
                    "确定");
                serializedAsset.Update();
                return false;
            }

            PendingDataDeletion deletion;
            try
            {
                deletion = BuildPendingDataDeletion();
            }
            catch (Exception exception)
            {
                EditorUtility.DisplayDialog(
                    "无法检查本地化数据",
                    $"配置尚未保存，无法安全判断待删除文件：\n\n{exception.Message}",
                    "确定");
                serializedAsset.Update();
                return false;
            }
            if (deletion.HasChanges && !ConfirmPendingDataDeletion(deletion))
            {
                // 取消删除时同时撤销这次尚未提交的配置变化，避免配置与数据目录脱节。
                serializedAsset.Update();
                return false;
            }

            if (!DeletePendingData(deletion))
            {
                serializedAsset.Update();
                return false;
            }

            serializedAsset.ApplyModifiedProperties();

            if (!LocalizationDataSyncUtility.PrepareSourceDataRoot(
                    out string prepareMessage))
            {
                EditorUtility.DisplayDialog("源目录初始化失败", prepareMessage, "确定");
                return false;
            }

            asset.NormalizeConfiguration();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            CaptureSavedConfiguration();
            serializedAsset.Update();
            return true;
        }

        private static List<string> ReadCategoryValues(SerializedProperty categoriesProperty)
        {
            var categories = new List<string>();
            if (categoriesProperty == null)
                return categories;

            for (int i = 0; i < categoriesProperty.arraySize; i++)
            {
                categories.Add(
                    categoriesProperty.GetArrayElementAtIndex(i).stringValue);
            }

            return categories;
        }

        private List<string> CollectCategoryValidationErrors(
            IEnumerable<string> categoryValues)
        {
            var errors = new List<string>();
            var currentCategoryNames = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

            if (categoryValues != null)
            {
                int index = 0;
                foreach (string categoryValue in categoryValues)
                {
                    index++;
                    string category = categoryValue?.Trim();
                    if (string.IsNullOrEmpty(category))
                    {
                        errors.Add($"第 {index} 个主分类不能为空。");
                        continue;
                    }

                    if (!LocalizationPath.IsSafeCategory(category))
                    {
                        errors.Add(
                            $"主分类“{category}”无效：不能为空、不能包含路径分隔符或非法文件名字符。");
                        continue;
                    }

                    if (!currentCategoryNames.Add(category))
                    {
                        errors.Add($"主分类“{category}”重复，分类名称必须全局唯一。");
                    }
                }
            }

            return errors;
        }

        private PendingDataDeletion BuildPendingDataDeletion()
        {
            var deletion = new PendingDataDeletion();
            var currentCategories = new HashSet<string>(
                ReadCategoryValues(serializedAsset.FindProperty("categories"))
                    .Select(value => value?.Trim())
                    .Where(value => !string.IsNullOrEmpty(value)),
                StringComparer.OrdinalIgnoreCase);
            var currentLocales = new HashSet<string>(
                ReadLocaleCodes(serializedAsset.FindProperty("supportedLocales")),
                StringComparer.OrdinalIgnoreCase);
            string dataRoot = LocalizationPath.GetSourceDataRoot();

            foreach (string savedCategory in savedCategoryNames)
            {
                if (currentCategories.Contains(savedCategory))
                    continue;

                deletion.RemovedCategories.Add(savedCategory);
                string categoryDirectory = LocalizationPath.GetCategoryDirectory(
                    dataRoot,
                    savedCategory);
                if (!Directory.Exists(categoryDirectory))
                    continue;

                foreach (string filePath in Directory.GetFiles(
                             categoryDirectory,
                             "*.json",
                             SearchOption.AllDirectories))
                {
                    deletion.AddFile(filePath, $"已删除主分类“{savedCategory}”");
                }
            }

            foreach (string savedLocale in savedLocaleCodes)
            {
                if (currentLocales.Contains(savedLocale))
                    continue;

                foreach (string currentCategory in currentCategories)
                {
                    string categoryDirectory = LocalizationPath.GetCategoryDirectory(
                        dataRoot,
                        currentCategory);
                    string expectedPath = LocalizationPath.GetLanguageFilePath(
                        dataRoot,
                        currentCategory,
                        savedLocale,
                        savedJsonFileNamePattern);
                    if (File.Exists(expectedPath))
                    {
                        deletion.AddFile(
                            expectedPath,
                            $"已从支持语言中删除“{savedLocale}”");
                        continue;
                    }

                    // 兼容历史自定义文件名格式无法反推语言码的情况；标准文件名仍可识别。
                    if (!Directory.Exists(categoryDirectory))
                        continue;

                    foreach (string filePath in Directory.GetFiles(
                                 categoryDirectory,
                                 "*.json",
                                 SearchOption.TopDirectoryOnly))
                    {
                        if (LocaleInfo.TryFromFileName(
                                Path.GetFileName(filePath),
                                out string fileLocale)
                            && string.Equals(
                                fileLocale,
                                savedLocale,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            deletion.AddFile(
                                filePath,
                                $"已从支持语言中删除“{savedLocale}”");
                        }
                    }
                }
            }

            return deletion;
        }

        private bool ConfirmPendingDataDeletion(PendingDataDeletion deletion)
        {
            var message = new StringBuilder();

            foreach (string category in deletion.RemovedCategories)
                message.AppendLine($"- 主分类“{category}”及其语言表文件");

            foreach (KeyValuePair<string, string> file in deletion.Files
                         .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                         .Take(12))
            {
                message.AppendLine(
                    $"- {LocalizationPath.ToProjectDisplayPath(file.Key)}（{file.Value}）");
            }

            if (deletion.Files.Count > 12)
                message.AppendLine($"- 其余 {deletion.Files.Count - 12} 个文件省略。");

            message.AppendLine("\n确认后将删除列出的源语言 JSON；运行时副本会在随后同步时一并清理。");
            message.AppendLine("未列出的非 JSON 文件不会自动删除。");
            return PendingDeletionWindow.Show(message.ToString());
        }

        private bool DeletePendingData(PendingDataDeletion deletion)
        {
            if (!deletion.HasChanges)
                return true;

            string dataRoot = LocalizationPath.GetSourceDataRoot();
            try
            {
                foreach (string filePath in deletion.Files.Keys)
                {
                    if (!IsPathInsideRoot(filePath, dataRoot))
                        throw new InvalidOperationException(
                            $"拒绝删除本地化目录之外的文件：{filePath}");

                    if (File.Exists(filePath))
                        File.Delete(filePath);
                }

                foreach (string category in deletion.RemovedCategories)
                {
                    string categoryDirectory = LocalizationPath.GetCategoryDirectory(
                        dataRoot,
                        category);
                    if (Directory.Exists(categoryDirectory)
                        && Directory.GetFileSystemEntries(categoryDirectory).Length == 0)
                    {
                        Directory.Delete(categoryDirectory);
                    }
                }

                AssetDatabase.Refresh();
                return true;
            }
            catch (Exception exception)
            {
                EditorUtility.DisplayDialog(
                    "删除本地化数据失败",
                    $"配置尚未完成保存，请检查文件权限后重试：\n\n{exception.Message}",
                    "确定");
                return false;
            }
        }

        private static bool IsPathInsideRoot(string path, string root)
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(root))
                return false;

            string fullPath = Path.GetFullPath(path)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string fullRoot = Path.GetFullPath(root)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            return fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase);
        }

        private static List<string> ReadLocaleCodes(SerializedProperty localesProperty)
        {
            var locales = new List<string>();
            if (localesProperty == null)
                return locales;

            for (int i = 0; i < localesProperty.arraySize; i++)
            {
                SerializedProperty locale = localesProperty.GetArrayElementAtIndex(i);
                string code = locale.FindPropertyRelative("code").stringValue;
                if (LocaleInfo.TryNormalize(code, out string normalized))
                    locales.Add(normalized);
            }

            return locales;
        }

        private void CaptureSavedConfiguration()
        {
            savedCategoryNames.Clear();
            savedLocaleCodes.Clear();
            savedJsonFileNamePattern = asset?.JsonFileNamePattern ?? "{0}.json";

            if (asset?.Categories != null)
            {
                foreach (string category in asset.Categories)
                {
                    string normalized = category?.Trim();
                    if (!string.IsNullOrEmpty(normalized)
                        && !savedCategoryNames.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                    {
                        savedCategoryNames.Add(normalized);
                    }
                }
            }

            if (asset?.SupportedLocales != null)
            {
                foreach (LocaleInfo locale in asset.SupportedLocales)
                {
                    if (locale != null
                        && LocaleInfo.TryNormalize(locale.Code, out string normalized)
                        && !savedLocaleCodes.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                    {
                        savedLocaleCodes.Add(normalized);
                    }
                }
            }
        }

        private void DrawLocalePopup(
            string l,
            SerializedProperty codeProperty,
            SerializedProperty supportedLocalesProperty)
        {
            var options = new List<LocaleInfo>();
            if (supportedLocalesProperty != null)
            {
                for (int i = 0; i < supportedLocalesProperty.arraySize; i++)
                {
                    SerializedProperty locale =
                        supportedLocalesProperty.GetArrayElementAtIndex(i);
                    string code = locale.FindPropertyRelative("code").stringValue;
                    if (!LocaleInfo.TryNormalize(code, out string normalized)
                        || FindLocaleIndex(options, normalized) >= 0)
                    {
                        continue;
                    }

                    string displayName =
                        locale.FindPropertyRelative("displayName").stringValue;
                    options.Add(new LocaleInfo(
                        normalized,
                        string.IsNullOrWhiteSpace(displayName)
                            ? normalized
                            : displayName.Trim()));
                }
            }

            string currentCode = codeProperty.stringValue;
            int selectedIndex = FindLocaleIndex(options, currentCode);

            string[] labels = BuildLocaleLabels(options);
            if (labels.Length == 0)
                return;

            selectedIndex = Mathf.Clamp(selectedIndex, 0, labels.Length - 1);
            int nextIndex = EditorGUILayout.Popup(l, selectedIndex, labels);
            if (nextIndex == selectedIndex)
                return;

            LocaleInfo selected = options[nextIndex];
            codeProperty.stringValue = selected.Code;
        }

        private static int FindLocaleIndex(IReadOnlyList<LocaleInfo> locales, string code)
        {
            if (!LocaleInfo.TryNormalize(code, out string normalized))
                return -1;

            for (int i = 0; i < locales.Count; i++)
            {
                if (string.Equals(locales[i].Code, normalized, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }

        private static string[] BuildLocaleLabels(IReadOnlyList<LocaleInfo> locales)
        {
            var labels = new string[locales.Count];
            for (int i = 0; i < locales.Count; i++)
                labels[i] = LocaleCatalog.GetEditorLabel(locales[i]);

            return labels;
        }

        private void ReloadSettings()
        {
            asset = LocalizationSettingsEditorLoader.LoadOrCreate();
            // 配置页同时管理资源表入口；首次打开时也要补齐资源表资产，
            // 这样切换到资源表不再依赖先打开其他模块设置。
            LocalizationAssetTableEditorLoader.LoadOrCreate();
            serializedAsset = asset != null ? new SerializedObject(asset) : null;
            SyncCategoriesFromSource();
            CaptureSavedConfiguration();
        }

        /// <summary>
        /// 打开配置时将源数据目录中的新分类合并写回配置资产。
        /// 只新增、不删除，避免源目录暂时缺失时静默破坏配置。
        /// </summary>
        private void SyncCategoriesFromSource()
        {
            if (asset == null
                || serializedAsset == null
                || asset.Categories == null)
                return;

            IReadOnlyList<string> discoveredCategories =
                LocalizationEditorDataUtility.DiscoverCategories();
            if (discoveredCategories.Count == 0)
                return;

            SerializedProperty categoriesProperty = serializedAsset.FindProperty("categories");
            if (categoriesProperty == null)
                return;

            var existingCategories = new HashSet<string>(
                ReadCategoryValues(categoriesProperty)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value.Trim()),
                StringComparer.OrdinalIgnoreCase);
            var categoriesToAdd = discoveredCategories
                .Where(existingCategories.Add)
                .ToList();
            if (categoriesToAdd.Count == 0)
                return;

            int originalSize = categoriesProperty.arraySize;
            categoriesProperty.arraySize = originalSize + categoriesToAdd.Count;
            for (int i = 0; i < categoriesToAdd.Count; i++)
            {
                categoriesProperty.GetArrayElementAtIndex(originalSize + i).stringValue =
                    categoriesToAdd[i];
            }

            serializedAsset.ApplyModifiedPropertiesWithoutUndo();
            asset.NormalizeConfiguration();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            serializedAsset.Update();
        }
    }
}
