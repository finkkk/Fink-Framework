using FinkFramework.Editor.Modules.Settings.Loaders;
using FinkFramework.Editor.Windows.Common;
using FinkFramework.Runtime.Environments;
using FinkFramework.Runtime.Settings.ScriptableObjects;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FinkFramework.Editor.Modules.Settings.Providers
{
    public class DataSettingsProvider : SettingsProvider
    {
        private GlobalSettingsAsset asset;

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
            // Data Export Mode
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

            EditorGUILayout.LabelField("代码生成路径设置", FFEditorStyles.SectionTitle);
            GUILayout.Space(6);
            // ----------------------------------------------------
            // C# Path Mode
            // ----------------------------------------------------
            asset.CSharpUseExternal =
                EditorGUILayout.Toggle("C#: 使用外部路径", asset.CSharpUseExternal);

            EditorGUILayout.LabelField(
                asset.CSharpUseExternal
                    ? "当前状态：生成的 C# 数据类将输出到 FinkFramework_Data/DataClass 下。"
                    : "当前状态：生成的 C# 数据类将输出到 Assets/FinkFramework_Data/DataClass 下。",
                FFEditorStyles.Description);
          
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

        private void DrawPathDisplay(string label, string path)
        {
            GUILayout.Space(4);
            EditorGUILayout.LabelField($"<b>{label}:</b>", FFEditorStyles.Description);
            EditorGUILayout.LabelField(path, FFEditorStyles.Description);
        }
    }
}
