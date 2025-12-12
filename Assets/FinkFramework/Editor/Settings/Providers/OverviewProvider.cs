using FinkFramework.Editor.Utils;
using UnityEditor;
using UnityEngine;
using FinkFramework.Editor.Windows.Common;

namespace FinkFramework.Editor.Settings.Providers
{
    public class OverviewProvider : SettingsProvider
    {
        public OverviewProvider(string path, SettingsScope scope)
            : base(path, scope) { }

        [SettingsProvider]
        public static SettingsProvider CreateProvider()
        {
            // 关键：路径不带子项 → 点击父节点时进入 Overview 页
            return new OverviewProvider("Project/Fink Framework", SettingsScope.Project)
            {
                keywords = new[] 
                { 
                    "Fink", "Framework", "Overview", "Settings",
                    "Pipeline", "Data", "UI", "Encryption"
                }
            };
        }

        public override void OnGUI(string searchContext)
        {
            GUILayout.Space(10);
            
            // ===== Logo =====
            var logo = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/FinkFramework/Editor/EditorResources/Icon/FinkFramework_logo.png");
            
            if (logo != null)
            {
                GUILayout.Space(10);
                FFEditorGUI.Center(() =>
                {
                    GUILayout.Label(logo, GUILayout.Width(128), GUILayout.Height(128));
                });
                GUILayout.Space(10);
            }

            // ===== Title =====
            FFEditorGUI.Center(() =>
            {
                GUILayout.Label("Fink Framework 项目配置", FFEditorStyles.Title);
            });

            GUILayout.Space(4);

            FFEditorGUI.Center(() =>
            {
                GUILayout.Label("Modular Unity Game Framework", FFEditorStyles.Description);
            });

            GUILayout.Space(15);

            GUILayout.BeginVertical(FFEditorStyles.SectionBox);

            // ===== Framework Summary =====
            EditorGUILayout.LabelField("框架简介 (Overview)", FFEditorStyles.SectionTitle);
            GUILayout.Space(4);

            EditorGUILayout.LabelField(
                "Fink Framework 是一个模块化 Unity 游戏框架，旨在提供：\n\n" +
                "• 数据驱动工作流（Excel → C# → JSON → Binary）\n" +
                "• 可插拔式资源加载系统（Resources / Web / AB 可扩展）\n" +
                "• 多层级 UI 架构与 VR 适配支持\n" +
                "• 统一的加密系统、运行时环境状态管理、工具面板\n" +
                "• 可视化的 Project Settings 统一配置\n\n" +
                "框架目标是提供清晰、规范、可扩展、高可维护性的工程结构。",
                FFEditorStyles.Description);

            GUILayout.Space(14);

            // ===== Quick Links =====
            EditorGUILayout.LabelField("项目配置列表 (Modules)", FFEditorStyles.SectionTitle);
            GUILayout.Space(4);

            EditorGUILayout.LabelField(
                "• Environment（环境配置）\n" +
                "• UI Settings（UI 配置）\n" +
                "• Data Pipeline（数据配置）\n" +
                "• Encryption（加密配置）",
                FFEditorStyles.Description);

            GUILayout.Space(14);

            // ===== Version =====
            EditorGUILayout.LabelField("版本信息 (Version)", FFEditorStyles.SectionTitle);
            GUILayout.Space(4);

            EditorGUILayout.LabelField($"Framework Version: {Runtime.Environments.EnvironmentState.FrameworkVersion}",
                FFEditorStyles.Description);

            GUILayout.Space(6);
            
            if (GUILayout.Button("立即检查更新", FFEditorStyles.SmallButton, GUILayout.Width(120)))
                UpdateCheckUtil.CheckUpdateManual();
            
            GUILayout.Space(14);

            // ===== Footer =====
            EditorGUILayout.LabelField(
                "作者：Finkkk\n" +
                "仓库：https://github.com/Finkkk/FinkFramework\n",
                FFEditorStyles.Description);

            GUILayout.EndVertical();

            GUILayout.Space(20);

            FFEditorGUI.Center(() =>
            {
                GUILayout.Label("Copyright © 2025 Fink Framework", FFEditorStyles.Footer);
            });
        }
    }
}
