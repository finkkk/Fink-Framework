using System;
using FinkFramework.Editor.Modules.Settings.Loaders;
using FinkFramework.Editor.Windows.Common;
using FinkFramework.Runtime.Data;
using FinkFramework.Runtime.Environments;
using FinkFramework.Runtime.Settings.ScriptableObjects;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FinkFramework.Editor.Modules.Settings.Providers
{
    public class DataSettingsProvider : SettingsProvider
    {
        private static readonly string[] CSharpPathModeLabels =
        {
            "内部（Assets）",
            "外部（项目目录）"
        };

        private GlobalSettingsAsset asset;
        private string internalPathDraft;
        private string externalPathDraft;
        private bool draftsInitialized;

        public DataSettingsProvider(string path, SettingsScope scope)
            : base(path, scope) { }

        [SettingsProvider]
        public static SettingsProvider CreateProvider()
        {
            return new DataSettingsProvider("Project/Fink Framework/Data Pipeline", SettingsScope.Project)
            {
                keywords = new[] { "Fink", "Data", "Pipeline", "Excel", "Json", "Binary" }
            };
        }

        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            asset = GlobalSettingsEditorLoader.LoadOrCreate();
            SyncPathDrafts();
        }

        public override void OnGUI(string searchContext)
        {
            if (!asset)
            {
                EditorGUILayout.HelpBox("GlobalSettingsAsset 缺失，请重新导入框架或点击按钮自动修复。", MessageType.Error);
                if (GUILayout.Button("重新创建配置文件"))
                {
                    asset = GlobalSettingsEditorLoader.LoadOrCreate();
                }
                return;
            }

            if (!draftsInitialized)
                SyncPathDrafts();

            GUILayout.Space(10);
            FFEditorGUI.Center(() =>
            {
                GUILayout.Label("数据配置 (Data Pipeline Settings)", FFEditorStyles.Title);
            });

            GUILayout.Space(8);
            EditorGUILayout.LabelField(
                "控制 Excel → C# → JSON → 二进制数据 的数据处理流程所使用的输出路径和管线模式。",
                FFEditorStyles.Description);
            GUILayout.Space(12);

            // ===== 主区域 =====
            GUILayout.BeginVertical(FFEditorStyles.SectionBox);
            
            EditorGUILayout.LabelField("运行时数据源", FFEditorStyles.SectionTitle);
            GUILayout.Space(6);
            // ----------------------------------------------------
            // 运行时数据源模式
            // ----------------------------------------------------
            asset.CurrentDataLoadMode =
                (EnvironmentState.DataLoadMode)
                EditorGUILayout.EnumPopup("数据导出模式", asset.CurrentDataLoadMode);

            if (asset.CurrentDataLoadMode == EnvironmentState.DataLoadMode.Binary)
            {
                EditorGUILayout.LabelField(
                    "当前模式：运行时使用 二进制 数据作为数据源。\n" +
                    "同时会额外导出 JSON 文件用于调试（仅外部存储）。",
                    FFEditorStyles.Description);
            }
            else
            {
                EditorGUILayout.LabelField(
                    "当前模式：运行时使用 JSON 文件作为数据源。\n" +
                    "不会导出二进制文件，并且 JSON 将写入 StreamingAssets 中以供读取。",
                    FFEditorStyles.Description);
            }

            GUILayout.Space(12);

            DrawCSharpPathSettings();
          
            GUILayout.Space(16);

            GUILayout.EndVertical();

            GUILayout.Space(20);

            // ===== 页脚 =====
            FFEditorGUI.Center(() =>
            {
                GUILayout.Label("Copyright © 2025 Fink Framework", FFEditorStyles.Footer);
            });

            // 保存修改
            if (GUI.changed)
            {
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();
            }
        }

        /// <summary>
        /// 绘制 C# 输出模式、当前模式的路径设置，以及最终生效路径。
        /// </summary>
        private void DrawCSharpPathSettings()
        {
            // 路径输入使用草稿值，只有点击“应用”才写入 GlobalSettingsAsset。
            EditorGUILayout.LabelField("代码生成路径设置", FFEditorStyles.SectionTitle);
            GUILayout.Space(6);

            int modeIndex = Mathf.Clamp((int)asset.CSharpPathMode, 0, CSharpPathModeLabels.Length - 1);
            modeIndex = EditorGUILayout.Popup("C# 输出位置", modeIndex, CSharpPathModeLabels);
            asset.CSharpPathMode = (EnvironmentState.CSharpOutputPathMode)modeIndex;

            bool isInternal = asset.CSharpPathMode == EnvironmentState.CSharpOutputPathMode.Internal;
            bool useCustomPath = isInternal
                ? DrawInternalPathSettings()
                : DrawExternalPathSettings();

            EditorGUILayout.LabelField(
                isInternal
                    ? "当前模式：生成的 C# 数据类将输出到 Assets 内部目录。"
                    : "当前模式：生成的 C# 数据类将输出到项目外部目录。",
                FFEditorStyles.Description);

            DrawPathDisplay(
                useCustomPath ? "当前生效路径（自定义）" : "当前生效路径（默认）",
                DataPipelinePath.CSharpRoot);
        }

        /// <summary>
        /// 绘制 Assets 内部路径设置。Assets/ 前缀固定不可编辑。
        /// </summary>
        private bool DrawInternalPathSettings()
        {
            asset.UseCustomInternalCSharpOutputPath = EditorGUILayout.Toggle(
                "启用自定义内部路径",
                asset.UseCustomInternalCSharpOutputPath);

            if (!asset.UseCustomInternalCSharpOutputPath)
                return false;

            string internalSuffix = GetInternalPathSuffix(internalPathDraft);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("内部输出目录", GUILayout.Width(90));
            EditorGUILayout.LabelField("Assets/", GUILayout.Width(48));
            internalSuffix = EditorGUILayout.TextField(internalSuffix);
            internalSuffix = NormalizePathSuffix(internalSuffix);
            internalPathDraft = $"Assets/{internalSuffix}";

            bool pathIsValid = DataPipelinePath.TryValidateCSharpOutputPath(
                internalPathDraft,
                true,
                out string pathError);
            DrawResetButton(true);
            DrawApplyButton(true, pathIsValid);
            EditorGUILayout.EndHorizontal();
            DrawPathValidation(pathIsValid, pathError);
            return true;
        }

        /// <summary>
        /// 绘制项目外部路径设置。
        /// </summary>
        private bool DrawExternalPathSettings()
        {
            asset.UseCustomExternalCSharpOutputPath = EditorGUILayout.Toggle(
                "启用自定义外部路径",
                asset.UseCustomExternalCSharpOutputPath);

            if (!asset.UseCustomExternalCSharpOutputPath)
                return false;

            EditorGUILayout.BeginHorizontal();
            string externalPath = EditorGUILayout.TextField(
                "外部输出目录",
                externalPathDraft);
            externalPathDraft = NormalizeProjectRelativePath(externalPath);

            bool pathIsValid = DataPipelinePath.TryValidateCSharpOutputPath(
                externalPathDraft,
                false,
                out string pathError);
            DrawResetButton(false);
            DrawApplyButton(false, pathIsValid);
            EditorGUILayout.EndHorizontal();
            DrawPathValidation(pathIsValid, pathError);
            return true;
        }

        private void DrawResetButton(bool isInternal)
        {
            if (!GUILayout.Button("恢复默认", GUILayout.Width(70)))
                return;

            GUI.FocusControl(null);
            if (isInternal)
                internalPathDraft = DataPipelinePath.GetDefaultCSharpOutputPath(true);
            else
                externalPathDraft = DataPipelinePath.GetDefaultCSharpOutputPath(false);

            GUI.changed = true;
            GUIUtility.ExitGUI();
        }

        private void SyncPathDrafts()
        {
            if (!asset)
                return;

            // 进入面板或重新创建配置时，从已保存配置初始化输入框草稿。
            internalPathDraft = asset.InternalCSharpOutputPath;
            externalPathDraft = asset.ExternalCSharpOutputPath;
            draftsInitialized = true;
        }

        private void ApplyPathSettings(bool isInternal)
        {
            // 应用前再次校验，避免其他代码绕过按钮状态直接提交非法路径。
            if (isInternal)
            {
                if (!DataPipelinePath.TryValidateCSharpOutputPath(internalPathDraft, true, out _))
                    return;

                asset.InternalCSharpOutputPath = NormalizeInternalPath(internalPathDraft);
            }
            else
            {
                if (!DataPipelinePath.TryValidateCSharpOutputPath(externalPathDraft, false, out _))
                    return;

                asset.ExternalCSharpOutputPath = NormalizeProjectRelativePath(externalPathDraft);
            }

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
        }

        private bool HasPendingPathChanges(bool isInternal)
        {
            return isInternal
                ? asset.UseCustomInternalCSharpOutputPath &&
                  internalPathDraft != asset.InternalCSharpOutputPath
                : asset.UseCustomExternalCSharpOutputPath &&
                  externalPathDraft != asset.ExternalCSharpOutputPath;
        }

        private void DrawApplyButton(bool isInternal, bool pathIsValid)
        {
            bool canApply = pathIsValid && HasPendingPathChanges(isInternal);
            EditorGUI.BeginDisabledGroup(!canApply);
            if (GUILayout.Button("应用", GUILayout.Width(70)))
                ApplyPathSettings(isInternal);
            EditorGUI.EndDisabledGroup();
        }

        private void DrawPathDisplay(string label, string path)
        {
            GUILayout.Space(4);
            EditorGUILayout.LabelField($"<b>{label}:</b>", FFEditorStyles.Description);
            EditorGUILayout.LabelField(path, FFEditorStyles.Description);
        }

        private static string GetInternalPathSuffix(string path)
        {
            string normalizedPath = string.IsNullOrWhiteSpace(path)
                ? string.Empty
                : path.Trim().Replace('\\', '/');

            const string assetsPrefix = "Assets/";
            return normalizedPath.StartsWith(assetsPrefix, StringComparison.OrdinalIgnoreCase)
                ? normalizedPath.Substring(assetsPrefix.Length)
                : string.Empty;
        }

        private static string NormalizePathSuffix(string suffix)
        {
            return string.IsNullOrWhiteSpace(suffix)
                ? string.Empty
                : suffix.Trim().Replace('\\', '/').Trim('/');
        }

        private static string NormalizeInternalPath(string path)
        {
            string suffix = NormalizePathSuffix(GetInternalPathSuffix(path));
            return $"Assets/{suffix}";
        }

        private static string NormalizeProjectRelativePath(string path)
        {
            return string.IsNullOrWhiteSpace(path)
                ? string.Empty
                : path.Trim().Replace('\\', '/').TrimEnd('/');
        }

        private static void DrawPathValidation(bool isValid, string error)
        {
            if (!isValid)
                EditorGUILayout.HelpBox(error, MessageType.Warning);
        }
    }
}
