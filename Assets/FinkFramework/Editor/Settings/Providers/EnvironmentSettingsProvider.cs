using FinkFramework.Editor.Settings.Core;
using UnityEditor;
using UnityEngine;
using FinkFramework.Editor.Windows.Common;
using FinkFramework.Runtime.Settings;
using UnityEngine.UIElements;

namespace FinkFramework.Editor.Settings.Providers
{
    public class EnvironmentSettingsProvider : SettingsProvider
    {
        private GlobalSettingsAsset asset;

        public EnvironmentSettingsProvider(string path, SettingsScope scope)
            : base(path, scope) { }

        [SettingsProvider]
        public static SettingsProvider CreateProvider()
        {
            return new EnvironmentSettingsProvider("Project/Fink Framework/Environment", SettingsScope.Project)
            {
                keywords = new[] { "Fink", "Framework", "Environment", "Setting" }
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
                GUILayout.Label("环境配置 (Environment Settings)", FFEditorStyles.Title);
            });

            GUILayout.Space(8);
            EditorGUILayout.LabelField(
                "控制框架的环境行为，包括 XR、输入系统、渲染管线等基础环境控制开关。",
                FFEditorStyles.Description);
            GUILayout.Space(12);

            // ===== 主区域 =====
            GUILayout.BeginVertical(FFEditorStyles.SectionBox);

            // XR
            EditorGUILayout.LabelField("XR 设置", FFEditorStyles.SectionTitle);
            GUILayout.Space(6);
            asset.ForceDisableXR =
                EditorGUILayout.Toggle("强制关闭 XR", asset.ForceDisableXR);
            EditorGUILayout.LabelField(
                "若启用，则即使项目安装了 XR 插件（XRI），框架也会按非 VR 模式运行，框架内一切针对VR相关的功能将失效。",
                FFEditorStyles.Description);
            GUILayout.Space(8);

            // 新输入系统
            EditorGUILayout.LabelField("输入系统 设置", FFEditorStyles.SectionTitle);
            GUILayout.Space(6);
            asset.ForceDisableNewInputSystem =
                EditorGUILayout.Toggle("强制关闭 新输入系统", asset.ForceDisableNewInputSystem);
            EditorGUILayout.LabelField(
                "启用后，即便项目安装了 InputSystem，也会强制使用框架内关于旧输入系统（Input Manager）的一切逻辑。",
                FFEditorStyles.Description);
            GUILayout.Space(8);

            // URP
            EditorGUILayout.LabelField("渲染管线 设置", FFEditorStyles.SectionTitle);
            GUILayout.Space(6);
            asset.ForceDisableURP =
                EditorGUILayout.Toggle("强制关闭 URP", asset.ForceDisableURP);
            EditorGUILayout.LabelField(
                "启用后，框架将按非 URP 环境运行，即便项目当前使用 URP 渲染管线。",
                FFEditorStyles.Description);
            GUILayout.Space(8);

            // EditorURL 打包检测
            EditorGUILayout.LabelField("构建 设置", FFEditorStyles.SectionTitle);
            GUILayout.Space(6);
            asset.EnableEditorUrlCheck =
                EditorGUILayout.Toggle("启用 编辑器加载 打包检测", asset.EnableEditorUrlCheck);
            EditorGUILayout.LabelField(
                "构建前自动扫描代码，如果发现 editor:// 路径引用，将阻止打包，避免运行时无法加载资源。",
                FFEditorStyles.Description);

            GUILayout.EndVertical();

            GUILayout.Space(20);

            // ===== 页脚 =====
            FFEditorGUI.Center(() =>
            {
                GUILayout.Label("Copyright \u00A9 2025 Fink Framework", FFEditorStyles.Footer);
            });

            // 保存并同步
            if (GUI.changed)
            {
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();
            }
        }
    }
}
