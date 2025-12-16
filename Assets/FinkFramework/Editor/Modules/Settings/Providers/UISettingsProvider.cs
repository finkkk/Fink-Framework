using FinkFramework.Editor.Modules.Settings.Core;
using FinkFramework.Editor.Windows.Common;
using FinkFramework.Runtime.Settings;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FinkFramework.Editor.Modules.Settings.Providers
{
    public class UISettingsProvider : SettingsProvider
    {
        private GlobalSettingsAsset asset;

        public UISettingsProvider(string path, SettingsScope scope)
            : base(path, scope) { }

        [SettingsProvider]
        public static SettingsProvider CreateProvider()
        {
            return new UISettingsProvider("Project/Fink Framework/UI Settings", SettingsScope.Project)
            {
                keywords = new[] { "Fink", "Framework", "UI", "Settings", "Canvas" }
            };
        }

        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            asset = GlobalSettingsLoader.LoadOrCreate();
        }

        public override void OnGUI(string searchContext)
        {
            if (!asset)
            {
                EditorGUILayout.HelpBox("GlobalSettingsAsset 缺失，请重新导入框架或点击按钮自动修复。", MessageType.Error);
                if (GUILayout.Button("重新创建配置文件"))
                {
                    asset = GlobalSettingsLoader.LoadOrCreate();
                }
                return;
            }

            GUILayout.Space(10);

            // ===== 标题 =====
            FFEditorGUI.Center(() =>
            {
                GUILayout.Label("UI 配置 (UI Settings)", FFEditorStyles.Title);
            });

            GUILayout.Space(6);
            EditorGUILayout.LabelField(
                "用于配置框架 UI 系统行为，包括 UI 渲染模式和 UI 管理相关选项。",
                FFEditorStyles.Description);
            GUILayout.Space(10);

            // ===== 主区域 =====
            GUILayout.BeginVertical(FFEditorStyles.SectionBox);

            EditorGUILayout.LabelField("UI 渲染设置", FFEditorStyles.SectionTitle);
            GUILayout.Space(6);
            asset.CurrentUIMode = (Runtime.Environments.EnvironmentState.UIMode)EditorGUILayout.EnumPopup("UI 渲染模式", asset.CurrentUIMode);
            EditorGUILayout.LabelField(
                "<b>ScreenSpace:</b> 普通 2D UI（适用于大多数项目）\n" +
                "<b>WorldSpace:</b> 3D 空间 UI（适用于 VR 项目或需要 3D Canvas 的内容）\n" +
                "<b>Auto:</b> 自动判断当前项目是否为 VR 模式。\n" +
                "若为 VR → 使用 WorldSpace；否则使用 ScreenSpace。",
                FFEditorStyles.Description);

            GUILayout.EndVertical();

            GUILayout.Space(20);

            // ===== 页脚 =====
            FFEditorGUI.Center(() =>
            {
                GUILayout.Label("Copyright \u00A9 2025 Fink Framework", FFEditorStyles.Footer);
            });

            // 保存
            if (GUI.changed)
            {
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();
            }
        }
    }
}
